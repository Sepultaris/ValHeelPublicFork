using ACE.Entity.Enum.Properties;
using ACE.Server.WorldObjects;
using ACE.Server.WorldObjects.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Server.DuskfallMods
{
    static class RaiseTargetHelpers
    {
        //TODO: Decide if this should update player of lum/exp and the raised property
        public static void SetLevel(this RaiseTarget target, Player player, int level)
        {
            //If it's an attribute being changed, make sure to update the starting value
            if (target.TryGetAttribute(player, out CreatureAttribute attribute))
            {
                //Find the change in current and desired level
                var levelChange = level - GetLevel(target, player);
                attribute.StartingValue += (uint)levelChange;   //Tested to work with negatives
            }            

            //Set the appropriate RaisedAttr or rating to desired level
            switch (target)
            {
                case RaiseTarget.Str:
                    player.RaisedStr = level;
                    break;
                case RaiseTarget.End:
                    player.RaisedEnd = level;
                    break;
                case RaiseTarget.Quick:
                    player.RaisedQuick = level;
                    break;
                case RaiseTarget.Coord:
                    player.RaisedCoord = level;
                    break;
                case RaiseTarget.Focus:
                    player.RaisedFocus = level;
                    break;
                case RaiseTarget.Self:
                    player.RaisedSelf = level;
                    break;                
                // new vitals                
                case RaiseTarget.World:
                    player.LumAugAllSkills = level;
                    break;
                case RaiseTarget.Invulnerability:
                    player.LumAugDamageReductionRating = level;
                    break;
                case RaiseTarget.Destruction:
                    player.LumAugDamageRating = level;
                    break;
                case RaiseTarget.Glory:
                    player.LumAugCritDamageRating = level;
                    break;
                case RaiseTarget.Temperance:
                    player.LumAugCritReductionRating = level;
                    break;
                case RaiseTarget.Vitality:                    
                    break;               
            }                        
            return;
        }

        public static void SetVitalLevel(this RaiseTargetVital target, Player player, int level)
        {                        
            if (target.TryGetVital(player, out CreatureVital vital))
            {
                //Find the change in current and desired level
                var levelChange = level - GetVitalLevel(target, player);
                vital.StartingValue += (uint)levelChange;   //Tested to work with negatives
            }

            switch (target)
            {
                case RaiseTargetVital.MaxHealth:
                    player.RaisedHealth = level;
                    break;
                case RaiseTargetVital.MaxStamina:
                    player.RaisedStamina = level;
                    break;
                case RaiseTargetVital.MaxMana:
                    player.RaisedMana = level;
                    break;
            }
            return;
        }
        public static int GetLevel(this RaiseTarget target, Player player)
        {
            switch (target)
            {
                //Attributes
                case RaiseTarget.Str: return player.RaisedStr;
                case RaiseTarget.End: return player.RaisedEnd;
                case RaiseTarget.Quick: return player.RaisedQuick;
                case RaiseTarget.Coord: return player.RaisedCoord;
                case RaiseTarget.Focus: return player.RaisedFocus;
                case RaiseTarget.Self: return player.RaisedSelf;                
                //Ratings
                case RaiseTarget.World: return player.LumAugAllSkills;
                case RaiseTarget.Invulnerability: return player.LumAugDamageReductionRating;                
                case RaiseTarget.Destruction: return player.LumAugDamageRating;
                case RaiseTarget.Glory: return player.LumAugCritDamageRating;
                case RaiseTarget.Temperance: return player.LumAugCritReductionRating;
                case RaiseTarget.Vitality: return player.LumAugVitality;
            }
            return -1;
        }
        public static int GetVitalLevel(this RaiseTargetVital target, Player player)
        {
            switch (target)
            {                
                case RaiseTargetVital.MaxHealth: return player.RaisedHealth;
                case RaiseTargetVital.MaxStamina: return player.RaisedStamina;
                case RaiseTargetVital.MaxMana: return player.RaisedMana;                
            }
            return -1;
        }
        public static int StartingLevel(this RaiseTarget target)
        {
            switch (target)
            {
                //Attributes
                case RaiseTarget t when t.IsAttribute(): return 1;                
                //Ratings return the normal max.
                ////Comment out to allow leveling down to 0 which would let a player go through the normal process to net a little Lum
                ///case RaiseTarget.World: return 10;  //Max World 
                case RaiseTarget.Invulnerability: return 1;
                case RaiseTarget.Destruction: return 1;
                case RaiseTarget.Glory: return 1;
                case RaiseTarget.Temperance: return 1;
                default: return 0;
            }
        }

        public static int StartingVitalLevel(this RaiseTargetVital target)
        {
            switch (target)
            {
                
                case RaiseTargetVital i when i.IsVital(): return 1;                
                default: return 0;
            }
        }
        public static bool TryGetCostToLevel(this RaiseTarget target, int startLevel, int numLevels, out long cost)
        {
            cost = uint.MaxValue;
            //This may be too restrictive but it guarantees you are /raising some amount from a valid starting point
            if (startLevel < target.StartingLevel() || numLevels < 1)
                return false;

            try
            {
                switch (target)
                {
                    case RaiseTarget t when t.IsAttribute():
                        var avgLevel = (1);  //Could use a decimal, but being off a very small amount should be fine
                        long avgCost = (long)(ValheelSettings.RAISE_ATTR_MULT * avgLevel / (ValheelSettings.RAISE_ATTR_MULT_DECAY - ValheelSettings.RAISE_ATTR_LVL_DECAY * avgLevel));
                        cost = checked(avgCost * numLevels);
                        return true;                    
                   /* case RaiseTarget.Destruction:
                    case RaiseTarget.Invulnerability:
                    case RaiseTarget.Glory:
                    case RaiseTarget.Temperance:
                    case RaiseTarget.Vitality:
                        cost = checked(numLevels * ValheelSettings.RAISE_RATING_MULT);
                        return true;
                    case RaiseTarget.World:
                        cost = checked(numLevels * ValheelSettings.RAISE_WORLD_MULT);
                        return true; */
                }
            }
            catch (OverflowException ex) { }
            return false;
        }

        public static bool TryGetCostToLevelVital(this RaiseTargetVital target, int startLevel, int numLevels, out long cost)
        {
            cost = uint.MaxValue;
            //This may be too restrictive but it guarantees you are /raising some amount from a valid starting point
            if (startLevel < target.StartingVitalLevel() || numLevels < 1)
                return false;

            try
            {
                switch (target)
                {
                    
                    case RaiseTargetVital i when i.IsVital():
                        var avgVitalLevel = (1);  //Could use a decimal, but being off a very small amount should be fine
                        long avgVitalCost = (long)(ValheelSettings.RAISE_ATTR_MULT * avgVitalLevel / (ValheelSettings.RAISE_ATTR_MULT_DECAY - ValheelSettings.RAISE_ATTR_LVL_DECAY * avgVitalLevel));
                        cost = checked(avgVitalCost * numLevels);
                        return true;                  
                }
            }
            catch (OverflowException ex) { }
            return false;
        }
        private static bool IsAttribute(this RaiseTarget target) { return target < RaiseTarget.World; }

        private static bool IsVital(this RaiseTargetVital target) { return target <= RaiseTargetVital.MaxMana; }

        public static bool TryGetAttribute(this RaiseTarget target, Player player, out CreatureAttribute? attribute)
        {
            attribute = null;
            if (!target.IsAttribute())
                return false;

            //If the target is an attribute set it and succeed
            attribute = player.Attributes[(PropertyAttribute)target];  //TODO: Requires the RaiseTarget enum to line up with the PropertyAttribute-- probably should do this a better way
            return true;
        }

        public static bool TryGetVital(this RaiseTargetVital target, Player player, out CreatureVital? vital)
        {
            vital = null;
            if (!target.IsVital())
                return false;

            //If the target is an attribute set it and succeed
            vital = player.Vitals[(PropertyAttribute2nd)target];  //TODO: Requires the RaiseTarget enum to line up with the PropertyAttribute-- probably should do this a better way
            return true;
        }
    }
}
