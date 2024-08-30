using System.Collections.Generic;

using ACE.Common;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Server.WorldObjects;

namespace ACE.Server.Factories
{
    public static partial class LootGenerationFactory
    {
        public static WorldObject HandlePolishMirra()
        {
            var rng = ThreadSafeRandom.Next(0, LootTables.MirraMatrix.Length - 1);

            var wcid = (uint)LootTables.MirraMatrix[rng];

            //var wcid = MirraChance.Roll(profile);

            var wo = WorldObjectFactory.CreateNewWorldObject(wcid);

            var levelRoll = ThreadSafeRandom.Next(0, 1000);

            
            if (levelRoll >= 0 && levelRoll <= 599)
            {
                wo.Level = 3;
            }
            else if (levelRoll >= 600 && levelRoll <= 899)
            {
                wo.Level = 4;
            }
            else if (levelRoll >= 900 && levelRoll <= 1000)
            {
                wo.Level = 5;
            }

            MutateMirra(wo);

            return wo;
        }

        public static WorldObject CreateMirra(TreasureDeath profile, bool isMagical, bool mutate = true)
        {
            if (profile.TreasureType != 5000)
                return null;

            var rng = ThreadSafeRandom.Next(0, LootTables.MirraMatrix.Length - 1);

            var wcid = (uint)LootTables.MirraMatrix[rng];
            
            //var wcid = MirraChance.Roll(profile);

            var wo = WorldObjectFactory.CreateNewWorldObject(wcid);

            var chance = ThreadSafeRandom.Next(0.00f, 1.00f);

            if(wo == null)
            {
                return null;
            }

            if (wo.WeenieClassId == 801966 && chance > 0.25f || wo.WeenieClassId == 801967 && chance > 0.25f || wo.WeenieClassId == 801975 && chance > 0.25f || wo.WeenieClassId == 801976 && chance > 0.25f)
                return null;
            if (wo.WeenieClassId == 801968 && chance > 0.4f || wo.WeenieClassId == 801969 && chance > 0.4f || wo.WeenieClassId == 801970 && chance > 0.4f || wo.WeenieClassId == 801971 && chance > 0.4f || wo.WeenieClassId == 801972 && chance > 0.4f || wo.WeenieClassId == 801973 && chance > 0.4f || wo.WeenieClassId == 801974 && chance > 0.4f)
                return null;
            if (wo.WeenieClassId == 801977 && chance > 0.05)
                return null;

            if (wo != null && mutate)
                MutateMirra(wo);

            return wo;
        }
     
        private static int? Roll_MirraLevel(WorldObject wo)        
        {
            var levelRoll = ThreadSafeRandom.Next(0, 1000);

            if (levelRoll <= 499)
            {
                return wo.Level = 1;
            }
            else if (levelRoll >= 500 && levelRoll <= 809)
            {
                return wo.Level = 2;
            }
            else if (levelRoll >= 810 && levelRoll <= 949)
            {
                return wo.Level = 3;
            }
            else if (levelRoll >= 950 && levelRoll <= 997)
            {
                return wo.Level = 4;
            }
            else if (levelRoll >= 998 && levelRoll <= 1000)
            {
                return wo.Level = 5;
            }
            else return 1; //default
        }

