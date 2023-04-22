using System;
using System.Collections.Generic;

using ACE.Common;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Physics.Animation;
using ACE.Entity.Enum.Properties;
using System.Numerics;
using log4net;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Summonable monsters combat AI
    /// </summary>
    public partial class CombatPet : Pet
    {
        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public CombatPet(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public CombatPet(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
        }

        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public override bool? Init(Player player, PetDevice petDevice)
        {
            var success = base.Init(player, petDevice);

            if (success == null || !success.Value)
                return success;

            SetCombatMode(CombatMode.Melee);
            MonsterState = State.Awake;
            IsAwake = true;

            double petRatingScaleFactor = PropertyManager.GetDouble("combat_pet_rating_scale").Item;

            // inherit ratings from pet device
            if (petDevice.DamageRating == null)
            {
                DamageRating = 1;
            }
            if (petDevice.DamageResistRating == null)
            {
                DamageResistRating = 1;
            }
            if (petDevice.CritDamageRating == null)
            {
                CritDamageRating = 1;
            }
            if (petDevice.CritDamageResistRating == null)
            {
                CritDamageResistRating = 1;
            }
            if (player.GearDamage == null)
            {
                DamageRating = petDevice.DamageRating;
            }
            DamageRating = petDevice.GearDamage + (int)(player.LumAugDamageRating * petRatingScaleFactor * 5);
            if (player.DamageResistRating == null)
            {
                DamageResistRating = petDevice.DamageResistRating;
            }
            DamageResistRating = petDevice.GearDamageResist + (int)(player.LumAugDamageReductionRating * petRatingScaleFactor * 2);
            if (player.CritDamageRating == null)
            {
                CritDamageRating = petDevice.CritDamageRating;
            }
            CritDamageRating = petDevice.GearCritDamage + (int)(player.LumAugCritDamageRating * petRatingScaleFactor * 5);
            if (player.CritDamageResistRating == null)
            {
                CritDamageResistRating = petDevice.CritDamageResistRating;
            }
            CritDamageResistRating = petDevice.GearCritDamageResist + (int)(player.LumAugCritReductionRating * petRatingScaleFactor * 2);
            if (player.CritRating == null)
            {
                CritRating = petDevice.CritRating;
            }
            CritRating = petDevice.GearCrit;
            if (player.CritResistRating == null)
            {
                CritResistRating = petDevice.CritResistRating;
            }
            CritResistRating = petDevice.GearCritResist;

            Level = Level + (player.Level / 2);

            // are CombatPets supposed to attack monsters that are in the same faction as the pet owner?
            // if not, there are a couple of different approaches to this
            // the easiest way for the code would be to simply set Faction1Bits for the CombatPet to match the pet owner's
            // however, retail pcaps did not contain Faction1Bits for CombatPets

            // doing this the easiest way for the code here, and just removing during appraisal
            Faction1Bits = player.Faction1Bits;

            return true;
        }

        public override void HandleFindTarget()
        {
            var creature = AttackTarget as Creature;

            if (creature == null || creature.IsDead || !IsVisibleTarget(creature))
                FindNextTarget();
        }

        public override bool FindNextTarget()
        {
            var nearbyMonsters = GetNearbyMonsters();

            if (nearbyMonsters.Count == 0)
            {
                CombatPetStartFollow();
                return false;
                //Console.WriteLine($"{Name}.FindNextTarget(): empty");                
            }

            // get nearest monster
            var nearest = BuildTargetDistance(nearbyMonsters, true);


            if (nearest[0].Distance > VisualAwarenessRangeSq)
            {
                CombatPetStartFollow();
                return false;
            }

            AttackTarget = nearest[0].Target;

            //Console.WriteLine($"{Name}.FindNextTarget(): {AttackTarget.Name}");

            return true;

        }

        /// <summary>
        /// Returns a list of attackable monsters in this pet's visible targets
        /// </summary>
        public List<Creature> GetNearbyMonsters()
        {
            var monsters = new List<Creature>();

            foreach (var creature in PhysicsObj.ObjMaint.GetVisibleTargetsValuesOfTypeCreature())
            {
                // why does this need to be in here?
                if (creature.IsDead)
                {
                    //Console.WriteLine($"{Name}.GetNearbyMonsters(): refusing to add dead creature {creature.Name} ({creature.Guid})");
                    continue;
                }

                // combat pets do not aggro monsters belonging to the same faction as the pet owner?
                if (SameFaction(creature))
                {
                    // unless the pet owner or the pet is being retaliated against?
                    if (!creature.HasRetaliateTarget(P_PetOwner) && !creature.HasRetaliateTarget(this))
                        continue;
                }

                monsters.Add(creature);

            }

            return monsters;
        }

        public override void Sleep()
        {

            // pets dont really go to sleep, per say
            // they keep scanning for new targets,
            // which is the reverse of the current ACE jurassic park model

            return;  // empty by default

        }

        public void CombatPetTick(double currentUnixTime)
        {
            NextMonsterTickTime = currentUnixTime + monsterTickInterval;

            if (IsMoving)
            {
                PhysicsObj.update_object();

                UpdatePosition_SyncLocation();

                SendUpdatePosition();
            }

            if (currentUnixTime >= nextSlowTickTime)
                CombatPetSlowTick(currentUnixTime);
        }

        private static readonly double slowTickSeconds = 1.0;
        private double nextSlowTickTime;

        /// <summary>
        /// Called 1x per second
        /// </summary>
        public void CombatPetSlowTick(double currentUnixTime)
        {
            //Console.WriteLine($"{Name}.HeartbeatStatic({currentUnixTime})");
            var dist = GetCylinderDistance(P_PetOwner);

            nextSlowTickTime += slowTickSeconds;

            if (P_PetOwner?.PhysicsObj == null)
            {
                log.Error($"{Name} ({Guid}).SlowTick() - P_PetOwner: {P_PetOwner}, P_PetOwner.PhysicsObj: {P_PetOwner?.PhysicsObj}");
                Destroy();
                return;
            }

            if(GetDistance(P_PetOwner) < dist)
            {
                HandleFindTarget();
                return;
            }
          
            if (dist > MaxDistance)
                Destroy();

            if (!IsMoving && dist > MinDistance && FindNextTarget() == false)
                CombatPetStartFollow();
        }

        // if the passive pet is between min-max distance to owner,
        // it will turn and start running torwards its owner

        private static readonly float MinDistance = 5.0f;
        private static readonly float MaxDistance = 192.0f;

        private void CombatPetStartFollow()
        {
            // similar to Monster_Navigation.StartTurn()

            //Console.WriteLine($"{Name}.StartFollow()");

            IsMoving = true;

            // broadcast to clients
            MoveTo(P_PetOwner, RunRate);

            // perform movement on server
            var mvp = new MovementParameters();
            mvp.DistanceToObject = MinDistance;
            mvp.WalkRunThreshold = 0.0f;

            //mvp.UseFinalHeading = true;

            PhysicsObj.MoveToObject(P_PetOwner.PhysicsObj, mvp);

            // prevent snap forward
            PhysicsObj.UpdateTime = Physics.Common.PhysicsTimer.CurrentTime;
        }

        /// <summary>
        /// Broadcasts passive pet movement to clients
        /// </summary>
        public override void MoveTo(WorldObject target, float runRate = 1.0f)
        {
            /*if (!IsPassivePet)
            {
                base.MoveTo(target, runRate);
                return;
            }*/

            if (MoveSpeed == 0.0f)
                GetMovementSpeed();

            var motion = new Motion(this, target, MovementType.MoveToObject);

            motion.MoveToParameters.MovementParameters |= MovementParams.CanCharge;
            motion.MoveToParameters.DistanceToObject = MinDistance;
            motion.MoveToParameters.WalkRunThreshold = 0.0f;

            motion.RunRate = RunRate;

            CurrentMotionState = motion;

            EnqueueBroadcastMotion(motion);
        }

        /// <summary>
        /// Called when the MoveTo process has completed
        /// </summary>
        public override void OnMoveComplete(WeenieError status)
        {
            //Console.WriteLine($"{Name}.OnMoveComplete({status})");            

            if (status != WeenieError.None)
                return;

            PhysicsObj.CachedVelocity = Vector3.Zero;
            IsMoving = false;


        }
    }
}

        

