using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord;
using ACE.Entity.Enum;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Managers;
using ACE.Server.Entity;
using System.Timers;
using System.Collections.Concurrent;
using System;
using System.Text.RegularExpressions;
using ACE.Server.WorldObjects;
using System.Collections.Generic;
using System.Linq;
using ACE.Entity.Enum.Properties;

namespace ACE.Server.ValheelMods
{
    public class DiscordRelay
    {
        //TurbineChatHandler.cs and GameMessageTurbineChat.cs are what's relevant
        //Todo: Filtering relevant messages.  White/blacklisting.
        //Todo: Implement a settings file for the bot token and channel IDs

        //Supply credentials
        private const ulong RELAY_CHANNEL_ID = ;
        private const long HC_RELAY_CHANNEL_ID = ;
        private const string BOT_TOKEN = "";

        private static DiscordSocketClient discord;
       
        private static IMessageChannel channel;
        private static IMessageChannel hcchannel;

        //Outgoing messages
        private static ConcurrentQueue<string> outgoingMessages;
        private static ConcurrentQueue<string> hcLeaderboardMessages;
        private static Timer messageTimer;
        private static Timer hcLeaderboardTimer;
        private static double HC_LEADERBOARD_INTERVAL = 14400000;
        private const int MAX_MESSAGE_LENGTH = 10000;
        private const double MESSAGE_INTERVAL = 10000;
        private const string PREFIX = "~";
        private static bool IsInitialized = false;

        //Initialize in Program.cs or on first use?
        public async static void Initialize()
        {
            if (IsInitialized)
                return;

            //Set up outgoing message queue
            outgoingMessages = new ConcurrentQueue<string>();
            messageTimer = new Timer
            {
                AutoReset = true,
                Enabled = false,
                Interval = MESSAGE_INTERVAL,
            };
            messageTimer.Elapsed += SendQueuedMessages;

            //Set up outgoing message queue for Hc Leaderboard
            hcLeaderboardMessages = new ConcurrentQueue<string>();
            hcLeaderboardTimer = new Timer
            {
                AutoReset = true,
                Enabled = false,
                Interval = HC_LEADERBOARD_INTERVAL,
            };
            hcLeaderboardTimer.Elapsed += SendHcLeaderboard;
            hcLeaderboardTimer.Start(); // Start the timer

            discord = new DiscordSocketClient();
            await discord.LoginAsync(TokenType.Bot, BOT_TOKEN);
            await discord.StartAsync();
            discord.Ready += OnReady;
            
        }

        //Finish initializing when logged in to Discord
        private static Task OnReady()
        {
            if (IsInitialized)
                return Task.CompletedTask;

            //Grab the channel to be used for relaying messages
            channel = discord.GetChannel(RELAY_CHANNEL_ID) as IMessageChannel;
            if (channel == null)
            {
                return Task.CompletedTask;
            }

            //Grab the Hcchannel to be used for relaying messages
            hcchannel = discord.GetChannel(HC_RELAY_CHANNEL_ID) as IMessageChannel;
            if (hcchannel == null)
            {
                return Task.CompletedTask;
            }

            //Set up relay
            discord.MessageReceived += OnDiscordChat;

            //Start ACE-->Discord timer
            messageTimer.Enabled = true;

            //Say hi
            QueueMessageForDiscord("Valheel Chat Relay is online.");

            IsInitialized = true;

            return Task.CompletedTask;
        }

        //Batch messages going to Discord to help with rate limits
        private static void SendQueuedMessages(object sender, ElapsedEventArgs e)
        {
            if (channel is null)
                return;

            var batchedMessage = new StringBuilder();

            while (batchedMessage.Length < MAX_MESSAGE_LENGTH &&
                outgoingMessages.TryDequeue(out string message))
            {
                batchedMessage.AppendLine(message);
            }

            Task.Run(async () =>
            {
                await channel.SendMessageAsync(batchedMessage.ToString());
            });
        }

