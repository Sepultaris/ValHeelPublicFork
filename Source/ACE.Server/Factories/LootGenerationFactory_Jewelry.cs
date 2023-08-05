using System.Collections.Generic;
using System.Linq;

using ACE.Common;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Tables;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;

namespace ACE.Server.Factories
{
    public static partial class LootGenerationFactory
    {
        private static WorldObject CreateJewelry(TreasureDeath profile, bool isMagical, bool mutate = true)
        {
            // 31% chance ring, 31% chance bracelet, 30% chance necklace 8% chance Trinket

            int jewelrySlot = ThreadSafeRandom.Next(1, 100);
            int jewelType;

            // Made this easier to read (switch -> if statement)
            if (jewelrySlot <= 31)
                jewelType = LootTables.ringItems[ThreadSafeRandom.Next(0, LootTables.ringItems.Length - 1)];
            else if (jewelrySlot <= 62)
                jewelType = LootTables.braceletItems[ThreadSafeRandom.Next(0, LootTables.braceletItems.Length - 1)];
            else if (jewelrySlot <= 92)
                jewelType = LootTables.necklaceItems[ThreadSafeRandom.Next(0, LootTables.necklaceItems.Length - 1)];
            else
                jewelType = LootTables.trinketItems[ThreadSafeRandom.Next(0, LootTables.trinketItems.Length - 1)];

            WorldObject wo = WorldObjectFactory.CreateNewWorldObject((uint)jewelType);

            if (wo != null && mutate)
                MutateJewelry(wo, profile, isMagical);

            return wo;
        }
              
