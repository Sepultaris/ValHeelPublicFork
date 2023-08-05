using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Server.ValheelMods
{
    public static class ValheelSettings
    {
        //TODO: Decide if these should be added to PropertyManager to allow modification with commands like /modifybool
        //RAISE_ATTR_MULT * L / (RAISE_ATTR_MULT_DECAY - RAISE_ATTR_LVL_DECAY * L), where L = current amount raised
        public const double RAISE_ATTR_MULT = 10000000000D;
        public const double RAISE_ATTR_MULT_DECAY = 1D;
        public const double RAISE_ATTR_LVL_DECAY = 0.000D;
        public const long RAISE_RATING_MULT = 10000000;
        public const long RAISE_WORLD_MULT = 0000000;
        public const uint RAISE_MAX = 100000;
    }
}
