using System;

using ACE.Entity;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    public class DamageHistoryInfo
    {
        public readonly WeakReference<WorldObject> Attacker;

        public readonly ObjectGuid Guid;
        public readonly string Name;
        public readonly int DoTOwnerGuid;

        public float TotalDamage;
        public float TotalThreat;

        public readonly WeakReference<Player> PetOwner;

        public bool IsPlayer => Guid.IsPlayer();

        public DamageHistoryInfo(WorldObject attacker, float totalDamage = 0.0f)
        {
            Attacker = new WeakReference<WorldObject>(attacker);

            Guid = attacker.Guid;
            Name = attacker.Name;
            DoTOwnerGuid = attacker.DoTOwnerGuid;

            TotalDamage = totalDamage;
            TotalThreat = totalDamage;

            var tankTotalThreatMod = 5.0f;

            if (attacker is Player player)
            {
                if (player.IsTank)
                {
                    if (player.TauntTimerActive)
                        tankTotalThreatMod = 10.0f;

                    TotalThreat *= tankTotalThreatMod;
                }
                else
                    TotalThreat *= totalDamage * 0.5f;
            }

            if (attacker is CombatPet combatPet && combatPet.P_PetOwner != null)
                PetOwner = new WeakReference<Player>(combatPet.P_PetOwner);

            if (attacker.WeenieClassId == 300501)
            {
                foreach (var p in PlayerManager.GetAllOnline())
                {
                    if (p.Guid.Full == attacker.DoTOwnerGuid)
                    {
                        if (DoTOwnerGuid != 0)
                        {
                            Guid = p.Guid;
                            Name = p.Name;
                        }
                    }
                }
            }
        }

        public WorldObject TryGetAttacker()
        {
            Attacker.TryGetTarget(out var attacker);

            return attacker;
        }

        public Player TryGetPetOwner()
        {
            PetOwner.TryGetTarget(out var petOwner);

            return petOwner;
        }

        public WorldObject TryGetPetOwnerOrAttacker()
        {
            if (PetOwner != null)
                return TryGetPetOwner();
            else
                return TryGetAttacker();
        }
    }
}
