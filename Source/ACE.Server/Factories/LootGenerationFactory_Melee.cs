using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Common;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;
using ACE.Server.Factories.Tables;
using ACE.Server.WorldObjects;
using Google.Protobuf.Collections;

namespace ACE.Server.Factories
{
    public static partial class LootGenerationFactory
    {
        /// <summary>
        /// Creates and optionally mutates a new MeleeWeapon
        /// </summary>
        public static WorldObject CreateMeleeWeapon(TreasureDeath profile, bool isMagical, MeleeWeaponSkill weaponSkill = MeleeWeaponSkill.Undef, bool mutate = true)
        {
            var wcid = 0;
            var weaponType = 0;

            var eleType = ThreadSafeRandom.Next(0, 4);

            if (weaponSkill == MeleeWeaponSkill.Undef)
                weaponSkill = (MeleeWeaponSkill)ThreadSafeRandom.Next(1, 4);

            switch (weaponSkill)                
            {
                case MeleeWeaponSkill.HeavyWeapons:

                    weaponType = ThreadSafeRandom.Next(0, LootTables.HeavyWeaponsMatrix.Length - 1);
                    wcid = LootTables.HeavyWeaponsMatrix[weaponType][eleType];
                    break;

                case MeleeWeaponSkill.LightWeapons:

                    weaponType = ThreadSafeRandom.Next(0, LootTables.LightWeaponsMatrix.Length - 1);
                    wcid = LootTables.LightWeaponsMatrix[weaponType][eleType];
                    break;

                case MeleeWeaponSkill.FinesseWeapons:

                    weaponType = ThreadSafeRandom.Next(0, LootTables.FinesseWeaponsMatrix.Length - 1);
                    wcid = LootTables.FinesseWeaponsMatrix[weaponType][eleType];
                    break;

                case MeleeWeaponSkill.TwoHandedCombat:

                    weaponType = ThreadSafeRandom.Next(0, LootTables.TwoHandedWeaponsMatrix.Length - 1);
                    wcid = LootTables.TwoHandedWeaponsMatrix[weaponType][eleType];
                    break;
            }

            var wo = WorldObjectFactory.CreateNewWorldObject((uint)wcid);

            if (wo != null && mutate)
            {
                if (!MutateMeleeWeapon(wo, profile, isMagical))
                {
                    log.Warn($"[LOOT] {wo.WeenieClassId} - {wo.Name} is not a MeleeWeapon");
                    return null;
                }
            }
            return wo;
        }

        
        private static bool MutateMeleeWeapon(WorldObject wo, TreasureDeath profile, bool isMagical, TreasureRoll roll = null)
        {
            if (!(wo is MeleeWeapon))
                return false;

            if (roll == null)
            {
                // previous method
                var wieldDifficulty = RollWieldDifficulty(profile.Tier, TreasureWeaponType.MeleeWeapon);

                if (!MutateStats_OldMethod(wo, profile, wieldDifficulty))
                    return false;
            }
            else
            {
                // thanks to 4eyebiped for helping with the data analysis of magloot retail logs
                // that went into reversing these mutation scripts

                var weaponSkill = wo.WeaponSkill.ToMeleeWeaponSkill();

                // mutate Damage / WieldDifficulty / Variance
                var scriptName = GetDamageScript(weaponSkill, roll.WeaponType);

                var mutationFilter = MutationCache.GetMutation(scriptName);

                mutationFilter.TryMutate(wo, profile.Tier);

                // mutate WeaponOffense / WeaponDefense
                scriptName = GetOffenseDefenseScript(weaponSkill, roll.WeaponType);

                mutationFilter = MutationCache.GetMutation(scriptName);

                mutationFilter.TryMutate(wo, profile.Tier);
            }

            // weapon speed
            if (wo.WeaponTime != null)
            {
                var weaponSpeedMod = RollWeaponSpeedMod(profile);
                wo.WeaponTime = (int)(wo.WeaponTime * weaponSpeedMod);
            }

            // material type
            var materialType = GetMaterialType(wo, profile.Tier);
            if (materialType > 0)
                wo.MaterialType = materialType;

            // item color
            MutateColor(wo);

            // gem count / gem material
            if (wo.GemCode != null)
                wo.GemCount = GemCountChance.Roll(wo.GemCode.Value, profile.Tier);
            else
                wo.GemCount = ThreadSafeRandom.Next(1, 5);

            wo.GemType = RollGemType(profile.Tier);

            // workmanship
            wo.ItemWorkmanship = WorkmanshipChance.Roll(profile.Tier);

            // burden
            MutateBurden(wo, profile, true);

            // missile / magic defense
            wo.WeaponMissileDefense = MissileMagicDefense.Roll(profile.Tier);
            wo.WeaponMagicDefense = MissileMagicDefense.Roll(profile.Tier);

            // spells
            if (!isMagical)
            {
                // clear base
                wo.ItemManaCost = null;
                wo.ItemMaxMana = null;
                wo.ItemCurMana = null;
                wo.ItemSpellcraft = null;
                wo.ItemDifficulty = null;
            }
            else
                AssignMagic(wo, profile, roll);

            // item value
            //if (wo.HasMutateFilter(MutateFilter.Value))   // fixme: data
            MutateValue(wo, profile.Tier, roll);

            // long description
            wo.LongDesc = GetLongDesc(wo);

            // Empowered melee weapons T9 only.

            var cleavingRoll = ThreadSafeRandom.Next(0.0f, 1.0f);
            var empowered = ThreadSafeRandom.Next(0.0f, 1.0f);
            var cleaving = wo.GetProperty(PropertyInt.Cleaving);
            var attacktype = wo.GetProperty(PropertyInt.AttackType);

            wo.Empowered = false;

            // apply bonuses based on melee weapon type
            if (profile.Tier == 9 && empowered <= 0.5f && isMagical && cleaving > 1 && profile.TreasureType == 3111 && wo.Empowered == false || profile.Tier == 9 && isMagical && cleaving > 1 && profile.TreasureType == 3112 && wo.Empowered == false)               
            {
                TryRollEquipmentSet(wo, profile, roll);
                var maxlevel = 10;
                var basexp = 10000000000;
                var oldname = wo.GetProperty(PropertyString.Name);
                var name = $"Empowered {oldname}";
                var weapondamage = wo.GetProperty(PropertyInt.Damage);
                var damagebonus = 41;
                int newweapondamage = (int)(weapondamage + damagebonus);

                wo.SetProperty(PropertyBool.Empowered, true);
                wo.ItemMaxLevel = maxlevel;
                wo.SetProperty(PropertyInt.ItemXpStyle, 1);
                wo.ItemBaseXp = basexp;
                wo.SetProperty(PropertyInt64.ItemTotalXp, 0);
                wo.SetProperty(PropertyString.Name, name);
                wo.SetProperty(PropertyInt.Damage, newweapondamage);
                wo.SetProperty(PropertyFloat.CriticalFrequency, 0.3f);
                wo.SetProperty(PropertyFloat.CriticalMultiplier, 1.2f);
                if (cleavingRoll <= 0.1f)
                    wo.SetProperty(PropertyInt.Cleaving, 3);
                wo.Empowered = true;
                wo.SetProperty(PropertyInt.WieldDifficulty, 453);
            }
            if (profile.Tier == 9 && empowered <= 0.5f && isMagical && attacktype > 25 && profile.TreasureType == 3111 && wo.Empowered == false || profile.Tier == 9 && isMagical && attacktype > 25 && profile.TreasureType == 3112 && wo.Empowered == false)
            {
                TryRollEquipmentSet(wo, profile, roll);
                var maxlevel = 10;
                var basexp = 10000000000;
                var oldname = wo.GetProperty(PropertyString.Name);
                var name = $"Empowered {oldname}";
                var weapondamage = wo.GetProperty(PropertyInt.Damage);
                var damagebonus = 41;
                int newweapondamage = (int)(weapondamage + damagebonus);

                wo.SetProperty(PropertyBool.Empowered, true);
                wo.ItemMaxLevel = maxlevel;
                wo.SetProperty(PropertyInt.ItemXpStyle, 1);
                wo.ItemBaseXp = basexp;
                wo.SetProperty(PropertyInt64.ItemTotalXp, 0);
                wo.SetProperty(PropertyString.Name, name);
                wo.SetProperty(PropertyInt.Damage, newweapondamage);
                wo.SetProperty(PropertyFloat.CriticalFrequency, 0.3f);
                wo.SetProperty(PropertyFloat.CriticalMultiplier, 1.2f);
                wo.Empowered = true;
                wo.SetProperty(PropertyInt.WieldDifficulty, 453);
            }
            if (profile.Tier == 9 && empowered <= 0.5f && isMagical && profile.TreasureType == 3111 && wo.Empowered == false || profile.Tier == 9 && isMagical && profile.TreasureType == 3112 && wo.Empowered == false)
            {
                TryRollEquipmentSet(wo, profile, roll);
                var maxlevel = 10;
                var basexp = 10000000000;
                var oldname = wo.GetProperty(PropertyString.Name);
                var name = $"Empowered {oldname}";
                var weapondamage = wo.GetProperty(PropertyInt.Damage);
                var damagebonus = 41;
                int newweapondamage = (int)(weapondamage + damagebonus);

                wo.SetProperty(PropertyBool.Empowered, true);
                wo.ItemMaxLevel = maxlevel;
                wo.SetProperty(PropertyInt.ItemXpStyle, 1);
                wo.ItemBaseXp = basexp;
                wo.SetProperty(PropertyInt64.ItemTotalXp, 0);
                wo.SetProperty(PropertyString.Name, name);
                wo.SetProperty(PropertyInt.Damage, newweapondamage);                
                wo.SetProperty(PropertyFloat.CriticalFrequency, 0.3f);
                wo.SetProperty(PropertyFloat.CriticalMultiplier, 1.2f);
                wo.Empowered = true;
                wo.SetProperty(PropertyInt.WieldDifficulty, 453);
            }
            if (profile.Tier == 9 && empowered <= 0.5f && isMagical && profile.TreasureType == 3111 && wo.Empowered == false && wo.WieldSkillType == 41 && !wo.IsCleaving || profile.Tier == 9 && isMagical && profile.TreasureType == 3112 && wo.Empowered == false && wo.WieldSkillType == 41 && !wo.IsCleaving)
            {
                TryRollEquipmentSet(wo, profile, roll);
                var maxlevel = 10;
                var basexp = 10000000000;
                var oldname = wo.GetProperty(PropertyString.Name);
                var name = $"Empowered {oldname}";
                var weapondamage = wo.GetProperty(PropertyInt.Damage);
                var damagebonus = 41;
                int newweapondamage = (int)(weapondamage + damagebonus);

                wo.SetProperty(PropertyBool.Empowered, true);
                wo.ItemMaxLevel = maxlevel;
                wo.SetProperty(PropertyInt.ItemXpStyle, 1);
                wo.ItemBaseXp = basexp;
                wo.SetProperty(PropertyInt64.ItemTotalXp, 0);
                wo.SetProperty(PropertyString.Name, name);
                wo.SetProperty(PropertyInt.Damage, newweapondamage);
                wo.SetProperty(PropertyFloat.CriticalFrequency, 0.3f);
                wo.SetProperty(PropertyFloat.CriticalMultiplier, 1.2f);
                wo.Empowered = true;
                wo.SetProperty(PropertyInt.WieldDifficulty, 453);
            }
            // Proto Weapons
            if (profile.Tier == 9 && isMagical && cleaving > 1 && profile.TreasureType == 4111)
            {
                TryRollEquipmentSet(wo, profile, roll);
                var maxlevel = 20;
                var basexp = 10000000000;
                var oldname = wo.GetProperty(PropertyString.Name);
                var name = $"Proto {oldname}";
                var weapondamage = wo.GetProperty(PropertyInt.Damage);
                var damagebonus = 61;
                int newweapondamage = (int)(weapondamage + damagebonus);

                wo.SetProperty(PropertyBool.Proto, true);
                wo.ItemMaxLevel = maxlevel;
                wo.SetProperty(PropertyInt.ItemXpStyle, 1);
                wo.ItemBaseXp = basexp;
                wo.SetProperty(PropertyInt64.ItemTotalXp, 0);
                wo.SetProperty(PropertyString.Name, name);
                wo.SetProperty(PropertyInt.Damage, newweapondamage);
                wo.SetProperty(PropertyFloat.CriticalFrequency, 0.35f);
                wo.SetProperty(PropertyFloat.CriticalMultiplier, 1.25f);
                wo.SetProperty(PropertyInt.Cleaving, 3);
                wo.SetProperty(PropertyInt.WieldDifficulty, 651);

                wo.EquipmentSetId = (EquipmentSet)ThreadSafeRandom.Next((int)EquipmentSet.Soldiers, (int)EquipmentSet.Lightningproof);

                if (wo.EquipmentSetId != null)
                {
                    var equipSetId = wo.EquipmentSetId;

                    var equipSetName = equipSetId.ToString();

                    if (equipSetId >= EquipmentSet.Soldiers && equipSetId <= EquipmentSet.Crafters)
                        equipSetName = equipSetName.TrimEnd('s') + "'s";

                    wo.Name = $"{equipSetName} {wo.Name}";
                }
            }
            if (profile.Tier == 9 && isMagical && attacktype > 25 && profile.TreasureType == 4111)
            {
                TryRollEquipmentSet(wo, profile, roll);
                var maxlevel = 20;
                var basexp = 10000000000;
                var oldname = wo.GetProperty(PropertyString.Name);
                var name = $"Proto {oldname}";
                var weapondamage = wo.GetProperty(PropertyInt.Damage);
                var damagebonus = 61;
                int newweapondamage = (int)(weapondamage + damagebonus);

                wo.SetProperty(PropertyBool.Proto, true);
                wo.ItemMaxLevel = maxlevel;
                wo.SetProperty(PropertyInt.ItemXpStyle, 1);
                wo.ItemBaseXp = basexp;
                wo.SetProperty(PropertyInt64.ItemTotalXp, 0);
                wo.SetProperty(PropertyString.Name, name);
                wo.SetProperty(PropertyInt.Damage, newweapondamage);
                wo.SetProperty(PropertyFloat.CriticalFrequency, 0.35f);
                wo.SetProperty(PropertyFloat.CriticalMultiplier, 1.25f);
                wo.SetProperty(PropertyInt.WieldDifficulty, 651);

                wo.EquipmentSetId = (EquipmentSet)ThreadSafeRandom.Next((int)EquipmentSet.Soldiers, (int)EquipmentSet.Lightningproof);

                if (wo.EquipmentSetId != null)
                {
                    var equipSetId = wo.EquipmentSetId;

                    var equipSetName = equipSetId.ToString();

                    if (equipSetId >= EquipmentSet.Soldiers && equipSetId <= EquipmentSet.Crafters)
                        equipSetName = equipSetName.TrimEnd('s') + "'s";

                    wo.Name = $"{equipSetName} {wo.Name}";
                }
            }
            if (profile.Tier == 9 && isMagical && profile.TreasureType == 4111)
            {
                if (wo.Proto == true)
                {
                    return true;
                }
                TryRollEquipmentSet(wo, profile, roll);
                var maxlevel = 20;
                var basexp = 10000000000;
                var oldname = wo.GetProperty(PropertyString.Name);
                var name = $"Proto {oldname}";
                var weapondamage = wo.GetProperty(PropertyInt.Damage);
                var damagebonus = 61;
                int newweapondamage = (int)(weapondamage + damagebonus);

                wo.SetProperty(PropertyBool.Proto, true);
                wo.ItemMaxLevel = maxlevel;
                wo.SetProperty(PropertyInt.ItemXpStyle, 1);
                wo.ItemBaseXp = basexp;
                wo.SetProperty(PropertyInt64.ItemTotalXp, 0);
                wo.SetProperty(PropertyString.Name, name);
                wo.SetProperty(PropertyInt.Damage, newweapondamage);
                wo.SetProperty(PropertyFloat.CriticalFrequency, 0.35f);
                wo.SetProperty(PropertyFloat.CriticalMultiplier, 1.25f);
                wo.SetProperty(PropertyInt.WieldDifficulty, 651);

                wo.EquipmentSetId = (EquipmentSet)ThreadSafeRandom.Next((int)EquipmentSet.Soldiers, (int)EquipmentSet.Lightningproof);

                if (wo.EquipmentSetId != null)
                {
                    var equipSetId = wo.EquipmentSetId;

                    var equipSetName = equipSetId.ToString();

                    if (equipSetId >= EquipmentSet.Soldiers && equipSetId <= EquipmentSet.Crafters)
                        equipSetName = equipSetName.TrimEnd('s') + "'s";

                    wo.Name = $"{equipSetName} {wo.Name}";
                }
            }
            if (profile.Tier == 9 && isMagical && profile.TreasureType == 4111 && wo.WieldSkillType == 41 && !wo.IsCleaving)
            {
                if (wo.Proto == true)
                {
                    return true;
                }
                TryRollEquipmentSet(wo, profile, roll);
                var maxlevel = 20;
                var basexp = 10000000000;
                var oldname = wo.GetProperty(PropertyString.Name);
                var name = $"Proto {oldname}";
                var weapondamage = wo.GetProperty(PropertyInt.Damage);
                var damagebonus = 61;
                int newweapondamage = (int)(weapondamage + damagebonus);

                wo.SetProperty(PropertyBool.Proto, true);
                wo.ItemMaxLevel = maxlevel;
                wo.SetProperty(PropertyInt.ItemXpStyle, 1);
                wo.ItemBaseXp = basexp;
                wo.SetProperty(PropertyInt64.ItemTotalXp, 0);
                wo.SetProperty(PropertyString.Name, name);
                wo.SetProperty(PropertyInt.Damage, newweapondamage);
                wo.SetProperty(PropertyFloat.CriticalFrequency, 0.35f);
                wo.SetProperty(PropertyFloat.CriticalMultiplier, 1.25f);
                wo.SetProperty(PropertyInt.WieldDifficulty, 651);

                wo.EquipmentSetId = (EquipmentSet)ThreadSafeRandom.Next((int)EquipmentSet.Soldiers, (int)EquipmentSet.Lightningproof);

                if (wo.EquipmentSetId != null)
                {
                    var equipSetId = wo.EquipmentSetId;

                    var equipSetName = equipSetId.ToString();

                    if (equipSetId >= EquipmentSet.Soldiers && equipSetId <= EquipmentSet.Crafters)
                        equipSetName = equipSetName.TrimEnd('s') + "'s";

                    wo.Name = $"{equipSetName} {wo.Name}";
                }
            }

            if (profile.Tier == 10 && isMagical && wo.IsCleaving)
            {               
                TryRollEquipmentSet(wo, profile, roll);
                var maxlevel = 50;
                var basexp = 10000000000;
                var oldname = wo.GetProperty(PropertyString.Name);
                var name = $"Arramoran {oldname}";
                var weapondamage = wo.GetProperty(PropertyInt.Damage);
                var damagebonus = 81;
                int newweapondamage = (int)(weapondamage + damagebonus);

                wo.Arramoran = true;
                wo.ItemMaxLevel = maxlevel;
                wo.SetProperty(PropertyInt.ItemXpStyle, 1);
                wo.ItemBaseXp = basexp;
                wo.SetProperty(PropertyInt64.ItemTotalXp, 0);
                wo.Sockets = 1;
                wo.SetProperty(PropertyString.Name, name);
                wo.SetProperty(PropertyInt.Damage, newweapondamage);
                wo.SetProperty(PropertyFloat.CriticalFrequency, 0.4f);
                wo.SetProperty(PropertyFloat.CriticalMultiplier, 1.3f);
                wo.SetProperty(PropertyInt.Cleaving, 3);
                wo.SetProperty(PropertyInt.WieldDifficulty, 1046);

                wo.EquipmentSetId = (EquipmentSet)ThreadSafeRandom.Next((int)EquipmentSet.Soldiers, (int)EquipmentSet.Lightningproof);

                if (wo.EquipmentSetId != null)
                {
                    var equipSetId = wo.EquipmentSetId;

                    var equipSetName = equipSetId.ToString();

                    if (equipSetId >= EquipmentSet.Soldiers && equipSetId <= EquipmentSet.Crafters)
                        equipSetName = equipSetName.TrimEnd('s') + "'s";

                    wo.Name = $"{equipSetName} {wo.Name}";
                }
                
            }
            /*if (profile.Tier == 10 && isMagical && attacktype > 25)
            {
                TryRollEquipmentSet(wo, profile, roll);
                var maxlevel = 500;
                var basexp = 50000000000;
                var oldname = wo.GetProperty(PropertyString.Name);
                var name = $"Arramoran {oldname}";
                var weapondamage = wo.GetProperty(PropertyInt.Damage);
                var damagebonus = 2000;
                int newweapondamage = (int)(weapondamage + damagebonus);

                wo.Arramoran = true;
                wo.ItemMaxLevel = maxlevel;
                wo.SetProperty(PropertyInt.ItemXpStyle, 1);
                wo.ItemBaseXp = basexp;
                wo.SetProperty(PropertyInt64.ItemTotalXp, 0);
                wo.Sockets = 1;
                wo.SetProperty(PropertyString.Name, name);
                wo.SetProperty(PropertyInt.Damage, newweapondamage);
                wo.SetProperty(PropertyFloat.CriticalFrequency, 1f);
                wo.SetProperty(PropertyFloat.CriticalMultiplier, 60f);
                wo.SetProperty(PropertyInt.WieldDifficulty, 700);

                wo.EquipmentSetId = (EquipmentSet)ThreadSafeRandom.Next((int)EquipmentSet.Soldiers, (int)EquipmentSet.Lightningproof);

                if (wo.EquipmentSetId != null)
                {
                    var equipSetId = wo.EquipmentSetId;

                    var equipSetName = equipSetId.ToString();

                    if (equipSetId >= EquipmentSet.Soldiers && equipSetId <= EquipmentSet.Crafters)
                        equipSetName = equipSetName.TrimEnd('s') + "'s";

                    wo.Name = $"{equipSetName} {wo.Name}";
                }
                
            }*/
            if (profile.Tier == 10 && isMagical)
            {
                if (wo.Arramoran == true)
                    return true;

                TryRollEquipmentSet(wo, profile, roll);
                var maxlevel = 50;
                var basexp = 10000000000;
                var oldname = wo.GetProperty(PropertyString.Name);
                var name = $"Arramoran {oldname}";
                var weapondamage = wo.GetProperty(PropertyInt.Damage);
                if (wo.WieldSkillType == 41 && !wo.IsCleaving)
                {
                    var damagebonus = 140;
                    int newweapondamage = (int)(weapondamage + damagebonus);
                    wo.SetProperty(PropertyInt.Damage, newweapondamage);
                    wo.SetProperty(PropertyFloat.CriticalMultiplier, 1.3f);
                }
                if (wo.WieldSkillType == 44)
                {
                    var damagebonus = 140;
                    int newweapondamage = (int)(weapondamage + damagebonus);
                    wo.SetProperty(PropertyInt.Damage, newweapondamage);
                    wo.SetProperty(PropertyFloat.CriticalMultiplier, 1.3f);
                }
                if (wo.WieldSkillType == 45)
                {
                    var damagebonus = 140;
                    int newweapondamage = (int)(weapondamage + damagebonus);
                    wo.SetProperty(PropertyInt.Damage, newweapondamage);
                    wo.SetProperty(PropertyFloat.CriticalMultiplier, 1.3f);
                }
                if (wo.WieldSkillType == 46)
                {
                    var damagebonus = 140;
                    int newweapondamage = (int)(weapondamage + damagebonus);
                    wo.SetProperty(PropertyInt.Damage, newweapondamage);
                    wo.SetProperty(PropertyFloat.CriticalMultiplier, 1.3f);
                }


                wo.Arramoran = true;
                wo.ItemMaxLevel = maxlevel;
                wo.SetProperty(PropertyInt.ItemXpStyle, 1);
                wo.ItemBaseXp = basexp;
                wo.SetProperty(PropertyInt64.ItemTotalXp, 0);
                wo.Sockets = 1;
                wo.SetProperty(PropertyString.Name, name);                
                wo.SetProperty(PropertyFloat.CriticalFrequency, 0.4f);              
                wo.SetProperty(PropertyInt.WieldDifficulty, 1046);

                wo.EquipmentSetId = (EquipmentSet)ThreadSafeRandom.Next((int)EquipmentSet.Soldiers, (int)EquipmentSet.Lightningproof);

                if (wo.EquipmentSetId != null)
                {
                    var equipSetId = wo.EquipmentSetId;

                    var equipSetName = equipSetId.ToString();

                    if (equipSetId >= EquipmentSet.Soldiers && equipSetId <= EquipmentSet.Crafters)
                        equipSetName = equipSetName.TrimEnd('s') + "'s";

                    wo.Name = $"{equipSetName} {wo.Name}";
                }
                
            }

            return true;
        }

