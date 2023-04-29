using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Database;
using ACE.Database.Models.World;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        public TreasureDeath DeathTreasure { get => DeathTreasureType.HasValue ? DatabaseManager.World.GetCachedDeathTreasure(DeathTreasureType.Value) : null; }

        private bool onDeathEntered = false;

        /// <summary>
        /// Called when a monster or player dies, in conjunction with Die()
        /// </summary>
        /// <param name="lastDamager">The last damager that landed the death blow</param>
        /// <param name="damageType">The damage type for the death message</param>
        /// <param name="criticalHit">True if the death blow was a critical hit, generates a critical death message</param>
        public virtual DeathMessage OnDeath(DamageHistoryInfo lastDamager, DamageType damageType, bool criticalHit = false)
        {
            if (onDeathEntered)
                return GetDeathMessage(lastDamager, damageType, criticalHit);

            onDeathEntered = true;

            IsTurning = false;
            IsMoving = false;

            //QuestManager.OnDeath(lastDamager?.TryGetAttacker());

            if (KillQuest != null)
                OnDeath_HandleKillTask(KillQuest);
            if (KillQuest2 != null)
                OnDeath_HandleKillTask(KillQuest2);
            if (KillQuest3 != null)
                OnDeath_HandleKillTask(KillQuest3);

            if (!IsOnNoDeathXPLandblock)
                OnDeath_GrantXP();

            return GetDeathMessage(lastDamager, damageType, criticalHit);
        }


        public DeathMessage GetDeathMessage(DamageHistoryInfo lastDamagerInfo, DamageType damageType, bool criticalHit = false)
        {
            var lastDamager = lastDamagerInfo?.TryGetAttacker();

            if (lastDamagerInfo == null || lastDamagerInfo.Guid == Guid || lastDamager is Hotspot)
                return Strings.General[1];

            var deathMessage = Strings.GetDeathMessage(damageType, criticalHit);

            // if killed by a player, send them a message
            if (lastDamagerInfo.IsPlayer)
            {
                if (criticalHit && this is Player)
                    deathMessage = Strings.PKCritical[0];

                var killerMsg = string.Format(deathMessage.Killer, Name);

                if (lastDamager is Player playerKiller)
                    playerKiller.Session.Network.EnqueueSend(new GameEventKillerNotification(playerKiller.Session, killerMsg));
            }
            return deathMessage;
        }

        /// <summary>
        /// Kills a player/creature and performs the full death sequence
        /// </summary>
        public void Die()
        {
            Die(DamageHistory.LastDamager, DamageHistory.TopDamager);
        }

        private bool dieEntered = false;

        /// <summary>
        /// Performs the full death sequence for non-Player creatures
        /// </summary>
        protected virtual void Die(DamageHistoryInfo lastDamager, DamageHistoryInfo topDamager)
        {
            if (dieEntered) return;

            dieEntered = true;

            UpdateVital(Health, 0);

            if (topDamager != null && !IsCombatPet)
            {
                KillerId = topDamager.Guid.Full;

                if (topDamager.IsPlayer)
                {
                    var topDamagerPlayer = topDamager.TryGetAttacker();
                    if (topDamagerPlayer != null)
                        topDamagerPlayer.CreatureKills = (topDamagerPlayer.CreatureKills ?? 0) + 1;
                }
            }
            if (IsCombatPet)
            {
                CurrentMotionState = new Motion(MotionStance.NonCombat, MotionCommand.Ready);
                //IsMonster = false;

                PhysicsObj.StopCompletely(true);

                // broadcast death animation
                var motionDeath1 = new Motion(MotionStance.NonCombat, MotionCommand.Dead);
                var deathAnimLength1 = ExecuteMotion(motionDeath1);

                var dieChain1 = new ActionChain();

                // wait for death animation to finish
                //var deathAnimLength = DatManager.PortalDat.ReadFromDat<MotionTable>(MotionTableId).GetAnimationLength(MotionCommand.Dead);
                dieChain1.AddDelaySeconds(deathAnimLength1);

                dieChain1.AddAction(this, () =>
                {
                    Destroy();
                });

                dieChain1.EnqueueChain();
            }
            else
            {
                CurrentMotionState = new Motion(MotionStance.NonCombat, MotionCommand.Ready);
                //IsMonster = false;

                PhysicsObj.StopCompletely(true);

                // broadcast death animation
                var motionDeath = new Motion(MotionStance.NonCombat, MotionCommand.Dead);
                var deathAnimLength = ExecuteMotion(motionDeath);

                EmoteManager.OnDeath(lastDamager);

                var dieChain = new ActionChain();

                // wait for death animation to finish
                //var deathAnimLength = DatManager.PortalDat.ReadFromDat<MotionTable>(MotionTableId).GetAnimationLength(MotionCommand.Dead);
                dieChain.AddDelaySeconds(deathAnimLength);

                dieChain.AddAction(this, () =>
                {
                    CreateCorpse(topDamager);
                    Destroy();
                });

                dieChain.EnqueueChain();
            }
        }

        /// <summary>
        /// Called when an admin player uses the /smite command
        /// to instantly kill a creature
        /// </summary>
        public void Smite(WorldObject smiter, bool useTakeDamage = false)
        {
            if (useTakeDamage)
            {
                // deal remaining damage
                TakeDamage(smiter, DamageType.Bludgeon, Health.Current);
            }
            else
            {
                OnDeath();
                var smiterInfo = new DamageHistoryInfo(smiter);
                Die(smiterInfo, smiterInfo);
            }
        }

        public void OnDeath()
        {
            OnDeath(null, DamageType.Undef);
        }

        /// <summary>
        /// Grants XP to players in damage history
        /// </summary>
        public void OnDeath_GrantXP()
        {
            if (this is Player && PlayerKillerStatus == PlayerKillerStatus.PKLite)
                return;

            var totalHealth = DamageHistory.TotalHealth;

            if (totalHealth == 0)
                return;

            foreach (var kvp in DamageHistory.TotalDamage)
            {
                var damager = kvp.Value.TryGetAttacker();

                var playerDamager = damager as Player;

                if (playerDamager == null && kvp.Value.PetOwner != null)
                    playerDamager = kvp.Value.TryGetPetOwner();

                if (playerDamager == null)
                    continue;

                var totalDamage = kvp.Value.TotalDamage;

                var damagePercent = totalDamage / totalHealth;

                var totalXP = (XpOverride ?? 0) * damagePercent;

                playerDamager.EarnXP((long)Math.Round(totalXP), XpType.Kill);

                // handle luminance
                if (LuminanceAward != null)
                {
                    var totalLuminance = (long)Math.Round(LuminanceAward.Value * damagePercent);
                    playerDamager.EarnLuminance(totalLuminance, XpType.Kill);
                }
            }
        }

        /// <summary>
        /// Handles the KillTask for a killed creature
        /// </summary>
        public void OnDeath_HandleKillTask(string killQuest)
        {
            var receivers = KillTask_GetEligibleReceivers(killQuest);

            foreach (var receiver in receivers)
            {
                var damager = receiver.Value.TryGetAttacker();

                var player = damager as Player;

                if (player == null && receiver.Value.PetOwner != null)
                    player = receiver.Value.TryGetPetOwner();

                if (player != null)
                    player.QuestManager.HandleKillTask(killQuest, this);
            }
        }

        /// <summary>
        /// Returns a flattened structure of eligible Players, Fellows, and CombatPets
        /// </summary>
        public Dictionary<ObjectGuid, DamageHistoryInfo> KillTask_GetEligibleReceivers(string killQuest)
        {
            // http://acpedia.org/wiki/Announcements_-_2012/12_-_A_Growing_Twilight#Release_Notes

            var questName = QuestManager.GetQuestName(killQuest);

            // we are using DamageHistoryInfo here, instead of Creature or WorldObjectInfo
            // WeakReference<CombatPet> may be null for expired CombatPets, but we still need the WeakReference<PetOwner> references

            var receivers = new Dictionary<ObjectGuid, DamageHistoryInfo>();

            foreach (var kvp in DamageHistory.TotalDamage)
            {
                if (kvp.Value.TotalDamage <= 0)
                    continue;

                var damager = kvp.Value.TryGetAttacker();

                var playerDamager = damager as Player;

                if (playerDamager == null && kvp.Value.PetOwner != null)
                {
                    // handle combat pets
                    playerDamager = kvp.Value.TryGetPetOwner();

                    if (playerDamager != null && playerDamager.QuestManager.HasQuest(questName))
                    {
                        // only add combat pet to eligible receivers if player has quest, and allow_summoning_killtask_multicredit = true (default, retail)
                        if (DamageHistory.HasDamager(playerDamager, true) && PropertyManager.GetBool("allow_summoning_killtask_multicredit").Item)
                            receivers[kvp.Value.Guid] = kvp.Value;
                        else
                            receivers[playerDamager.Guid] = new DamageHistoryInfo(playerDamager);
                    }

                    // regardless if combat pet is eligible, we still want to continue traversing to the pet owner, and possibly fellows

                    // in a scenario where combat pet does 100% damage:

                    // - regardless if allow_summoning_killtask_multicredit is enabled/disabled, it should continue traversing into pet owner and possibly their fellows

                    // - if pet owner doesn't have kill task, and fellow_kt_killer=false, any fellows with the task should still receive 1 credit
                }

                if (playerDamager == null)
                    continue;

                // factors:
                // - has quest
                // - is killer (last damager, top damager, or any damager? in current context, considering it to be any damager)
                // - has fellowship
                // - server option: fellow_kt_killer
                // - server option: fellow_kt_landblock

                if (playerDamager.QuestManager.HasQuest(questName))
                {
                    // just add a fake DamageHistoryInfo for reference
                    receivers[playerDamager.Guid] = new DamageHistoryInfo(playerDamager);
                }
                else if (PropertyManager.GetBool("fellow_kt_killer").Item)
                {
                    // if this option is enabled (retail default), the killer is required to have kill task
                    // for it to share with fellowship
                    continue;
                }

                // we want to add fellowship members in a flattened structure
                // in this inner loop, instead of the outer loop

                // scenarios:

                // i am a summoner in a fellowship with 1 other player
                // we both have a killtask

                // - my combatpet does 100% damage to the monster
                // result: i get 1 killtask credit, and my fellow gets 1 killtask credit

                // - my combatpet does 50% damage to monster, and i do 50% damage
                // result: i get 2 killtask credits (1 if allow_summoning_killtask_multicredit server option is disabled), and my fellow gets 1 killtask credit

                // - my combatpet does 33% damage to monster, i do 33% damage, and fellow does 33% damage
                // result: same as previous scenario

                // 2 players not in a fellowship both have a killtask
                // they each do 50% damage to monster

                // result: both players receive killtask credit

                if (playerDamager.Fellowship == null)
                    continue;

                // share with fellows in kill task range
                var fellows = playerDamager.Fellowship.WithinRange(playerDamager);

                foreach (var fellow in fellows)
                {
                    if (fellow.QuestManager.HasQuest(questName))
                        receivers[fellow.Guid] = new DamageHistoryInfo(fellow);
                }
            }
            return receivers;
        }

        /// <summary>
        /// Create a corpse for both creatures and players currently
        /// </summary>
        protected void CreateCorpse(DamageHistoryInfo killer)
        {
            if (NoCorpse)
            {
                var loot = GenerateTreasure(killer, null);

                foreach(var item in loot)
                {
                    item.Location = new Position(Location);
                    LandblockManager.AddObject(item);
                }
                return;
            }

            var cachedWeenie = DatabaseManager.World.GetCachedWeenie("corpse");

            var corpse = WorldObjectFactory.CreateNewWorldObject(cachedWeenie) as Corpse;

            var prefix = "Corpse";

            if (TreasureCorpse)
            {
                // Hardcoded values from PCAPs of Treasure Pile Corpses, everything else lines up exactly with existing corpse weenie
                corpse.SetupTableId  = 0x02000EC4;
                corpse.MotionTableId = 0x0900019B;
                corpse.SoundTableId  = 0x200000C2;
                corpse.ObjScale      = 0.4f;

                prefix = "Treasure";
            }
            else
            {
                corpse.SetupTableId = SetupTableId;
                corpse.MotionTableId = MotionTableId;
                //corpse.SoundTableId = SoundTableId; // Do not change sound table for corpses
                corpse.PaletteBaseDID = PaletteBaseDID;
                corpse.ClothingBase = ClothingBase;
                corpse.PhysicsTableId = PhysicsTableId;

                if (ObjScale.HasValue)
                    corpse.ObjScale = ObjScale;
                if (PaletteTemplate.HasValue)
                    corpse.PaletteTemplate = PaletteTemplate;
                if (Shade.HasValue)
                    corpse.Shade = Shade;
                //if (Translucency.HasValue) // Shadows have Translucency but their corpses do not, videographic evidence can be found on YouTube.
                //corpse.Translucency = Translucency;


                // Pull and save objdesc for correct corpse apperance at time of death
                var objDesc = CalculateObjDesc();

                corpse.Biota.PropertiesAnimPart = objDesc.AnimPartChanges.Clone(corpse.BiotaDatabaseLock);

                corpse.Biota.PropertiesPalette = objDesc.SubPalettes.Clone(corpse.BiotaDatabaseLock);

                corpse.Biota.PropertiesTextureMap = objDesc.TextureChanges.Clone(corpse.BiotaDatabaseLock);
            }

            // use the physics location for accuracy,
            // especially while jumping
            corpse.Location = PhysicsObj.Position.ACEPosition();

            corpse.VictimId = Guid.Full;
            corpse.Name = $"{prefix} of {Name}";

            // set 'killed by' for looting rights
            var killerName = "misadventure";
            if (killer != null)
            {
                if (!(Generator != null && Generator.Guid == killer.Guid) && Guid != killer.Guid)
                {
                    if (!string.IsNullOrWhiteSpace(killer.Name))
                        killerName = killer.Name.TrimStart('+');  // vtank requires + to be stripped for regex matching.

                    corpse.KillerId = killer.Guid.Full;

                    if (killer.PetOwner != null)
                    {
                        var petOwner = killer.TryGetPetOwner();
                        if (petOwner != null)
                            corpse.KillerId = petOwner.Guid.Full;
                    }
                }
            }

            corpse.LongDesc = $"Killed by {killerName}.";

            bool saveCorpse = false;

            var player = this as Player;

            if (player != null)
            {
                corpse.SetPosition(PositionType.Location, corpse.Location);
                var dropped = player.CalculateDeathItems(corpse);
                corpse.RecalculateDecayTime(player);

                if (dropped.Count > 0)
                    saveCorpse = true;

                if ((player.Location.Cell & 0xFFFF) < 0x100)
                {
                    player.SetPosition(PositionType.LastOutsideDeath, new Position(corpse.Location));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePosition(player, PositionType.LastOutsideDeath, corpse.Location));

                    if (dropped.Count > 0)
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your corpse is located at ({corpse.Location.GetMapCoordStr()}).", ChatMessageType.Broadcast));
                }

                var isPKdeath = player.IsPKDeath(killer);
                var isPKLdeath = player.IsPKLiteDeath(killer);

                if (isPKdeath)
                    corpse.PkLevel = PKLevel.PK;

                if (!isPKdeath && !isPKLdeath)
                {
                    var miserAug = player.AugmentationLessDeathItemLoss * 5;
                    if (miserAug > 0)
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your augmentation has reduced the number of items you can lose by {miserAug}!", ChatMessageType.Broadcast));
                }

                if (dropped.Count == 0 && !isPKLdeath)
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have retained all your items. You do not need to recover your corpse!", ChatMessageType.Broadcast));
            }
            else
            {                
                corpse.IsMonster = true;
                if (killer.IsPlayer)
                GenerateTreasure(killer, corpse);
                if (killer.PetOwner != null && !killer.IsPlayer)
                {
                    var petOwner = killer.TryGetPetOwner();
                    if (corpse.KillerId == petOwner.Guid.Full)
                    {
                        corpse.KillerId = petOwner.Guid.Full;
                    }
                    GenerateTreasure(killer, corpse);
                }
                if (IsOnLootMultiplierLandblock)
                {
                    GenerateTreasure(killer, corpse);
                }

                if (killer != null && killer.IsPlayer)
                {
                    if (Level >= 100)
                    {
                        CanGenerateRare = true;
                    }
                    else
                    {
                        var killerPlayer = killer.TryGetAttacker();
                        if (killerPlayer != null && Level > killerPlayer.Level)
                            CanGenerateRare = true;
                    }
                }
                else
                    CanGenerateRare = false;
            }

            corpse.RemoveProperty(PropertyInt.Value);

            if (CanGenerateRare && killer != null)
                corpse.TryGenerateRare(killer);

            corpse.InitPhysicsObj();

            // persist the original creature velocity (only used for falling) to corpse
            corpse.PhysicsObj.Velocity = PhysicsObj.Velocity;

            corpse.EnterWorld();

            if (player != null)
            {
                if (corpse.PhysicsObj == null || corpse.PhysicsObj.Position == null)
                    log.Debug($"[CORPSE] {Name}'s corpse (0x{corpse.Guid}) failed to spawn! Tried at {player.Location.ToLOCString()}");
                else
                    log.Debug($"[CORPSE] {Name}'s corpse (0x{corpse.Guid}) is located at {corpse.PhysicsObj.Position}");
            }

            if (saveCorpse)
            {
                corpse.SaveBiotaToDatabase();

                foreach (var item in corpse.Inventory.Values)
                    item.SaveBiotaToDatabase();
            }
        }

        public bool CanGenerateRare
        {
            get => GetProperty(PropertyBool.CanGenerateRare) ?? false;
            set { if (!value) RemoveProperty(PropertyBool.CanGenerateRare); else SetProperty(PropertyBool.CanGenerateRare, value); }
        }

        /// <summary>
        /// Transfers generated treasure from creature to corpse
        /// </summary>
        private List<WorldObject> GenerateTreasure(DamageHistoryInfo killer, Corpse corpse)
        {
            var droppedItems = new List<WorldObject>();

            // create death treasure from loot generation factory
            if (DeathTreasure != null)
            {
                List<WorldObject> items = LootGenerationFactory.CreateRandomLootObjects(DeathTreasure);
                foreach (WorldObject wo in items)
                {
                    if (corpse != null)
                        corpse.TryAddToInventory(wo);
                    else
                        droppedItems.Add(wo);

                    DoCantripLogging(killer, wo);
                }
            }

            // contain and non-wielded treasure create
            if (Biota.PropertiesCreateList != null)
            {
                var createList = Biota.PropertiesCreateList.Where(i => (i.DestinationType & DestinationType.Contain) != 0 ||
                                (i.DestinationType & DestinationType.Treasure) != 0 && (i.DestinationType & DestinationType.Wield) == 0).ToList();

                var selected = CreateListSelect(createList);

                foreach (var item in selected)
                {
                    var wo = WorldObjectFactory.CreateNewWorldObject(item);

                    if (wo != null)
                    {
                        if (corpse != null)
                            corpse.TryAddToInventory(wo);
                        else
                            droppedItems.Add(wo);
                    }
                }
            }

            // move wielded treasure over, which also should include Wielded objects not marked for destroy on death.
            // allow server operators to configure this behavior due to errors in createlist post 16py data
            var dropFlags = PropertyManager.GetBool("creatures_drop_createlist_wield").Item ? DestinationType.WieldTreasure : DestinationType.Treasure;

            var wieldedTreasure = Inventory.Values.Concat(EquippedObjects.Values).Where(i => (i.DestinationType & dropFlags) != 0);
            foreach (var item in wieldedTreasure.ToList())
            {
                if (item.Bonded == BondedStatus.Destroy)
                    continue;

                if (TryDequipObjectWithBroadcasting(item.Guid, out var wo, out var wieldedLocation))
                    EnqueueBroadcast(new GameMessagePublicUpdateInstanceID(item, PropertyInstanceId.Wielder, ObjectGuid.Invalid));

                if (corpse != null)
                {
                    corpse.TryAddToInventory(item);
                    EnqueueBroadcast(new GameMessagePublicUpdateInstanceID(item, PropertyInstanceId.Container, corpse.Guid), new GameMessagePickupEvent(item));
                }
                else
                    droppedItems.Add(item);
            }

            return droppedItems;
        }

        public void DoCantripLogging(DamageHistoryInfo killer, WorldObject wo)
        {
            var epicCantrips = wo.EpicCantrips;
            var legendaryCantrips = wo.LegendaryCantrips;

            if (epicCantrips.Count > 0)
                log.Debug($"[LOOT][EPIC] {Name} ({Guid}) generated item with {epicCantrips.Count} epic{(epicCantrips.Count > 1 ? "s" : "")} - {wo.Name} ({wo.Guid}) - {GetSpellList(epicCantrips)} - killed by {killer?.Name} ({killer?.Guid})");

            if (legendaryCantrips.Count > 0)
                log.Debug($"[LOOT][LEGENDARY] {Name} ({Guid}) generated item with {legendaryCantrips.Count} legendar{(legendaryCantrips.Count > 1 ? "ies" : "y")} - {wo.Name} ({wo.Guid}) - {GetSpellList(legendaryCantrips)} - killed by {killer?.Name} ({killer?.Guid})");
        }

        public static string GetSpellList(Dictionary<int, float> spellTable)
        {
            var spells = new List<Server.Entity.Spell>();

            foreach (var kvp in spellTable)
                spells.Add(new Server.Entity.Spell(kvp.Key, false));

            return string.Join(", ", spells.Select(i => i.Name));
        }

        public bool IsOnNoDeathXPLandblock => Location != null ? NoDeathXP_Landblocks.Contains(Location.LandblockId.Landblock) : false;
        public bool IsOnLootMultiplierLandblock => Location != null ? LootMultiplier_Landblocks.Contains(Location.LandblockId.Landblock) : false;
        public bool IsOnPKLandblock => Location != null ? PKLandblock_Landblocks.Contains(Location.LandblockId.Landblock) : false;

        public bool IsOnSpeedRunLandblock => Location != null ? SpeedRunLandblock_Landblocks.Contains(Location.LandblockId.Landblock) : false;

        /// <summary>
        /// A list of landblocks the player gains no xp from creature kills
        /// </summary>
        public static HashSet<ushort> NoDeathXP_Landblocks = new HashSet<ushort>()
        {
            0x00B0,     // Colosseum Arena One
            0x00B1,     // Colosseum Arena Two
            0x00B2,     // Colosseum Arena Three
            0x00B3,     // Colosseum Arena Four
            0x00B4,     // Colosseum Arena Five
            0x5960,     // Gauntlet Arena One (Celestial Hand)
            0x5961,     // Gauntlet Arena Two (Celestial Hand)
            0x5962,     // Gauntlet Arena One (Eldritch Web)
            0x5963,     // Gauntlet Arena Two (Eldritch Web)
            0x5964,     // Gauntlet Arena One (Radiant Blood)
            0x5965,     // Gauntlet Arena Two (Radiant Blood)
            0x596B,     // Gauntlet Staging Area (All Societies)
        };

        public static HashSet<ushort> LootMultiplier_Landblocks = new HashSet<ushort>()
        {
            0x5558,
            0x5559,
            0x555A,
            0x555B,
            0x555C,
            0x555D,
            0x555E,
            0x555F,
            0x5560,
            0x5561,
            0x5562,
            0x5563,
            0x5564,
            0x5565,
            0x5566,
            0x5567,
            0x5568,
            0x5569,
            0x556A,
            0x556B,
            0x556C,
            0x556D,
            0x556E,
            0x556F,
            0x5570,
            0x5571,
            0x5572,
            0x5573,
            0x5574,
            0x5575,
            0x5576,
            0x5577,
            0x5658,
            0x5659,
            0x565A,
            0x565B,
            0x565C,
            0x565D,
            0x565E,
            0x565F,
            0x5660,
            0x5661,
            0x5662,
            0x5663,
            0x5664,
            0x5665,
            0x5666,
            0x5667,
            0x5668,
            0x5669,
            0x566A,
            0x566B,
            0x566C,
            0x566D,
            0x566E,
            0x566F,
            0x5670,
            0x5671,
            0x5672,
            0x5673,
            0x5674,
            0x5675,
            0x5676,
            0x5677,
            0x5758,
            0x5759,
            0x575A,
            0x575B,
            0x575C,
            0x575D,
            0x575E,
            0x575F,
            0x5760,
            0x5761,
            0x5762,
            0x5763,
            0x5764,
            0x5765,
            0x5766,
            0x5767,
            0x5768,
            0x5769,
            0x576A,
            0x576B,
            0x576C,
            0x576D,
            0x576E,
            0x576F,
            0x5770,
            0x5771,
            0x5772,
            0x5773,
            0x5774,
            0x5775,
            0x5776,
            0x5777,
            0x5858,
            0x5859,
            0x585A,
            0x585B,
            0x585C,
            0x585D,
            0x585E,
            0x585F,
            0x5860,
            0x5861,
            0x5862,
            0x5863,
            0x5864,
            0x5865,
            0x5866,
            0x5867,
            0x5868,
            0x5869,
            0x586A,
            0x586B,
            0x586C,
            0x586D,
            0x586E,
            0x586F,
            0x5870,
            0x5871,
            0x5872,
            0x5873,
            0x5874,
            0x5875,
            0x5876,
            0x5877,
            0x5958,
            0x5959,
            0x595A,
            0x595B,
            0x595C,
            0x595D,
            0x595E,
            0x595F,
            0x5960,
            0x5961,
            0x5962,
            0x5963,
            0x5964,
            0x5965,
            0x5966,
            0x5967,
            0x5968,
            0x5969,
            0x596A,
            0x596B,
            0x596C,
            0x596D,
            0x596E,
            0x596F,
            0x5970,
            0x5971,
            0x5972,
            0x5973,
            0x5974,
            0x5975,
            0x5976,
            0x5977,
            0x5A58,
            0x5A59,
            0x5A5A,
            0x5A5B,
            0x5A5C,
            0x5A5D,
            0x5A5E,
            0x5A5F,
            0x5A60,
            0x5A61,
            0x5A62,
            0x5A63,
            0x5A64,
            0x5A65,
            0x5A66,
            0x5A67,
            0x5A68,
            0x5A69,
            0x5A6A,
            0x5A6B,
            0x5A6C,
            0x5A6D,
            0x5A6E,
            0x5A6F,
            0x5A70,
            0x5A71,
            0x5A72,
            0x5A73,
            0x5A74,
            0x5A75,
            0x5A76,
            0x5A77,
            0x5B58,
            0x5B59,
            0x5B5A,
            0x5B5B,
            0x5B5C,
            0x5B5D,
            0x5B5E,
            0x5B5F,
            0x5B60,
            0x5B61,
            0x5B62,
            0x5B63,
            0x5B64,
            0x5B65,
            0x5B66,
            0x5B67,
            0x5B68,
            0x5B69,
            0x5B6A,
            0x5B6B,
            0x5B6C,
            0x5B6D,
            0x5B6E,
            0x5B6F,
            0x5B70,
            0x5B71,
            0x5B72,
            0x5B73,
            0x5B74,
            0x5B75,
            0x5B76,
            0x5B77,
            0x5C58,
            0x5C59,
            0x5C5A,
            0x5C5B,
            0x5C5C,
            0x5C5D,
            0x5C5E,
            0x5C5F,
            0x5C60,
            0x5C61,
            0x5C62,
            0x5C63,
            0x5C64,
            0x5C65,
            0x5C66,
            0x5C67,
            0x5C68,
            0x5C69,
            0x5C6A,
            0x5C6B,
            0x5C6C,
            0x5C6D,
            0x5C6E,
            0x5C6F,
            0x5C70,
            0x5C71,
            0x5C72,
            0x5C73,
            0x5C74,
            0x5C75,
            0x5C76,
            0x5C77,
            0x5D58,
            0x5D59,
            0x5D5A,
            0x5D5B,
            0x5D5C,
            0x5D5D,
            0x5D5E,
            0x5D5F,
            0x5D60,
            0x5D61,
            0x5D62,
            0x5D63,
            0x5D64,
            0x5D65,
            0x5D66,
            0x5D67,
            0x5D68,
            0x5D69,
            0x5D6A,
            0x5D6B,
            0x5D6C,
            0x5D6D,
            0x5D6E,
            0x5D6F,
            0x5D70,
            0x5D71,
            0x5D72,
            0x5D73,
            0x5D74,
            0x5D75,
            0x5D76,
            0x5D77,
            0x5E58,
            0x5E59,
            0x5E5A,
            0x5E5B,
            0x5E5C,
            0x5E5D,
            0x5E5E,
            0x5E5F,
            0x5E60,
            0x5E61,
            0x5E62,
            0x5E63,
            0x5E64,
            0x5E65,
            0x5E66,
            0x5E67,
            0x5E68,
            0x5E69,
            0x5E6A,
            0x5E6B,
            0x5E6C,
            0x5E6D,
            0x5E6E,
            0x5E6F,
            0x5E70,
            0x5E71,
            0x5E72,
            0x5E73,
            0x5E74,
            0x5E75,
            0x5E76,
            0x5E77,
            0x5F58,
            0x5F59,
            0x5F5A,
            0x5F5B,
            0x5F5C,
            0x5F5D,
            0x5F5E,
            0x5F5F,
            0x5F60,
            0x5F61,
            0x5F62,
            0x5F63,
            0x5F64,
            0x5F65,
            0x5F66,
            0x5F67,
            0x5F68,
            0x5F69,
            0x5F6A,
            0x5F6B,
            0x5F6C,
            0x5F6D,
            0x5F6E,
            0x5F6F,
            0x5F70,
            0x5F71,
            0x5F72,
            0x5F73,
            0x5F74,
            0x5F75,
            0x5F76,
            0x5F77,
            0x6058,
            0x6059,
            0x605A,
            0x605B,
            0x605C,
            0x605D,
            0x605E,
            0x605F,
            0x6060,
            0x6061,
            0x6062,
            0x6063,
            0x6064,
            0x6065,
            0x6066,
            0x6067,
            0x6068,
            0x6069,
            0x606A,
            0x606B,
            0x606C,
            0x606D,
            0x606E,
            0x606F,
            0x6070,
            0x6071,
            0x6072,
            0x6073,
            0x6074,
            0x6075,
            0x6076,
            0x6077,
            0x6158,
            0x6159,
            0x615A,
            0x615B,
            0x615C,
            0x615D,
            0x615E,
            0x615F,
            0x6160,
            0x6161,
            0x6162,
            0x6163,
            0x6164,
            0x6165,
            0x6166,
            0x6167,
            0x6168,
            0x6169,
            0x616A,
            0x616B,
            0x616C,
            0x616D,
            0x616E,
            0x616F,
            0x6170,
            0x6171,
            0x6172,
            0x6173,
            0x6174,
            0x6175,
            0x6176,
            0x6177,
            0x6258,
            0x6259,
            0x625A,
            0x625B,
            0x625C,
            0x625D,
            0x625E,
            0x625F,
            0x6260,
            0x6261,
            0x6262,
            0x6263,
            0x6264,
            0x6265,
            0x6266,
            0x6267,
            0x6268,
            0x6269,
            0x626A,
            0x626B,
            0x626C,
            0x626D,
            0x626E,
            0x626F,
            0x6270,
            0x6271,
            0x6272,
            0x6273,
            0x6274,
            0x6275,
            0x6276,
            0x6277,
            0x6358,
            0x6359,
            0x635A,
            0x635B,
            0x635C,
            0x635D,
            0x635E,
            0x635F,
            0x6360,
            0x6361,
            0x6362,
            0x6363,
            0x6364,
            0x6365,
            0x6366,
            0x6367,
            0x6368,
            0x6369,
            0x636A,
            0x636B,
            0x636C,
            0x636D,
            0x636E,
            0x636F,
            0x6370,
            0x6371,
            0x6372,
            0x6373,
            0x6374,
            0x6375,
            0x6376,
            0x6377,
            0x6458,
            0x6459,
            0x645A,
            0x645B,
            0x645C,
            0x645D,
            0x645E,
            0x645F,
            0x6460,
            0x6461,
            0x6462,
            0x6463,
            0x6464,
            0x6465,
            0x6466,
            0x6467,
            0x6468,
            0x6469,
            0x646A,
            0x646B,
            0x646C,
            0x646D,
            0x646E,
            0x646F,
            0x6470,
            0x6471,
            0x6472,
            0x6473,
            0x6474,
            0x6475,
            0x6476,
            0x6477,
        };

        public static HashSet<ushort> PKLandblock_Landblocks = new HashSet<ushort>()
        {
            0x5558,
            0x5559,
            0x555A,
            0x555B,
            0x555C,
            0x555D,
            0x555E,
            0x555F,
            0x5560,
            0x5561,
            0x5562,
            0x5563,
            0x5564,
            0x5565,
            0x5566,
            0x5567,
            0x5568,
            0x5569,
            0x556A,
            0x556B,
            0x556C,
            0x556D,
            0x556E,
            0x556F,
            0x5570,
            0x5571,
            0x5572,
            0x5573,
            0x5574,
            0x5575,
            0x5576,
            0x5577,
            0x5658,
            0x5659,
            0x565A,
            0x565B,
            0x565C,
            0x565D,
            0x565E,
            0x565F,
            0x5660,
            0x5661,
            0x5662,
            0x5663,
            0x5664,
            0x5665,
            0x5666,
            0x5667,
            0x5668,
            0x5669,
            0x566A,
            0x566B,
            0x566C,
            0x566D,
            0x566E,
            0x566F,
            0x5670,
            0x5671,
            0x5672,
            0x5673,
            0x5674,
            0x5675,
            0x5676,
            0x5677,
            0x5758,
            0x5759,
            0x575A,
            0x575B,
            0x575C,
            0x575D,
            0x575E,
            0x575F,
            0x5760,
            0x5761,
            0x5762,
            0x5763,
            0x5764,
            0x5765,
            0x5766,
            0x5767,
            0x5768,
            0x5769,
            0x576A,
            0x576B,
            0x576C,
            0x576D,
            0x576E,
            0x576F,
            0x5770,
            0x5771,
            0x5772,
            0x5773,
            0x5774,
            0x5775,
            0x5776,
            0x5777,
            0x5858,
            0x5859,
            0x585A,
            0x585B,
            0x585C,
            0x585D,
            0x585E,
            0x585F,
            0x5860,
            0x5861,
            0x5862,
            0x5863,
            0x5864,
            0x5865,
            0x5866,
            0x5867,
            0x5868,
            0x5869,
            0x586A,
            0x586B,
            0x586C,
            0x586D,
            0x586E,
            0x586F,
            0x5870,
            0x5871,
            0x5872,
            0x5873,
            0x5874,
            0x5875,
            0x5876,
            0x5877,
            0x5958,
            0x5959,
            0x595A,
            0x595B,
            0x595C,
            0x595D,
            0x595E,
            0x595F,
            0x5960,
            0x5961,
            0x5962,
            0x5963,
            0x5964,
            0x5965,
            0x5966,
            0x5967,
            0x5968,
            0x5969,
            0x596A,
            0x596B,
            0x596C,
            0x596D,
            0x596E,
            0x596F,
            0x5970,
            0x5971,
            0x5972,
            0x5973,
            0x5974,
            0x5975,
            0x5976,
            0x5977,
            0x5A58,
            0x5A59,
            0x5A5A,
            0x5A5B,
            0x5A5C,
            0x5A5D,
            0x5A5E,
            0x5A5F,
            0x5A60,
            0x5A61,
            0x5A62,
            0x5A63,
            0x5A64,
            0x5A65,
            0x5A66,
            0x5A67,
            0x5A68,
            0x5A69,
            0x5A6A,
            0x5A6B,
            0x5A6C,
            0x5A6D,
            0x5A6E,
            0x5A6F,
            0x5A70,
            0x5A71,
            0x5A72,
            0x5A73,
            0x5A74,
            0x5A75,
            0x5A76,
            0x5A77,
            0x5B58,
            0x5B59,
            0x5B5A,
            0x5B5B,
            0x5B5C,
            0x5B5D,
            0x5B5E,
            0x5B5F,
            0x5B60,
            0x5B61,
            0x5B62,
            0x5B63,
            0x5B64,
            0x5B65,
            0x5B66,
            0x5B67,
            0x5B68,
            0x5B69,
            0x5B6A,
            0x5B6B,
            0x5B6C,
            0x5B6D,
            0x5B6E,
            0x5B6F,
            0x5B70,
            0x5B71,
            0x5B72,
            0x5B73,
            0x5B74,
            0x5B75,
            0x5B76,
            0x5B77,
            0x5C58,
            0x5C59,
            0x5C5A,
            0x5C5B,
            0x5C5C,
            0x5C5D,
            0x5C5E,
            0x5C5F,
            0x5C60,
            0x5C61,
            0x5C62,
            0x5C63,
            0x5C64,
            0x5C65,
            0x5C66,
            0x5C67,
            0x5C68,
            0x5C69,
            0x5C6A,
            0x5C6B,
            0x5C6C,
            0x5C6D,
            0x5C6E,
            0x5C6F,
            0x5C70,
            0x5C71,
            0x5C72,
            0x5C73,
            0x5C74,
            0x5C75,
            0x5C76,
            0x5C77,
            0x5D58,
            0x5D59,
            0x5D5A,
            0x5D5B,
            0x5D5C,
            0x5D5D,
            0x5D5E,
            0x5D5F,
            0x5D60,
            0x5D61,
            0x5D62,
            0x5D63,
            0x5D64,
            0x5D65,
            0x5D66,
            0x5D67,
            0x5D68,
            0x5D69,
            0x5D6A,
            0x5D6B,
            0x5D6C,
            0x5D6D,
            0x5D6E,
            0x5D6F,
            0x5D70,
            0x5D71,
            0x5D72,
            0x5D73,
            0x5D74,
            0x5D75,
            0x5D76,
            0x5D77,
            0x5E58,
            0x5E59,
            0x5E5A,
            0x5E5B,
            0x5E5C,
            0x5E5D,
            0x5E5E,
            0x5E5F,
            0x5E60,
            0x5E61,
            0x5E62,
            0x5E63,
            0x5E64,
            0x5E65,
            0x5E66,
            0x5E67,
            0x5E68,
            0x5E69,
            0x5E6A,
            0x5E6B,
            0x5E6C,
            0x5E6D,
            0x5E6E,
            0x5E6F,
            0x5E70,
            0x5E71,
            0x5E72,
            0x5E73,
            0x5E74,
            0x5E75,
            0x5E76,
            0x5E77,
            0x5F58,
            0x5F59,
            0x5F5A,
            0x5F5B,
            0x5F5C,
            0x5F5D,
            0x5F5E,
            0x5F5F,
            0x5F60,
            0x5F61,
            0x5F62,
            0x5F63,
            0x5F64,
            0x5F65,
            0x5F66,
            0x5F67,
            0x5F68,
            0x5F69,
            0x5F6A,
            0x5F6B,
            0x5F6C,
            0x5F6D,
            0x5F6E,
            0x5F6F,
            0x5F70,
            0x5F71,
            0x5F72,
            0x5F73,
            0x5F74,
            0x5F75,
            0x5F76,
            0x5F77,
            0x6058,
            0x6059,
            0x605A,
            0x605B,
            0x605C,
            0x605D,
            0x605E,
            0x605F,
            0x6060,
            0x6061,
            0x6062,
            0x6063,
            0x6064,
            0x6065,
            0x6066,
            0x6067,
            0x6068,
            0x6069,
            0x606A,
            0x606B,
            0x606C,
            0x606D,
            0x606E,
            0x606F,
            0x6070,
            0x6071,
            0x6072,
            0x6073,
            0x6074,
            0x6075,
            0x6076,
            0x6077,
            0x6158,
            0x6159,
            0x615A,
            0x615B,
            0x615C,
            0x615D,
            0x615E,
            0x615F,
            0x6160,
            0x6161,
            0x6162,
            0x6163,
            0x6164,
            0x6165,
            0x6166,
            0x6167,
            0x6168,
            0x6169,
            0x616A,
            0x616B,
            0x616C,
            0x616D,
            0x616E,
            0x616F,
            0x6170,
            0x6171,
            0x6172,
            0x6173,
            0x6174,
            0x6175,
            0x6176,
            0x6177,
            0x6258,
            0x6259,
            0x625A,
            0x625B,
            0x625C,
            0x625D,
            0x625E,
            0x625F,
            0x6260,
            0x6261,
            0x6262,
            0x6263,
            0x6264,
            0x6265,
            0x6266,
            0x6267,
            0x6268,
            0x6269,
            0x626A,
            0x626B,
            0x626C,
            0x626D,
            0x626E,
            0x626F,
            0x6270,
            0x6271,
            0x6272,
            0x6273,
            0x6274,
            0x6275,
            0x6276,
            0x6277,
            0x6358,
            0x6359,
            0x635A,
            0x635B,
            0x635C,
            0x635D,
            0x635E,
            0x635F,
            0x6360,
            0x6361,
            0x6362,
            0x6363,
            0x6364,
            0x6365,
            0x6366,
            0x6367,
            0x6368,
            0x6369,
            0x636A,
            0x636B,
            0x636C,
            0x636D,
            0x636E,
            0x636F,
            0x6370,
            0x6371,
            0x6372,
            0x6373,
            0x6374,
            0x6375,
            0x6376,
            0x6377,
            0x6458,
            0x6459,
            0x645A,
            0x645B,
            0x645C,
            0x645D,
            0x645E,
            0x645F,
            0x6460,
            0x6461,
            0x6462,
            0x6463,
            0x6464,
            0x6465,
            0x6466,
            0x6467,
            0x6468,
            0x6469,
            0x646A,
            0x646B,
            0x646C,
            0x646D,
            0x646E,
            0x646F,
            0x6470,
            0x6471,
            0x6472,
            0x6473,
            0x6474,
            0x6475,
            0x6476,
            0x6477,
        };

        public static HashSet<ushort> SpeedRunLandblock_Landblocks = new HashSet<ushort>()
        {
            0x9204,
            0x9203,
            0x9202,
        };
    }
}
