using System;
using System.Collections.Generic;
using System.Numerics;
using ACE.Common;
using ACE.Entity.Enum;
using ACE.Server.Entity.Actions;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Physics.Animation;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        private float _accuracyLevel;

        public float AccuracyLevel
        {
            get => IsExhausted ? 0.0f : _accuracyLevel;
            set => _accuracyLevel = value;
        }

        public Creature MissileTarget;

        public PowerAccuracy GetAccuracyRange()
        {
            if (AccuracyLevel < 0.33f)
                return PowerAccuracy.Low;
            else if (AccuracyLevel < 0.66f)
                return PowerAccuracy.Medium;
            else
                return PowerAccuracy.High;
        }

        /// <summary>
        /// Called by network packet handler 0xA - GameActionTargetedMissileAttack
        /// </summary>
        /// <param name="targetGuid">The target guid</param>
        /// <param name="attackHeight">The attack height 1-3</param>
        /// <param name="accuracyLevel">The 0-1 accuracy bar level</param>
        public void HandleActionTargetedMissileAttack(uint targetGuid, uint attackHeight, float accuracyLevel)
        {
            //log.Info($"-");

            if (CombatMode != CombatMode.Missile)
            {
                log.Error($"{Name}.HandleActionTargetedMissileAttack({targetGuid:X8}, {attackHeight}, {accuracyLevel}) - CombatMode mismatch {CombatMode}, LastCombatMode: {LastCombatMode}");

                if (LastCombatMode == CombatMode.Missile)
                    CombatMode = CombatMode.Missile;
                else
                {
                    OnAttackDone();
                    return;
                }
            }

            if (IsBusy || Teleporting || suicideInProgress)
            {
                SendWeenieError(WeenieError.YoureTooBusy);
                OnAttackDone();
                return;
            }

            if (IsJumping)
            {
                SendWeenieError(WeenieError.YouCantDoThatWhileInTheAir);
                OnAttackDone();
                return;
            }

            if (PKLogout)
            {
                SendWeenieError(WeenieError.YouHaveBeenInPKBattleTooRecently);
                OnAttackDone();
                return;
            }

            var weapon = GetEquippedMissileWeapon();
            var ammo = GetEquippedAmmo();

            // sanity check
            accuracyLevel = Math.Clamp(accuracyLevel, 0.0f, 1.0f);

            if (weapon == null || weapon.IsAmmoLauncher && ammo == null)
            {
                OnAttackDone();
                return;
            }

            AttackHeight = (AttackHeight)attackHeight;
            AttackQueue.Add(accuracyLevel);

            if (MissileTarget == null)
                AccuracyLevel = accuracyLevel;  // verify

            // get world object of target guid
            var target = CurrentLandblock?.GetObject(targetGuid) as Creature;
            if (target == null || target.Teleporting)
            {
                //log.Warn($"{Name}.HandleActionTargetedMissileAttack({targetGuid:X8}, {AttackHeight}, {accuracyLevel}) - couldn't find creature target guid");
                OnAttackDone();
                return;
            }

            if (Attacking || MissileTarget != null && MissileTarget.IsAlive)
                return;

            if (!CanDamage(target))
            {
                SendTransientError($"You cannot attack {target.Name}");
                OnAttackDone();
                return;
            }

            //log.Info($"{Name}.HandleActionTargetedMissileAttack({targetGuid:X8}, {attackHeight}, {accuracyLevel})");

            AttackTarget = target;
            MissileTarget = target;

            var attackSequence = ++AttackSequence;

            // record stance here and pass it along
            // accounts for odd client behavior with swapping bows during repeat attacks
            var stance = CurrentMotionState.Stance;

            // turn if required
            var rotateTime = Rotate(target);
            var actionChain = new ActionChain();

            var delayTime = rotateTime;
            if (NextRefillTime > DateTime.UtcNow.AddSeconds(delayTime))
                delayTime = (float)(NextRefillTime - DateTime.UtcNow).TotalSeconds;

            actionChain.AddDelaySeconds(delayTime);

            // do missile attack
            actionChain.AddAction(this, () => LaunchMissile(target, attackSequence, stance));
            actionChain.EnqueueChain();
        }

        /// <summary>
        /// Launches a missile attack from player to target
        /// </summary>
        public void LaunchMissile(WorldObject target, int attackSequence, MotionStance stance, bool subsequent = false)
        {
            if (AttackSequence != attackSequence)
                return;

            var weapon = GetEquippedMissileWeapon();
            if (weapon == null || CombatMode == CombatMode.NonCombat)
            {
                OnAttackDone();
                return;
            }

            var ammo = weapon.IsAmmoLauncher ? GetEquippedAmmo() : weapon;
            if (ammo == null)
            {
                OnAttackDone();
                return;
            }

            var launcher = GetEquippedMissileLauncher();

            var creature = target as Creature;
            if (!IsAlive || IsBusy || MissileTarget == null || creature == null || !creature.IsAlive || suicideInProgress)
            {
                OnAttackDone();
                return;
            }

            if (!TargetInRange(target))
            {
                // this must also be sent to actually display the transient message
                SendWeenieError(WeenieError.MissileOutOfRange);

                // this prevents the accuracy bar from refilling when 'repeat attacks' is enabled
                OnAttackDone();

                return;
            }

            var actionChain = new ActionChain();

            if (subsequent && !IsFacing(target))
            {
                var rotateTime = Rotate(target);
                actionChain.AddDelaySeconds(rotateTime);
            }

            // launch animation
            // point of no return beyond this point -- cannot be cancelled
            actionChain.AddAction(this, () => Attacking = true);

            if (subsequent)
            {
                // client shows hourglass, until attack done is received
                // retail only did this for subsequent attacks w/ repeat attacks on
                Session.Network.EnqueueSend(new GameEventCombatCommenceAttack(Session));
            }

            var projectileSpeed = GetProjectileSpeed();

            // get z-angle for aim motion
            var aimVelocity = GetAimVelocity(target, projectileSpeed);

            var aimLevel = GetAimLevel(aimVelocity);

            // calculate projectile spawn pos and velocity
            var localOrigin = GetProjectileSpawnOrigin(ammo.WeenieClassId, aimLevel);

            var velocity = CalculateProjectileVelocity(localOrigin, target, projectileSpeed, out Vector3 origin, out Quaternion orientation);

            //Console.WriteLine($"Velocity: {velocity}");

            if (velocity == Vector3.Zero)
            {
                // pre-check succeeded, but actual velocity calculation failed
                SendWeenieError(WeenieError.MissileOutOfRange);

                // this prevents the accuracy bar from refilling when 'repeat attacks' is enabled
                Attacking = false;
                OnAttackDone();
                return;
            }

            var launchTime = EnqueueMotionPersist(actionChain, aimLevel);

            // launch projectile
            actionChain.AddAction(this, () =>
            {
                // handle self-procs
                TryProcEquippedItems(this, this, true, weapon);

                var sound = GetLaunchMissileSound(weapon);
                EnqueueBroadcast(new GameMessageSound(Guid, sound, 1.0f));

                // stamina usage
                // TODO: ensure enough stamina for attack
                // TODO: verify formulas - double/triple cost for bow/xbow?
                var staminaCost = GetAttackStamina(GetAccuracyRange());
                UpdateVitalDelta(Stamina, -staminaCost);

                var projectile = LaunchProjectile(launcher, ammo, target, origin, orientation, velocity);
                UpdateAmmoAfterLaunch(ammo);

                if (weapon != null && weapon.IsCleaving)
                {
                    var cleave = GetMissileCleaveTarget(creature, weapon);

                    foreach (var cleaveHit in cleave)
                    {
                        // target procs don't happen for cleaving
                        /*DamageTarget(cleaveHit, weapon);*/
                        /*LaunchCleaveMissile(cleaveHit, attackSequence, stance, subsequent = false);*/
                        var projectileSpeed = GetProjectileSpeed();
                        var aimVelocity = GetAimVelocity(cleaveHit, projectileSpeed);
                        var localOrigin = GetProjectileSpawnOrigin(ammo.WeenieClassId, aimLevel);
                        var velocity = CalculateProjectileVelocity(localOrigin, cleaveHit, projectileSpeed, out Vector3 origin, out Quaternion orientation);

                        LaunchProjectile(launcher, ammo, cleaveHit, origin, orientation, velocity);
                        UpdateAmmoAfterLaunch(ammo);
                    }
                }

                if (IsDps)
                {
                    var paRoll = ThreadSafeRandom.Next(0.0f, 1.0f);

                    if (PowerAttackChance >= paRoll)
                    {
                        IsDamageBuffed = true;
                    }
                }

                if (IsDps || !IsDps && DoMissileAoE)
                {
                    // AoE Missile Attack
                    var aoeRoll = ThreadSafeRandom.Next(0.0f, 1.0f);

                    var aoeTarget = target as Creature;

                    if (aoeRoll <= MissileAoEChance && aoeTarget != null)
                    {
                        foreach (var m in GetMissileAoETarget(aoeTarget, weapon))
                        {
                            var projectileSpeed = GetProjectileSpeed();
                            var aimVelocity = GetAimVelocity(m, projectileSpeed);
                            var localOrigin = GetProjectileSpawnOrigin(ammo.WeenieClassId, aimLevel);
                            var velocity = CalculateProjectileVelocity(localOrigin, m, projectileSpeed, out Vector3 origin, out Quaternion orientation);

                            LaunchProjectile(launcher, ammo, m, origin, orientation, velocity);
                        }
                    }

                    DoMissileAoE = false;
                }  
            });       

            // ammo remaining?
            if (!ammo.UnlimitedUse && (ammo.StackSize == null || ammo.StackSize <= 1))
            {
                actionChain.AddAction(this, () =>
                {
                    Session.Network.EnqueueSend(new GameEventCommunicationTransientString(Session, "You are out of ammunition!"));
                    SetCombatMode(CombatMode.NonCombat);
                    Attacking = false;
                    OnAttackDone();
                });

                actionChain.EnqueueChain();
                return;
            }

            // reload animation
            var animSpeed = GetAnimSpeed();
            var reloadTime = EnqueueMotionPersist(actionChain, stance, MotionCommand.Reload, animSpeed);

            // reset for next projectile
            EnqueueMotionPersist(actionChain, stance, MotionCommand.Ready);
            var linkTime = MotionTable.GetAnimationLength(MotionTableId, stance, MotionCommand.Reload, MotionCommand.Ready);
            //var cycleTime = MotionTable.GetCycleLength(MotionTableId, CurrentMotionState.Stance, MotionCommand.Ready);

            actionChain.AddAction(this, () =>
            {
                if (CombatMode == CombatMode.Missile)
                    EnqueueBroadcast(new GameMessageParentEvent(this, ammo, ACE.Entity.Enum.ParentLocation.RightHand, ACE.Entity.Enum.Placement.RightHandCombat));
            }); 

            actionChain.AddDelaySeconds(linkTime);

            actionChain.AddAction(this, () =>
            {
                Attacking = false;

                if (creature.IsAlive && GetCharacterOption(CharacterOption.AutoRepeatAttacks) && !IsBusy && !AttackCancelled)
                {
                    // client starts refilling accuracy bar
                    Session.Network.EnqueueSend(new GameEventAttackDone(Session));

                    AccuracyLevel = AttackQueue.Fetch();

                    // can be cancelled, but cannot be pre-empted with another attack
                    var nextAttack = new ActionChain();
                    var nextRefillTime = AccuracyLevel;

                    NextRefillTime = DateTime.UtcNow.AddSeconds(nextRefillTime);
                    nextAttack.AddDelaySeconds(nextRefillTime);

                    // perform next attack
                    nextAttack.AddAction(this, () => { LaunchMissile(target, attackSequence, stance, true); });
                    nextAttack.EnqueueChain();

                    if (DoBrutalizeAttack)
                    {
                        var currentUnixTime = Time.GetUnixTime();
                        DoBrutalizeAttack = false;
                        LastBrutalizeTimestamp = currentUnixTime;
                        PlayParticleEffect(PlayScript.EnchantDownRed, Guid);
                    }
                }
                else
                {
                    if (DoBrutalizeAttack)
                    {
                        var currentUnixTime = Time.GetUnixTime();
                        DoBrutalizeAttack = false;
                        LastBrutalizeTimestamp = currentUnixTime;
                        PlayParticleEffect(PlayScript.EnchantDownRed, Guid);
                    }

                    OnAttackDone();
                }
            });

            actionChain.EnqueueChain();

            if (UnderLifestoneProtection)
                LifestoneProtectionDispel();
        }

        public void LaunchCleaveMissile(WorldObject target, int attackSequence, MotionStance stance, bool subsequent = false)
        {
            // cleaving skips original target
            if (AttackSequence != attackSequence)
                return;

            var weapon = GetEquippedMissileWeapon();
            if (weapon == null || CombatMode == CombatMode.NonCombat)
            {
                OnAttackDone();
                return;
            }

            var ammo = weapon.IsAmmoLauncher ? GetEquippedAmmo() : weapon;
            if (ammo == null)
            {
                OnAttackDone();
                return;
            }

            var launcher = GetEquippedMissileLauncher();

            var creature = target as Creature;
            if (!IsAlive || IsBusy || MissileTarget == null || creature == null || !creature.IsAlive || suicideInProgress)
            {
                OnAttackDone();
                return;
            }

            if (!TargetInRange(target))
            {
                // this must also be sent to actually display the transient message
                SendWeenieError(WeenieError.MissileOutOfRange);

                // this prevents the accuracy bar from refilling when 'repeat attacks' is enabled
                OnAttackDone();

                return;
            }

            var actionChain = new ActionChain();

            if (subsequent && !IsFacing(target))
            {
                var rotateTime = Rotate(target);
                actionChain.AddDelaySeconds(rotateTime);
            }

            // launch animation
            // point of no return beyond this point -- cannot be cancelled
            actionChain.AddAction(this, () => Attacking = true);

            if (subsequent)
            {
                // client shows hourglass, until attack done is received
                // retail only did this for subsequent attacks w/ repeat attacks on
                Session.Network.EnqueueSend(new GameEventCombatCommenceAttack(Session));
            }

            var projectileSpeed = GetProjectileSpeed();

            // get z-angle for aim motion
            var aimVelocity = GetAimVelocity(target, projectileSpeed);

            var aimLevel = GetAimLevel(aimVelocity);

            // calculate projectile spawn pos and velocity
            var localOrigin = GetProjectileSpawnOrigin(ammo.WeenieClassId, aimLevel);

            var velocity = CalculateProjectileVelocity(localOrigin, target, projectileSpeed, out Vector3 origin, out Quaternion orientation);

            //Console.WriteLine($"Velocity: {velocity}");

            if (velocity == Vector3.Zero)
            {
                // pre-check succeeded, but actual velocity calculation failed
                SendWeenieError(WeenieError.MissileOutOfRange);

                // this prevents the accuracy bar from refilling when 'repeat attacks' is enabled
                Attacking = false;
                OnAttackDone();
                return;
            }

            var launchTime = EnqueueMotionPersist(actionChain, aimLevel);

            // launch projectile
            actionChain.AddAction(this, () =>
            {
                // handle self-procs
                TryProcEquippedItems(this, this, true, weapon);

                var sound = GetLaunchMissileSound(weapon);
                EnqueueBroadcast(new GameMessageSound(Guid, sound, 1.0f));

                // stamina usage
                // TODO: ensure enough stamina for attack
                // TODO: verify formulas - double/triple cost for bow/xbow?
                var staminaCost = GetAttackStamina(GetAccuracyRange());
                UpdateVitalDelta(Stamina, -staminaCost);

                var projectile = LaunchProjectile(launcher, ammo, target, origin, orientation, velocity);
                UpdateAmmoAfterLaunch(ammo);
            });

            actionChain.EnqueueChain();

            if (UnderLifestoneProtection)
                LifestoneProtectionDispel();
        }

        // TODO: the damage pipeline currently uses the creature ammo instead of the projectile
        // for calculating damage. when the last arrow is launched, the player ammo will be null
        // give projectiles an owner, and have the damage pipeline take the actual damage source object
        // (ie. the arrow-in-flight, or a melee weapon)

        public override float GetAimHeight(WorldObject target)
        {
            switch (AttackHeight.Value)
            {
                case ACE.Entity.Enum.AttackHeight.High: return 1.0f;
                case ACE.Entity.Enum.AttackHeight.Medium: return 2.0f;
                //case AttackHeight.Low: return target.Height;
                case ACE.Entity.Enum.AttackHeight.Low: return 3.0f;
            }
            return 2.0f;
        }

        public override void UpdateAmmoAfterLaunch(WorldObject ammo)
        {
            //if (ammo.UnlimitedUse)
            //    return;

            // hide previously held ammo
            EnqueueBroadcast(new GameMessagePickupEvent(ammo));

            if (ammo.UnlimitedUse)
                return;

            if (ammo.StackSize == null || ammo.StackSize <= 1)
                TryDequipObjectWithNetworking(ammo.Guid, out _, DequipObjectAction.ConsumeItem);
            else
                TryConsumeFromInventoryWithNetworking(ammo, 1);
        }

        public bool TargetInRange(WorldObject target)
        {
            // 2d or 3d distance?
            var dist = Location.DistanceTo(target.Location);

            var maxRange = GetMaxMissileRange();

            return dist <= maxRange;
        }
        public List<Creature> GetMissileCleaveTarget(Creature target, WorldObject weapon)
        {
            var player = this as Player;

            if (!weapon.IsCleaving) return null;

            // sort visible objects by ascending distance
            var visible = PhysicsObj.ObjMaint.GetVisibleObjectsValuesWhere(o => o.WeenieObj.WorldObject != null);
            visible.Sort(DistanceComparator);

            var cleaveTargets = new List<Creature>();
            var totalCleaves = weapon.CleaveTargets;

            foreach (var obj in visible)
            {
                // cleaving skips original target
                if (obj.ID == target.PhysicsObj.ID || target == null)
                    continue;

                // only cleave creatures
                var creature = obj.WeenieObj.WorldObject as Creature;
                if (creature == null || creature.Teleporting || creature.IsDead) continue;

                if (player != null && player.CheckPKStatusVsTarget(creature, null) != null)
                    continue;

                if (!creature.Attackable && creature.TargetingTactic == TargetingTactic.None || creature.Teleporting)
                    continue;

                if (creature is CombatPet && (player != null || this is CombatPet))
                    continue;

                // no objects in cleave range
                var cylDist = GetCylinderDistance(creature);
                if (cylDist > MissileCleaveCylRange)
                    return cleaveTargets;

                // only cleave in front of attacker
                var angle = GetAngle(creature);
                if (Math.Abs(angle) > MissileCleaveAngle / 2.0f)
                    continue;

                // found cleavable object
                cleaveTargets.Add(creature);
                if (cleaveTargets.Count == totalCleaves)
                    break;
            }
            return cleaveTargets;
        }

        public List<Creature> GetMissileAoETarget(Creature target, WorldObject weapon)
        {
            var player = this as Player;

            // sort visible objects by ascending distance
            var visible = PhysicsObj.ObjMaint.GetVisibleObjectsValuesWhere(o => o.WeenieObj.WorldObject != null);
            visible.Sort(DistanceComparator);

            var cleaveTargets = new List<Creature>();
            var totalCleaves = weapon.CleaveTargets;

            foreach (var obj in visible)
            {
                // only cleave creatures
                var creature = obj.WeenieObj.WorldObject as Creature;
                if (creature == null || creature.Teleporting || creature.IsDead) continue;

                if (player != null && player.CheckPKStatusVsTarget(creature, null) != null)
                    continue;

                if (!creature.Attackable && creature.TargetingTactic == TargetingTactic.None || creature.Teleporting)
                    continue;

                if (creature is CombatPet && (player != null || this is CombatPet))
                    continue;

                // no objects in cleave range
                var cylDist = GetCylinderDistance(creature);
                if (cylDist > MissileAoECylRange)
                    return cleaveTargets;

                // only cleave in front of attacker
                var angle = GetAngle(creature);
                if (Math.Abs(angle) > MissileAoEAngle)
                    continue;

                // found cleavable object
                cleaveTargets.Add(creature);
                if (cleaveTargets.Count == totalCleaves)
                    break;
            }
            return cleaveTargets;
        }
    }
}