        private static bool MutateStats_OldMethod(WorldObject wo, TreasureDeath profile, int wieldDifficulty)
        {
            var success = false;

            switch (wo.WeaponSkill)
            {
                case Skill.HeavyWeapons:

                    success = MutateHeavyWeapon(wo, profile, wieldDifficulty);
                    break;

                case Skill.LightWeapons:

                    success = MutateLightWeapon(wo, profile, wieldDifficulty);
                    break;

                case Skill.FinesseWeapons:

                    success = MutateFinesseWeapon(wo, profile, wieldDifficulty);
                    break;

                case Skill.TwoHandedCombat:

                    success = MutateTwoHandedWeapon(wo, profile, wieldDifficulty);
                    break;
            }

            if (!success)
                return false;

            // wield requirements
            if (wieldDifficulty > 0)
            {
                wo.WieldDifficulty = wieldDifficulty;
                wo.WieldRequirements = WieldRequirement.RawSkill;
                wo.WieldSkillType = (int)wo.WeaponSkill;

            }
            else
            {
                // if no wield requirements, clear base
                wo.WieldDifficulty = null;
                wo.WieldRequirements = WieldRequirement.Invalid;
                wo.WieldSkillType = null;
            }
            return true;
        }

        private static string GetDamageScript(MeleeWeaponSkill weaponSkill, TreasureWeaponType weaponType)
        {
            return "MeleeWeapons.Damage_WieldDifficulty_DamageVariance." + weaponSkill.GetScriptName_Combined() + "_" + weaponType.GetScriptName() + ".txt";
        }