        //Relay messages from Discord
        private static Task OnDiscordChat(SocketMessage msg)
        {
            if (msg is null)
                return Task.CompletedTask;

            //Ignore bot chat and incorrect channels
            if (msg.Author.IsBot || msg.Channel.Id != RELAY_CHANNEL_ID)
                return Task.CompletedTask;

            //Check if the server has disabled general chat
            if (PropertyManager.GetBool("chat_disable_general").Item)
                return Task.CompletedTask;

            if (msg.Content.Length == 0)
                return Task.CompletedTask;

            if (msg.Content is not string)
                return Task.CompletedTask;

            if (msg.Content.Length > 255)
                return Task.CompletedTask;

            Console.WriteLine($"Received message: {msg.Content}");

            string input = msg.Content;
            string pattern = $"{"<"}.*?{">"}{" "}";
            string newPatern = Regex.Replace(input, pattern, string.Empty);

            //Construct message
            var chatMessage = new GameMessageTurbineChat(
                ChatNetworkBlobType.NETBLOB_EVENT_BINARY,
                ChatNetworkBlobDispatchType.ASYNCMETHOD_SENDTOROOMBYNAME,
                TurbineChatChannel.General,
                PREFIX + msg.Author.Username,
                newPatern,
                0,
                ChatType.General);
            //var gameMessageTurbineChat = new GameMessageTurbineChat(ChatNetworkBlobType.NETBLOB_EVENT_BINARY, ChatNetworkBlobDispatchType.ASYNCMETHOD_SENDTOROOMBYNAME, adjustedChannelID, session.Player.Name, message, senderID, adjustedchatType);


            //Send a message to any player who is listening to general chat
            foreach (var recipient in PlayerManager.GetAllOnline())
            {
                // handle filters
                if (!recipient.GetCharacterOption(CharacterOption.ListenToGeneralChat))
                    return Task.CompletedTask;

                //Todo: think about how to handle squelches?
                //if (recipient.SquelchManager.Squelches.Contains(session.Player, ChatMessageType.AllChannels))
                //    continue;

                recipient.Session.Network.EnqueueSend(chatMessage);
            }

            return Task.CompletedTask;
        }

        //Called when a GameMessageTurbineChat is created to see if it should be sent to Discord
        public static void RelayIngameChat(string message, string senderName, ChatType chatType, uint channel, uint senderID, ChatNetworkBlobType chatNetworkBlobType, ChatNetworkBlobDispatchType chatNetworkBlobDispatchType)
        {
            if (message is null || senderName is null)
                return;
            if (senderName.StartsWith(PREFIX))
                return;

            if (chatType == ChatType.General || chatType == ChatType.LFG)
                QueueMessageForDiscord($"[{chatType}] {senderName}: {message}");
        }