        public static void MutateMirra(WorldObject wo)
        {
            if (wo.Level == null)
                wo.Level = Roll_MirraLevel(wo);
 
            if (wo.WeenieClassId == 801966)
            {
                wo.UiEffects = UiEffects.Magical;
                wo.ItemUseable = Usable.SourceContainedTargetContained;
                wo.TargetType = ItemType.Vestements;

                if (wo.Level <= 0 || wo.Level == null)
                    wo.Level = 1;

                if (wo.Level == 1)
                {
                    wo.IconOverlayId = 0x6006C34; // 1
                    wo.MirraArmorBonus = ThreadSafeRandom.Next(10, 24);
                }
                if (wo.Level == 2)
                {
                    wo.IconOverlayId = 0x6006C35; // 2
                    wo.MirraArmorBonus = ThreadSafeRandom.Next(25, 49);
                }
                if (wo.Level == 3)
                {
                    wo.IconOverlayId = 0x6006C36; // 3
                    wo.MirraArmorBonus = ThreadSafeRandom.Next(50, 74);
                }
                if (wo.Level == 4)
                {
                    wo.IconOverlayId = 0x6006C37; // 4
                    wo.MirraArmorBonus = ThreadSafeRandom.Next(75, 99);
                }
                if (wo.Level == 5)
                {
                    wo.IconOverlayId = 0x6006C38; // 5
                    wo.MirraArmorBonus = ThreadSafeRandom.Next(100, 150);
                }

            }
            else if (wo.WeenieClassId == 801967)
            {
                var damageBonus = ThreadSafeRandom.Next(0, 100);

                wo.UiEffects = UiEffects.Magical;
                wo.ItemUseable = Usable.SourceContainedTargetContained;
                wo.TargetType = ItemType.MeleeWeapon;

                if (wo.Level <= 0 || wo.Level == null)
                    wo.Level = 1;

                if (wo.Level == 1)
                {
                    wo.IconOverlayId = 0x6006C34; // 1
                    wo.MirraWeaponBonus = 15;
                }
                if (wo.Level == 2)
                {
                    wo.IconOverlayId = 0x6006C35; // 1
                    wo.MirraWeaponBonus = 25;
                }
                if (wo.Level == 3)
                {
                    wo.IconOverlayId = 0x6006C36; // 1
                    wo.MirraWeaponBonus = 50;
                }
                if (wo.Level == 4)
                {
                    wo.IconOverlayId = 0x6006C37; // 1
                    wo.MirraWeaponBonus = 75;
                }
                if (wo.Level == 5)
                {
                    wo.IconOverlayId = 0x6006C38; // 1
                    wo.MirraWeaponBonus = 100;
                }
            }
            else if (wo.WeenieClassId == 801968)
            {                
                wo.UiEffects = UiEffects.Magical;
                wo.ItemUseable = Usable.SourceContainedTargetContained;
                wo.TargetType = ItemType.Vestements;

                if (wo.Level == 1)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.10f, 0.19f);
                    wo.IconOverlayId = 0x6006C34; // 1                  
                }
                if (wo.Level == 2)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.20f, 0.39f);
                    wo.IconOverlayId = 0x6006C35; // 1
                    
                }
                if (wo.Level == 3)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.40f, 0.59f);
                    wo.IconOverlayId = 0x6006C36; // 1
                    
                }
                if (wo.Level == 4)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.60f, 0.79f);
                    wo.IconOverlayId = 0x6006C37; // 1
                    
                }
                if (wo.Level == 5)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.80f, 1.00f);
                    wo.IconOverlayId = 0x6006C38; // 1
                    
                }
                              
            }
            else if (wo.WeenieClassId == 801969)
            {
                var resistanceBonus = (float)ThreadSafeRandom.Next(0.10f, 1.00f);

                wo.MirraResistanceBonus = resistanceBonus;
                wo.UiEffects = UiEffects.Magical;
                wo.ItemUseable = Usable.SourceContainedTargetContained;
                wo.TargetType = ItemType.Vestements;

                if (wo.Level == 1)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.01f, 0.19f);
                    wo.IconOverlayId = 0x6006C34; // 1                  
                }
                if (wo.Level == 2)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.20f, 0.39f);
                    wo.IconOverlayId = 0x6006C35; // 1

                }
                if (wo.Level == 3)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.40f, 0.59f);
                    wo.IconOverlayId = 0x6006C36; // 1

                }
                if (wo.Level == 4)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.60f, 0.79f);
                    wo.IconOverlayId = 0x6006C37; // 1

                }
                if (wo.Level == 5)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.80f, 1.00f);
                    wo.IconOverlayId = 0x6006C38; // 1

                }
            }
            else if (wo.WeenieClassId == 801970)
            {
                var resistanceBonus = (float)ThreadSafeRandom.Next(0.10f, 1.00f);

                wo.MirraResistanceBonus = resistanceBonus;
                wo.UiEffects = UiEffects.Magical;
                wo.ItemUseable = Usable.SourceContainedTargetContained;
                wo.TargetType = ItemType.Vestements;

                if (wo.Level == 1)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.10f, 0.19f);
                    wo.IconOverlayId = 0x6006C34; // 1                  
                }
                if (wo.Level == 2)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.20f, 0.39f);
                    wo.IconOverlayId = 0x6006C35; // 1

                }
                if (wo.Level == 3)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.40f, 0.59f);
                    wo.IconOverlayId = 0x6006C36; // 1

                }
                if (wo.Level == 4)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.60f, 0.79f);
                    wo.IconOverlayId = 0x6006C37; // 1

                }
                if (wo.Level == 5)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.80f, 1.00f);
                    wo.IconOverlayId = 0x6006C38; // 1

                }
            }
            else if (wo.WeenieClassId == 801971)
            {
                var resistanceBonus = (float)ThreadSafeRandom.Next(0.10f, 1.00f);

                wo.MirraResistanceBonus = resistanceBonus;
                wo.UiEffects = UiEffects.Magical;
                wo.ItemUseable = Usable.SourceContainedTargetContained;
                wo.TargetType = ItemType.Vestements;

                if (wo.Level == 1)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.10f, 0.19f);
                    wo.IconOverlayId = 0x6006C34; // 1                  
                }
                if (wo.Level == 2)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.20f, 0.39f);
                    wo.IconOverlayId = 0x6006C35; // 1

                }
                if (wo.Level == 3)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.40f, 0.59f);
                    wo.IconOverlayId = 0x6006C36; // 1

                }
                if (wo.Level == 4)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.60f, 0.79f);
                    wo.IconOverlayId = 0x6006C37; // 1

                }
                if (wo.Level == 5)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.80f, 1.00f);
                    wo.IconOverlayId = 0x6006C38; // 1

                }
            }
            else if (wo.WeenieClassId == 801972)
            {
                var resistanceBonus = (float)ThreadSafeRandom.Next(0.10f, 1.00f);

                wo.MirraResistanceBonus = resistanceBonus;
                wo.UiEffects = UiEffects.Magical;
                wo.ItemUseable = Usable.SourceContainedTargetContained;
                wo.TargetType = ItemType.Vestements;

                if (wo.Level == 1)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.10f, 0.19f);
                    wo.IconOverlayId = 0x6006C34; // 1                  
                }
                if (wo.Level == 2)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.20f, 0.39f);
                    wo.IconOverlayId = 0x6006C35; // 1

                }
                if (wo.Level == 3)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.40f, 0.59f);
                    wo.IconOverlayId = 0x6006C36; // 1

                }
                if (wo.Level == 4)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.60f, 0.79f);
                    wo.IconOverlayId = 0x6006C37; // 1

                }
                if (wo.Level == 5)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.80f, 1.00f);
                    wo.IconOverlayId = 0x6006C38; // 1

                }
            }
            else if (wo.WeenieClassId == 801973)
            {
                var resistanceBonus = (float)ThreadSafeRandom.Next(0.10f, 1.00f);

                wo.MirraResistanceBonus = resistanceBonus;
                wo.UiEffects = UiEffects.Magical;
                wo.ItemUseable = Usable.SourceContainedTargetContained;
                wo.TargetType = ItemType.Vestements;

                if (wo.Level == 1)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.10f, 0.19f);
                    wo.IconOverlayId = 0x6006C34; // 1                  
                }
                if (wo.Level == 2)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.20f, 0.39f);
                    wo.IconOverlayId = 0x6006C35; // 1

                }
                if (wo.Level == 3)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.40f, 0.59f);
                    wo.IconOverlayId = 0x6006C36; // 1

                }
                if (wo.Level == 4)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.60f, 0.79f);
                    wo.IconOverlayId = 0x6006C37; // 1

                }
                if (wo.Level == 5)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.80f, 1.00f);
                    wo.IconOverlayId = 0x6006C38; // 1

                }
            }
            else if (wo.WeenieClassId == 801974)
            {
                var resistanceBonus = (float)ThreadSafeRandom.Next(0.10f, 1.00f);

                wo.MirraResistanceBonus = resistanceBonus;
                wo.UiEffects = UiEffects.Magical;
                wo.ItemUseable = Usable.SourceContainedTargetContained;
                wo.TargetType = ItemType.Vestements;

                if (wo.Level == 1)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.10f, 0.19f);
                    wo.IconOverlayId = 0x6006C34; // 1                  
                }
                if (wo.Level == 2)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.20f, 0.39f);
                    wo.IconOverlayId = 0x6006C35; // 1

                }
                if (wo.Level == 3)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.40f, 0.59f);
                    wo.IconOverlayId = 0x6006C36; // 1

                }
                if (wo.Level == 4)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.60f, 0.79f);
                    wo.IconOverlayId = 0x6006C37; // 1

                }
                if (wo.Level == 5)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.80f, 1.00f);
                    wo.IconOverlayId = 0x6006C38; // 1

                }
            }
            else if (wo.WeenieClassId == 801975)
            {
                var damageBonus = (float)ThreadSafeRandom.Next(0.00f, 1.00f);

                wo.MirraDamageModBonus = damageBonus;
                wo.UiEffects = UiEffects.Magical;
                wo.ItemUseable = Usable.SourceContainedTargetContained;
                wo.TargetType = ItemType.Caster;

                if (wo.Level == 1)
                {
                    wo.MirraDamageModBonus = (float)ThreadSafeRandom.Next(0.01f, 0.04f);
                    wo.IconOverlayId = 0x6006C34; // 1                  
                }
                if (wo.Level == 2)
                {
                    wo.MirraDamageModBonus = (float)ThreadSafeRandom.Next(0.05f, 0.09f);
                    wo.IconOverlayId = 0x6006C35; // 1

                }
                if (wo.Level == 3)
                {
                    wo.MirraDamageModBonus = (float)ThreadSafeRandom.Next(0.1f, 0.14f);
                    wo.IconOverlayId = 0x6006C36; // 1

                }
                if (wo.Level == 4)
                {
                    wo.MirraDamageModBonus = (float)ThreadSafeRandom.Next(0.15f, 0.19f);
                    wo.IconOverlayId = 0x6006C37; // 1

                }
                if (wo.Level == 5)
                {
                    wo.MirraDamageModBonus = (float)ThreadSafeRandom.Next(0.2f, 0.25f);
                    wo.IconOverlayId = 0x6006C38; // 1

                }

            }
            else if (wo.WeenieClassId == 801976)
            {
                var damageBonus = (float)ThreadSafeRandom.Next(0.00f, 1.00f);

                wo.MirraDamageModBonus = damageBonus;
                wo.UiEffects = UiEffects.Magical;
                wo.ItemUseable = Usable.SourceContainedTargetContained;
                wo.TargetType = ItemType.MissileWeapon;

                if (wo.Level == 1)
                {
                    wo.MirraDamageModBonus = (float)ThreadSafeRandom.Next(0.01f, 0.04f);
                    wo.IconOverlayId = 0x6006C34; // 1                  
                }
                if (wo.Level == 2)
                {
                    wo.MirraDamageModBonus = (float)ThreadSafeRandom.Next(0.05f, 0.09f);
                    wo.IconOverlayId = 0x6006C35; // 1

                }
                if (wo.Level == 3)
                {
                    wo.MirraDamageModBonus = (float)ThreadSafeRandom.Next(0.1f, 0.14f);
                    wo.IconOverlayId = 0x6006C36; // 1

                }
                if (wo.Level == 4)
                {
                    wo.MirraDamageModBonus = (float)ThreadSafeRandom.Next(0.15f, 0.19f);
                    wo.IconOverlayId = 0x6006C37; // 1

                }
                if (wo.Level == 5)
                {
                    wo.MirraDamageModBonus = (float)ThreadSafeRandom.Next(0.2f, 0.25f);
                    wo.IconOverlayId = 0x6006C38; // 1

                }
            }
            else if (wo.WeenieClassId == 801977)
            {                
                wo.UiEffects = UiEffects.Magical;
                wo.ItemUseable = Usable.SourceContainedTargetContained;
                wo.TargetType = ItemType.Vestements;

                if (wo.Level == 1)
                {
                    wo.MirraRatingBonus = ThreadSafeRandom.Next(5, 5);
                    wo.IconOverlayId = 0x6006C34; // 1                  
                }
                if (wo.Level == 2)
                {
                    wo.MirraRatingBonus = ThreadSafeRandom.Next(10, 10);
                    wo.IconOverlayId = 0x6006C35; // 1

                }
                if (wo.Level == 3)
                {
                    wo.MirraRatingBonus = ThreadSafeRandom.Next(15, 15);
                    wo.IconOverlayId = 0x6006C36; // 1

                }
                if (wo.Level == 4)
                {
                    wo.MirraRatingBonus = ThreadSafeRandom.Next(20, 20);
                    wo.IconOverlayId = 0x6006C37; // 1

                }
                if (wo.Level == 5)
                {
                    wo.MirraRatingBonus = ThreadSafeRandom.Next(25, 25);
                    wo.IconOverlayId = 0x6006C38; // 1

                }
            }
            else return;

        }

        public static void MutatePolishedMirra(WorldObject wo)
        {
            if (wo.WeenieClassId == 801966)
            {
                wo.UiEffects = UiEffects.Magical;
                wo.ItemUseable = Usable.SourceContainedTargetContained;
                wo.TargetType = ItemType.Vestements;

                if (wo.Level <= 0 || wo.Level == null)
                    wo.Level = 1;

                if (wo.Level == 1)
                {
                    wo.IconOverlayId = 0x6006C34; // 1
                    wo.MirraArmorBonus = ThreadSafeRandom.Next(10, 24);
                }
                if (wo.Level == 2)
                {
                    wo.IconOverlayId = 0x6006C35; // 2
                    wo.MirraArmorBonus = ThreadSafeRandom.Next(25, 49);
                }
                if (wo.Level == 3)
                {
                    wo.IconOverlayId = 0x6006C36; // 3
                    wo.MirraArmorBonus = ThreadSafeRandom.Next(50, 74);
                }
                if (wo.Level == 4)
                {
                    wo.IconOverlayId = 0x6006C37; // 4
                    wo.MirraArmorBonus = ThreadSafeRandom.Next(75, 99);
                }
                if (wo.Level == 5)
                {
                    wo.IconOverlayId = 0x6006C38; // 5
                    wo.MirraArmorBonus = ThreadSafeRandom.Next(100, 150);
                }

            }
            if (wo.WeenieClassId == 801967)
            {
                var damageBonus = ThreadSafeRandom.Next(0, 100);

                wo.MirraWeaponBonus = damageBonus;
                wo.UiEffects = UiEffects.Magical;
                wo.ItemUseable = Usable.SourceContainedTargetContained;
                wo.TargetType = ItemType.MeleeWeapon;

                if (wo.Level <= 0 || wo.Level == null)
                    wo.Level = 1;

                if (wo.Level == 1)
                {
                    wo.IconOverlayId = 0x6006C34; // 1
                    wo.MirraArmorBonus = ThreadSafeRandom.Next(15, 24);
                }
                if (wo.Level == 2)
                {
                    wo.IconOverlayId = 0x6006C35; // 1
                    wo.MirraArmorBonus = ThreadSafeRandom.Next(25, 49);
                }
                if (wo.Level == 3)
                {
                    wo.IconOverlayId = 0x6006C36; // 1
                    wo.MirraArmorBonus = ThreadSafeRandom.Next(50, 74);
                }
                if (wo.Level == 4)
                {
                    wo.IconOverlayId = 0x6006C37; // 1
                    wo.MirraArmorBonus = ThreadSafeRandom.Next(75, 90);
                }
                if (wo.Level == 5)
                {
                    wo.IconOverlayId = 0x6006C38; // 1
                    wo.MirraArmorBonus = ThreadSafeRandom.Next(90, 100);
                }
            }
            if (wo.WeenieClassId == 801968)
            {
                wo.UiEffects = UiEffects.Magical;
                wo.ItemUseable = Usable.SourceContainedTargetContained;
                wo.TargetType = ItemType.Vestements;

                if (wo.Level == 1)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.10f, 0.19f);
                    wo.IconOverlayId = 0x6006C34; // 1                  
                }
                if (wo.Level == 2)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.20f, 0.39f);
                    wo.IconOverlayId = 0x6006C35; // 1

                }
                if (wo.Level == 3)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.40f, 0.59f);
                    wo.IconOverlayId = 0x6006C36; // 1

                }
                if (wo.Level == 4)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.60f, 0.79f);
                    wo.IconOverlayId = 0x6006C37; // 1

                }
                if (wo.Level == 5)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.80f, 1.00f);
                    wo.IconOverlayId = 0x6006C38; // 1

                }

            }
            if (wo.WeenieClassId == 801969)
            {
                var resistanceBonus = (float)ThreadSafeRandom.Next(0.10f, 1.00f);

                wo.MirraResistanceBonus = resistanceBonus;
                wo.UiEffects = UiEffects.Magical;
                wo.ItemUseable = Usable.SourceContainedTargetContained;
                wo.TargetType = ItemType.Vestements;

                if (wo.Level == 1)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.01f, 0.19f);
                    wo.IconOverlayId = 0x6006C34; // 1                  
                }
                if (wo.Level == 2)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.20f, 0.39f);
                    wo.IconOverlayId = 0x6006C35; // 1

                }
                if (wo.Level == 3)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.40f, 0.59f);
                    wo.IconOverlayId = 0x6006C36; // 1

                }
                if (wo.Level == 4)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.60f, 0.79f);
                    wo.IconOverlayId = 0x6006C37; // 1

                }
                if (wo.Level == 5)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.80f, 1.00f);
                    wo.IconOverlayId = 0x6006C38; // 1

                }
            }
            if (wo.WeenieClassId == 801970)
            {
                var resistanceBonus = (float)ThreadSafeRandom.Next(0.10f, 1.00f);

                wo.MirraResistanceBonus = resistanceBonus;
                wo.UiEffects = UiEffects.Magical;
                wo.ItemUseable = Usable.SourceContainedTargetContained;
                wo.TargetType = ItemType.Vestements;

                if (wo.Level == 1)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.10f, 0.19f);
                    wo.IconOverlayId = 0x6006C34; // 1                  
                }
                if (wo.Level == 2)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.20f, 0.39f);
                    wo.IconOverlayId = 0x6006C35; // 1

                }
                if (wo.Level == 3)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.40f, 0.59f);
                    wo.IconOverlayId = 0x6006C36; // 1

                }
                if (wo.Level == 4)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.60f, 0.79f);
                    wo.IconOverlayId = 0x6006C37; // 1

                }
                if (wo.Level == 5)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.80f, 1.00f);
                    wo.IconOverlayId = 0x6006C38; // 1

                }
            }
            if (wo.WeenieClassId == 801971)
            {
                var resistanceBonus = (float)ThreadSafeRandom.Next(0.10f, 1.00f);

                wo.MirraResistanceBonus = resistanceBonus;
                wo.UiEffects = UiEffects.Magical;
                wo.ItemUseable = Usable.SourceContainedTargetContained;
                wo.TargetType = ItemType.Vestements;

                if (wo.Level == 1)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.10f, 0.19f);
                    wo.IconOverlayId = 0x6006C34; // 1                  
                }
                if (wo.Level == 2)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.20f, 0.39f);
                    wo.IconOverlayId = 0x6006C35; // 1

                }
                if (wo.Level == 3)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.40f, 0.59f);
                    wo.IconOverlayId = 0x6006C36; // 1

                }
                if (wo.Level == 4)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.60f, 0.79f);
                    wo.IconOverlayId = 0x6006C37; // 1

                }
                if (wo.Level == 5)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.80f, 1.00f);
                    wo.IconOverlayId = 0x6006C38; // 1

                }
            }
            if (wo.WeenieClassId == 801972)
            {
                var resistanceBonus = (float)ThreadSafeRandom.Next(0.10f, 1.00f);

                wo.MirraResistanceBonus = resistanceBonus;
                wo.UiEffects = UiEffects.Magical;
                wo.ItemUseable = Usable.SourceContainedTargetContained;
                wo.TargetType = ItemType.Vestements;

                if (wo.Level == 1)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.10f, 0.19f);
                    wo.IconOverlayId = 0x6006C34; // 1                  
                }
                if (wo.Level == 2)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.20f, 0.39f);
                    wo.IconOverlayId = 0x6006C35; // 1

                }
                if (wo.Level == 3)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.40f, 0.59f);
                    wo.IconOverlayId = 0x6006C36; // 1

                }
                if (wo.Level == 4)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.60f, 0.79f);
                    wo.IconOverlayId = 0x6006C37; // 1

                }
                if (wo.Level == 5)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.80f, 1.00f);
                    wo.IconOverlayId = 0x6006C38; // 1

                }
            }
            if (wo.WeenieClassId == 801973)
            {
                var resistanceBonus = (float)ThreadSafeRandom.Next(0.10f, 1.00f);

                wo.MirraResistanceBonus = resistanceBonus;
                wo.UiEffects = UiEffects.Magical;
                wo.ItemUseable = Usable.SourceContainedTargetContained;
                wo.TargetType = ItemType.Vestements;

                if (wo.Level == 1)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.10f, 0.19f);
                    wo.IconOverlayId = 0x6006C34; // 1                  
                }
                if (wo.Level == 2)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.20f, 0.39f);
                    wo.IconOverlayId = 0x6006C35; // 1

                }
                if (wo.Level == 3)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.40f, 0.59f);
                    wo.IconOverlayId = 0x6006C36; // 1

                }
                if (wo.Level == 4)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.60f, 0.79f);
                    wo.IconOverlayId = 0x6006C37; // 1

                }
                if (wo.Level == 5)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.80f, 1.00f);
                    wo.IconOverlayId = 0x6006C38; // 1

                }
            }
            if (wo.WeenieClassId == 801974)
            {
                var resistanceBonus = (float)ThreadSafeRandom.Next(0.10f, 1.00f);

                wo.MirraResistanceBonus = resistanceBonus;
                wo.UiEffects = UiEffects.Magical;
                wo.ItemUseable = Usable.SourceContainedTargetContained;
                wo.TargetType = ItemType.Vestements;

                if (wo.Level == 1)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.10f, 0.19f);
                    wo.IconOverlayId = 0x6006C34; // 1                  
                }
                if (wo.Level == 2)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.20f, 0.39f);
                    wo.IconOverlayId = 0x6006C35; // 1

                }
                if (wo.Level == 3)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.40f, 0.59f);
                    wo.IconOverlayId = 0x6006C36; // 1

                }
                if (wo.Level == 4)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.60f, 0.79f);
                    wo.IconOverlayId = 0x6006C37; // 1

                }
                if (wo.Level == 5)
                {
                    wo.MirraResistanceBonus = (float)ThreadSafeRandom.Next(0.80f, 1.00f);
                    wo.IconOverlayId = 0x6006C38; // 1

                }
            }
            if (wo.WeenieClassId == 801975)
            {
                var damageBonus = (float)ThreadSafeRandom.Next(0.00f, 1.00f);

                wo.MirraDamageModBonus = damageBonus;
                wo.UiEffects = UiEffects.Magical;
                wo.ItemUseable = Usable.SourceContainedTargetContained;
                wo.TargetType = ItemType.Caster;

                if (wo.Level == 1)
                {
                    wo.MirraDamageModBonus = (float)ThreadSafeRandom.Next(0.01f, 0.04f);
                    wo.IconOverlayId = 0x6006C34; // 1                  
                }
                if (wo.Level == 2)
                {
                    wo.MirraDamageModBonus = (float)ThreadSafeRandom.Next(0.05f, 0.09f);
                    wo.IconOverlayId = 0x6006C35; // 1

                }
                if (wo.Level == 3)
                {
                    wo.MirraDamageModBonus = (float)ThreadSafeRandom.Next(0.1f, 0.14f);
                    wo.IconOverlayId = 0x6006C36; // 1

                }
                if (wo.Level == 4)
                {
                    wo.MirraDamageModBonus = (float)ThreadSafeRandom.Next(0.15f, 0.19f);
                    wo.IconOverlayId = 0x6006C37; // 1

                }
                if (wo.Level == 5)
                {
                    wo.MirraDamageModBonus = (float)ThreadSafeRandom.Next(0.2f, 0.25f);
                    wo.IconOverlayId = 0x6006C38; // 1

                }

            }
            if (wo.WeenieClassId == 801976)
            {
                var damageBonus = (float)ThreadSafeRandom.Next(0.00f, 1.00f);

                wo.MirraDamageModBonus = damageBonus;
                wo.UiEffects = UiEffects.Magical;
                wo.ItemUseable = Usable.SourceContainedTargetContained;
                wo.TargetType = ItemType.MissileWeapon;

                if (wo.Level == 1)
                {
                    wo.MirraDamageModBonus = (float)ThreadSafeRandom.Next(0.01f, 0.04f);
                    wo.IconOverlayId = 0x6006C34; // 1                  
                }
                if (wo.Level == 2)
                {
                    wo.MirraDamageModBonus = (float)ThreadSafeRandom.Next(0.05f, 0.09f);
                    wo.IconOverlayId = 0x6006C35; // 1

                }
                if (wo.Level == 3)
                {
                    wo.MirraDamageModBonus = (float)ThreadSafeRandom.Next(0.1f, 0.14f);
                    wo.IconOverlayId = 0x6006C36; // 1

                }
                if (wo.Level == 4)
                {
                    wo.MirraDamageModBonus = (float)ThreadSafeRandom.Next(0.15f, 0.19f);
                    wo.IconOverlayId = 0x6006C37; // 1

                }
                if (wo.Level == 5)
                {
                    wo.MirraDamageModBonus = (float)ThreadSafeRandom.Next(0.2f, 0.25f);
                    wo.IconOverlayId = 0x6006C38; // 1

                }
            }
            if (wo.WeenieClassId == 801977)
            {
                wo.UiEffects = UiEffects.Magical;
                wo.ItemUseable = Usable.SourceContainedTargetContained;
                wo.TargetType = ItemType.Vestements;

                if (wo.Level == 1)
                {
                    wo.MirraRatingBonus = ThreadSafeRandom.Next(5, 5);
                    wo.IconOverlayId = 0x6006C34; // 1                  
                }
                if (wo.Level == 2)
                {
                    wo.MirraRatingBonus = ThreadSafeRandom.Next(10, 10);
                    wo.IconOverlayId = 0x6006C35; // 1

                }
                if (wo.Level == 3)
                {
                    wo.MirraRatingBonus = ThreadSafeRandom.Next(15, 15);
                    wo.IconOverlayId = 0x6006C36; // 1

                }
                if (wo.Level == 4)
                {
                    wo.MirraRatingBonus = ThreadSafeRandom.Next(20, 20);
                    wo.IconOverlayId = 0x6006C37; // 1

                }
                if (wo.Level == 5)
                {
                    wo.MirraRatingBonus = ThreadSafeRandom.Next(25, 25);
                    wo.IconOverlayId = 0x6006C38; // 1

                }
            }

        }
    }
}
