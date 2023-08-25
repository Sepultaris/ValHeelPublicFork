using System;
using System.Collections.Generic;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Command.Handlers;
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
            if (contractor == null)
                return;
            if (target == null)
                return;

            if (!AllPlayersNamesList().Contains(target))
            {
                contractor.Session.Network.EnqueueSend(new GameMessageSystemChat($"A player with the name {target} could not be found.", ChatMessageType.Broadcast));
                return;
            }

            if (target == contractor.Name)
            {
                contractor.Session.Network.EnqueueSend(new GameMessageSystemChat($"You cannot place a bounty on yourself.", ChatMessageType.Broadcast));
                return;
            }

            if (amount <= 0)
            {
                contractor.Session.Network.EnqueueSend(new GameMessageSystemChat($"You must place a bounty of at least 1 AshCoin.", ChatMessageType.Broadcast));
                return;
            }

            if (contractor.BankedAshcoin < amount)
            {
                contractor.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough AshCoin to place that bounty.", ChatMessageType.Broadcast));
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

                    contractor.BankedAshcoin -= amount;
                    ValHeelCurrencyMarket.RemoveACFromCirculation(amount);
                    contractor.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have placed a bounty on {target}'s head for {amount} AshCoin.", ChatMessageType.Broadcast));
                }
            }

            foreach (var p in PlayerManager.GetAllOnline())
            {
                if (p.Name == target && p.PriceOnHead == null || p.Name == target && p.PriceOnHead == 0)
                    p.Session.Network.EnqueueSend(new GameMessageSystemChat($"{contractor.Name} has placed a bounty on your head for {amount} AshCoin.", ChatMessageType.Broadcast));
                if (p.Name == target && p.PriceOnHead > 0)
                    p.Session.Network.EnqueueSend(new GameMessageSystemChat($"{contractor.Name} has increased the bounty on your head by {amount} AshCoin. Your bounty is now {p.PriceOnHead} AshCoin", ChatMessageType.Broadcast));

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

        public static void PayOffBounty(Player player)
        {
            var cost = player.PriceOnHead * 2;

            if (player.PriceOnHead == null || player.PriceOnHead == 0)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have a bounty on your head.", ChatMessageType.Broadcast));
                return;
            }

            if (player.BankedAshcoin < cost)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough AshCoin to pay off your bounty. You need {cost} AshCoin.", ChatMessageType.Broadcast));
                return;
            }

            if (player.PriceOnHead > 1000000)
            {
                player.PlayerKillerStatus = PlayerKillerStatus.NPK;
                player.Session.Network.EnqueueSend(new GameMessagePublicUpdatePropertyInt(player, PropertyInt.PlayerKillerStatus, (int)player.PlayerKillerStatus));
                player.SetProperty(PropertyInt64.PriceOnHead, 0);
                player.SetProperty(PropertyBool.HasBounty, false);
                player.BankedAshcoin -= cost;
                ValHeelCurrencyMarket.RemoveACFromCirculation((long)cost);
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have paid off your bounty at a cost of {cost} AshCoin", ChatMessageType.Broadcast));
                return;
            }
            else
            {
                player.SetProperty(PropertyInt64.PriceOnHead, 0);
                player.SetProperty(PropertyBool.HasBounty, false);
                player.BankedAshcoin -= cost;
                ValHeelCurrencyMarket.RemoveACFromCirculation((long)cost);
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have paid off your bounty at a cost of {cost} AshCoin", ChatMessageType.Broadcast));
                return;
            }
        }
    }
}
