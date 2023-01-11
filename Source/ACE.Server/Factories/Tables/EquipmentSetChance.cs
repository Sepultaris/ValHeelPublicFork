using System.Collections.Generic;

using ACE.Common;
using ACE.Entity.Enum;
using ACE.Database.Models.World;
using ACE.Server.Factories.Entity;
using ACE.Server.WorldObjects;
using ACE.Entity.Enum.Properties;

namespace ACE.Server.Factories.Tables
{
    public static class EquipmentSetChance
    {
        // t7 and t8 armor has a ~1/3 chance of having an equipment set
        private static readonly ChanceTable<bool> armorSetChance = new ChanceTable<bool>()
        {
            ( false, 0.66f ),
            ( true,  0.34f ),
        };

        private static readonly List<EquipmentSet> armorSets = new List<EquipmentSet>()
        {
            EquipmentSet.Soldiers,
            EquipmentSet.Adepts,
            EquipmentSet.Archers,
            EquipmentSet.Defenders,
            EquipmentSet.Tinkers,
            EquipmentSet.Crafters,
            EquipmentSet.Hearty,
            EquipmentSet.Dexterous,
            EquipmentSet.Wise,
            EquipmentSet.Swift,
            EquipmentSet.Hardened,
            EquipmentSet.Reinforced,
            EquipmentSet.Interlocking,
            EquipmentSet.Flameproof,
            EquipmentSet.Acidproof,
            EquipmentSet.Coldproof,
            EquipmentSet.Lightningproof,
        };

        public static EquipmentSet? Roll(WorldObject wo, TreasureDeath profile, TreasureRoll roll)
        {
            if (profile.Tier < 6 || !roll.HasArmorLevel(wo))
                return null;

            var canRoll = false;

            if (wo.GetProperty(PropertyInt.ItemType) == 1)
                canRoll = true;

            if (wo.GetProperty(PropertyInt.ItemType) == 2)
                canRoll = true;

            if (wo.GetProperty(PropertyInt.ItemType) == 4)
                canRoll = true;

            if (wo.GetProperty(PropertyInt.ItemType) == 8)
                canRoll = true;

            if (wo.GetProperty(PropertyInt.ItemType) == 100)
                canRoll = true;

            if (wo.GetProperty(PropertyInt.ItemType) == 8000)
                canRoll = true;

            if (canRoll != true)            
                return null;
           
                

            /*if (wo.ClothingPriority == null || (wo.ClothingPriority & (CoverageMask)CoverageMaskHelper.Outerwear) == 0)
                return null;*/

            // loot quality mod?
            /*if (!armorSetChance.Roll(profile.LootQualityMod))
                return null;*/

            // each armor set has an even chance of being selected
            var rng = ThreadSafeRandom.Next(0, armorSets.Count - 1);

            return armorSets[rng];
        }
    }
}
