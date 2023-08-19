using System;
using System.Collections.Generic;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.ValheelMods
{
    internal class ValHeelBounty
    {
        public static bool HasBountyCheck(string player)
        {
            if (ActiveBoutiesNames().Contains(player))
                return true;
            else
                return false;
        }

        public static void PlaceBounty(Player contractor, string target, long amount)
        {
            if (!AllPlayersNamesList().Contains(target))
            {
                contractor.Session.Network.EnqueueSend(new GameMessageSystemChat($"{target} could not be found.", ChatMessageType.Broadcast));
                return;
            }

            if (target == contractor.Name)
            {
                contractor.Session.Network.EnqueueSend(new GameMessageSystemChat($"You cannot place a bounty on yourself.", ChatMessageType.Broadcast));
                return;
            }

            if (contractor.BankedAshcoin < amount)
            {
                contractor.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough AshCoin to place a bounty on {target}.", ChatMessageType.Broadcast));
                return;
            }

            foreach (var p in AllPlayersList())
            {
                if (p.Name == target)
                {
                    if (p.HasBounty == false)
                        p.SetProperty(PropertyBool.HasBounty, true);
                    if (p.PriceOnHead == null)
                        p.SetProperty(PropertyInt64.PriceOnHead, amount);
                    else
                    {
                        long newPrice = (long)p.PriceOnHead + amount;

                        p.SetProperty(PropertyInt64.PriceOnHead, newPrice);
                    }
                }
            }

            foreach (var p in PlayerManager.GetAllOnline())
            {
                if (p.Name == target)
                    p.Session.Network.EnqueueSend(new GameMessageSystemChat($"{contractor.Name} has placed a bounty on your head for {amount} AshCoin.", ChatMessageType.Broadcast));
            }
        }

        public static List<string> ActiveBoutiesNames()
        {
            List<string> playerList = new List<string>();

            foreach (var p in ActiveBoutiesPlayerList())
            {
                playerList.Add(p.Name);
            }

            return playerList;
        }

        public static List<IPlayer> ActiveBoutiesPlayerList()
        {
            List<Player> onlinePlayers = new List<Player>();
            List<OfflinePlayer> offlinePlayers = new List<OfflinePlayer>();
            List<IPlayer> allPlayers = new List<IPlayer>();

            foreach (var p in PlayerManager.GetAllOnline())
            {
                if (p.HasBounty)
                    onlinePlayers.Add(p);
            }

            foreach (var p in PlayerManager.GetAllOffline())
            {
                if (p.HasBounty)
                    offlinePlayers.Add(p);
            }

            allPlayers.AddRange(onlinePlayers);
            allPlayers.AddRange(offlinePlayers);

            return allPlayers;
        }

        public static List<IPlayer> AllPlayersList()
        {
            List<Player> onlinePlayers = new List<Player>();
            List<OfflinePlayer> offlinePlayers = new List<OfflinePlayer>();
            List<IPlayer> allPlayers = new List<IPlayer>();

            foreach (var p in PlayerManager.GetAllOnline())
            {
                onlinePlayers.Add(p);
            }

            foreach (var p in PlayerManager.GetAllOffline())
            {
                offlinePlayers.Add(p);
            }

            allPlayers.AddRange(onlinePlayers);
            allPlayers.AddRange(offlinePlayers);

            return allPlayers;
        }

        public static List<string> AllPlayersNamesList()
        {
            List<string> playerList = new List<string>();

            foreach (var p in AllPlayersList())
            {
                playerList.Add(p.Name);
            }

            return playerList;
        }
    }
}