        public static async void SendHcLeaderboard(object sender, ElapsedEventArgs e)
        {
            if (hcchannel is null)
                return;

            List<Player> onlineHcPlayers = new List<Player>();
            List<OfflinePlayer> offlineHcPlayers = new List<OfflinePlayer>();

            foreach (var p in PlayerManager.GetAllOnline())
            {
                if (p.Hardcore == true)
                    onlineHcPlayers.Add(p);
            }

            foreach (var i in PlayerManager.GetAllOffline())
            {
                if (i.Hardcore == true)
                    offlineHcPlayers.Add(i);
            }

            List<IPlayer> allHcPlayers = new List<IPlayer>();
            allHcPlayers.AddRange(onlineHcPlayers);
            allHcPlayers.AddRange(offlineHcPlayers);

            List<IPlayer> playersToRemove = new List<IPlayer>();

            foreach (var p in allHcPlayers)
            {
                if (CalculateHcPlayerAge(p, GetCurrentUnixTime()) >= 5_184_000 && p.Level >= 10000)
                {
                    playersToRemove.Add(p);
                }
            }

            foreach (var p in playersToRemove)
            {
                allHcPlayers.Remove(p);
            }

            List<IPlayer> top10Players = allHcPlayers.OrderByDescending(p => p.HcScore).Take(10).ToList();

            StringBuilder result = new StringBuilder();
            int rank = 1;

            int maxRankLength = Math.Max(4, top10Players.Count.ToString().Length);
            int maxNameLength = Math.Max(26, top10Players.Max(player => player.Name.Length));
            int maxLevelLength = Math.Max(7, top10Players.Max(player => player.Level.ToString().Length));
            int maxAgeLength = Math.Max(15, top10Players.Max(player => player.HcAge.ToString().Length));
            int maxCreatureKillsLength = Math.Max(16, top10Players.Max(player => player.CreatureKills.ToString().Length));
            int maxPyrealsWonLength = Math.Max(16, top10Players.Max(player => player.HcPyrealsWon.ToString().Length));
            int maxScoreLength = Math.Max(11, top10Players.Max(player => player.HcScore.ToString().Length));

            // Format the string using the calculated maximum lengths
            int headerWidth = maxRankLength + maxNameLength + maxLevelLength + maxAgeLength + maxCreatureKillsLength + maxPyrealsWonLength + maxScoreLength + 26;

            // Now, use the constants to format the string
            result.AppendLine("```fix");
            result.AppendLine($"{"Hardcore Top Ten!".PadLeft(headerWidth / 2 + 16)}\n\n");
            result.AppendLine($"{"Rank".PadRight(maxRankLength)} | {"Name".PadRight(maxNameLength)} | {"Level".PadRight(maxLevelLength)} | {"Age".PadRight(maxAgeLength)} | {"Creature Kills".PadRight(maxCreatureKillsLength)} | {"Pyreals Won".PadRight(maxPyrealsWonLength)} | {"Score".PadRight(maxScoreLength)}");
            result.AppendLine($"{"".PadRight(maxRankLength, '-')}-+-{"".PadRight(maxNameLength, '-')}-+-{"".PadRight(maxLevelLength, '-')}-+-{"".PadRight(maxAgeLength, '-')}-+-{"".PadRight(maxCreatureKillsLength, '-')}-+-{"".PadRight(maxPyrealsWonLength, '-')}-+-{"".PadRight(maxScoreLength, '-')}");

            foreach (var player in top10Players)
            {
                result.AppendLine($"{rank.ToString().PadRight(maxRankLength)} | {player.Name.PadRight(maxNameLength)} | {player.Level.ToString().PadRight(maxLevelLength)} | {player.HcAge.ToString().PadRight(maxAgeLength)} | {player.CreatureKills.ToString().PadRight(maxCreatureKillsLength)} | {player.HcPyrealsWon.ToString().PadRight(maxPyrealsWonLength)} | {player.HcScore.ToString().PadRight(maxScoreLength)}");
                rank++;
            }

            result.AppendLine("```");

            string finalResult = result.ToString();

            await hcchannel.SendMessageAsync(finalResult);
        }

