using ACE.Common;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Enum;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Entity;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        public void DoAbility(Player player, WorldObject abilityItem)
        {
            if (abilityItem == null || player == null)
                return;

            if (abilityItem.WeenieClassId == 802918)
                player.IsDamageBuffed = true;
        }

        public void ValHeelAbilityManager(Player player)
        {
            var currentUnixTime = Time.GetUnixTime();

            player.HoTBuffHandler(currentUnixTime);
            player.SoTBuffHandler(currentUnixTime);
            player.DefenseBuffHandler(player, currentUnixTime);
            player.DamageBuffHandler(player, currentUnixTime);
        }

        /// <summary>
        /// This handles the Tanks Bastion ability. It will check if the ability is active and if so, it will increase the players defense rating by 4x the amount of their current defense rating.
        /// </summary>
        public void DefenseBuffHandler(Player player, double currentUnixTime)
        {
            if (currentUnixTime - LastTankBuffTimestamp > 30 && IsTankBuffed && GetEquippedShield() != null)
            {
                int playerDefenseRating = player.LumAugDamageReductionRating;
                int ratingIncreaseAmount = playerDefenseRating * 4;
                int finalRatingAmount = playerDefenseRating + ratingIncreaseAmount;

                player.TankDefenseRatingIncrease = ratingIncreaseAmount;
                player.LumAugDamageReductionRating = finalRatingAmount;
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageReductionRating, finalRatingAmount));
                LastTankBuffTimestamp = currentUnixTime;
                player.PlayParticleEffect(PlayScript.ShieldUpGrey, Guid);
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You've activated Bastion, increaing your damage reduction rating for 10 seconds.", ChatMessageType.Broadcast));
                TankBuffedTimer = true;
            }
            if (currentUnixTime - LastTankBuffTimestamp >= 10 && TankBuffedTimer == true)
            {
                var returnValue = player.LumAugDamageReductionRating - player.TankDefenseRatingIncrease;

                player.LumAugDamageReductionRating = returnValue;
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageReductionRating, returnValue));
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Bastion has ended.", ChatMessageType.Broadcast));
                player.PlayParticleEffect(PlayScript.ShieldDownGrey, Guid);
                TankBuffedTimer = false;
            }
            if (currentUnixTime - LastTankBuffTimestamp >= 29 && IsTankBuffed == true)
            {
                IsTankBuffed = false;
            }
        }

        /// <summary>
        /// This is the Damage Buff handler. It will check if the spell is active and if so, it will increase the players damage rating by 6x the amount of their current damage rating.
        /// </summary>
        public void DamageBuffHandler(Player player, double currentUnixTime)
        {
            if (currentUnixTime - LastDamageBuffTimestamp > 30 && IsDamageBuffed)
            {
                int playerDamageRating = player.LumAugDamageRating;
                int ratingIncreaseAmount = playerDamageRating * 6;
                int finalRatingAmount = playerDamageRating + ratingIncreaseAmount;

                player.DamageRatingIncrease = ratingIncreaseAmount;
                player.LumAugDamageRating = finalRatingAmount;
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageRating, finalRatingAmount));
                LastDamageBuffTimestamp = currentUnixTime;
                player.PlayParticleEffect(PlayScript.ShieldUpRed, Guid);
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You've activated Power Attack, increaing your damage rating for 10 seconds.", ChatMessageType.Broadcast));
                DamageBuffedTimer = true;
            }
            if (currentUnixTime - LastDamageBuffTimestamp >= 10 && DamageBuffedTimer == true)
            {
                var returnValue = player.LumAugDamageRating - player.DamageRatingIncrease;

                player.LumAugDamageRating = returnValue;
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageRating, returnValue));
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Power Attack has ended.", ChatMessageType.Broadcast));
                player.PlayParticleEffect(PlayScript.ShieldDownRed, Guid);
                DamageBuffedTimer = false;
            }
            if (currentUnixTime - LastDamageBuffTimestamp >= 29 && IsDamageBuffed == true)
            {
                IsDamageBuffed = false;
            }
        }

        /// <summary>
        /// This is the handler for the HoT spell. It will check if the spell is active and if so, it will cast the spell every 3 seconds for the duration of the spell.
        /// </summary>
        /// <param name="currentUnixTime"></param>
        public void HoTBuffHandler(double currentUnixTime)
        {
            if (Hot8)
            {
                var spell = new Spell(SpellId.HealSelf8);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);
                int tickTime = HoTDuration / HoTTicks;

                if (currentUnixTime - castTime > duration)
                {
                    CreatePlayerSpell(this, spell, false);
                    Hot8 = false;
                    IsHoTTicking = false;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % tickTime == 0 && currentUnixTime - LastHoTTickTimestamp >= tickTime)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                }
            }
            else if (Hot7)
            {
                var spell = new Spell(SpellId.HealSelf7);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);

                if (currentUnixTime - castTime >= duration)
                {
                    Hot7 = false;
                    IsHoTTicking = false;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % 3 == 0 && currentUnixTime - LastHoTTickTimestamp >= 3)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                }
            }
            else if (Hot6)
            {
                var spell = new Spell(SpellId.HealSelf6);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);

                if (currentUnixTime - castTime >= duration)
                {
                    Hot6 = false;
                    IsHoTTicking = false;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % 3 == 0 && currentUnixTime - LastHoTTickTimestamp >= 3)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                }
            }
            else if (Hot5)
            {
                var spell = new Spell(SpellId.HealSelf5);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);

                if (currentUnixTime - castTime >= duration)
                {
                    Hot5 = false;
                    IsHoTTicking = false;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % 3 == 0 && currentUnixTime - LastHoTTickTimestamp >= 3)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                }
            }
            else if (Hot4)
            {
                var spell = new Spell(SpellId.HealSelf4);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);

                if (currentUnixTime - castTime >= duration)
                {
                    Hot4 = false;
                    IsHoTTicking = false;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % 3 == 0 && currentUnixTime - LastHoTTickTimestamp >= 3)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                }
            }
            else if (Hot3)
            {
                var spell = new Spell(SpellId.HealSelf3);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);

                if (currentUnixTime - castTime >= duration)
                {
                    Hot3 = false;
                    IsHoTTicking = false;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % 3 == 0 && currentUnixTime - LastHoTTickTimestamp >= 3)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                }
            }
            else if (Hot2)
            {
                var spell = new Spell(SpellId.HealSelf2);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);

                if (currentUnixTime - castTime >= duration)
                {
                    Hot2 = false;
                    IsHoTTicking = false;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % 3 == 0 && currentUnixTime - LastHoTTickTimestamp >= 3)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                }
            }
            else if (Hot1)
            {
                var spell = new Spell(SpellId.HealSelf1);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);

                if (currentUnixTime - castTime >= duration)
                {
                    Hot1 = false;
                    IsHoTTicking = false;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % 3 == 0 && currentUnixTime - LastHoTTickTimestamp >= 3)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                }
            }
        }

        public void SoTBuffHandler(double currentUnixTime)
        {
            if (Sot8)
            {
                var spell = new Spell(SpellId.RevitalizeSelf8);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);

                if (currentUnixTime - castTime >= duration)
                {
                    Sot8 = false;
                    IsHoTTicking = false;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % 3 == 0 && currentUnixTime - LastHoTTickTimestamp >= 3)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                }
            }
            else if (Sot7)
            {
                var spell = new Spell(SpellId.RevitalizeSelf7);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);

                if (currentUnixTime - castTime >= duration)
                {
                    Sot7 = false;
                    IsHoTTicking = false;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % 3 == 0 && currentUnixTime - LastHoTTickTimestamp >= 3)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                }
            }
            else if (Sot6)
            {
                var spell = new Spell(SpellId.RevitalizeSelf6);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);

                if (currentUnixTime - castTime >= duration)
                {
                    Sot6 = false;
                    IsHoTTicking = false;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % 3 == 0 && currentUnixTime - LastHoTTickTimestamp >= 3)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                }
            }
            else if (Sot5)
            {
                var spell = new Spell(SpellId.RevitalizeSelf5);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);

                if (currentUnixTime - castTime >= duration)
                {
                    Sot5 = false;
                    IsHoTTicking = false;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % 3 == 0 && currentUnixTime - LastHoTTickTimestamp >= 3)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                }
            }
            else if (Sot4)
            {
                var spell = new Spell(SpellId.RevitalizeSelf4);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);

                if (currentUnixTime - castTime >= duration)
                {
                    Sot4 = false;
                    IsHoTTicking = false;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % 3 == 0 && currentUnixTime - LastHoTTickTimestamp >= 3)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                }
            }
            else if (Sot3)
            {
                var spell = new Spell(SpellId.RevitalizeSelf3);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);

                if (currentUnixTime - castTime >= duration)
                {
                    Sot3 = false;
                    IsHoTTicking = false;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % 3 == 0 && currentUnixTime - LastHoTTickTimestamp >= 3)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                }
            }
            else if (Sot2)
            {
                var spell = new Spell(SpellId.RevitalizeSelf2);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);

                if (currentUnixTime - castTime >= duration)
                {
                    Sot2 = false;
                    IsHoTTicking = false;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % 3 == 0 && currentUnixTime - LastHoTTickTimestamp >= 3)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                }
            }
            else if (Sot1)
            {
                var spell = new Spell(SpellId.RevitalizeSelf1);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);

                if (currentUnixTime - castTime >= duration)
                {
                    Sot1 = false;
                    IsHoTTicking = false;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % 3 == 0 && currentUnixTime - LastHoTTickTimestamp >= 3)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                }
            }
        }
    }
}
