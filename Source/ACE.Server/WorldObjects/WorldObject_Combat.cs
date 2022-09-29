using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Factories;
using ACE.Server.Entity;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.WorldObjects
{
    partial class WorldObject
    {
        /// <summary>
        /// Determines if WorldObject can damage a target via PlayerKillerStatus
        /// </summary>
        /// <returns>null if no errors, else pk error list</returns>
        public virtual List<WeenieErrorWithString> CheckPKStatusVsTarget(WorldObject target, Spell spell)
        {
            // no restrictions here
            // player attacker restrictions handled in override
            return null;
        }

        /// <summary>
        /// Tries to proc any relevant items for the attack
        /// </summary>
        public void TryProcEquippedItems(WorldObject attacker, Creature target, bool selfTarget, WorldObject weapon)
        {
            // handle procs directly on this item -- ie. phials
            // this could also be monsters with the proc spell directly on the creature
            if (HasProc && ProcSpellSelfTargeted == selfTarget)
            {
                // projectile
                // monster
                TryProcItem(attacker, target);
            }

            // handle proc spells for weapon
            // this could be a melee weapon, or a missile launcher
            if (weapon != null && weapon.HasProc && weapon.ProcSpellSelfTargeted == selfTarget)
            {
                // weapon
                weapon.TryProcItem(attacker, target);
            }

            if (attacker != this && attacker.HasProc && attacker.ProcSpellSelfTargeted == selfTarget)
            {
                // handle special case -- missile projectiles from monsters w/ a proc directly on the mob
                // monster
                attacker.TryProcItem(attacker, target);
            }

            // handle aetheria procs
            if (attacker is Creature wielder)
            {
                var equippedAetheria = wielder.EquippedObjects.Values.Where(i => Aetheria.IsAetheria(i.WeenieClassId) && i.HasProc && i.ProcSpellSelfTargeted == selfTarget);
               

                // aetheria
                foreach (var aetheria in equippedAetheria)
                    aetheria.TryProcItem(attacker, target);

               /* // Void Siphon
                foreach (var siphon in equippedAetheria)
                if (siphon.WeenieClassId == 300301);
                if (!(activator is Player player))
                    return;

                // Good PCAP example of using a PetDevice to summon a pet:
                // Asherons-Call-packets-includes-3-towers\pkt_2017-1-30_1485823896_log.pcap lines 27837 - 27843

                if (PetClass == null)
                {
                    log.Error($"{activator.Name}.ActOnUse({Name}) - PetClass is null for PetDevice {WeenieClassId}");
                    return;
                }

                if (Structure == 0)
                {
                    //player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, "You must refill the essence to use it again."));
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat("Your summoning device does not have enough charges to function!", ChatMessageType.Broadcast));
                    return;
                }

                var wcid = (uint)PetClass;

                var result = SummonCreature(player, wcid); */










            }
        }
    }
}
