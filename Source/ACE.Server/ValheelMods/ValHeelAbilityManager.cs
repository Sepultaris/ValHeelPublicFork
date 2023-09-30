using ACE.Common;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Enum;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Entity;
using ACE.Database;
using ACE.Entity;
using ACE.Server.Factories;
using System.Collections.Generic;
using ACE.Server.Command.Handlers;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        public void DoAbility(Player player, WorldObject abilityItem)
        {
            if (abilityItem == null || player == null)
                return;

            if (abilityItem.WeenieClassId == 802918)
                player.Brutalize = true;
        }

        public void ValHeelAbilityManager(Player player)
        {
            var currentUnixTime = Time.GetUnixTime();

            player.HoTBuffHandler(currentUnixTime);
            player.SoTBuffHandler(currentUnixTime);
            player.DefenseRatingBuffHandler(player, currentUnixTime);
            player.DamageRatingBuffHandler(player, currentUnixTime);
            player.BrutalizeHandler(player, currentUnixTime);
            player.LifeWellHandler(player, currentUnixTime);
        }

        public void LifeWellHandler(Player player, double currentUnixtime)
        {
            if (LifeWell == true && currentUnixtime - LastLifeWellTimestamp > 30)
            {
                var target = CommandHandlerHelper.GetLastAppraisedObject(player.Session);
                var dot = DatabaseManager.World.GetCachedWeenie(300503);

                if (target != null && target is Player targetPlayer)
                {
                    List<Player> targets = new List<Player>();
                    List<WorldObject> dotObjects = new List<WorldObject>();

                    var newDot = WorldObjectFactory.CreateNewWorldObject(dot);

                    targets.Add((Player)target);
                    dotObjects.Add(newDot);

                    newDot.DoTOwnerGuid = (int)player.Guid.Full;
                    newDot.Damage = -(int)(targetPlayer.Health.MaxValue * 0.1f);
                    newDot.Location = targetPlayer.Location;
                    newDot.Location.LandblockId = new LandblockId(newDot.Location.GetCell());
                    newDot.EnterWorld();

                    LifeWell = false;
                    LastLifeWellTimestamp = currentUnixtime;
                }
                if (target == null && LifeWell == true && currentUnixtime - LastLifeWellTimestamp > 30)
                {
                    List<Player> targets = new List<Player>();
                    List<WorldObject> dotObjects = new List<WorldObject>();

                    var newDot = WorldObjectFactory.CreateNewWorldObject(dot);

                    targets.Add(player);
                    dotObjects.Add(newDot);

                    newDot.DoTOwnerGuid = (int)player.Guid.Full;
                    newDot.Damage = (int)(player.Health.Current * 0.1f);
                    newDot.Location = player.Location;
                    newDot.Location.LandblockId = new LandblockId(newDot.Location.GetCell());
                    newDot.EnterWorld();

                    LifeWell = false;
                    LastLifeWellTimestamp = currentUnixtime;
                }
                else if (LifeWell == true && currentUnixtime - LastLifeWellTimestamp < 30)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You can't use Life Well yet.", ChatMessageType.Broadcast));
                }
            }
        }

        /// <summary>
        /// This is the Brutalize ability handler. It will check if the ability is active and if so, it will set the player to do a Brutalize attack.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="currentUnixtime"></param>
        public void BrutalizeHandler (Player player, double currentUnixtime)
        {
            if (player.Brutalize == true && currentUnixtime - LastBrutalizeTimestamp > 30)
            {
                player.DoBrutalizeAttack = true;
                player.PlayParticleEffect(PlayScript.EnchantUpRed, Guid);
                player.BrutalizeTimestamp = currentUnixtime;
                player.LastBrutalizeTimestamp = currentUnixtime;
                player.Brutalize = false;
            }
            if (player.DoBrutalizeAttack == true && currentUnixtime - BrutalizeTimestamp > 10)
            {
                player.Brutalize = false;
                player.DoBrutalizeAttack = false;
                player.PlayParticleEffect(PlayScript.EnchantDownRed, Guid);
            }
            if (player.Brutalize == true && currentUnixtime - LastBrutalizeTimestamp < 30)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You can't use Brutalize yet.", ChatMessageType.Broadcast));
                player.Brutalize = false;
            }
        }

        /// <summary>
        /// This handles the Tanks Bastion ability. It will check if the ability is active and if so, it will increase the players defense rating by 4x the amount of their current defense rating.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="currentUnixTime"></param>
        public void DefenseRatingBuffHandler(Player player, double currentUnixTime)
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
        public void DamageRatingBuffHandler(Player player, double currentUnixTime)
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
                    Hot8 = false;
                    IsHoTTicking = false;
                    HoTsTicked = 0;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % tickTime == 0 && currentUnixTime - LastHoTTickTimestamp >= tickTime && HoTsTicked < HoTTicks)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                    HoTsTicked++;
                }
            }
            else if (Hot7)
            {
                var spell = new Spell(SpellId.HealSelf7);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);
                int tickTime = HoTDuration / HoTTicks;

                if (currentUnixTime - castTime >= duration)
                {
                    Hot7 = false;
                    IsHoTTicking = false;
                    HoTsTicked = 0;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % tickTime == 0 && currentUnixTime - LastHoTTickTimestamp >= tickTime && HoTsTicked < HoTTicks)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                    HoTsTicked++;
                }
            }
            else if (Hot6)
            {
                var spell = new Spell(SpellId.HealSelf6);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);
                int tickTime = HoTDuration / HoTTicks;

                if (currentUnixTime - castTime >= duration)
                {
                    Hot6 = false;
                    IsHoTTicking = false;
                    HoTsTicked = 0;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % tickTime == 0 && currentUnixTime - LastHoTTickTimestamp >= tickTime && HoTsTicked < HoTTicks)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                    HoTsTicked++;
                }
            }
            else if (Hot5)
            {
                var spell = new Spell(SpellId.HealSelf5);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);
                int tickTime = HoTDuration / HoTTicks;

                if (currentUnixTime - castTime >= duration)
                {
                    Hot5 = false;
                    IsHoTTicking = false;
                    HoTsTicked = 0;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % tickTime == 0 && currentUnixTime - LastHoTTickTimestamp >= tickTime && HoTsTicked < HoTTicks)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                    HoTsTicked++;
                }
            }
            else if (Hot4)
            {
                var spell = new Spell(SpellId.HealSelf4);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);
                int tickTime = HoTDuration / HoTTicks;

                if (currentUnixTime - castTime >= duration)
                {
                    Hot4 = false;
                    IsHoTTicking = false;
                    HoTsTicked = 0;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % tickTime == 0 && currentUnixTime - LastHoTTickTimestamp >= tickTime && HoTsTicked < HoTTicks)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                    HoTsTicked++;
                }
            }
            else if (Hot3)
            {
                var spell = new Spell(SpellId.HealSelf3);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);
                int tickTime = HoTDuration / HoTTicks;

                if (currentUnixTime - castTime >= duration)
                {
                    Hot3 = false;
                    IsHoTTicking = false;
                    HoTsTicked = 0;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % tickTime == 0 && currentUnixTime - LastHoTTickTimestamp >= tickTime && HoTsTicked < HoTTicks)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                    HoTsTicked++;
                }
            }
            else if (Hot2)
            {
                var spell = new Spell(SpellId.HealSelf2);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);
                int tickTime = HoTDuration / HoTTicks;

                if (currentUnixTime - castTime >= duration)
                {
                    Hot2 = false;
                    IsHoTTicking = false;
                    HoTsTicked = 0;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % tickTime == 0 && currentUnixTime - LastHoTTickTimestamp >= tickTime && HoTsTicked < HoTTicks)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                    HoTsTicked++;
                }
            }
            else if (Hot1)
            {
                var spell = new Spell(SpellId.HealSelf1);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);
                int tickTime = HoTDuration / HoTTicks;

                if (currentUnixTime - castTime >= duration)
                {
                    Hot1 = false;
                    IsHoTTicking = false;
                    HoTsTicked = 0;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % tickTime == 0 && currentUnixTime - LastHoTTickTimestamp >= tickTime && HoTsTicked < HoTTicks)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                    HoTsTicked++;
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
                int tickTime = HoTDuration / HoTTicks;

                if (currentUnixTime - castTime >= duration)
                {
                    Sot8 = false;
                    IsHoTTicking = false;
                    HoTsTicked = 0;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % tickTime == 0 && currentUnixTime - LastHoTTickTimestamp >= tickTime && HoTsTicked < HoTTicks)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                    HoTsTicked++;
                }
            }
            else if (Sot7)
            {
                var spell = new Spell(SpellId.RevitalizeSelf7);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);
                int tickTime = HoTDuration / HoTTicks;

                if (currentUnixTime - castTime >= duration)
                {
                    Sot7 = false;
                    IsHoTTicking = false;
                    HoTsTicked = 0;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % tickTime == 0 && currentUnixTime - LastHoTTickTimestamp >= tickTime && HoTsTicked < HoTTicks)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                    HoTsTicked++;
                }
            }
            else if (Sot6)
            {
                var spell = new Spell(SpellId.RevitalizeSelf6);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);
                int tickTime = HoTDuration / HoTTicks;

                if (currentUnixTime - castTime >= duration)
                {
                    Sot6 = false;
                    IsHoTTicking = false;
                    HoTsTicked = 0;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % tickTime == 0 && currentUnixTime - LastHoTTickTimestamp >= tickTime && HoTsTicked < HoTTicks)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                    HoTsTicked++;
                }
            }
            else if (Sot5)
            {
                var spell = new Spell(SpellId.RevitalizeSelf5);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);
                int tickTime = HoTDuration / HoTTicks;

                if (currentUnixTime - castTime >= duration)
                {
                    Sot5 = false;
                    IsHoTTicking = false;
                    HoTsTicked = 0;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % tickTime == 0 && currentUnixTime - LastHoTTickTimestamp >= tickTime && HoTsTicked < HoTTicks)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                    HoTsTicked++;
                }
            }
            else if (Sot4)
            {
                var spell = new Spell(SpellId.RevitalizeSelf4);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);
                int tickTime = HoTDuration / HoTTicks;

                if (currentUnixTime - castTime >= duration)
                {
                    Sot4 = false;
                    IsHoTTicking = false;
                    HoTsTicked = 0;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % tickTime == 0 && currentUnixTime - LastHoTTickTimestamp >= tickTime && HoTsTicked < HoTTicks)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                    HoTsTicked++;
                }
            }
            else if (Sot3)
            {
                var spell = new Spell(SpellId.RevitalizeSelf3);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);
                int tickTime = HoTDuration / HoTTicks;

                if (currentUnixTime - castTime >= duration)
                {
                    Sot3 = false;
                    IsHoTTicking = false;
                    HoTsTicked = 0;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % tickTime == 0 && currentUnixTime - LastHoTTickTimestamp >= tickTime && HoTsTicked < HoTTicks)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                    HoTsTicked++;
                }
            }
            else if (Sot2)
            {
                var spell = new Spell(SpellId.RevitalizeSelf2);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);
                int tickTime = HoTDuration / HoTTicks;

                if (currentUnixTime - castTime >= duration)
                {
                    Sot2 = false;
                    IsHoTTicking = false;
                    HoTsTicked = 0;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % tickTime == 0 && currentUnixTime - LastHoTTickTimestamp >= tickTime && HoTsTicked < HoTTicks)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                    HoTsTicked++;
                }
            }
            else if (Sot1)
            {
                var spell = new Spell(SpellId.RevitalizeSelf1);
                var castTime = HoTTimestamp;
                var duration = HoTDuration;
                int timePast = (int)(currentUnixTime - castTime);
                int tickTime = HoTDuration / HoTTicks;

                if (currentUnixTime - castTime >= duration)
                {
                    Sot1 = false;
                    IsHoTTicking = false;
                    HoTsTicked = 0;
                    return;
                }
                else if (currentUnixTime - castTime < duration && HoTTicks > 0 && timePast % tickTime == 0 && currentUnixTime - LastHoTTickTimestamp >= tickTime && HoTsTicked < HoTTicks)
                {
                    IsHoTTicking = true;
                    CreatePlayerSpell(this, spell, false);
                    LastHoTTickTimestamp = currentUnixTime;
                    HoTsTicked++;
                }
            }
        }
    }
}
