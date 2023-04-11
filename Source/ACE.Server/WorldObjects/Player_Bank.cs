using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace ACE.Server.WorldObjects
{
    class Player_Bank
    {
        public static void GenerateAccountNumber(Player player)
        {
            var generatedNumber = ThreadSafeRandom.Next(000000000, 999999999);

            if (VerifyNumber(player, generatedNumber))
            {
                player.BankAccountNumber = generatedNumber;
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Your account number is {generatedNumber}", ChatMessageType.x1B));
            }
            else
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Failed to create your account, please reissue the command.", ChatMessageType.x1B));
        }

        public static bool VerifyNumber(Player player, int generatedNumber)
        {
            var allplayers = PlayerManager.GetAllPlayers();

            foreach (var character in allplayers)
            {
                if (character.BankAccountNumber != null)
                {
                    if (character.BankAccountNumber == generatedNumber)
                        return false;
                }
            }

            return true;
        }


        public static void Deposit(Player player, long amount, bool all, bool pyreal, bool ashcoin, bool luminance)
        {
            if (player != null)
            {
                var fiftykACNote = player.GetInventoryItemsOfWCID(801910);
                var tenkACNote = player.GetInventoryItemsOfWCID(801909);
                var fivekACNote = player.GetInventoryItemsOfWCID(801908);
                var onekACNote = player.GetInventoryItemsOfWCID(801907);
                var ashCoin = player.GetInventoryItemsOfWCID(801690);
                var mmd = player.GetInventoryItemsOfWCID(20630);
                var pyreals = player.GetInventoryItemsOfWCID(273);
                long totalValue = 0;
                long inheritedValue = 0;
                long inheritedashcoinvalue = 0;
                long lumInheritedValue = 0;
                long oldBalanceP = (long)player.BankedPyreals;
                long oldBalanceL = (long)player.BankedLuminance;
                long oldBalanceA = (long)player.BankedAshcoin;

                if (player.BankedPyreals == null)
                {
                    player.BankedPyreals = 0;
                }

                if (player.BankedLuminance == null)
                {
                    player.BankedLuminance = 0;
                }

                if (player.BankedAshcoin == null)
                {
                    player.BankedAshcoin = 0;
                }

                if (all)
                {
                    if (mmd == null)
                        return;

                    foreach (var item in mmd)
                    {
                        if (item == null)
                            continue;

                        if (item.StackSize > 0)
                            totalValue = (long)item.StackSize * 250000;
                        else
                            totalValue = 250000;

                        player.TryConsumeFromInventoryWithNetworking(20630);

                        if (!player.BankedPyreals.HasValue)
                            player.BankedPyreals = 0;

                        player.BankedPyreals += totalValue;

                        inheritedValue += totalValue;
                    }

                    foreach (var item in fiftykACNote)
                    {
                        if (item == null)
                            continue;

                        if (item.StackSize > 0)
                            totalValue = (long)item.StackSize * 50000;
                        else
                            totalValue = 50000;

                        player.TryConsumeFromInventoryWithNetworking(801910);

                        if (!player.BankedAshcoin.HasValue)
                            player.BankedAshcoin = 0;

                        player.BankedAshcoin += totalValue;

                        inheritedashcoinvalue += totalValue;
                    }

                    foreach (var item in tenkACNote)
                    {
                        if (item == null)
                            continue;

                        if (item.StackSize > 0)
                            totalValue = (long)item.StackSize * 10000;
                        else
                            totalValue = 10000;

                        player.TryConsumeFromInventoryWithNetworking(801909);

                        if (!player.BankedAshcoin.HasValue)
                            player.BankedAshcoin = 0;

                        player.BankedAshcoin += totalValue;

                        inheritedashcoinvalue += totalValue;
                    }

                    foreach (var item in fivekACNote)
                    {
                        if (item == null)
                            continue;

                        if (item.StackSize > 0)
                            totalValue = (long)item.StackSize * 5000;
                        else
                            totalValue = 5000;

                        player.TryConsumeFromInventoryWithNetworking(801908);

                        if (!player.BankedAshcoin.HasValue)
                            player.BankedAshcoin = 0;

                        player.BankedAshcoin += totalValue;

                        inheritedashcoinvalue += totalValue;
                    }

                    foreach (var item in onekACNote)
                    {
                        if (item == null)
                            continue;

                        if (item.StackSize > 0)
                            totalValue = (long)item.StackSize * 1000;
                        else
                            totalValue = 1000;

                        player.TryConsumeFromInventoryWithNetworking(801907);

                        if (!player.BankedAshcoin.HasValue)
                            player.BankedAshcoin = 0;

                        player.BankedAshcoin += totalValue;

                        inheritedashcoinvalue += totalValue;
                    }

                    foreach (var item in ashCoin)
                    {
                        if (item != null)
                        {
                            totalValue = (long)item.StackSize;

                            player.TryConsumeFromInventoryWithNetworking(801690);

                            if (!player.BankedAshcoin.HasValue)
                                player.BankedAshcoin = 0;

                            player.BankedAshcoin += totalValue;

                            inheritedashcoinvalue += totalValue;
                        }
                    }

                    foreach (var item in pyreals)
                    {
                        if (item != null)
                        {
                            totalValue = (long)item.StackSize;

                            player.TryConsumeFromInventoryWithNetworking(273);

                            if (!player.BankedPyreals.HasValue)
                                player.BankedPyreals = 0;

                            player.BankedPyreals += totalValue;

                            inheritedValue += totalValue;
                        }
                    }

                    if (player.AvailableLuminance == null)
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.Broadcast));
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] You banked a total of {inheritedValue:N0} Pyreals, {lumInheritedValue:N0} Luminance, and {inheritedashcoinvalue:N0} AshCoin", ChatMessageType.x1D));
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Old Account Balances: {oldBalanceP:N0} Pyreals || {oldBalanceL:N0} Luminance || {oldBalanceA:N0} AshCoin", ChatMessageType.Help));
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] New Account Balances: {player.BankedPyreals:N0} Pyreals || {player.BankedLuminance:N0} Luminance || {player.BankedAshcoin:N0} AshCoin", ChatMessageType.x1B));
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.Broadcast));
                        return;
                    }
                    if (player.AvailableLuminance > 0)
                    {
                        player.BankedLuminance += player.AvailableLuminance;
                        lumInheritedValue += (long)player.AvailableLuminance;
                        player.AvailableLuminance = 0;
                        player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.AvailableLuminance, player.AvailableLuminance ?? 0));
                    }

                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.Broadcast));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] You banked a total of {inheritedValue:N0} Pyreals, {lumInheritedValue:N0} Luminance, and {inheritedashcoinvalue:N0} AshCoin", ChatMessageType.x1D));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Old Account Balances: {oldBalanceP:N0} Pyreals || {oldBalanceL:N0} Luminance || {oldBalanceA:N0} AshCoin", ChatMessageType.Help));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] New Account Balances: {player.BankedPyreals:N0} Pyreals || {player.BankedLuminance:N0} Luminance || {player.BankedAshcoin:N0} AshCoin", ChatMessageType.x1B));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.Broadcast));
                }
                if (pyreal)
                {
                    long amountDeposited = 0;

                    for (var i = amount; i >= 25000; i -= 25000)
                    {
                        amount -= 25000;
                        player.TryConsumeFromInventoryWithNetworking(273, 25000);
                        player.BankedPyreals += 25000;
                        amountDeposited += 25000;
                    }

                    if (amount < 25000)
                    {
                        player.TryConsumeFromInventoryWithNetworking(273, (int)amount);
                        player.BankedPyreals += amount;
                        amountDeposited += amount;
                    }

                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.Broadcast));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] You banked {amountDeposited:N0} Pyreals", ChatMessageType.x1D));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Old Account Balance: {oldBalanceP:N0} Pyreals", ChatMessageType.Help));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] New Account Balance: {player.BankedPyreals:N0} Pyreals", ChatMessageType.x1B));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.Broadcast));
                }
                if (ashcoin)
                {
                    long amountDeposited = 0;

                    for (var i = amount; i >= 50000; i -= 50000)
                    {
                        amount -= 50000;
                        player.TryConsumeFromInventoryWithNetworking(801690, 50000);
                        player.BankedAshcoin += 50000;
                        amountDeposited += 50000;
                    }

                    if (amount < 50000)
                    {
                        player.TryConsumeFromInventoryWithNetworking(801690, (int)amount);
                        player.BankedAshcoin += amount;
                        amountDeposited += amount;
                    }

                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.Broadcast));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] You banked {amountDeposited:N0} AshCoin", ChatMessageType.x1D));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Old Account Balance: {oldBalanceA:N0} AshCoin", ChatMessageType.Help));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] New Account Balance: {player.BankedAshcoin:N0} AshCoin", ChatMessageType.x1B));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.Broadcast));
                }
                if (luminance)
                {
                    long amountDeposited = 0;

                    player.BankedLuminance += amount;
                    amountDeposited += amount;
                    player.AvailableLuminance -= amount;
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.AvailableLuminance, player.AvailableLuminance ?? 0));

                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.Broadcast));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] You banked {amountDeposited:N0} Luminance", ChatMessageType.x1D));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Old Account Balance: {oldBalanceL:N0} Luminance", ChatMessageType.Help));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] New Account Balance: {player.BankedLuminance:N0} Luminance", ChatMessageType.x1B));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.Broadcast));
                }
            }
            else
                return;
        }

        public static void Send(Player player, int bankAccountNumber)
        {





        }

        public static void HandleInterestPayments(Player player)
        {
            if (player.InterestTimer == null)
            {
                player.InterestTimer = 0L;
            }

            double interestRate = PropertyManager.GetDouble("interest_rate").Item;
            long currentTime = (long)Time.GetUnixTime();
            long duration = (long)(currentTime - player.InterestTimer.Value);
            int payPeriod = 2592000; // 30 days in seconds = 2592000
            int numOfPayPeriods = (int)(duration / payPeriod);

            if (duration >= payPeriod && Time.GetUnixTime() >= player.InterestTimer.Value)
            {
                long payment = (long)(player.BankedPyreals * interestRate);
                long multipayments = payment * numOfPayPeriods;

                if (numOfPayPeriods > 1)
                {
                    player.PyrealSavings += payment * multipayments;
                    player.RemoveProperty(PropertyFloat.InterestTimer);
                    player.InterestTimer = currentTime;

                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.Broadcast));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] You have received an interest payment from the Bank of ValHeel in the amount of: {multipayments:N0}", ChatMessageType.x1D));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] New Savings Account Balance: {player.PyrealSavings:N0} Pyreals", ChatMessageType.x1B));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.Broadcast));
                }
                else
                player.PyrealSavings += payment;
                player.RemoveProperty(PropertyFloat.InterestTimer);
                player.InterestTimer = currentTime;

                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.Broadcast));
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] You have received an interest payment from the Bank of ValHeel in the amount of: {payment:N0}", ChatMessageType.x1D));
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] New Savings Account Balance: {player.PyrealSavings:N0} Pyreals", ChatMessageType.x1B));
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.Broadcast));
            }
        }
    }
}
