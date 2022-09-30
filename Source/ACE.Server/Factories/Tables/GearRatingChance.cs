using log4net;

using ACE.Database.Models.World;
using ACE.Server.Factories.Entity;
using ACE.Server.WorldObjects;
using ACE.Common;
using Microsoft.VisualBasic;

namespace ACE.Server.Factories.Tables
{
    public static class GearRatingChance
    {       

        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        

        private static ChanceTable<bool> RatingChance = new ChanceTable<bool>()
        {
            ( false, 0.50f ),
            ( true,  0.50f ),
        };

        private static ChanceTable<int> ArmorRating = new ChanceTable<int>()
        {
            ( 1, 0.70f ),
            ( 2, 0.25f ),
            ( 3, 0.05f ),
        };

        private static ChanceTable<int> ClothingJewelryRating = new ChanceTable<int>()
        {
            ( 1, 0.70f ),
            ( 2, 0.25f ),
            ( 3, 0.05f ),
        };

        private static ChanceTable<int> T9_ArmorRating = new ChanceTable<int>()       
        {
            ( 8, 0.70f ),
            ( 9, 0.25f ),
            ( 10, 0.05f ),
            ( 11, 0.05f ),
            ( 12, 0.05f ),
            ( 13, 0.05f ),
            ( 14, 0.05f ),
            ( 15, 0.05f ),           

        };

        private static ChanceTable<int> T9_ClothingJewelryRating = new ChanceTable<int>()
        {

            ( 8, 0.70f ),
            ( 9, 0.25f ),
            ( 10, 0.05f ),
            ( 11, 0.05f ),
            ( 12, 0.05f ),
            ( 13, 0.05f ),
            ( 14, 0.05f ),
            ( 15, 0.05f ),

        };
        
        public static int Roll(WorldObject wo, TreasureDeath profile, TreasureRoll roll)
        {
            // initial roll for rating chance
            if (!RatingChance.Roll(profile.LootQualityMod))
                return 0;

            // roll for the actual rating
            ChanceTable<int> rating = null;

            if (roll.HasArmorLevel(wo) && profile.Tier <= 8)
            {                
                rating = ArmorRating;
            }
            else if (roll.IsClothing && profile.Tier <= 8  || roll.IsJewelry && profile.Tier <= 8  || roll.IsCloak && profile.Tier <= 8)
            {
                rating = ClothingJewelryRating;
            }
            // T9 roll
            else if (roll.HasArmorLevel(wo) && profile.Tier == 9)
            {
                rating = T9_ArmorRating;
            }
            else if (roll.IsClothing && profile.Tier == 9 || roll.IsJewelry && profile.Tier == 9 || roll.IsCloak && profile.Tier == 9)
            {
                rating = T9_ClothingJewelryRating;
            }
            else
            {
                log.Error($"GearRatingChance.Roll({wo.Name}, {profile.TreasureType}, {roll.ItemType}): unknown item type");
                return 0;
            }        
            return rating.Roll(profile.LootQualityMod);
        }
    }
}
