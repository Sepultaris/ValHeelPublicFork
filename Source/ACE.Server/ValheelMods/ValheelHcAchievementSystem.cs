using ACE.Entity.Enum;
using ACE.Server.Factories;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using System;

namespace ACE.Server.ValheelMods
{
    public class HcAchievementSystem
    {
        private static readonly int[] MonsterKillsMilestones = { 100, 1000, 10000, 100000, 500000, 1000000 };
        private static readonly ulong[] HcPyrealsWonMilestones = { 10000, 100000, 500000, 1000000, 5000000, 10000000 };
        private static readonly int[] HcScoreMilestones = { 10, 100, 1000, 10000, 100000, 500000 };
        private static readonly int[] LevelMilestones = { 100, 275, 700, 1500, 5000, 7500, 10000 };
        private static readonly int[] PrestigeMilestones = { 1, 10, 25, 50, 75, 100 };
        public static void CheckAndHandleAchievements(Player player)
        {
            CheckMonsterKillsAchievements(player);
            CheckBankedPyrealsAchievements(player);
            CheckHcScoreAchievements(player);
            CheckLevelAchievements(player);
            CheckPrestigeAchievements(player);
        }

        private static void CheckPrestigeAchievements(Player player)
        {
            if (player.PrestigeMilestones >= 6)
                return;

            for (int i = 0; i < PrestigeMilestones.Length; i++)
            {
                if (player.PrestigeMilestones < PrestigeMilestones.Length && player.QuestManager.GetCurrentSolves("Prestige") >= PrestigeMilestones[player.PrestigeMilestones])
                {
                    long winnings = 250000 * (((player.PrestigeMilestones * player.PrestigeMilestones) * 2) * 10);

                    GrantAchievement($"Prestigious {i + 1}", player, winnings);
                    player.PrestigeMilestones++;
                    if (player.BankedPyreals == null)
                        player.BankedPyreals = (long)player.HcPyrealsWon;
                    player.BankedPyreals = player.BankedPyreals + winnings;

                    int amount = 0;

                    if (player.PrestigeMilestones == 1)
                        amount = 5000;
                    if (player.PrestigeMilestones == 2)
                        amount = 15000;
                    if (player.PrestigeMilestones == 3)
                        amount = 30000;
                    if (player.PrestigeMilestones == 4)
                        amount = 50000;
                    if (player.PrestigeMilestones == 5)
                        amount = 75000;
                    if (player.PrestigeMilestones == 6)
                        amount = 100000;

                    player.QuestManager.Increment("Reputation", amount);
                    player.GrantLevelProportionalXp(0.75, 1000, 1000000000000, false);
                }
            }
        }

        private static void CheckMonsterKillsAchievements(Player player)
        {
            if (player.MonsterKillsMilestones >= 6)
                return;

            for (int i = 0; i < MonsterKillsMilestones.Length; i++)
            {
                if (player.MonsterKillsMilestones < MonsterKillsMilestones.Length && player.CreatureKills >= MonsterKillsMilestones[player.MonsterKillsMilestones])
                {
                    long winnings = 250000 * (((player.MonsterKillsMilestones * player.MonsterKillsMilestones) * 2) * 10);

                    GrantAchievement($"Monster Slayer {player.MonsterKillsMilestones + 1}", player, winnings);
                    player.MonsterKillsMilestones++;
                    if (player.BankedPyreals == null)
                        player.BankedPyreals = (long)player.HcPyrealsWon;
                    player.BankedPyreals = player.BankedPyreals + winnings;
                    var wo = WorldObjectFactory.CreateNewWorldObject(802614);
                    var amount = (((player.MonsterKillsMilestones * player.MonsterKillsMilestones) * 2) * 10);
                    wo.SetStackSize(amount);
                    player.TryCreateInInventoryWithNetworking(wo);
                    player.GrantLevelProportionalXp(0.75, 1000, 1000000000000, false);
                }
            }
        }

