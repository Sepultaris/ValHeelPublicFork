using System.Collections.Generic;

using ACE.Common;
using ACE.Database.Models.World;
using ACE.Server.Factories.Enum;
using ACE.Server.Factories.Entity;
using Org.BouncyCastle.Crypto;
using ACE.Server.Entity;


namespace ACE.Server.Factories.Tables
{
    public static class MirraChance
    {
        private static readonly ChanceTable<WeenieClassName> mirraWcids = new ChanceTable<WeenieClassName>()
        {
            (WeenieClassName.SteelMirra, 2.0003f),
            (WeenieClassName.IronMirra, 2.0003f),
            (WeenieClassName.BluntMirra, 5.0004f),
            (WeenieClassName.BladedMirra, 5.0004f),
            (WeenieClassName.PiercingMirra, 5.0004f),
            (WeenieClassName.FrostMirra, 5.0004f), 
            (WeenieClassName.FireMirra, 5.0004f),
            (WeenieClassName.CausticMirra, 5.0004f),
            (WeenieClassName.StaticMirra, 5.0004f),
            (WeenieClassName.GreenGarnetMirra, 2.0003f),
            (WeenieClassName.MahoganyMirra, 2.0003f),
            (WeenieClassName.AlabasterMirra, 0.1001f),

        };

        private static readonly List<ChanceTable<WeenieClassName>> Mirra = new List<ChanceTable<WeenieClassName>>()
        {
            mirraWcids
        };

        public static WeenieClassName Roll(int tier)
        {

            return Mirra[0].Roll();
        }

        private static readonly HashSet<WeenieClassName> _combined = new HashSet<WeenieClassName>();

        static MirraChance()
        {
            foreach (var tierChance in Mirra)
            {
                foreach (var entry in tierChance)
                    _combined.Add(entry.result);
            }
        }

        public static bool Contains(WeenieClassName wcid)
        {
            return _combined.Contains(wcid);
        }
    }
}