        public static async void UpdateHcLeaderboard()
        {
            if (hcchannel is null)
                return;

            List<Player> onlineHcPlayers = new List<Player>();
            List<OfflinePlayer> offlineHcPlayers = new List<OfflinePlayer>();

            foreach (var p in PlayerManager.GetAllOnline())
            {
                if (p.Hardcore == true)
                    onlineHcPlayers.Add(p);
            }

            foreach (var i in PlayerManager.GetAllOffline())
            {
                if (i.Hardcore == true)
                    offlineHcPlayers.Add(i);
            }

            List<IPlayer> allHcPlayers = new List<IPlayer>();
            allHcPlayers.AddRange(onlineHcPlayers);
            allHcPlayers.AddRange(offlineHcPlayers);

            List<IPlayer> playersToRemove = new List<IPlayer>();

            foreach (var p in allHcPlayers)
            {
                if (CalculateHcPlayerAge(p, GetCurrentUnixTime()) >= 5_184_000 && p.Level >= 10000)
                {
                    playersToRemove.Add(p);
                }
            }

            foreach (var p in playersToRemove)
            {
                allHcPlayers.Remove(p);
            }

            List<IPlayer> top10Players = allHcPlayers.OrderByDescending(p => p.HcScore).Take(10).ToList();

            StringBuilder result = new StringBuilder();
            int rank = 1;

            int maxRankLength = Math.Max(4, top10Players.Count.ToString().Length);
            int maxNameLength = Math.Max(26, top10Players.Max(player => player.Name.Length));
            int maxLevelLength = Math.Max(7, top10Players.Max(player => player.Level.ToString().Length));
            int maxAgeLength = Math.Max(15, top10Players.Max(player => player.HcAge.ToString().Length));
            int maxCreatureKillsLength = Math.Max(16, top10Players.Max(player => player.CreatureKills.ToString().Length));
            int maxPyrealsWonLength = Math.Max(16, top10Players.Max(player => player.HcPyrealsWon.ToString().Length));
            int maxScoreLength = Math.Max(11, top10Players.Max(player => player.HcScore.ToString().Length));

            // Format the string using the calculated maximum lengths
            int headerWidth = maxRankLength + maxNameLength + maxLevelLength + maxAgeLength + maxCreatureKillsLength + maxPyrealsWonLength + maxScoreLength + 26;

            // Now, use the constants to format the string
            result.AppendLine("```fix");
            result.AppendLine($"{"Hardcore Top Ten!".PadLeft(headerWidth / 2 + 16)}\n\n");
            result.AppendLine($"{"Rank".PadRight(maxRankLength)} | {"Name".PadRight(maxNameLength)} | {"Level".PadRight(maxLevelLength)} | {"Age".PadRight(maxAgeLength)} | {"Creature Kills".PadRight(maxCreatureKillsLength)} | {"Pyreals Won".PadRight(maxPyrealsWonLength)} | {"Score".PadRight(maxScoreLength)}");
            result.AppendLine($"{"".PadRight(maxRankLength, '-')}-+-{"".PadRight(maxNameLength, '-')}-+-{"".PadRight(maxLevelLength, '-')}-+-{"".PadRight(maxAgeLength, '-')}-+-{"".PadRight(maxCreatureKillsLength, '-')}-+-{"".PadRight(maxPyrealsWonLength, '-')}-+-{"".PadRight(maxScoreLength, '-')}");

            foreach (var player in top10Players)
            {
                result.AppendLine($"{rank.ToString().PadRight(maxRankLength)} | {player.Name.PadRight(maxNameLength)} | {player.Level.ToString().PadRight(maxLevelLength)} | {player.HcAge.ToString().PadRight(maxAgeLength)} | {player.CreatureKills.ToString().PadRight(maxCreatureKillsLength)} | {player.HcPyrealsWon.ToString().PadRight(maxPyrealsWonLength)} | {player.HcScore.ToString().PadRight(maxScoreLength)}");
                rank++;
            }

            result.AppendLine("```");

            string finalResult = result.ToString();

            await hcchannel.SendMessageAsync(finalResult);
        }

        public static long CalculateHcPlayerAge(IPlayer player, double currentUnxiTime)
        {
            var dob = player.GetProperty(PropertyString.DateOfBirth);

            DateTime dateFromString = DateTime.Parse(dob);
            long originalUnixTimestamp = ((DateTimeOffset)dateFromString).ToUnixTimeSeconds();

            DateTime currentDate = DateTime.Now.ToUniversalTime();
            long currentUnixTimestamp = ((DateTimeOffset)currentDate).ToUnixTimeSeconds();

            long ageInSeconds = currentUnixTimestamp - originalUnixTimestamp;

            player.HcAgeTimestamp = ageInSeconds;

            return ageInSeconds;
        }

        public static double GetCurrentUnixTime()
        {
            DateTime currentDate = DateTime.Now.ToUniversalTime();
            double currentUnixTimestamp = ((DateTimeOffset)currentDate).ToUnixTimeSeconds();

            return currentUnixTimestamp;
        }

        public static void QueueMessageForDiscord(string message)
        {
            outgoingMessages.Enqueue(message);
        }
    }
}