        private static string GetOffenseDefenseScript(MeleeWeaponSkill weaponSkill, TreasureWeaponType weaponType)
        {
            return "MeleeWeapons.WeaponOffense_WeaponDefense." + weaponType.GetScriptShortName() + "_offense_defense.txt";
        }

        private enum LootWeaponType
        {
            Axe         = 0,
            Dagger      = 1,
            DaggerMulti = 2,
            Mace        = 3,
            Spear       = 4,
            Sword       = 5,
            SwordMulti  = 6,
            Staff       = 7,
            Unarmed     = 8,
            Jitte       = 9,
            TwoHanded   = 0,
            Cleaving    = 0,
            Spears      = 1,
        }

        private static bool MutateHeavyWeapon(WorldObject wo, TreasureDeath profile, int wieldDifficulty)
        {
            switch (wo.W_WeaponType)
            {
                case WeaponType.Axe:

                    wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.Axe);
                    wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.Axe);

                    wo.WeaponDefense = GetMaxDamageMod(profile.Tier, 18);
                    wo.WeaponOffense = GetMaxDamageMod(profile.Tier, 22);

                    break;

                case WeaponType.Dagger:

                    if (!wo.W_AttackType.IsMultiStrike())
                    {
                        wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.Dagger);
                        wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.Dagger);
                    }
                    else
                    {
                        wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.DaggerMulti);
                        wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.DaggerMulti);
                    }

                    wo.WeaponDefense = GetMaxDamageMod(profile.Tier, 20);
                    wo.WeaponOffense = GetMaxDamageMod(profile.Tier, 20);

                    break;

                case WeaponType.Mace:

                    wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.Mace);
                    wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.Mace);

                    wo.WeaponDefense = GetMaxDamageMod(profile.Tier, 22);
                    wo.WeaponOffense = GetMaxDamageMod(profile.Tier, 18);

                    break;

                case WeaponType.Spear:

                    wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.Spear);
                    wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.Spear);

                    wo.WeaponDefense = GetMaxDamageMod(profile.Tier, 15);
                    wo.WeaponOffense = GetMaxDamageMod(profile.Tier, 25);

                    break;

                case WeaponType.Staff:

                    wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.Staff);
                    wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.Staff);

                    wo.WeaponDefense = GetMaxDamageMod(profile.Tier, 25);
                    wo.WeaponOffense = GetMaxDamageMod(profile.Tier, 15);

                    break;

                case WeaponType.Sword:

                    if (!wo.W_AttackType.IsMultiStrike())
                    {
                        wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.Sword);
                        wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.Sword);
                    }
                    else
                    {
                        wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.SwordMulti);
                        wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.SwordMulti);
                    }

                    wo.WeaponDefense = GetMaxDamageMod(profile.Tier, 20);
                    wo.WeaponOffense = GetMaxDamageMod(profile.Tier, 20);

                    break;

                case WeaponType.Unarmed:

                    wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.Unarmed);
                    wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.Unarmed);

                    wo.WeaponDefense = GetMaxDamageMod(profile.Tier, 20);
                    wo.WeaponOffense = GetMaxDamageMod(profile.Tier, 20);

                    break;

                default:
                    return false;
            }

            return true;
        }

        private static bool MutateLightWeapon(WorldObject wo, TreasureDeath profile, int wieldDifficulty)
        {
            switch (wo.W_WeaponType)
            {
                case WeaponType.Axe:

                    wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.Axe);
                    wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.Axe);

                    wo.WeaponDefense = GetMaxDamageMod(profile.Tier, 18);
                    wo.WeaponOffense = GetMaxDamageMod(profile.Tier, 22);

                    break;

                case WeaponType.Dagger:

                    if (!wo.W_AttackType.IsMultiStrike())
                    {
                        wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.Dagger);
                        wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.Dagger);
                    }
                    else
                    {
                        wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.DaggerMulti);
                        wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.DaggerMulti);
                    }

                    wo.WeaponDefense = GetMaxDamageMod(profile.Tier, 20);
                    wo.WeaponOffense = GetMaxDamageMod(profile.Tier, 20);

                    break;

                case WeaponType.Mace:

                    wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.Mace);
                    wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.Mace);

                    wo.WeaponDefense = GetMaxDamageMod(profile.Tier, 22);
                    wo.WeaponOffense = GetMaxDamageMod(profile.Tier, 18);

                    break;

                case WeaponType.Spear:

                    wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.Spear);
                    wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.Spear);

                    wo.WeaponDefense = GetMaxDamageMod(profile.Tier, 15);
                    wo.WeaponOffense = GetMaxDamageMod(profile.Tier, 25);

                    break;

                case WeaponType.Staff:

                    wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.Staff);
                    wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.Staff);

                    wo.WeaponDefense = GetMaxDamageMod(profile.Tier, 25);
                    wo.WeaponOffense = GetMaxDamageMod(profile.Tier, 15);

                    break;

                case WeaponType.Sword:

                    if (!wo.W_AttackType.IsMultiStrike())
                    {
                        wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.Sword);
                        wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.Sword);
                    }
                    else
                    {
                        wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.SwordMulti);
                        wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.SwordMulti);

                    }

                    wo.WeaponDefense = GetMaxDamageMod(profile.Tier, 20);
                    wo.WeaponOffense = GetMaxDamageMod(profile.Tier, 20);

                    break;

                case WeaponType.Unarmed:

                    wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.Unarmed);
                    wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.Unarmed);

                    wo.WeaponDefense = GetMaxDamageMod(profile.Tier, 20);
                    wo.WeaponOffense = GetMaxDamageMod(profile.Tier, 20);

                    break;

                default:
                    return false;
            }

            return true;
        }

        private static bool MutateFinesseWeapon(WorldObject wo, TreasureDeath profile, int wieldDifficulty)
        {
            switch (wo.W_WeaponType)
            {
                case WeaponType.Axe:

                    wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.Axe);
                    wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.Axe);

                    wo.WeaponDefense = GetMaxDamageMod(profile.Tier, 18);
                    wo.WeaponOffense = GetMaxDamageMod(profile.Tier, 22);

                    break;

                case WeaponType.Dagger:

                    if (!wo.W_AttackType.IsMultiStrike())
                    {
                        wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.Dagger);
                        wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.Dagger);
                    }
                    else
                    {
                        wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.DaggerMulti);
                        wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.DaggerMulti);

                    }

                    wo.WeaponDefense = GetMaxDamageMod(profile.Tier, 20);
                    wo.WeaponOffense = GetMaxDamageMod(profile.Tier, 20);

                    break;

                case WeaponType.Mace:

                    wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.Mace);

                    if (wo.TsysMutationData != 0x06080402)
                    {
                        wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.Mace);

                        wo.WeaponDefense = GetMaxDamageMod(profile.Tier, 22);
                        wo.WeaponOffense = GetMaxDamageMod(profile.Tier, 18);
                    }
                    else  // handle jittes
                    {
                        wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.Jitte);

                        wo.WeaponDefense = GetMaxDamageMod(profile.Tier, 25);
                        wo.WeaponOffense = GetMaxDamageMod(profile.Tier, 15);
                    }
                    break;

                case WeaponType.Spear:

                    wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.Spear);
                    wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.Spear);

                    wo.WeaponDefense = GetMaxDamageMod(profile.Tier, 15);
                    wo.WeaponOffense = GetMaxDamageMod(profile.Tier, 25);

                    break;

                case WeaponType.Staff:

                    wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.Staff);
                    wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.Staff);

                    wo.WeaponDefense = GetMaxDamageMod(profile.Tier, 25);
                    wo.WeaponOffense = GetMaxDamageMod(profile.Tier, 15);

                    break;

                case WeaponType.Sword:

                    if (!wo.W_AttackType.IsMultiStrike())
                    {
                        wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.Sword);
                        wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.Sword);
                    }
                    else
                    {
                        wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.SwordMulti);
                        wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.SwordMulti);
                    }

                    wo.WeaponDefense = GetMaxDamageMod(profile.Tier, 20);
                    wo.WeaponOffense = GetMaxDamageMod(profile.Tier, 20);

                    break;

                case WeaponType.Unarmed:

                    wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.Unarmed);
                    wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.Unarmed);

                    wo.WeaponDefense = GetMaxDamageMod(profile.Tier, 20);
                    wo.WeaponOffense = GetMaxDamageMod(profile.Tier, 20);

                    break;

                default:
                    return false;
            }

            return true;
        }

        private static bool MutateTwoHandedWeapon(WorldObject wo, TreasureDeath profile, int wieldDifficulty)
        {
            if (wo.IsCleaving)
            {
                wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.Cleaving);
                wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.TwoHanded);

                wo.WeaponDefense = GetMaxDamageMod(profile.Tier, 18);
                wo.WeaponOffense = GetMaxDamageMod(profile.Tier, 22);
            }
            else
            {
                wo.Damage = GetMeleeMaxDamage(wo.WeaponSkill, wieldDifficulty, LootWeaponType.Spears);
                wo.DamageVariance = GetVariance(wo.WeaponSkill, LootWeaponType.TwoHanded);

                wo.WeaponDefense = GetMaxDamageMod(profile.Tier, 20);
                wo.WeaponOffense = GetMaxDamageMod(profile.Tier, 20);
            }
            return true;
        }

        // The percentages for variances need to be fixed
        /// <summary>
        /// Gets Melee Weapon Variance
        /// </summary>
        /// <param name="category"></param><param name="type"></param>
        /// <returns>Returns Melee Weapon Variance</returns>
        private static double GetVariance(Skill category, LootWeaponType type)
        {
            double variance = 0;
            int chance = ThreadSafeRandom.Next(0, 99);

            switch (category)
            {
                case Skill.HeavyWeapons:
                    switch (type)
                    {
                        case LootWeaponType.Axe:
                            if (chance < 10)
                                variance = .90;
                            else if (chance < 30)
                                variance = .93;
                            else if (chance < 70)
                                variance = .95;
                            else if (chance < 90)
                                variance = .97;
                            else
                                variance = .99;
                            break;
                        case LootWeaponType.Dagger:
                            if (chance < 10)
                                variance = .47;
                            else if (chance < 30)
                                variance = .50;
                            else if (chance < 70)
                                variance = .53;
                            else if (chance < 90)
                                variance = .57;
                            else
                                variance = .62;
                            break;
                        case LootWeaponType.DaggerMulti:
                            if (chance < 10)
                                variance = .40;
                            else if (chance < 30)
                                variance = .43;
                            else if (chance < 70)
                                variance = .48;
                            else if (chance < 90)
                                variance = .53;
                            else
                                variance = .58;
                            break;
                        case LootWeaponType.Mace:
                            if (chance < 10)
                                variance = .30;
                            else if (chance < 30)
                                variance = .33;
                            else if (chance < 70)
                                variance = .37;
                            else if (chance < 90)
                                variance = .42;
                            else
                                variance = .46;
                            break;
                        case LootWeaponType.Spear:
                            if (chance < 10)
                                variance = .59;
                            else if (chance < 30)
                                variance = .63;
                            else if (chance < 70)
                                variance = .68;
                            else if (chance < 90)
                                variance = .72;
                            else
                                variance = .75;
                            break;
                        case LootWeaponType.Staff:
                            if (chance < 10)
                                variance = .38;
                            else if (chance < 30)
                                variance = .42;
                            else if (chance < 70)
                                variance = .45;
                            else if (chance < 90)
                                variance = .50;
                            else
                                variance = .52;
                            break;
                        case LootWeaponType.Sword:
                            if (chance < 10)
                                variance = .47;
                            else if (chance < 30)
                                variance = .50;
                            else if (chance < 70)
                                variance = .53;
                            else if (chance < 90)
                                variance = .57;
                            else
                                variance = .62;
                            break;
                        case LootWeaponType.SwordMulti:
                            if (chance < 10)
                                variance = .40;
                            else if (chance < 30)
                                variance = .43;
                            else if (chance < 70)
                                variance = .48;
                            else if (chance < 90)
                                variance = .53;
                            else
                                variance = .60;
                            break;
                        case LootWeaponType.Unarmed:
                            if (chance < 10)
                                variance = .44;
                            else if (chance < 30)
                                variance = .48;
                            else if (chance < 70)
                                variance = .53;
                            else if (chance < 90)
                                variance = .58;
                            else
                                variance = .60;
                            break;
                    }
                    break;
                case Skill.LightWeapons:
                case Skill.FinesseWeapons:
                    switch (type)
                    {
                        case LootWeaponType.Axe:
                            // Axe
                            if (chance < 10)
                                variance = .80;
                            else if (chance < 30)
                                variance = .83;
                            else if (chance < 70)
                                variance = .85;
                            else if (chance < 90)
                                variance = .90;
                            else
                                variance = .95;
                            break;
                        case LootWeaponType.Dagger:
                            // Dagger
                            if (chance < 10)
                                variance = .42;
                            else if (chance < 30)
                                variance = .47;
                            else if (chance < 70)
                                variance = .52;
                            else if (chance < 90)
                                variance = .56;
                            else
                                variance = .60;
                            break;
                        case LootWeaponType.DaggerMulti:
                            // Dagger MultiStrike
                            if (chance < 10)
                                variance = .24;
                            else if (chance < 30)
                                variance = .28;
                            else if (chance < 70)
                                variance = .35;
                            else if (chance < 90)
                                variance = .40;
                            else
                                variance = .45;
                            break;
                        case LootWeaponType.Mace:
                            // Mace
                            if (chance < 10)
                                variance = .23;
                            else if (chance < 30)
                                variance = .28;
                            else if (chance < 70)
                                variance = .32;
                            else if (chance < 90)
                                variance = .37;
                            else
                                variance = .43;
                            break;
                        case LootWeaponType.Jitte:
                            // Jitte
                            if (chance < 10)
                                variance = .325;
                            else if (chance < 30)
                                variance = .35;
                            else if (chance < 70)
                                variance = .40;
                            else if (chance < 90)
                                variance = .45;
                            else
                                variance = .50;
                            break;
                        case LootWeaponType.Spear:
                            // Spear
                            if (chance < 10)
                                variance = .65;
                            else if (chance < 30)
                                variance = .68;
                            else if (chance < 70)
                                variance = .71;
                            else if (chance < 90)
                                variance = .75;
                            else
                                variance = .80;
                            break;
                        case LootWeaponType.Staff:
                            // Staff
                            if (chance < 10)
                                variance = .325;
                            else if (chance < 30)
                                variance = .35;
                            else if (chance < 70)
                                variance = .40;
                            else if (chance < 90)
                                variance = .45;
                            else
                                variance = .50;
                            break;
                        case LootWeaponType.Sword:
                            // Sword
                            if (chance < 10)
                                variance = .42;
                            else if (chance < 30)
                                variance = .47;
                            else if (chance < 70)
                                variance = .52;
                            else if (chance < 90)
                                variance = .56;
                            else
                                variance = .60;
                            break;
                        case LootWeaponType.SwordMulti:
                            // Sword Multistrike
                            if (chance < 10)
                                variance = .24;
                            else if (chance < 30)
                                variance = .28;
                            else if (chance < 70)
                                variance = .35;
                            else if (chance < 90)
                                variance = .40;
                            else
                                variance = .45;
                            break;
                        case LootWeaponType.Unarmed:
                            // UA
                            if (chance < 10)
                                variance = .44;
                            else if (chance < 30)
                                variance = .48;
                            else if (chance < 70)
                                variance = .53;
                            else if (chance < 90)
                                variance = .58;
                            else
                                variance = .60;
                            break;
                    }
                    break;
                case Skill.TwoHandedCombat:
                    // Two Handed only have one set of variances
                    if (chance < 5)
                        variance = .30;
                    else if (chance < 20)
                        variance = .35;
                    else if (chance < 50)
                        variance = .40;
                    else if (chance < 80)
                        variance = .45;
                    else if (chance < 95)
                        variance = .50;
                    else
                        variance = .55;
                    break;
                default:
                    return 0;
            }

            return variance;
        }

        /// <summary>
        /// Gets Melee Weapon Index
        /// </summary>
        private static int GetMeleeWieldToIndex(int wieldDiff)
        {
            int index = 0;

            switch (wieldDiff)
            {
                case 250:
                    index = 1;
                    break;
                case 300:
                    index = 2;
                    break;
                case 325:
                    index = 3;
                    break;
                case 350:
                    index = 4;
                    break;
                case 370:
                    index = 5;
                    break;
                case 400:
                    index = 6;
                    break;
                case 420:
                    index = 7;
                    break;
                case 430:
                    index = 8;
                    break;
                case 530:
                    index = 9;
                    break;
                default:
                    index = 0;
                    break;
            }

            return index;
        }

        /// <summary>
        /// Gets Melee Weapon Max Damage
        /// </summary>
        /// <param name="weaponType"></param><param name="wieldDiff"></param><param name="baseWeapon"></param>
        /// <returns>Melee Weapon Max Damage</returns>
        private static int GetMeleeMaxDamage(Skill weaponType, int wieldDiff, LootWeaponType baseWeapon)
        {
            int damageTable = 0;

            switch (weaponType)
            {
                case Skill.HeavyWeapons:
                    damageTable = LootTables.HeavyWeaponDamageTable[(int)baseWeapon, GetMeleeWieldToIndex(wieldDiff)];
                    break;
                case Skill.FinesseWeapons:
                case Skill.LightWeapons:
                    damageTable = LootTables.LightWeaponDamageTable[(int)baseWeapon, GetMeleeWieldToIndex(wieldDiff)];
                    break;
                case Skill.TwoHandedCombat:
                    damageTable = LootTables.TwoHandedWeaponDamageTable[(int)baseWeapon, GetMeleeWieldToIndex(wieldDiff)];
                    break;
                default:
                    return 0;
            }

            // To add a little bit of randomness to Max weapon damage
            int maxDamageVariance = ThreadSafeRandom.Next(-4, 0);

            return damageTable + maxDamageVariance;
        }

        private static bool GetMutateMeleeWeaponData(uint wcid)
        {
            // linear search = slow... but this is only called for /lootgen
            // if this ever needs to be fast, create a lookup table

            for (int weaponType = 0; weaponType < LootTables.MeleeWeaponsMatrices.Count; weaponType++)
            {
                var lootTable = LootTables.MeleeWeaponsMatrices[weaponType];
                for (int subtype = 0; subtype < lootTable.Length; subtype++)
                {
                    if (lootTable[subtype].Contains((int)wcid))
                        return true;
                }
            }
            return false;
        }
    }
}