        private static void MutateJewelry(WorldObject wo, TreasureDeath profile, bool isMagical, TreasureRoll roll = null)
        {
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

            // wield level requirement for t7+
            if (profile.Tier > 6)
                RollWieldLevelReq_T7_T8(wo, profile);
            // wield level requirement for t9
            if (profile.Tier > 8)
                RollWieldLevelReq_T9(wo, profile);

            // assign magic
            if (isMagical)
                AssignMagic(wo, profile, roll);
            else
            {
                wo.ItemManaCost = null;
                wo.ItemMaxMana = null;
                wo.ItemCurMana = null;
                wo.ItemSpellcraft = null;
                wo.ItemDifficulty = null;
                wo.ManaRate = null;
            }
            // Empowered jewelry
            if (profile.Tier == 9 && profile.TreasureType != 4111)
            {
                TryRollEquipmentSet(wo, profile, roll);
                TryMutateGearRating(wo, profile, roll);
                wo.Empowered = false;
                var empoweredJewelry = ThreadSafeRandom.Next(1.0f, 0.0f);
                var oldname = wo.GetProperty(PropertyString.Name);
                var name = $"Empowered {oldname}";

                if (empoweredJewelry <= 0.25f && profile.Tier >= 9 || profile.TreasureType == 3112)
                {
                    wo.SetProperty(PropertyBool.Empowered, true);
                    wo.SetProperty(PropertyString.Name, name);
                    wo.SetProperty(PropertyInt.WieldRequirements, 7);
                    wo.SetProperty(PropertyInt.WieldDifficulty, 275);
                }

            }
            // Proto Jewelry
            if (profile.TreasureType == 4111 && isMagical)
            {                
                TryMutateGearRating(wo, profile, roll);
                wo.Proto = false;
                var oldname = wo.GetProperty(PropertyString.Name);
                var name = $"Proto {oldname}";
                var maxlevel = 20;
                var basexp = 10000000000;
                var jewelryProc = ThreadSafeRandom.Next(0.0f, 1.0f);               

                wo.SetProperty(PropertyBool.Proto, true);
                wo.SetProperty(PropertyString.Name, name);
                wo.SetProperty(PropertyInt.WieldRequirements, 7);
                wo.SetProperty(PropertyInt.WieldDifficulty, 700);
                wo.ItemMaxLevel = maxlevel;
                wo.SetProperty(PropertyInt.ItemXpStyle, 1);
                wo.ItemBaseXp = basexp;
                wo.SetProperty(PropertyInt64.ItemTotalXp, 0);

                if (jewelryProc <= 0.3f)
                {
                    var spellProc = ThreadSafeRandom.Next(0.0f, 1.0f);

                    if (spellProc <= 0.3f)
                    {
                        wo.SetProperty(PropertyInt.ItemSpellcraft, 999);
                        wo.SetProperty(PropertyFloat.ProcSpellRate, 0.02f);
                        wo.SetProperty(PropertyDataId.ProcSpell, 4643);
                    }                        
                    else if (spellProc > 0.3f && spellProc < 0.6f)
                    {
                        wo.SetProperty(PropertyInt.ItemSpellcraft, 999);
                        wo.SetProperty(PropertyFloat.ProcSpellRate, 0.02f);
                        wo.SetProperty(PropertyDataId.ProcSpell, 4644);
                    }                        
                    else if (spellProc >= 0.6f)
                    {
                        wo.SetProperty(PropertyInt.ItemSpellcraft, 999);
                        wo.SetProperty(PropertyFloat.ProcSpellRate, 0.02f);
                        wo.SetProperty(PropertyDataId.ProcSpell, 4645);
                    }
                        
                }


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

            if (profile.Tier == 10 && isMagical)
            {
                TryMutateGearRating(wo, profile, roll);
                var oldname = wo.GetProperty(PropertyString.Name);
                var name = $"Arramoran {oldname}";
                var maxlevel = 50;
                var basexp = 10000000000;
                var jewelryProc = ThreadSafeRandom.Next(0.0f, 1.0f);

                wo.SetProperty(PropertyBool.Arramoran, true);
                wo.SetProperty(PropertyString.Name, name);
                wo.SetProperty(PropertyInt.WieldRequirements, 7);
                wo.ItemMaxLevel = maxlevel;
                wo.SetProperty(PropertyInt.ItemXpStyle, 1);
                wo.ItemBaseXp = basexp;
                wo.SetProperty(PropertyInt64.ItemTotalXp, 0);
                wo.SetProperty(PropertyInt.WieldDifficulty, 1500);

                if (jewelryProc >= 0.0f)
                {
                    var spellProc = ThreadSafeRandom.Next(0.0f, 1.0f);

                    if (spellProc <= 0.3f)
                    {
                        wo.SetProperty(PropertyInt.ItemSpellcraft, 999);
                        wo.SetProperty(PropertyFloat.ProcSpellRate, 0.02f);
                        wo.SetProperty(PropertyDataId.ProcSpell, 4643);
                    }
                    else if (spellProc > 0.3f && spellProc < 0.6f)
                    {
                        wo.SetProperty(PropertyInt.ItemSpellcraft, 999);
                        wo.SetProperty(PropertyFloat.ProcSpellRate, 0.02f);
                        wo.SetProperty(PropertyDataId.ProcSpell, 4644);
                    }
                    else if (spellProc >= 0.6f)
                    {
                        wo.SetProperty(PropertyInt.ItemSpellcraft, 999);
                        wo.SetProperty(PropertyFloat.ProcSpellRate, 0.02f);
                        wo.SetProperty(PropertyDataId.ProcSpell, 4645);
                    }
                }

                wo.GearHealingBoost += (ThreadSafeRandom.Next(5, 15));
                wo.GearMaxHealth += (ThreadSafeRandom.Next(5, 15));
                wo.GearCritDamage += (ThreadSafeRandom.Next(5, 15));
                wo.GearCritDamageResist += (ThreadSafeRandom.Next(5, 15));
                wo.GearDamage += (ThreadSafeRandom.Next(5, 15));
                wo.GearDamageResist += (ThreadSafeRandom.Next(5, 15));

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
            // gear rating (t8)
            if (roll != null && profile.Tier >= 8)
                TryMutateGearRating(wo, profile, roll);

            // item value
            //  if (wo.HasMutateFilter(MutateFilter.Value))     // fixme: data
            MutateValue(wo, profile.Tier, roll);

            wo.LongDesc = GetLongDesc(wo);
        }

        private static bool GetMutateJewelryData(uint wcid)
        {
            foreach (var jewelryTable in LootTables.jewelryTables)
            {
                if (jewelryTable.Contains((int)wcid))
                    return true;
            }
            return false;
        }
    }
}
