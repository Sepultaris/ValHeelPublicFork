using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity.Actions;
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


        public static void Deposit(Player player, long amount, bool all, bool pyreal, bool ashcoin, bool luminance, bool pyrealSavings)
        {
            if (player != null)
            {
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

                if (player.PyrealSavings == null)
                {
                    player.PyrealSavings = 0;
                }

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
                long oldBalancePSavings = (long)player.PyrealSavings;
                long oldBalanceL = (long)player.BankedLuminance;
                long oldBalanceA = (long)player.BankedAshcoin;

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
                if (pyrealSavings)
                {
                    long amountDeposited = 0;

                    for (var i = amount; i >= 25000; i -= 25000)
                    {
                        amount -= 25000;
                        player.TryConsumeFromInventoryWithNetworking(273, 25000);
                        player.PyrealSavings += 25000;
                        amountDeposited += 25000;
                    }

                    if (amount < 25000)
                    {
                        player.TryConsumeFromInventoryWithNetworking(273, (int)amount);
                        player.PyrealSavings += amount;
                        amountDeposited += amount;
                    }

                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.Broadcast));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] You banked {amountDeposited:N0} Pyreals", ChatMessageType.x1D));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] Old Account Balance: {oldBalancePSavings:N0} Pyreals in savings", ChatMessageType.Help));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK] New Account Balance: {player.PyrealSavings:N0} Pyreals in savings", ChatMessageType.x1B));
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
            if (player.BankAccountNumber != null)
            {
                double interestRate = PropertyManager.GetDouble("interest_rate").Item;
                double repBoost = 0;
                double finalInterest = 0;

                if (player.QuestManager.HasQuestCompletes("Reputation"))
                {
                    int reputation = player.QuestManager.GetCurrentSolves("Reputation");

                    if (reputation >= 0 && reputation <= 14999)
                        repBoost = 0.01;
                    else if (reputation >= 15000 && reputation <= 24999)
                        repBoost = 0.017;
                    else if (reputation >= 25000 && reputation <= 49999)
                        repBoost = 0.024;
                    else if (reputation >= 50000 && reputation <= 99999)
                        repBoost = 0.032;
                    else if (reputation >= 100000 && reputation <= 199999)
                        repBoost = 0.04;
                    else if (reputation >= 200000)
                        repBoost = 0.046;
                    else repBoost = 0;

                    finalInterest = interestRate + repBoost;
                }

                long currentTime = (long)Time.GetUnixTime();

                if (player.InterestTimer == null)
                {
                    player.InterestTimer = currentTime;
                }
                
                long duration = (long)(currentTime - player.InterestTimer.Value);
                long interestPeriod = PropertyManager.GetLong("interest_period").Item;

                if (interestPeriod <= 0)
                {
                    interestPeriod = 30;
                }

                long payPeriod = 86400 * interestPeriod; // 1 day in seconds = 86400
                int numOfPayPeriods = (int)(duration / payPeriod);

                if (duration >= payPeriod && Time.GetUnixTime() >= player.InterestTimer.Value)
                {
                    long payment = (long)(player.BankedPyreals * finalInterest);
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
}
