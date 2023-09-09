using System;
using System.Collections.Generic;

using ACE.DatLoader.Entity.AnimationHooks;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Physics;
using ACE.Server.Physics.Animation;
using ACE.Entity.Enum.Properties;
using System.Numerics;
using ACE.Server.Factories;
using ACE.Common;
using ACE.Entity;
using ACE.Entity.Models;
using ACE.Database;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Player melee attack
    /// </summary>
    partial class Player
    {
        /// <summary>
        /// The target this player is currently performing a melee attack on
        /// </summary>
        public Creature MeleeTarget;

        private float _powerLevel;

        /// <summary>
        /// The power bar level, a value between 0-1
        /// </summary>
        public float PowerLevel
        {
            get => IsExhausted ? 0.0f : _powerLevel;
            set => _powerLevel = value;
        }

        public override PowerAccuracy GetPowerRange()
        {
            if (PowerLevel < 0.33f)
                return PowerAccuracy.Low;
            else if (PowerLevel < 0.66f)
                return PowerAccuracy.Medium;
            else
                return PowerAccuracy.High;
        }

        public AttackQueue AttackQueue;

        /// <summary>
        /// Called when a player first initiates a melee attack
        /// </summary>
        public void HandleActionTargetedMeleeAttack(uint targetGuid, uint attackHeight, float powerLevel)
        {
            //log.Info($"-");

            if (CombatMode != CombatMode.Melee)
            {
                log.Error($"{Name}.HandleActionTargetedMeleeAttack({targetGuid:X8}, {attackHeight}, {powerLevel}) - CombatMode mismatch {CombatMode}, LastCombatMode {LastCombatMode}");

                if (LastCombatMode == CombatMode.Melee)
                    CombatMode = CombatMode.Melee;
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

            // verify input
            powerLevel = Math.Clamp(powerLevel, 0.0f, 1.0f);

            AttackHeight = (AttackHeight)attackHeight;
            AttackQueue.Add(powerLevel);

            if (MeleeTarget == null)
                PowerLevel = AttackQueue.Fetch();

            // already in melee loop?
            if (Attacking || MeleeTarget != null && MeleeTarget.IsAlive)
                return;

            // get world object for target creature
            var target = CurrentLandblock?.GetObject(targetGuid);

            if (target == null)
            {
                //log.Debug($"{Name}.HandleActionTargetedMeleeAttack({targetGuid:X8}, {AttackHeight}, {powerLevel}) - couldn't find target guid");
                OnAttackDone();
                return;
            }

            var creatureTarget = target as Creature;
            if (creatureTarget == null)
            {
                log.Warn($"{Name}.HandleActionTargetedMeleeAttack({targetGuid:X8}, {AttackHeight}, {powerLevel}) - target guid not creature");
                OnAttackDone();
                return;
            }

            if (!CanDamage(creatureTarget))
            {
                SendTransientError($"You cannot attack {creatureTarget.Name}");
                OnAttackDone();
                return;
            }

            if (!creatureTarget.IsAlive)
            {
                OnAttackDone();
                return;
            }

            //log.Info($"{Name}.HandleActionTargetedMeleeAttack({targetGuid:X8}, {attackHeight}, {powerLevel})");

            MeleeTarget = creatureTarget;
            AttackTarget = MeleeTarget;

            // reset PrevMotionCommand / DualWieldAlternate each time button is clicked
            PrevMotionCommand = MotionCommand.Invalid;
            DualWieldAlternate = false;

            var attackSequence = ++AttackSequence;

            var weapon = GetEquippedMeleeWeapon();

            if (NextRefillTime > DateTime.UtcNow && weapon.IsGunblade == false)
            {
                var delayTime = (float)(NextRefillTime - DateTime.UtcNow).TotalSeconds;

                var actionChain = new ActionChain();
                actionChain.AddDelaySeconds(delayTime);
                actionChain.AddAction(this, () =>
                {
                    if (!creatureTarget.IsAlive)
                    {
                        OnAttackDone();
                        return;
                    }

                    HandleActionTargetedMeleeAttack_Inner(target, attackSequence);
                });
                actionChain.EnqueueChain();
            }
            else if (weapon == null)
                HandleActionTargetedMeleeAttack_Inner(target, attackSequence);
            else if (weapon.IsGunblade == false)
                HandleActionTargetedMeleeAttack_Inner(target, attackSequence);
            else if (NextRefillTime > DateTime.UtcNow && weapon.IsGunblade == true)
            {
                var delayTime = (float)(NextRefillTime - DateTime.UtcNow).TotalSeconds;

                var actionChain = new ActionChain();
                actionChain.AddDelaySeconds(delayTime);
                actionChain.AddAction(this, () =>
                {
                    if (!creatureTarget.IsAlive)
                    {
                        OnAttackDone();
                        return;
                    }

                    HandleActionTargetedGunBladeAttack_Inner(target, attackSequence);
                });
                actionChain.EnqueueChain();
            }
            else if (weapon.IsGunblade == true)
                HandleActionTargetedGunBladeAttack_Inner(target, attackSequence);
        }

        public static readonly float GunBladeDistance = 80.0f;
        public static readonly float MeleeDistance = 4.0f;
        public static readonly float StickyDistance = 4.0f;
        public static readonly float RepeatDistance = 16.0f;

        public void HandleActionTargetedMeleeAttack_Inner(WorldObject target, int attackSequence)
        {
            var dist = GetCylinderDistance(target);

            var weapon = GetEquippedMeleeWeapon();

            if (dist <= MeleeDistance || dist <= StickyDistance && IsMeleeVisible(target))
            {
                // sticky melee
                var angle = GetAngle(target);
                if (angle > PropertyManager.GetDouble("melee_max_angle").Item)
                {
                    var rotateTime = Rotate(target);

                    var actionChain = new ActionChain();
                    actionChain.AddDelaySeconds(rotateTime);
                    actionChain.AddAction(this, () => Attack(target, attackSequence));
                    actionChain.EnqueueChain();
                }
                else
                    Attack(target, attackSequence);
            }
            else
            {
                // turn / move to required
                if (GetCharacterOption(CharacterOption.UseChargeAttack))
                {
                    //log.Info($"{Name}.MoveTo({target.Name})");

                    // charge attack
                    MoveTo(target);
                }
                else
                {
                    CreateMoveToChain(target, (success) =>
                    {
                        if (success)
                            Attack(target, attackSequence);
                        else
                            OnAttackDone();
                    });
                }
            }
        }

        public void HandleActionTargetedGunBladeAttack_Inner(WorldObject target, int attackSequence)
        {
            var dist = GetCylinderDistance(target);

            var weapon = GetEquippedMeleeWeapon();

            var range = weapon.GetProperty(PropertyInt.WeaponRange);

            var ammo = GetEquippedAmmo();

            if (dist <= GunBladeDistance && dist > MeleeDistance && ammo == null)
                OnAttackDone();
            else if (dist > GunBladeDistance && dist > MeleeDistance)
                OnAttackDone();

            else if (dist <= GunBladeDistance && dist > MeleeDistance && ammo != null)
            {
                var projectileSpeed = GetGunBladeProjectileSpeed();
                var aimVelocity = GetAimVelocity(target, projectileSpeed);
                var aimLevel = GetAimLevel(aimVelocity);
                var localOrigin = GetProjectileSpawnOrigin(ammo.WeenieClassId, aimLevel);
                var velocity = CalculateProjectileVelocity(localOrigin, target, projectileSpeed, out Vector3 origin, out Quaternion orientation);
                var angle = GetAngle(target);
                var baseSpeed = GetAnimSpeed();
                var animSpeedMod = IsDualWieldAttack ? 1.2f : 1.0f;     // dual wield swing animation 20% faster
                var animSpeed = baseSpeed * animSpeedMod;
                var swingAnimation = GetSwingAnimation();
                var animLength = MotionTable.GetAnimationLength(MotionTableId, CurrentMotionState.Stance, swingAnimation, animSpeed);
                if (angle > PropertyManager.GetDouble("melee_max_angle").Item)
                {
                    var rotateTime = Rotate(target);
                    var actionChain = new ActionChain();

                    actionChain.AddDelaySeconds(rotateTime);
                    Rotate(target);
                    actionChain.AddAction(this, () =>
                    {
                        GunBladeAttack(target, attackSequence);
                        UpdateAmmoAfterLaunch(ammo);
                    });

                    actionChain.EnqueueChain();
                }
                else
                {
                    var actionChain = new ActionChain();

                    Rotate(target);
                    actionChain.AddAction(this, () =>
                    {
                        GunBladeAttack(target, attackSequence);
                        UpdateAmmoAfterLaunch(ammo);
                    });

                    actionChain.EnqueueChain();
                }
            }
            else if (dist <= MeleeDistance && ammo != null)
            {
                var projectileSpeed = GetGunBladeProjectileSpeed();
                var aimVelocity = GetAimVelocity(target, projectileSpeed);
                var aimLevel = GetAimLevel(aimVelocity);
                var localOrigin = GetProjectileSpawnOrigin(ammo.WeenieClassId, aimLevel);
                var velocity = CalculateProjectileVelocity(localOrigin, target, projectileSpeed, out Vector3 origin, out Quaternion orientation);
                var angle = GetAngle(target);
                if (angle > PropertyManager.GetDouble("melee_max_angle").Item)
                {
                    var rotateTime = Rotate(target);
                    var actionChain = new ActionChain();

                    actionChain.AddDelaySeconds(rotateTime);
                    Rotate(target);
                    actionChain.AddAction(this, () =>
                    {
                        Attack(target, attackSequence);
                    });
                    actionChain.EnqueueChain();

                }
                else
                {
                    var actionChain = new ActionChain();

                    Rotate(target);
                    actionChain.AddAction(this, () =>
                    {
                        Attack(target, attackSequence);
                    });
                    actionChain.EnqueueChain();

                }


            }
            else if (dist <= MeleeDistance && ammo == null)
            {
                var angle = GetAngle(target);
                if (angle > PropertyManager.GetDouble("melee_max_angle").Item)
                {
                    var rotateTime = Rotate(target);
                    var actionChain = new ActionChain();

                    actionChain.AddDelaySeconds(rotateTime);
                    Rotate(target);
                    actionChain.AddAction(this, () =>
                    {
                        Attack(target, attackSequence);
                    });
                    actionChain.EnqueueChain();
                }
                else
                {
                    var actionChain = new ActionChain();

                    Rotate(target);
                    actionChain.AddAction(this, () =>
                    {
                        Attack(target, attackSequence);
                    });
                    actionChain.EnqueueChain();
                }


            }
            else OnAttackDone();



        }

        public void OnAttackDone(WeenieError error = WeenieError.None)
        {
            // this function is called at the very end of an attack sequence,
            // and not between the repeat attacks

            // it sends action cancelled so the power / accuracy meter
            // is reset, and doesn't start refilling again

            // the werror for this network message is not displayed to the client --
            // if you wish to display a message, a separate GameEventWeenieError should also be sent

            Session.Network.EnqueueSend(new GameEventAttackDone(Session, WeenieError.ActionCancelled));

            AttackTarget = null;
            MeleeTarget = null;
            MissileTarget = null;

            AttackQueue.Clear();

            AttackCancelled = false;
        }

        /// <summary>
        /// called when client sends the 'Cancel attack' network message
        /// </summary>
        public void HandleActionCancelAttack(WeenieError error = WeenieError.None)
        {
            //Console.WriteLine($"{Name}.HandleActionCancelAttack()");

            if (Attacking)
                AttackCancelled = true;
            else if (AttackTarget != null)
                OnAttackDone();

            PhysicsObj.cancel_moveto();
        }

        /// <summary>
        /// Performs a player melee attack against a target
        /// </summary>
        public void Attack(WorldObject target, int attackSequence, bool subsequent = false)
        {
            //log.Info($"{Name}.Attack({target.Name}, {attackSequence})");
            var weapon = GetEquippedMeleeWeapon();

            if (AttackSequence != attackSequence)
                return;

            if (CombatMode != CombatMode.Melee || MeleeTarget == null && weapon == null || IsBusy || !IsAlive || suicideInProgress)
            {
                OnAttackDone();
                return;
            }
            else if (CombatMode != CombatMode.Melee || MeleeTarget == null && !weapon.IsGunblade || IsBusy || !IsAlive || suicideInProgress)
            {
                OnAttackDone();
                return;
            }

            var creature = target as Creature;
            if (creature == null || !creature.IsAlive)
            {
                OnAttackDone();
                return;
            }

            var animLength = DoSwingMotion(target, out var attackFrames);
            if (animLength == 0)
            {
                OnAttackDone();
                return;
            }

            // point of no return beyond this point -- cannot be cancelled
            Attacking = true;

            if (subsequent)
            {
                // client shows hourglass, until attack done is received
                // retail only did this for subsequent attacks w/ repeat attacks on
                Session.Network.EnqueueSend(new GameEventCombatCommenceAttack(Session));
            }

            var attackType = GetWeaponAttackType(weapon);
            var numStrikes = GetNumStrikes(attackType);
            var swingTime = animLength / numStrikes / 1.5f;

            var actionChain = new ActionChain();

            // stamina usage
            // TODO: ensure enough stamina for attack
            var staminaCost = GetAttackStamina(GetPowerRange());
            UpdateVitalDelta(Stamina, -staminaCost);

            if (numStrikes != attackFrames.Count)
            {
                //log.Warn($"{Name}.GetAttackFrames(): MotionTableId: {MotionTableId:X8}, MotionStance: {CurrentMotionState.Stance}, Motion: {GetSwingAnimation()}, AttackFrames.Count({attackFrames.Count}) != NumStrikes({numStrikes})");
                numStrikes = attackFrames.Count;
            }

            // handle self-procs
            TryProcEquippedItems(this, this, true, weapon);

            var prevTime = 0.0f;
            bool targetProc = false;

            for (var i = 0; i < numStrikes; i++)
            {
                // are there animation hooks for damage frames?
                //if (numStrikes > 1 && !TwoHandedCombat)
                //actionChain.AddDelaySeconds(swingTime);
                actionChain.AddDelaySeconds(attackFrames[i].time * animLength - prevTime);
                prevTime = attackFrames[i].time * animLength;

                actionChain.AddAction(this, () =>
                {
                    if (IsDead)
                    {
                        Attacking = false;
                        OnAttackDone();
                        return;
                    }

                    var damageEvent = DamageTarget(creature, weapon);

                    // handle target procs
                    if (damageEvent != null && damageEvent.HasDamage && !targetProc)
                    {
                        TryProcEquippedItems(this, creature, false, weapon);
                        targetProc = true;
                    }

                    if (weapon != null && weapon.IsCleaving && weapon.IsGunblade == false)
                    {
                        var cleave = GetCleaveTarget(creature, weapon);

                        foreach (var cleaveHit in cleave)
                        {
                            // target procs don't happen for cleaving
                            DamageTarget(cleaveHit, weapon);
                            TryProcEquippedItems(this, cleaveHit, false, weapon);
                        }
                    }

                    var ammo = GetEquippedAmmo();

                    if (weapon != null && weapon.IsCleaving && weapon.IsGunblade == true)
                    {
                        var cleave = GetCleaveTarget(creature, weapon);

                        foreach (var cleaveHit in cleave)
                        {
                            // target procs don't happen for cleaving
                            //var ammo = GetEquippedAmmo();
                            if (ammo != null && ammo.WeenieClassId == 300444)
                            {
                                var projectileSpeed = GetGunBladeProjectileSpeed();
                                var aimVelocity = GetAimVelocity(target, projectileSpeed);
                                var aimLevel = GetAimLevel(aimVelocity);
                                var localOrigin = GetProjectileSpawnOrigin(ammo.WeenieClassId, aimLevel);
                                var velocity = CalculateProjectileVelocity(localOrigin, target, projectileSpeed, out Vector3 origin, out Quaternion orientation);

                                DamageTarget(cleaveHit, weapon);
                                TryProcEquippedItems(this, cleaveHit, false, weapon);
                                LaunchProjectile(weapon, ammo, target, origin, orientation, velocity);

                                if (ammo.StackSize != null)
                                    UpdateAmmoAfterLaunch(ammo);
                            }
                            else
                            {
                                DamageTarget(cleaveHit, weapon);

                            }

                        }
                    }

                    if (weapon != null && weapon.IsGunblade == true && ammo != null)
                    {
                        if (ammo != null && ammo.WeenieClassId == 300444)
                        {
                            // var ammo = GetEquippedAmmo();
                            var projectileSpeed = GetGunBladeProjectileSpeed();
                            var aimVelocity = GetAimVelocity(target, projectileSpeed);
                            var aimLevel = GetAimLevel(aimVelocity);
                            var localOrigin = GetProjectileSpawnOrigin(ammo.WeenieClassId, aimLevel);
                            var velocity = CalculateProjectileVelocity(localOrigin, target, projectileSpeed, out Vector3 origin, out Quaternion orientation);

                            LaunchProjectile(weapon, ammo, target, origin, orientation, velocity);

                            if (ammo.StackSize != null)
                                UpdateAmmoAfterLaunch(ammo);
                        }
                    }
                });
            }

            //actionChain.AddDelaySeconds(animLength - swingTime * numStrikes);
            actionChain.AddDelaySeconds(animLength - prevTime);

            actionChain.AddAction(this, () =>
            {
                Attacking = false;

                // powerbar refill timing
                var refillMod = IsDualWieldAttack ? 0.8f : 1.0f;    // dual wield powerbar refills 20% faster

                PowerLevel = AttackQueue.Fetch();

                var nextRefillTime = PowerLevel * refillMod;
                NextRefillTime = DateTime.UtcNow.AddSeconds(nextRefillTime);
                var dotRoll = ThreadSafeRandom.Next(0.0f, 1.0f);

                var dist = GetCylinderDistance(target);

                if (creature.IsAlive && GetCharacterOption(CharacterOption.AutoRepeatAttacks) && IsMeleeVisible(target) && !IsBusy && !AttackCancelled && weapon == null)
                {
                    // client starts refilling power meter
                    Session.Network.EnqueueSend(new GameEventAttackDone(Session));

                    var nextAttack = new ActionChain();
                    nextAttack.AddDelaySeconds(nextRefillTime);
                    nextAttack.AddAction(this, () => Attack(target, attackSequence, true));
                    nextAttack.EnqueueChain();

                    if (IsDps)
                    {
                        if (MeleeDoTChance >= dotRoll)
                        {
                            //var dot = DatabaseManager.World.GetCachedWeenie(300501);
                            var dotTarget = target as Creature;
                            var targets = GetDoTTarget(dotTarget);
                            //var obj = WorldObjectFactory.CreateNewWorldObject(dot);

                            CreateDoTSpot(this, targets);
                        }
                    }
                    if (IsTank)
                    {
                        var chanceRoll = ThreadSafeRandom.Next(0.0f, 1.0f);

                        if (chanceRoll < 0.33f)
                        {
                            IsTankBuffed = true;
                        }
                    }
                }
                else if (creature.IsAlive && GetCharacterOption(CharacterOption.AutoRepeatAttacks) && IsMeleeVisible(target) && !IsBusy && !AttackCancelled && weapon.IsGunblade == true)
                {
                    // client starts refilling power meter
                    Session.Network.EnqueueSend(new GameEventAttackDone(Session));

                    var nextAttack = new ActionChain();
                    nextAttack.AddDelaySeconds(nextRefillTime);
                    nextAttack.AddAction(this, () => Attack(target, attackSequence, true));
                    nextAttack.EnqueueChain();

                    if (IsDps)
                    {
                        if (MeleeDoTChance >= dotRoll)
                        {
                            //var dot = DatabaseManager.World.GetCachedWeenie(300501);
                            var dotTarget = target as Creature;
                            var targets = GetDoTTarget(dotTarget);
                            //var obj = WorldObjectFactory.CreateNewWorldObject(dot);

                            CreateDoTSpot(this, targets);
                        }
                    }
                    if (IsTank)
                    {
                        var chanceRoll = ThreadSafeRandom.Next(0.0f, 1.0f);

                        if (chanceRoll < 0.33f)
                        {
                            IsTankBuffed = true;
                        }
                    }
                }

                else if (creature.IsAlive && GetCharacterOption(CharacterOption.AutoRepeatAttacks) && (dist <= MeleeDistance || dist <= StickyDistance && IsMeleeVisible(target)) && !IsBusy && !AttackCancelled)
                {
                    // client starts refilling power meter
                    Session.Network.EnqueueSend(new GameEventAttackDone(Session));

                    var nextAttack = new ActionChain();
                    nextAttack.AddDelaySeconds(nextRefillTime);
                    nextAttack.AddAction(this, () => Attack(target, attackSequence, true));
                    nextAttack.EnqueueChain();

                    if (IsDps)
                    {
                        if (MeleeDoTChance >= dotRoll)
                        {
                            //var dot = DatabaseManager.World.GetCachedWeenie(300501);
                            var dotTarget = target as Creature;
                            var targets = GetDoTTarget(dotTarget);
                            //var obj = WorldObjectFactory.CreateNewWorldObject(dot);

                            CreateDoTSpot(this, targets);
                        }
                    }
                    if (IsTank)
                    {
                        var chanceRoll = ThreadSafeRandom.Next(0.0f, 1.0f);

                        if (chanceRoll < 0.33f)
                        {
                            IsTankBuffed = true;
                        }
                    }
                }
                else
                    OnAttackDone();
            });

            actionChain.EnqueueChain();

            if (UnderLifestoneProtection)
                LifestoneProtectionDispel();
        }

        public void CreateDoTSpot(Player player, List<Creature> targets)
        {
            if (targets != null)
            {
                var dot = DatabaseManager.World.GetCachedWeenie(300501);

                List<WorldObject> dotObjects = new List<WorldObject>();

                foreach (var m in targets)
                {
                    var newDot = WorldObjectFactory.CreateNewWorldObject(dot);

                    dotObjects.Add(newDot);
                }

                for (int i = 0; i < dotObjects.Count; i++)
                {
                    dotObjects[i].DoTOwnerGuid = (int)player.Guid.Full;
                    dotObjects[i].Damage = (int)(targets[i].Health.Current * 0.005f);
                    dotObjects[i].Location = targets[i].Location;
                    dotObjects[i].Location.LandblockId = new LandblockId(dotObjects[i].Location.GetCell());
                    dotObjects[i].EnterWorld();
                }
            }
        }

        public void GunBladeAttack(WorldObject target, int attackSequence, bool subsequent = false)
        {
            //log.Info($"{Name}.Attack({target.Name}, {attackSequence})");
            var weapon = GetEquippedMeleeWeapon();

            if (AttackSequence != attackSequence)
                return;

            if (CombatMode != CombatMode.Melee || MeleeTarget == null && !weapon.IsGunblade || IsBusy || !IsAlive || suicideInProgress)
            {
                OnAttackDone();
                return;
            }

            var creature = target as Creature;
            if (creature == null || !creature.IsAlive)
            {
                OnAttackDone();
                return;
            }

            var animLength = DoSwingMotion(target, out var attackFrames);
            if (animLength == 0)
            {
                OnAttackDone();
                return;
            }

            // point of no return beyond this point -- cannot be cancelled
            Attacking = true;

            if (subsequent)
            {
                // client shows hourglass, until attack done is received
                // retail only did this for subsequent attacks w/ repeat attacks on
                Session.Network.EnqueueSend(new GameEventCombatCommenceAttack(Session));
            }

            var attackType = GetWeaponAttackType(weapon);
            var numStrikes = GetNumStrikes(attackType);
            var swingTime = animLength / numStrikes / 1.5f;

            var actionChain = new ActionChain();

            // stamina usage
            // TODO: ensure enough stamina for attack
            var staminaCost = GetAttackStamina(GetPowerRange());
            UpdateVitalDelta(Stamina, -staminaCost);

            if (numStrikes != attackFrames.Count)
            {
                //log.Warn($"{Name}.GetAttackFrames(): MotionTableId: {MotionTableId:X8}, MotionStance: {CurrentMotionState.Stance}, Motion: {GetSwingAnimation()}, AttackFrames.Count({attackFrames.Count}) != NumStrikes({numStrikes})");
                numStrikes = attackFrames.Count;
            }

            // handle self-procs
            TryProcEquippedItems(this, this, true, weapon);

            var prevTime = 0.0f;
            var ammo = GetEquippedAmmo();

            if (ammo == null && weapon.IsGunblade)
            {
                Attacking = false;
                OnAttackDone();
                return;
            }

            var projectileSpeed = GetGunBladeProjectileSpeed();
            var aimVelocity = GetAimVelocity(target, projectileSpeed);
            var aimLevel = GetAimLevel(aimVelocity);
            var localOrigin = GetProjectileSpawnOrigin(ammo.WeenieClassId, aimLevel);
            var velocity = CalculateProjectileVelocity(localOrigin, target, projectileSpeed, out Vector3 origin, out Quaternion orientation);

            for (var i = 0; i < numStrikes; i++)
            {
                // are there animation hooks for damage frames?
                //if (numStrikes > 1 && !TwoHandedCombat)
                //actionChain.AddDelaySeconds(swingTime);
                actionChain.AddDelaySeconds(attackFrames[i].time * animLength - prevTime);
                prevTime = attackFrames[i].time * animLength;

                actionChain.AddAction(this, () =>
                {
                    if (IsDead)
                    {
                        Attacking = false;
                        OnAttackDone();
                        return;
                    }

                    // handle target procs                  

                    var ammo = GetEquippedAmmo();

                    if (weapon != null && weapon.IsCleaving && weapon.IsGunblade == true)
                    {
                        var cleave = GetCleaveTarget(creature, weapon);

                        foreach (var cleaveHit in cleave)
                        {
                            // target procs don't happen for cleaving
                            DamageTarget(cleaveHit, weapon);
                            TryProcEquippedItems(this, cleaveHit, false, weapon);
                            if (ammo != null)
                            {
                                LaunchProjectile(weapon, ammo, cleaveHit, origin, orientation, velocity);
                                UpdateAmmoAfterLaunch(ammo);
                            }
                        }
                    }

                    if (weapon != null && weapon.IsGunblade == true && ammo == null)
                    {
                        TryProcEquippedItems(this, creature, false, weapon);
                    }

                    if (weapon != null && weapon.IsGunblade == true && ammo != null)
                    {
                        LaunchProjectile(weapon, ammo, target, origin, orientation, velocity);
                        UpdateAmmoAfterLaunch(ammo);
                    }
                });
            }

            actionChain.AddDelaySeconds(animLength - prevTime);

            actionChain.AddAction(this, () =>
            {
                Attacking = false;

                // powerbar refill timing
                var refillMod = IsDualWieldAttack ? 0.8f : 1.0f;    // dual wield powerbar refills 20% faster

                PowerLevel = AttackQueue.Fetch();

                var nextRefillTime = PowerLevel * refillMod;
                NextRefillTime = DateTime.UtcNow.AddSeconds(nextRefillTime);
                var dotRoll = ThreadSafeRandom.Next(0.0f, 1.0f);

                var dist = GetCylinderDistance(target);

                if (creature.IsAlive && GetCharacterOption(CharacterOption.AutoRepeatAttacks) && IsMeleeVisible(target) && !IsBusy && !AttackCancelled && weapon.IsGunblade == true)
                {
                    // client starts refilling power meter
                    Session.Network.EnqueueSend(new GameEventAttackDone(Session));

                    var nextAttack = new ActionChain();
                    nextAttack.AddDelaySeconds(nextRefillTime);
                    nextAttack.AddAction(this, () => GunBladeAttack(target, attackSequence, true));
                    nextAttack.EnqueueChain();

                    if (IsDps)
                    {
                        if (MeleeDoTChance >= dotRoll)
                        {
                            //var dot = DatabaseManager.World.GetCachedWeenie(300501);
                            var dotTarget = target as Creature;
                            var targets = GetDoTTarget(dotTarget);
                            //var obj = WorldObjectFactory.CreateNewWorldObject(dot);

                            CreateDoTSpot(this, targets);
                        }
                    }
                    if (IsTank)
                    {
                        var chanceRoll = ThreadSafeRandom.Next(0.0f, 1.0f);

                        if (chanceRoll < 0.33f)
                        {
                            IsTankBuffed = true;
                        }
                    }
                }

                else if (creature.IsAlive && GetCharacterOption(CharacterOption.AutoRepeatAttacks) && IsMeleeVisible(target) && !IsBusy && !AttackCancelled)
                {
                    // client starts refilling power meter
                    Session.Network.EnqueueSend(new GameEventAttackDone(Session));

                    var nextAttack = new ActionChain();
                    nextAttack.AddDelaySeconds(nextRefillTime);
                    nextAttack.AddAction(this, () => GunBladeAttack(target, attackSequence, true));
                    nextAttack.EnqueueChain();

                    if (IsDps)
                    {
                        if (MeleeDoTChance >= dotRoll)
                        {
                            //var dot = DatabaseManager.World.GetCachedWeenie(300501);
                            var dotTarget = target as Creature;
                            var targets = GetDoTTarget(dotTarget);
                            //var obj = WorldObjectFactory.CreateNewWorldObject(dot);

                            CreateDoTSpot(this, targets);
                        }
                    }
                    if (IsTank)
                    {
                        var chanceRoll = ThreadSafeRandom.Next(0.0f, 1.0f);

                        if (chanceRoll < 0.33f)
                        {
                            IsTankBuffed = true;
                        }
                    }
                }
                else
                    OnAttackDone();
            });

            actionChain.EnqueueChain();

            if (UnderLifestoneProtection)
                LifestoneProtectionDispel();
        }

        /// <summary>
        /// Performs the player melee swing animation
        /// </summary>
        public float DoSwingMotion(WorldObject target, out List<(float time, AttackHook attackHook)> attackFrames)
        {
            // get the proper animation speed for this attack,
            // based on weapon speed and player quickness
            var baseSpeed = GetAnimSpeed();
            var animSpeedMod = IsDualWieldAttack ? 1.2f : 1.0f;     // dual wield swing animation 20% faster
            var animSpeed = baseSpeed * animSpeedMod;

            var swingAnimation = GetSwingAnimation();
            var animLength = MotionTable.GetAnimationLength(MotionTableId, CurrentMotionState.Stance, swingAnimation, animSpeed);
            //Console.WriteLine($"AnimSpeed: {animSpeed}, AnimLength: {animLength}");

            attackFrames = MotionTable.GetAttackFrames(MotionTableId, CurrentMotionState.Stance, swingAnimation);
            //Console.WriteLine($"Attack frames: {string.Join(",", attackFrames)}");
            var weapon = GetEquippedMeleeWeapon();
            // broadcast player swing animation to clients
            if (weapon == null)
            {
                var motion = new Motion(this, swingAnimation, animSpeed);
                if (PropertyManager.GetBool("persist_movement").Item)
                {
                    motion.Persist(CurrentMotionState);
                }
                motion.MotionState.TurnSpeed = 2.25f;
                motion.MotionFlags |= MotionFlags.StickToObject;
                motion.TargetGuid = target.Guid;
                CurrentMotionState = motion;

                EnqueueBroadcastMotion(motion);
            }
            else if (weapon.IsGunblade == false)
            {
                var motion = new Motion(this, swingAnimation, animSpeed);
                if (PropertyManager.GetBool("persist_movement").Item)
                {
                    motion.Persist(CurrentMotionState);
                }
                motion.MotionState.TurnSpeed = 2.25f;
                motion.MotionFlags |= MotionFlags.StickToObject;
                motion.TargetGuid = target.Guid;
                CurrentMotionState = motion;

                EnqueueBroadcastMotion(motion);
            }
            else if (weapon.IsGunblade == true)
            {
                var motion = new Motion(this, swingAnimation, animSpeed);
                if (PropertyManager.GetBool("persist_movement").Item)
                {
                    motion.Persist(CurrentMotionState);
                }
                motion.MotionState.TurnSpeed = 2.25f;
                motion.TargetGuid = target.Guid;
                CurrentMotionState = motion;

                EnqueueBroadcastMotion(motion);
            }


            if (FastTick)
                PhysicsObj.stick_to_object(target.Guid.Full);

            return animLength;
        }

        public static readonly float KickThreshold = 0.75f;

        public MotionCommand PrevMotionCommand;

        /// <summary>
        /// Returns the melee swing animation - based on weapon,
        /// current stance, power bar, and attack height
        /// </summary>
        public MotionCommand GetSwingAnimation()
        {
            if (IsDualWieldAttack)
                DualWieldAlternate = !DualWieldAlternate;

            var offhand = IsDualWieldAttack && !DualWieldAlternate;

            var weapon = GetEquippedMeleeWeapon();

            // for reference: https://www.youtube.com/watch?v=MUaD53D9c74
            // a player with 1/2 power bar, or slightly below half
            // doing the backswing, well above 33%
            var subdivision = 0.33f;

            if (weapon != null)
            {
                AttackType = weapon.GetAttackType(CurrentMotionState.Stance, PowerLevel, offhand);
                if (weapon.IsThrustSlash)
                    subdivision = 0.66f;
            }
            else
            {
                AttackType = PowerLevel > KickThreshold ? AttackType.Kick : AttackType.Punch;
            }

            var motions = CombatTable.GetMotion(CurrentMotionState.Stance, AttackHeight.Value, AttackType, PrevMotionCommand);

            // higher-powered animation always in first slot ?
            var motion = motions.Count > 1 && PowerLevel < subdivision ? motions[1] : motions[0];

            PrevMotionCommand = motion;

            //Console.WriteLine($"{motion}");

            return motion;
        }
    }
}
