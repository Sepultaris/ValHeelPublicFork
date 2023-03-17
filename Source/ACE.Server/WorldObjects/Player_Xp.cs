using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Common.Extensions;
using ACE.DatLoader;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        public long XPGained = 0;

        /// <summary>
        /// A player earns XP through natural progression, ie. kills and quests completed
        /// </summary>
        /// <param name="amount">The amount of XP being added</param>
        /// <param name="xpType">The source of XP being added</param>
        /// <param name="shareable">True if this XP can be shared with Fellowship</param>
        public void EarnXP(long amount, XpType xpType, ShareType shareType = ShareType.All)
        {
            //Console.WriteLine($"{Name}.EarnXP({amount}, {sharable}, {fixedAmount})");

            // apply xp modifier
            var modifier = PropertyManager.GetDouble("xp_modifier").Item;

            // Quest Point Allegiance Allocation Checks.


            //additive enlightenment bonus with enchantments.
            var enlightenBonus = 0.25f * Enlightenment; // 25% XP bonus per enlightenment

            // should this be passed upstream to fellowship / allegiance?
            var enchantment = GetXPAndLuminanceModifier(xpType) + enlightenBonus;


            var m_amount = (long)Math.Round(amount * enchantment * modifier);

            if (m_amount < 0)
            {
                log.Warn($"{Name}.EarnXP({amount}, {shareType})");
                log.Warn($"modifier: {modifier}, enchantment: {enchantment}, m_amount: {m_amount}");
                return;
            }

            GrantXP(m_amount, xpType, shareType);
        }

        /// <summary>
        /// Directly grants XP to the player, without the XP modifier
        /// </summary>
        /// <param name="amount">The amount of XP to grant to the player</param>
        /// <param name="xpType">The source of the XP being granted</param>
        /// <param name="shareable">If TRUE, this XP can be shared with fellowship members</param>
        public void GrantXP(long amount, XpType xpType, ShareType shareType = ShareType.All)
        {
            if (Fellowship != null && Fellowship.ShareXP && shareType.HasFlag(ShareType.Fellowship)/*(xpType == XpType.Emote || xpType == XpType.Fellowship || xpType == XpType.Kill || xpType == XpType.Quest)*/)
            {
                // this will divy up the XP, and re-call this function
                // with ShareType.Fellowship removed
                Fellowship.SplitXp((ulong)amount, xpType, shareType, this);
                return;
            }

            XPGained = amount;

            // Make sure UpdateXpAndLevel is done on this players thread
            EnqueueAction(new ActionEventDelegate(() => UpdateXpAndLevel(amount, xpType)));

            // for passing XP up the allegiance chain,
            // this function is only called at the very beginning, to start the process.
            if (shareType.HasFlag(ShareType.Allegiance))
                UpdateXpAllegiance(amount);

            // only certain types of XP are granted to items
            if (xpType == XpType.Kill || xpType == XpType.Quest)
                GrantItemXP(amount);
        }

        /// <summary>
        /// Adds XP to a player's total XP, handles triggers (vitae, level up)
        /// </summary>
        private void UpdateXpAndLevel(long amount, XpType xpType)
        {
            // until we are max level we must make sure that we send
            var
                xpTable = DatManager.PortalDat.XpTable;

            var maxLevel = GetMaxLevel();
            var maxLevelXp = 9223372036854775807UL; // max int64 number

            if (Level != maxLevel)
            {
                var addAmount = amount;

                var amountLeftToEnd = (long)maxLevelXp - TotalExperience ?? 0;
                if (amount > amountLeftToEnd)
                    addAmount = amountLeftToEnd;

                AvailableExperience += addAmount;
                TotalExperience += addAmount;

                var xpTotalUpdate = new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.TotalExperience, TotalExperience ?? 0);
                var xpAvailUpdate = new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.AvailableExperience, AvailableExperience ?? 0);
                Session.Network.EnqueueSend(xpTotalUpdate, xpAvailUpdate);

                CheckForLevelup();
            }

            if (xpType == XpType.Quest)
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You've earned {amount:N0} experience.", ChatMessageType.Broadcast));

            if (HasVitae && xpType != XpType.Allegiance)
                UpdateXpVitae(amount);
        }

        /// <summary>
        /// Optionally passes XP up the Allegiance tree
        /// </summary>
        private void UpdateXpAllegiance(long amount)
        {
            if (!HasAllegiance) return;

            AllegianceManager.PassXP(AllegianceNode, (ulong)amount, true);
        }

        /// <summary>
        /// Handles updating the vitae penalty through earned XP
        /// </summary>
        /// <param name="amount">The amount of XP to apply to the vitae penalty</param>
        private void UpdateXpVitae(long amount)
        {
            var vitae = EnchantmentManager.GetVitae();

            if (vitae == null)
            {
                log.Error($"{Name}.UpdateXpVitae({amount}) vitae null, likely due to cross-thread operation or corrupt EnchantmentManager cache. Please report this.");
                log.Error(Environment.StackTrace);
                return;
            }

            var vitaePenalty = vitae.StatModValue;
            var startPenalty = vitaePenalty;

            var maxPool = VitaeCPPoolThreshold(vitaePenalty, DeathLevel.Value);
            var curPool = VitaeCpPool + amount;

            while (curPool >= (long)maxPool)
            {
                curPool -= (long)maxPool;
                vitaePenalty = EnchantmentManager.ReduceVitae();
                if (vitaePenalty == 1.0f)
                    break;
                maxPool = VitaeCPPoolThreshold(vitaePenalty, DeathLevel.Value);
            }
            VitaeCpPool = curPool;

            Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.VitaeCpPool, VitaeCpPool.Value));

            if (vitaePenalty != startPenalty)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"Your experience has reduced your Vitae penalty!", ChatMessageType.Magic));
                EnchantmentManager.SendUpdateVitae();
            }

            Session.Network.EnqueueSend(new GameMessageSystemChat($"Xp Required to reduce Vitae Penalty: {VitaeCpPool:N0}/{maxPool:n0}", ChatMessageType.Magic));

            if (vitaePenalty.EpsilonEquals(1.0f) || vitaePenalty > 1.0f)
            {
                var actionChain = new ActionChain();
                actionChain.AddDelaySeconds(2.0f);
                actionChain.AddAction(this, () =>
                {
                    var vitae = EnchantmentManager.GetVitae();
                    if (vitae != null)
                    {
                        var curPenalty = vitae.StatModValue;
                        if (curPenalty.EpsilonEquals(1.0f) || curPenalty > 1.0f)
                            EnchantmentManager.RemoveVitae();
                    }
                });
                actionChain.EnqueueChain();
            }
        }

        /// <summary>
        /// Returns the maximum possible character level
        /// </summary>
        public static uint GetMaxLevel()
        {
            return 10000;
        }

        /// <summary>
        /// Returns TRUE if player >= MaxLevel
        /// </summary>
        public bool IsMaxLevel => Level >= GetMaxLevel();

        /// <summary>
        /// Returns the remaining XP required to reach a level
        /// </summary>
        public long? GetRemainingXP(uint level)
        {
            var maxLevel = GetMaxLevel();
            if (level < 1)
                return null;


            if (level >= 275)
            {
                return TotalXpBeyond - TotalExperience.Value;
            }
            else
            {
                var levelTotalXP = DatManager.PortalDat.XpTable.CharacterLevelXPList[(int)level];
                return (long)levelTotalXP - TotalExperience.Value;
            }
        }

        /// <summary>
        /// Returns the remaining XP required to the next level
        /// </summary>
        /*public ulong GetRemainingXP()
        {
            var maxLevel = GetMaxLevel();
            if (Level >= maxLevel)
                return 0;

            var nextLevelTotalXP = DatManager.PortalDat.XpTable.CharacterLevelXPList[Level.Value + 1];

            return nextLevelTotalXP - (ulong)TotalExperience.Value;
        }*/

        /// <summary>
        /// Returns the total XP required to reach a level
        /// </summary>
        public static ulong GetTotalXP(int level)
        {
            var maxLevel = GetMaxLevel();
            if (level < 0 || level > maxLevel)
                return 0;

            return DatManager.PortalDat.XpTable.CharacterLevelXPList[level];
        }

        /// <summary>
        /// Returns the total amount of XP required for a player reach max level
        /// </summary>
        public static long MaxLevelXP
        {
            get
            {
                var xpTable = DatManager.PortalDat.XpTable.CharacterLevelXPList;

                return (long)xpTable[xpTable.Count - 0];
            }
        }

        /// <summary>
        /// Returns the XP required to go from level A to level B
        /// </summary>
        public ulong GetXPBetweenLevels(int levelA, int levelB)
        {
            // special case for max level
            var maxLevel = (int)GetMaxLevel();

            levelA = Math.Clamp(levelA, 1, maxLevel);
            levelB = Math.Clamp(levelB, 1, maxLevel);

            ulong levelA_totalXP = 0;
            ulong levelB_totalXP = 0;

            if (levelA < 275)
            {
                levelA_totalXP = DatManager.PortalDat.XpTable.CharacterLevelXPList[levelA];
                levelB_totalXP = DatManager.PortalDat.XpTable.CharacterLevelXPList[levelB];
            }
            else
            {
                levelA_totalXP = (ulong)TotalXpBeyond - 10000000000;
                levelB_totalXP = (ulong)TotalXpBeyond;
            }
            //Session.Network.EnqueueSend(new GameMessageSystemChat($"{levelB_totalXP - levelA_totalXP:N0} BETWEEN", ChatMessageType.Broadcast));

            return levelB_totalXP - levelA_totalXP;
        }

        public ulong GetXPToNextLevel(int level)
        {
            return GetXPBetweenLevels(level, level + 1);
        }

        /// <summary>
        /// Determines if the player has advanced a level
        /// </summary>
        private void CheckForLevelup()
        {
            var xpTable = DatManager.PortalDat.XpTable;

            var maxLevel = GetMaxLevel();

            if (Level >= maxLevel) return;

            var startingLevel = Level;
            bool creditEarned = false;

            // if level is not under 275(false) do normal stuff, otherwise if over 274(true) do custom function.
            // increases until the correct level is found
            while ((ulong)(TotalExperience ?? 0) >= ((Level < 275) ? xpTable.CharacterLevelXPList[(Level ?? 0) + 1] : (ulong)Catch275(false, startingLevel)))
            {
                Level++;

                // increase the skill credits if the chart allows this level to grant a credit

                // gotta limit this to levels 275 and under otherwise causes crash.
                if (Level <= 275)
                {
                    if (xpTable.CharacterLevelSkillCreditList[Level ?? 0] > 0)
                    {
                        AvailableSkillCredits += (int)xpTable.CharacterLevelSkillCreditList[Level ?? 0];
                        TotalSkillCredits += (int)xpTable.CharacterLevelSkillCreditList[Level ?? 0];
                        creditEarned = true;
                    }
                }

                // if a player is level 300 or higher and level is also divisible by 5 evenly give a skill credit.
                if (Level > 275)
                {
                    if (Level >= 275 && Level % 5 == 0)
                    {
                        AvailableSkillCredits += 1;
                        TotalSkillCredits += 1;
                        creditEarned = true;
                    }
                }

                // break if we reach max
                if (Level == maxLevel)
                {
                    PlayParticleEffect(PlayScript.WeddingBliss, Guid);
                    break;
                }
            }


            if (Level > startingLevel && Level < 275)
            {
                var message = (Level == maxLevel) ? $"You have reached the maximum level of {Level}!" : $"You are now level {Level}!";
                message += (AvailableSkillCredits > 0) ? $"\nYou have {AvailableExperience:#,###0} experience points and {AvailableSkillCredits} skill credits available to raise skills and attributes." : $"\nYou have {AvailableExperience:#,###0} experience points available to raise skills and attributes.";

                var levelUp = new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.Level, Level ?? 1);
                var currentCredits = new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.AvailableSkillCredits, AvailableSkillCredits ?? 0);

                // 275 and under code.
                if (Level != maxLevel && !creditEarned && Level < 275)
                {
                    var nextLevelWithCredits = 0;

                    for (int i = (Level ?? 0) + 1; i <= maxLevel; i++)
                    {
                        if (xpTable.CharacterLevelSkillCreditList[i] > 0)
                        {
                            nextLevelWithCredits = i;
                            break;
                        }
                    }
                    message += $"\nYou will earn another skill credit at level {nextLevelWithCredits}.";
                }

                if (Fellowship != null)
                    Fellowship.OnFellowLevelUp(this);

                if (AllegianceNode != null)
                    AllegianceNode.OnLevelUp();

                Session.Network.EnqueueSend(levelUp);

                SetMaxVitals();

                // play level up effect
                PlayParticleEffect(PlayScript.LevelUp, Guid);

                Session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.Advancement), currentCredits);
            }


            if (Level > startingLevel && Level >= 275)
            {
                var incrementxp = 10000000000;
                var message = (Level == maxLevel) ? $"You have reached the maximum level of {Level}!" : $"You are now level {Level}! You need {incrementxp:N0} to reach {Level + 1}";
                //Session.Network.EnqueueSend(new GameMessageSystemChat($"You need {incrementxp:N0} to reach {Level + 1}", ChatMessageType.Advancement));
                message += (AvailableSkillCredits > 0) ? $"\nYou have {AvailableExperience:#,###0} experience points and {AvailableSkillCredits} skill credits available to raise skills and attributes." : $"\nYou have {AvailableExperience:#,###0} experience points available to raise skills and attributes.";

                var levelUp = new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.Level, Level ?? 1);
                var currentCredits = new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.AvailableSkillCredits, AvailableSkillCredits ?? 0);

                // 275 and under code.
                if (Level != maxLevel && !creditEarned && Level <= 275)
                {
                    var nextLevelWithCredits = 0;

                    for (int i = (Level ?? 0) + 1; i <= maxLevel; i++)
                    {
                        if (xpTable.CharacterLevelSkillCreditList[i] > 0)
                        {
                            nextLevelWithCredits = i;
                            break;
                        }
                    }
                    message += $"\nYou will earn another skill credit at level {nextLevelWithCredits}.";
                }

                if (Fellowship != null)
                    Fellowship.OnFellowLevelUp(this);

                if (AllegianceNode != null)
                    AllegianceNode.OnLevelUp();

                Session.Network.EnqueueSend(levelUp);

                SetMaxVitals();

                // play level up effect
                PlayParticleEffect(PlayScript.LevelUp, Guid);

                Session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.Advancement), currentCredits);
            }
        }
        /// <summary>
        /// This function is just meant to catch a player right as they hit level 275 and transfer from using the Vanilla dats from 1-275, to a custom formula.
        /// It finds all its values, by using the total experience a player would have at level 275 and then divides it by 56. Why 56? Because it was the number that I felt closest resembled retails xp per level increases at around 270-275.
        /// It will then multiply it by the players level minus 275. EX. a level 274 player levels to 275 -> 275-275 is 0 -> now we take the total xp for a level 275 divide it by 56 and then multiply it by 1.10 -> giving us the xp between levels.
        /// Now we need to store the Total XP beyond 275 somewhere, in this case we store it on the player as a int64 named TotalXpBeyond. This variable will then have the formula above added to it each time it passes through, giving us a target
        /// total xp value to reach the next level.
        /// WARNING: if you grant a player enough xp to boost their level to an absurd level from under 275 the calculation has room for xp discrepencies because TotalXpBeyond doesn't get set until hitting 275. Nothing gamebreaking, but in case
        /// you have OCD lol.
        /// </summary>
        /// <param name="maxcheck"></param>
        /// <param name="startingLevel"></param>
        /// <returns></returns>
        public long? Catch275(bool maxcheck, int? startingLevel)
        {
            if (!maxcheck)
            {
                var incrementxp1 = 10000000000; // allows for a more dynamic increase per level.

                if (Level > startingLevel)
                {
                    if (!TotalXpBeyond.HasValue || TotalXpBeyond == 0)
                        TotalXpBeyond = 191226310247 + (long)incrementxp1;
                    else
                        TotalXpBeyond += (long)incrementxp1;
                }

                ///var currentxp = TotalXpBeyond;

                ///var currentremaining = currentxp - TotalExperience;

                /*Session.Network.EnqueueSend(new GameMessageSystemChat($"You need {currentremaining:N0}xp to reach level {Level + 1}. Required total xp is {currentxp:N0}", ChatMessageType.Broadcast));
                if (GetXPToNextLevel((int)Level) <= 0)
                    Level++;*/


                var nextLevel = TotalXpBeyond;
                ///Session.Network.EnqueueSend(new GameMessageSystemChat($"You need {incrementxp:N0} to reach {Level + 1}", ChatMessageType.Advancement));
                
                LastLevel = Level;
              
                return (long)nextLevel;
            }

            return (long)TotalXpBeyond;
        }
        /// <summary>
        /// Spends the amount of XP specified, deducting it from available experience
        /// </summary>
        public bool SpendXP(long amount, bool sendNetworkUpdate = true)
        {
            if (amount > AvailableExperience)
                return false;

            AvailableExperience -= amount;

            if (sendNetworkUpdate)
                Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.AvailableExperience, AvailableExperience ?? 0));

            return true;
        }

        /// <summary>
        /// Tries to spend all of the players Xp into Attributes, Vitals and Skills
        /// </summary>
        public void SpendAllXp(bool sendNetworkUpdate = true)
        {
            SpendAllAvailableAttributeXp(Strength, sendNetworkUpdate);
            SpendAllAvailableAttributeXp(Endurance, sendNetworkUpdate);
            SpendAllAvailableAttributeXp(Coordination, sendNetworkUpdate);
            SpendAllAvailableAttributeXp(Quickness, sendNetworkUpdate);
            SpendAllAvailableAttributeXp(Focus, sendNetworkUpdate);
            SpendAllAvailableAttributeXp(Self, sendNetworkUpdate);

            SpendAllAvailableVitalXp(Health, sendNetworkUpdate);
            SpendAllAvailableVitalXp(Stamina, sendNetworkUpdate);
            SpendAllAvailableVitalXp(Mana, sendNetworkUpdate);

            foreach (var skill in Skills)
            {
                if (skill.Value.AdvancementClass >= SkillAdvancementClass.Trained)
                    SpendAllAvailableSkillXp(skill.Value, sendNetworkUpdate);
            }
        }

        /// <summary>
        /// Gives available XP of the amount specified, without increasing total XP
        /// </summary>
        public void RefundXP(long amount)
        {
            AvailableExperience += amount;

            var xpUpdate = new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.AvailableExperience, AvailableExperience ?? 0);
            Session.Network.EnqueueSend(xpUpdate);
        }

        public void HandleMissingXp()
        {
            var verifyXp = GetProperty(PropertyInt64.VerifyXp) ?? 0;
            if (verifyXp == 0) return;

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(5.0f);
            actionChain.AddAction(this, () =>
            {
                var xpType = verifyXp > 0 ? "unassigned experience" : "experience points";

                var msg = $"This character was missing some {xpType} --\nYou have gained an additional {Math.Abs(verifyXp).ToString("N0")} {xpType}!";

                Session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

                if (verifyXp < 0)
                {
                    // add to character's total XP
                    TotalExperience -= verifyXp;

                    CheckForLevelup();
                }

                RemoveProperty(PropertyInt64.VerifyXp);
            });

            actionChain.EnqueueChain();
        }

        /// <summary>
        /// Returns the total amount of XP required to go from vitae to vitae + 0.01
        /// </summary>
        /// <param name="vitae">The current player life force, ie. 0.95f vitae = 5% penalty</param>
        /// <param name="level">The player DeathLevel, their level on last death</param>
        private double VitaeCPPoolThreshold(float vitae, int level)
        {
            if (level > 275)
                return (long)((Math.Pow(level, 1.0) * 1.0 + 10.0) * Math.Pow(vitae, 2.0) + 0.5) * (double)Level;


            return (long)(Math.Pow(level, 1.0) * 1.0 + 10.0) * Math.Pow(vitae, 2.0) + 0.5;
        }

        /// <summary>
        /// Raise the available XP by a percentage of the current level XP or a maximum
        /// </summary>
        public void GrantLevelProportionalXp(double percent, long min, long max, bool shareable = false)
        {
            var nextLevelXP = GetXPBetweenLevels(Level.Value, Level.Value + 1);

            var scaledXP = (long)Math.Round(nextLevelXP * percent);

            if (max > 0)
                scaledXP = Math.Min(scaledXP, max);

            if (min > 0)
                scaledXP = Math.Max(scaledXP, min);

            var shareType = shareable ? ShareType.All : ShareType.None;


            // apply xp modifiers?
            EarnXP(scaledXP, XpType.Quest, shareType);
        }

        /// <summary>
        /// The player earns XP for items that can be leveled up
        /// by killing creatures and completing quests,
        /// while those items are equipped.
        /// </summary>
        public void GrantItemXP(long amount)
        {
            foreach (var item in EquippedObjects.Values.Where(i => i.HasItemLevel))
                GrantItemXP(item, amount);
        }

        public void GrantItemXP(WorldObject item, long amount)
        {
            var prevItemLevel = item.ItemLevel.Value;
            var addItemXP = item.AddItemXP(amount);

            if (addItemXP > 0)
                Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(item, PropertyInt64.ItemTotalXp, item.ItemTotalXp.Value));

            // handle item leveling up
            var newItemLevel = item.ItemLevel.Value;
            if (newItemLevel > prevItemLevel)
            {
                OnItemLevelUp(item, prevItemLevel);

                var actionChain = new ActionChain();
                actionChain.AddAction(this, () =>
                {
                    var msg = $"Your {item.Name} has increased in power to level {newItemLevel}!";

                    Session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
                    EnqueueBroadcast(new GameMessageScript(Guid, PlayScript.AetheriaLevelUp));

                });
                actionChain.EnqueueChain();
            }

        }

        /// <summary>
        /// Returns the multiplier to XP and Luminance from Trinkets and Augmentations
        /// </summary>
        public float GetXPAndLuminanceModifier(XpType xpType)
        {
            var enchantmentBonus = EnchantmentManager.GetXPBonus();

            var augBonus = 0.0f;
            if (xpType == XpType.Kill && AugmentationBonusXp > 0)
                augBonus = AugmentationBonusXp * 0.05f;

            var modifier = 1.0f + enchantmentBonus + augBonus;
            //Console.WriteLine($"XPAndLuminanceModifier: {modifier}");

            return modifier;
        }
    }
}
