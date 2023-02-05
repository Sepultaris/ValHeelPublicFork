using System.Collections.Generic;

using ACE.Common;
using ACE.Server.Factories.Enum;

namespace ACE.Server.Factories.Tables.Wcids
{
    public class MirraWcids
    {
        private static readonly List<WeenieClassName> mirraWcids = new List<WeenieClassName>()
        {
            WeenieClassName.SteelMirra,
            WeenieClassName.IronMirra,
            WeenieClassName.BluntMirra,
            WeenieClassName.BladedMirra,
            WeenieClassName.PiercingMirra,
            WeenieClassName.FrostMirra,
            WeenieClassName.FireMirra,
            WeenieClassName.CausticMirra,
            WeenieClassName.StaticMirra,
            WeenieClassName.GreenGarnetMirra,
            WeenieClassName.MahoganyMirra,
            WeenieClassName.AlabasterMirra
            
        };

        public static WeenieClassName Roll()
        {
            // verify: even chance for each cloak?
            var rng = ThreadSafeRandom.Next(0, mirraWcids.Count - 1);

            return mirraWcids[rng];
        }

        private static readonly HashSet<WeenieClassName> _combined1 = new HashSet<WeenieClassName>();

        static MirraWcids()
        {
            foreach (var mirraWcid in mirraWcids)
                _combined1.Add(mirraWcid);
        }

        public static bool Contains(WeenieClassName wcid)
        {
            return _combined1.Contains(wcid);
        }
    }
}