        private static void CheckBankedPyrealsAchievements(Player player)
        {
            if (player.HcPyrealsWonMilestones >= 6)
                return;

            for (int i = 0; i < HcPyrealsWonMilestones.Length; i++)
            {
                if (player.HcPyrealsWonMilestones < HcPyrealsWonMilestones.Length && player.HcPyrealsWon >= HcPyrealsWonMilestones[player.HcPyrealsWonMilestones])
                {
                    long winnings = 250000 * (((player.HcPyrealsWonMilestones * player.HcPyrealsWonMilestones) * 2) * 10);

                    GrantAchievement($"Wealthy Banker {player.HcPyrealsWonMilestones + 1}", player, winnings);
                    player.HcPyrealsWonMilestones++;

                    if (player.BankedPyreals == null)
                        player.BankedPyreals = (long)player.HcPyrealsWon;

                    player.BankedPyreals = player.BankedPyreals + winnings;
                    var wo = WorldObjectFactory.CreateNewWorldObject(802759);
                    var amount = (((player.HcPyrealsWonMilestones * player.HcPyrealsWonMilestones) * 2) * 10);
                    wo.SetStackSize(amount);
                    player.TryCreateInInventoryWithNetworking(wo);
                    player.GrantLevelProportionalXp(0.75, 1000, 1000000000000, false);
                }
            }
        }

        private static void CheckHcScoreAchievements(Player player)
        {
            if (player.HcScoreMilestones >= 6)
                return;

            for (int i = 0; i < HcScoreMilestones.Length; i++)
            {
                if (player.HcScoreMilestones < HcScoreMilestones.Length && player.HcScore >= HcScoreMilestones[player.HcScoreMilestones])
                {
                    long winnings = 250000 * (((player.HcScoreMilestones * player.HcScoreMilestones) * 2) * 10);

                    GrantAchievement($"Hardcore Master {player.HcScoreMilestones + 1}", player, winnings);
                    player.HcScoreMilestones++;
                    if (player.BankedPyreals == null)
                        player.BankedPyreals = (long)player.HcPyrealsWon;

                    player.BankedPyreals = player.BankedPyreals + winnings;

                    var wo = WorldObjectFactory.CreateNewWorldObject(802812);

                    int amount = 0;

                    if (player.HcScoreMilestones == 1)
                    {
                        amount = 1;
                    }
                    else if (player.HcScoreMilestones == 3)
                    {
                        amount = 1;
                    }
                    else if (player.HcScoreMilestones == 6)
                    {
                        amount = 1;
                    }
                    else
                        amount = 0;

                    if (amount > 0)
                    {
                        wo.SetStackSize(amount);
                        player.TryCreateInInventoryWithNetworking(wo);
                    }

                    player.GrantLevelProportionalXp(0.75, 1000, 1000000000000, false);
                }
            }
        }

        private static void CheckLevelAchievements(Player player)
        {
            if (player.LevelMilestones >= 6)
                return;

            for (int i = 0; i < LevelMilestones.Length; i++)
            {
                if (player.LevelMilestones < LevelMilestones.Length && player.Level >= LevelMilestones[player.LevelMilestones])
                {
                    long winnings = 250000 * (((player.LevelMilestones * player.LevelMilestones) * 2) * 10);

                    GrantAchievement($"Leveling Up {player.LevelMilestones + 1}", player, winnings);
                    player.LevelMilestones++;
                    if (player.BankedPyreals == null)
                        player.BankedPyreals = (long)player.HcPyrealsWon;

                    player.BankedPyreals = player.BankedPyreals + winnings;
                    var wo = WorldObjectFactory.CreateNewWorldObject(801690);

                    int amount = 0;

                    if (player.LevelMilestones == 1)
                        amount = 10;
                    if (player.LevelMilestones == 2)
                        amount = 100;
                    if (player.LevelMilestones == 3)
                        amount = 1000;
                    if (player.LevelMilestones == 4)
                        amount = 5000;
                    if (player.LevelMilestones == 5)
                        amount = 10000;
                    if (player.LevelMilestones == 6)
                        amount = 50000;

                    wo.SetStackSize(amount);
                    player.TryCreateInInventoryWithNetworking(wo);
                    player.GrantLevelProportionalXp(0.75, 1000, 1000000000000, false);
                }
            }
        }

        private static void GrantAchievement(string achievementName, Player player, long winnings)
        {
            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Congratulations! You have unlocked the achievement: {achievementName}.", ChatMessageType.x1B));
            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK]{winnings} Pyreals have been deposited into your account.", ChatMessageType.x1B));
            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"[BANK]New balance: {player.BankedPyreals} Pyreals.", ChatMessageType.x1B));
            player.PlayParticleEffect(PlayScript.WeddingBliss, player.Guid);
        }
    }

}
