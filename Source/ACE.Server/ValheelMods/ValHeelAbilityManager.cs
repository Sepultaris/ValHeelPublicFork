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
using System;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        /// <summary>
        /// This calls each ability based on the ability item used.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="abilityItem"></param>
        public void DoAbility(Player player, WorldObject abilityItem)
        {
            if (abilityItem == null || player == null)
                return;

            if (abilityItem.WeenieClassId == 802941)
                player.IsDamageBuffed = true;

            if (abilityItem.WeenieClassId == 802942)
                player.Brutalize = true;

            if (abilityItem.WeenieClassId == 802943)
                player.IsTankBuffed = true;

            if (abilityItem.WeenieClassId == 802944)
                player.IsHoTCasting = true;

            if (abilityItem.WeenieClassId == 802945)
                player.IsSoTCasting = true;

            if (abilityItem.WeenieClassId == 802946)
                player.LifeWell = true;
        }

        /// <summary>
        /// This is the main ability manager for Valheel's ability system.
        /// </summary>
        /// <param name="player"></param>
        public void ValHeelAbilityManager(Player player)
        {
            var currentUnixTime = Time.GetUnixTime();

            player.HoTBuffHandler(currentUnixTime); // HoTs 1-8
            player.SoTBuffHandler(currentUnixTime); // SoTs 1-8
            player.DefenseRatingBuffHandler(player, currentUnixTime); // Bastion
            player.DamageRatingBuffHandler(player, currentUnixTime); // Power Attack
            player.BrutalizeHandler(player, currentUnixTime); // Brutalize
            player.LifeWellHandler(player, currentUnixTime); // Life Well
            player.StealthHandler(player, currentUnixTime); // Stealth
            player.Taunting(player, currentUnixTime); // Taunt
            player.HoTCastHandler(player, currentUnixTime); // HoT Cast
            player.SoTCastHandler(player, currentUnixTime); // SoT Cast
            player.MissileAoEHandler(player, currentUnixTime); // Missile AoE
        }

        /// <summary>
        /// This checks to see if the player has an active AoE timer.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="currentUnixTime"></param>
        public void MissileAoEHandler(Player player, double currentUnixTime)
        {
            if (MissileAoE == true && currentUnixTime - LastMissileAoETimestamp < 30)
            {
                MissileAoE = false;
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You can't use this ability yet.", ChatMessageType.Broadcast));
            }

            if (MissileAoE == true && currentUnixTime - LastMissileAoETimestamp >= 30)
            {
                MissileAoE = false;
                DoMissileAoE = true;
                LastMissileAoETimestamp = currentUnixTime;
            }

            if (DoMissileAoE == true && currentUnixTime - LastMissileAoETimestamp >= 10)
            {
                DoMissileAoE = false;
            }
        }

        /// <summary>
        /// This checks to see if the caster has an active HoT spell timer.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="currentUnixTime"></param>
        public void HoTCastHandler (Player player, double currentUnixTime)
        {
            if (player.IsHoTCasting == true && currentUnixTime - player.LastHoTCastTimestamp < 30)
            {
                player.IsHoTCasting = false;
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You can't use this ability yet.", ChatMessageType.Broadcast));
            }

            if (player.IsHoTCasting == true && currentUnixTime - player.LastHoTCastTimestamp >= 30)
            {
                player.IsHoTCasting = false;
                player.LastHoTCastTimestamp = currentUnixTime;

                var spellLevel = GetHoTLevel(player);

                LifeMagicHot(player, spellLevel);
            }
        }

        /// <summary>
        /// This checks to see if the caster has an active SoT spell timer.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="currentUnixTime"></param>
        public void SoTCastHandler(Player player, double currentUnixTime)
        {
            if (player.IsSoTCasting == true && currentUnixTime - player.LastHoTCastTimestamp < 30)
            {
                player.IsSoTCasting = false;
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You can't use this ability yet.", ChatMessageType.Broadcast));
            }

            if (player.IsSoTCasting == true && currentUnixTime - player.LastHoTCastTimestamp >= 30)
            {
                player.IsSoTCasting = false;
                player.LastHoTCastTimestamp = currentUnixTime;

                var spellLevel = GetHoTLevel(player);

                LifeMagicSot(player, spellLevel);
            }
        }

        /// <summary>
        /// This returns the HoT spell level based on the player's Life Magic skill.
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public int GetHoTLevel(Player player)
        {
            int hotLevel = (int)player.GetCreatureSkill(Skill.LifeMagic).Base / 10;

            if (hotLevel <= 0)
                hotLevel = 1;
            if (hotLevel > 8)
                hotLevel = 8;

            return hotLevel;
        }

        /// <summary>
        /// This is the HoT spell
        /// </summary>
        /// <param name="caster"></param>
        /// <param name="spell"></param>
        public void LifeMagicHot(Player caster, int spell)
        {
            // This sets the HoT flag on the target
            var fellows = caster.GetFellowshipTargets();
            double currentUnixTime = (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

            if (fellows != null)
            {
                foreach (var fellow in fellows)
                {
                    if (caster.GetDistance(fellow) < 30.0f)
                    {
                        if (spell == 8)
                        {
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyBool.Hot8, true);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyFloat.HoTTimestamp, currentUnixTime);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTDuration, MaxHoTDuration);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTTicks, MaxHoTTicks);
                        }
                        else if (spell == 7)
                        {
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyBool.Hot7, true);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyFloat.HoTTimestamp, currentUnixTime);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTDuration, MaxHoTDuration);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTTicks, MaxHoTTicks);
                        }
                        else if (spell == 6)
                        {
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyBool.Hot6, true);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyFloat.HoTTimestamp, currentUnixTime);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTDuration, MaxHoTDuration);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTTicks, MaxHoTTicks);
                        }
                        else if (spell == 5)
                        {
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyBool.Hot5, true);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyFloat.HoTTimestamp, currentUnixTime);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTDuration, MaxHoTDuration);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTTicks, MaxHoTTicks);
                        }
                        else if (spell == 4)
                        {
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyBool.Hot4, true);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyFloat.HoTTimestamp, currentUnixTime);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTDuration, MaxHoTDuration);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTTicks, MaxHoTTicks);
                        }
                        else if (spell == 3)
                        {
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyBool.Hot3, true);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyFloat.HoTTimestamp, currentUnixTime);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTDuration, MaxHoTDuration);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTTicks, MaxHoTTicks);
                        }
                        else if (spell == 2)
                        {
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyBool.Hot2, true);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyFloat.HoTTimestamp, currentUnixTime);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTDuration, MaxHoTDuration);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTTicks, MaxHoTTicks);
                        }
                        else if (spell == 1)
                        {
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyBool.Hot1, true);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyFloat.HoTTimestamp, currentUnixTime);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTDuration, MaxHoTDuration);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTTicks, MaxHoTTicks);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// This is the Life Magic Sot spell.
        /// </summary>
        /// <param name="caster"></param>
        /// <param name="spell"></param>
        public void LifeMagicSot(Player caster, int spell)
        {
            // This sets the HoT flag on the target
            var fellows = caster.GetFellowshipTargets();
            double currentUnixTime = (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

            if (fellows != null)
            {
                foreach (var fellow in fellows)
                {
                    if (caster.GetDistance(fellow) < 30.0f)
                    {
                        if (spell == 8)
                        {
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyBool.Sot8, true);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyFloat.HoTTimestamp, currentUnixTime);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTDuration, MaxHoTDuration);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTTicks, MaxHoTTicks);
                        }
                        else if (spell == 7)
                        {
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyBool.Sot7, true);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyFloat.HoTTimestamp, currentUnixTime);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTDuration, MaxHoTDuration);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTTicks, MaxHoTTicks);
                        }
                        else if (spell == 6)
                        {
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyBool.Sot6, true);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyFloat.HoTTimestamp, currentUnixTime);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTDuration, MaxHoTDuration);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTTicks, MaxHoTTicks);
                        }
                        else if (spell == 5)
                        {
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyBool.Sot5, true);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyFloat.HoTTimestamp, currentUnixTime);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTDuration, MaxHoTDuration);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTTicks, MaxHoTTicks);
                        }
                        else if (spell == 4)
                        {
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyBool.Sot4, true);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyFloat.HoTTimestamp, currentUnixTime);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTDuration, MaxHoTDuration);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTTicks, MaxHoTTicks);
                        }
                        else if (spell == 3)
                        {
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyBool.Sot3, true);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyFloat.HoTTimestamp, currentUnixTime);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTDuration, MaxHoTDuration);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTTicks, MaxHoTTicks);
                        }
                        else if (spell == 2)
                        {
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyBool.Sot2, true);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyFloat.HoTTimestamp, currentUnixTime);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTDuration, MaxHoTDuration);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTTicks, MaxHoTTicks);
                        }
                        else if (spell == 1)
                        {
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyBool.Sot1, true);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyFloat.HoTTimestamp, currentUnixTime);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTDuration, MaxHoTDuration);
                            fellow.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.HoTTicks, MaxHoTTicks);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// This is the taunt ability.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="currentUnixtime"></param>
        public void Taunting(Player player, double currentUnixtime)
        {
            if (player.IsTaunting && TauntTimerActive == true && currentUnixtime - player.LastTauntTimestamp < 30)
            {
                player.IsTaunting = false;
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You can't use this ability yet.", ChatMessageType.Broadcast));
            }

            if (player.IsTaunting == true && TauntTimerActive == false && currentUnixtime - player.LastTauntTimestamp >= 30)
            {
                var visibleCreatures = PhysicsObj.ObjMaint.GetVisibleObjectsValuesOfTypeCreature();

                foreach (var m in visibleCreatures)
                {
                    if (m == null)
                        continue;

                    if (!m.IsMonster)
                        visibleCreatures.Remove(m);

                    if (player.GetCylinderDistance(m) < 10)
                    {
                        m.AttackTarget = player;
                        m.PlayParticleEffect(PlayScript.EnchantUpRed, m.Guid);
                    }
                }
                player.LastTauntTimestamp = currentUnixtime;
                player.TauntTimerActive = true;
                player.IsTaunting = false;
            }
            if (player.TauntTimerActive == true && currentUnixtime - player.LastTauntTimestamp >= 10)
            {
                player.IsTaunting = false;
                player.TauntTimerActive = false;
            }
        }

        /// <summary>
        /// This method handles the stealth ability.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="currentUnixTime"></param>
        public void StealthHandler(Player player, double currentUnixTime)
        {
            if (player.Stealth == true && currentUnixTime - player.LastSneakTimestamp > 30)
            {
                player.Stealth = false;
                player.IsSneaking = true;
                player.LastSneakTimestamp = currentUnixTime;
                player.HandleSneak();
                player.SetProperty(PropertyInt.CloakStatus, (int)CloakStatus.On);
                player.PlayParticleEffect(PlayScript.EnchantDownGreen, Guid);
            }
            if (player.IsSneaking == true && currentUnixTime - player.LastSneakTimestamp > 10)
            {
                player.IsSneaking = false;
                player.UnSneak();
                player.SetProperty(PropertyInt.CloakStatus, (int)CloakStatus.Off);
                player.PlayParticleEffect(PlayScript.EnchantUpGreen, Guid);
            }
            if (player.Stealth == true && currentUnixTime - player.LastSneakTimestamp < 30)
            {   
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You can't use Sneak yet.", ChatMessageType.Broadcast));
                player.Stealth = false;
            }
        }

        /// <summary>
        /// This method handles the LifeWell ability.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="currentUnixtime"></param>
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
            int defenseRatingMultiplier = (int)PropertyManager.GetLong("ba_defense_multiplier").Item;
            int playerDefenseRating = player.LumAugDamageReductionRating;
            int ratingIncreaseAmount = playerDefenseRating * defenseRatingMultiplier;
            int finalRatingAmount = ratingIncreaseAmount;

            if (playerDefenseRating == 0)
            {
                player.SetProperty(PropertyInt.LumAugDamageReductionRating, 1);
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageReductionRating, 1));
            }
            if (currentUnixTime - LastTankBuffTimestamp > 30 && IsTankBuffed && GetEquippedShield() == null)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat("You must have a shield equipped to use this ability.", ChatMessageType.Broadcast));
                IsTankBuffed = false;
                return;
            }
            if (currentUnixTime - LastTankBuffTimestamp > 30 && IsTankBuffed && GetEquippedShield() != null)
            {
                player.TankDefenseRatingIncrease = ratingIncreaseAmount;
                player.LumAugDamageReductionRating = finalRatingAmount;
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageReductionRating, finalRatingAmount));
                LastTankBuffTimestamp = currentUnixTime;
                player.PlayParticleEffect(PlayScript.ShieldUpGrey, Guid);
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You've activated Bastion, increaing your damage reduction rating for 10 seconds.", ChatMessageType.Broadcast));
                TankBuffedTimer = true;
                IsTankBuffed = false;
            }
            if (currentUnixTime - LastTankBuffTimestamp >= 10 && TankBuffedTimer == true)
            {
                var returnValue = playerDefenseRating / defenseRatingMultiplier;

                player.LumAugDamageReductionRating = returnValue;
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageReductionRating, returnValue));
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Bastion has ended.", ChatMessageType.Broadcast));
                player.PlayParticleEffect(PlayScript.ShieldDownGrey, Guid);
                TankBuffedTimer = false;
            }
            if (currentUnixTime - LastTankBuffTimestamp <= 29 && IsTankBuffed == true)
            {
                IsTankBuffed = false;
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You can't use Bastion yet.", ChatMessageType.Broadcast));
            }
        }

        /// <summary>
        /// This is the Damage Buff handler. It will check if the spell is active and if so, it will increase the players damage rating by 6x the amount of their current damage rating.
        /// </summary>
        public void DamageRatingBuffHandler(Player player, double currentUnixTime)
        {
            int paDamageMultiplier = (int)PropertyManager.GetLong("pa_damage_multiplier").Item;
            int playerDamageRating = player.LumAugDamageRating;

            if (playerDamageRating == 0)
            {
                player.SetProperty(PropertyInt.LumAugDamageRating, 1);
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageRating, 1));
            }

            int ratingIncreaseAmount = playerDamageRating * paDamageMultiplier;
            int finalRatingAmount = ratingIncreaseAmount;

            if (currentUnixTime - LastDamageBuffTimestamp > 30 && IsDamageBuffed)
            {
                player.DamageRatingIncrease = ratingIncreaseAmount;
                player.LumAugDamageRating = finalRatingAmount;
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageRating, finalRatingAmount));
                LastDamageBuffTimestamp = currentUnixTime;
                player.PlayParticleEffect(PlayScript.ShieldUpRed, Guid);
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You've activated Power Attack, increaing your damage rating for 10 seconds.", ChatMessageType.Broadcast));
                DamageBuffedTimer = true;
                IsDamageBuffed = false;
            }
            if (currentUnixTime - LastDamageBuffTimestamp >= 10 && DamageBuffedTimer == true)
            {
                var returnValue = playerDamageRating / paDamageMultiplier;

                player.LumAugDamageRating = returnValue;
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageRating, returnValue));
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Power Attack has ended.", ChatMessageType.Broadcast));
                player.PlayParticleEffect(PlayScript.ShieldDownRed, Guid);
                DamageBuffedTimer = false;
            }
            if (currentUnixTime - LastDamageBuffTimestamp <= 29 && IsDamageBuffed == true)
            {
                IsDamageBuffed = false;
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You can't use Power Attack yet.", ChatMessageType.Broadcast));
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

        /// <summary>
        /// This is the handler for the SoT spell. It will check if the spell is active and if so, it will cast the spell every 3 seconds for the duration of the spell.
        /// </summary>
        /// <param name="currentUnixTime"></param>
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
