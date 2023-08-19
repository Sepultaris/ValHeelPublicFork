using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Command.Handlers;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Sequence;
using ACE.Server.Network.Structure;
using ACE.Server.Physics;
using ACE.Server.Physics.Common;
using ACE.Server.ValheelMods;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        private readonly ActionQueue actionQueue = new ActionQueue();

        private int initialAge;
        private DateTime initialAgeTime;

        private const double ageUpdateInterval = 7;
        private double nextAgeUpdateTime;

        private double houseRentWarnTimestamp;
        private const double houseRentWarnInterval = 3600;
        private ObjCell CurCell;

        public List<Creature> CombatPets = new List<Creature>();

        public void Player_Tick(double currentUnixTime)
        {
            actionQueue.RunActions();

            if (nextAgeUpdateTime <= currentUnixTime)
            {
                nextAgeUpdateTime = currentUnixTime + ageUpdateInterval;

                if (initialAgeTime == DateTime.MinValue)
                {
                    initialAge = Age ?? 1;
                    initialAgeTime = DateTime.UtcNow;
                }

                Age = initialAge + (int)(DateTime.UtcNow - initialAgeTime).TotalSeconds;

                // In retail, this is sent every 7 seconds. If you adjust ageUpdateInterval from 7, you'll need to re-add logic to send this every 7s (if you want to match retail)
                Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.Age, Age ?? 1));
            }

            if (FellowVitalUpdate && Fellowship != null)
            {
                Fellowship.OnVitalUpdate(this);
                FellowVitalUpdate = false;
            }

            if (House != null && PropertyManager.GetBool("house_rent_enabled").Item)
            {
                if (houseRentWarnTimestamp > 0 && currentUnixTime > houseRentWarnTimestamp)
                {
                    HouseManager.GetHouse(House.Guid.Full, (house) =>
                    {
                        if (house != null && !house.SlumLord.IsRentPaid())
                            Session.Network.EnqueueSend(new GameMessageSystemChat($"Warning!  You have not paid your maintenance costs for the last {(house.IsApartment ? "90" : "30")} day maintenance period.  Please pay these costs by this deadline or you will lose your house, and all your items within it.", ChatMessageType.Broadcast));
                    });

                    houseRentWarnTimestamp = Time.GetFutureUnixTime(houseRentWarnInterval);
                }
                else if (houseRentWarnTimestamp == 0)
                    houseRentWarnTimestamp = Time.GetFutureUnixTime(houseRentWarnInterval);
            }
        }
        public static void CalculatePlayerAge(Player player, double currentUnixTime)
        {
            var dob = player.GetProperty(PropertyString.DateOfBirth);

            DateTime currentDate = DateTime.Now;

            DateTime dateFromString = DateTime.Parse(dob);

            player.SetProperty(PropertyString.DateOfBirth, $"{dateFromString}");

            // Calculate the age
            TimeSpan ageSpan = DateTime.Now - dateFromString;
            int years = (int)(ageSpan.Days / 365.25);
            int months = (int)((ageSpan.Days % 365.25) / 30.44);
            int days = ageSpan.Days % 30;
            int hours = ageSpan.Hours;
            int minutes = ageSpan.Minutes;

            // Format the age
            string formattedAge = $"{years:00}:{months:00}:{days:00}:{hours:00}:{minutes:00}";

            // I used this to monitor the output on the console so I didn't have to log in 
            //Console.WriteLine($"Age as YY:MM:DD:HH:MM format: {formattedAge}");

            player.HcAge = formattedAge;
        }

        public static void CalculateHcPlayerAgeTimestamp(Player player, double currentUnxiTime)
        {
            var dob = player.GetProperty(PropertyString.DateOfBirth);

            DateTime dateFromString = DateTime.Parse(dob);
            long originalUnixTimestamp = ((DateTimeOffset)dateFromString).ToUnixTimeSeconds();

            DateTime currentDate = DateTime.Now.ToUniversalTime();
            long currentUnixTimestamp = ((DateTimeOffset)currentDate).ToUnixTimeSeconds();

            long ageInSeconds = currentUnixTimestamp - originalUnixTimestamp;

            player.HcAgeTimestamp = ageInSeconds;
        }

        public static void HcScoreCalculator(IPlayer player, long value)
        {
            if (player != null)
            {
                const long maxValue = 500_000_000;
                ulong pyrealsWon = player.HcPyrealsWon;
                float pyrealMultiplier = (float)(Math.Round((float)pyrealsWon + 1.0f) / (float)maxValue);

                if (pyrealMultiplier < 0.01)
                    pyrealMultiplier = 0.01f;

                long baseScore = (long)((player.Level * player.CreatureKills) + ((player.HcAgeTimestamp /60) /60));

                float score = (float)(baseScore + (baseScore * pyrealMultiplier) - ((player.HcAgeTimestamp / 60) / 60)) / 1000;

                if (score < 0)
                    score = 0;

                    player.HcScore = (long)Math.Round(score);
            }
        }

        public static void PlayerAcheivements(Player player)
        {
            if (!player.Hardcore)
                return;


        }

        private static readonly TimeSpan MaximumTeleportTime = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Called every ~5 seconds for Players
        /// </summary>
        public override void Heartbeat(double currentUnixTime)
        {
            NotifyLandblocks();

            ManaConsumersTick();

            HandleTargetVitals();

            LifestoneProtectionTick();

            PK_DeathTick();

            GagsTick();

            if (ValHeelBounty.HasBountyCheck(Name))
            {
                if (PriceOnHead == null)
                    PriceOnHead = 0;

                if (PriceOnHead > 1000000)
                {
                    PlayerKillerStatus = PlayerKillerStatus.PK;
                    Session.Player.EnqueueBroadcast(new GameMessagePublicUpdatePropertyInt(this, PropertyInt.PlayerKillerStatus, (int)PlayerKillerStatus));
                    CommandHandlerHelper.WriteOutputInfo(Session, $"WARNING!: Your bounty has become too high and you are now a wanted player.", ChatMessageType.Broadcast);
                }
            }

            if (Hardcore)
            {
                CalculatePlayerAge(this, currentUnixTime);

                CalculateHcPlayerAgeTimestamp(this, currentUnixTime);

                HcAchievementSystem.CheckAndHandleAchievements(this);

                HcScoreCalculator(this, (long)HcPyrealsWon);
            }

            PhysicsObj.ObjMaint.DestroyObjects();

            Player player = Session.Player;

            var session = Session;

            if (IsOnPKLandblock)
            {
                var pkStatus = player.PlayerKillerStatus;

                if (pkStatus == PlayerKillerStatus.NPK)
                {
                    player.PlaySoundEffect(Sound.UI_Bell, player.Guid, 1.0f);
                    player.PlayerKillerStatus = PlayerKillerStatus.PK;
                    session.Player.EnqueueBroadcast(new GameMessagePublicUpdatePropertyInt(session.Player, PropertyInt.PlayerKillerStatus, (int)session.Player.PlayerKillerStatus));
                    CommandHandlerHelper.WriteOutputInfo(session, $"WARNING:You have entered a Player Killer area: {session.Player.PlayerKillerStatus.ToString()}", ChatMessageType.Broadcast);

                    async Task RunActionChainAsync()
                    {
                        int randomNum = new Random().Next(1, 7);

                        switch (randomNum)
                        {
                            case 1:
                                LandblockManager.DoEnvironChange(EnvironChangeType.Thunder1Sound);
                                break;
                            case 2:
                                LandblockManager.DoEnvironChange(EnvironChangeType.Thunder2Sound);
                                break;
                            case 3:
                                LandblockManager.DoEnvironChange(EnvironChangeType.Thunder3Sound);
                                break;
                            case 4:
                                LandblockManager.DoEnvironChange(EnvironChangeType.Thunder4Sound);
                                break;
                            case 5:
                                LandblockManager.DoEnvironChange(EnvironChangeType.Thunder5Sound);
                                break;
                            case 6:
                                LandblockManager.DoEnvironChange(EnvironChangeType.Thunder6Sound);
                                break;
                            default:
                                break;
                        }

                        await Task.Delay(1000); // 1 second delay

                        int randomNum2 = new Random().Next(1, 7);

                        switch (randomNum2)
                        {
                            case 1:
                                LandblockManager.DoEnvironChange(EnvironChangeType.RoarSound);
                                break;
                            case 2:
                                LandblockManager.DoEnvironChange(EnvironChangeType.Chant2Sound);
                                break;
                            case 3:
                                LandblockManager.DoEnvironChange(EnvironChangeType.Chant1Sound);
                                break;
                            case 4:
                                LandblockManager.DoEnvironChange(EnvironChangeType.LostSoulsSound);
                                break;
                            case 5:
                                LandblockManager.DoEnvironChange(EnvironChangeType.DarkWhispers1Sound);
                                break;
                            case 6:
                                LandblockManager.DoEnvironChange(EnvironChangeType.DarkWindSound);
                                break;
                            default:
                                break;
                        }
                    }
                    _ = RunActionChainAsync();

                    ApplyVisualEffects(PlayScript.VisionDownBlack);
                    ApplyVisualEffects(PlayScript.BaelZharonSmite);
                }
            }
            else if (!IsOnPKLandblock)
            {
                var pkStatus = player.PlayerKillerStatus;

                if (pkStatus == PlayerKillerStatus.PK && !PKTimerActive && PriceOnHead < 1000000)
                {
                    player.PlaySoundEffect(Sound.UI_Bell, player.Guid, 1.0f);
                    player.PlayerKillerStatus = PlayerKillerStatus.NPK;
                    session.Player.EnqueueBroadcast(new GameMessagePublicUpdatePropertyInt(session.Player, PropertyInt.PlayerKillerStatus, (int)session.Player.PlayerKillerStatus));
                    CommandHandlerHelper.WriteOutputInfo(session, $"WARNING:You have exited a Player Killer area: {session.Player.PlayerKillerStatus.ToString()}", ChatMessageType.Broadcast);

                    ApplyVisualEffects(PlayScript.VisionUpWhite);
                    ApplyVisualEffects(PlayScript.RestrictionEffectBlue);
                }
            }

            if (IsOnSpeedRunLandblock)
            {               
                if (player.SpeedRunning == false)
                {
                    HandleSpeedRun(player, currentUnixTime);
                }
                if (player.SpeedRunning == true)
                {
                    HandleSpeedRun(player, currentUnixTime);
                }
            }
            else
            {
                if (player.SpeedRunning == true)
                {
                    HandleSpeedRun(player, currentUnixTime);
                }
            }

            // Check if we're due for our periodic SavePlayer
            if (LastRequestedDatabaseSave == DateTime.MinValue)
                LastRequestedDatabaseSave = DateTime.UtcNow;

            if (LastRequestedDatabaseSave.AddSeconds(PlayerSaveIntervalSecs) <= DateTime.UtcNow)
                SavePlayerToDatabase();

            if (Teleporting && DateTime.UtcNow > Time.GetDateTimeFromTimestamp(LastTeleportStartTimestamp ?? 0).Add(MaximumTeleportTime))
            {
                if (Session != null)
                    Session.LogOffPlayer(true);
                else
                    LogOut();
            }

            if (player.BankAccountNumber != null)
            Player_Bank.HandleInterestPayments(player);

            /*var visibleCreatureList = PhysicsObj.ObjMaint.GetVisibleObjectsValuesOfTypeCreature();

            foreach (var creature in visibleCreatureList)
            {
                if (creature != null && creature.IsCombatPet && creature.PetOwner == player.Guid.Full && !CombatPets.Contains(creature))
                {
                    CombatPets.Add(creature);
                    player.NumberOfPets = CombatPets.Count;
                }
            }
            
            for (int i = CombatPets.Count - 1; i >= 0; i--)
            {
                var combatPet = CombatPets[i];
                if (!visibleCreatureList.Contains(combatPet))
                {
                    CombatPets.RemoveAt(i);
                    player.NumberOfPets = CombatPets.Count;
                }
            }

            if (visibleCreatureList.Count > 4)
            {
                if (CombatPets.Count > 0)
                {
                    var combatPetToRemove = CombatPets[0];
                    combatPetToRemove.Die();
                    CombatPets.RemoveAt(0);
                }
            }*/

            base.Heartbeat(currentUnixTime);

            MonitorCombatPets(CombatPets, player);
        }

        public void MonitorCombatPets(List<Creature> CombatPets, Player player)
        {
            var session = Session;
            var visibleCreatures = PhysicsObj.ObjMaint.GetVisibleObjectsValuesOfTypeCreature();

            // Remove combat pets that are no longer visible and decrease player's pet count
            for (int i = CombatPets.Count - 1; i >= 0; i--)
            {
                var combatPet = CombatPets[i];
                if (!visibleCreatures.Contains(combatPet))
                {
                    CombatPets.RemoveAt(i);
                    player.NumberOfPets = CombatPets.Count;
                    combatPet.Die();
                    /*session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.x1B));
                    session.Network.EnqueueSend(new GameMessageSystemChat($"A pet was removed {CombatPets.Count}.", ChatMessageType.x1B));
                    session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.x1B));*/
                }
            }

            // Remove the first combat pet if more than 3 combat pets are visible
            if (visibleCreatures.Count(c => c.IsCombatPet && c.PetOwner == player.Guid.Full) > 3)
            {
                if (CombatPets.Count > 0)
                {
                    var oldPetCount = player.NumberOfPets;
                    var combatPetToRemove = CombatPets[0];
                    CombatPets.RemoveAt(0);
                    player.NumberOfPets = CombatPets.Count;
                    combatPetToRemove.Die();
                    /*session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.x1B));
                    session.Network.EnqueueSend(new GameMessageSystemChat($"You have too many pets! Killing the oldest! {oldPetCount} old count / {CombatPets.Count} new count.", ChatMessageType.x1B));
                    session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.x1B));*/
                }
            }

            // Add new combat pets to the list and increase player's pet count
            for (int i = 0; i < visibleCreatures.Count; i++)
            {
                var creature = visibleCreatures[i];

                if (creature != null && creature.IsCombatPet && creature.PetOwner == player.Guid.Full && !CombatPets.Contains(creature))
                {
                    CombatPets.Add(creature);
                    player.NumberOfPets = CombatPets.Count;
                    /*session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.x1B));
                    session.Network.EnqueueSend(new GameMessageSystemChat($"A pet was added {CombatPets.Count}.", ChatMessageType.x1B));
                    session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.x1B));*/
                }
                if (visibleCreatures.Count(c => c.IsCombatPet && c.PetOwner == player.Guid.Full) > 3 && NumberOfPets > 3)
                {
                    if (CombatPets.Count > 0)
                    {
                        var oldPetCount = player.NumberOfPets;
                        var combatPetToRemove = CombatPets[0];
                        CombatPets.RemoveAt(0);
                        player.NumberOfPets = CombatPets.Count;
                        combatPetToRemove.Die();
                        /*session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.x1B));
                        session.Network.EnqueueSend(new GameMessageSystemChat($"You have too many pets! Killing the oldest! {oldPetCount} old count / {CombatPets.Count} new count.", ChatMessageType.x1B));
                        session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.x1B));*/
                    }
                }
            }
        }

        public static void HandleSpeedRun(Player player, double? currentUnixTime)
        {
            if (player.SpeedRunning == false)
            {
                if (player.LastTime == null)
                {
                    player.LastTime = 0;
                }
                if (player.BestTime == null)
                {
                    player.BestTime = 0;
                }
                player.SpeedrunStartTime = currentUnixTime;
                player.PlaySoundEffect(Sound.UI_Bell, player.Guid, 1.0f);
                player.ApplyVisualEffects(PlayScript.VisionUpWhite);
                player.ApplyVisualEffects(PlayScript.RestrictionEffectBlue);
                player.SpeedRunning = true;
                CommandHandlerHelper.WriteOutputInfo(player.Session, $"Let the games begin! Good Luck!", ChatMessageType.Broadcast);
            }
            if (player.SpeedRunning == true && player.IsOnSpeedRunLandblock)
            {
                player.SpeedrunEndTime = currentUnixTime;
                var milTime = (double)(player.SpeedrunEndTime - player.SpeedrunStartTime);

                if(milTime >= 1200)
                {
                    if (player.Location.LandblockId.Landblock == 0x9204)
                    {
                        player.HandleActionTeleToLifestone();
                        player.LastTime = 0;
                        player.SpeedRunning = false;
                        CommandHandlerHelper.WriteOutputInfo(player.Session, $"Times Up!!! You've been disqualified!", ChatMessageType.Broadcast);
                        EventManager.StopEvent("SR135Active", player, null);
                        player.QuestManager.Erase("PrimordialMatronKilled");
                        player.QuestManager.Erase("PartOneDone");
                        player.QuestManager.Erase("PartTwoDone");
                        player.QuestManager.Erase("haroldinggemtimer");
                        player.QuestManager.Erase("GotTheGem");
                        player.QuestManager.Erase("RoomCleared");
                        player.QuestManager.Erase("SRRatKilled");
                        player.QuestManager.Erase("SRRatKilled2");
                        player.QuestManager.Erase("wildmushroompickup");
                    }
                    if (player.Location.LandblockId.Landblock == 0x9203)
                    {
                        player.HandleActionTeleToLifestone();
                        player.LastTime = 0;
                        player.SpeedRunning = false;
                        CommandHandlerHelper.WriteOutputInfo(player.Session, $"Times Up!!! You've been disqualified!", ChatMessageType.Broadcast);
                        EventManager.StopEvent("SR200Active", player, null);
                        player.QuestManager.Erase("PrimordialMatronKilled");
                        player.QuestManager.Erase("PartOneDone");
                        player.QuestManager.Erase("PartTwoDone");
                        player.QuestManager.Erase("haroldinggemtimer");
                        player.QuestManager.Erase("GotTheGem");
                        player.QuestManager.Erase("RoomCleared");
                        player.QuestManager.Erase("SRRatKilled");
                        player.QuestManager.Erase("SRRatKilled2");
                        player.QuestManager.Erase("wildmushroompickup");
                    }
                    if (player.Location.LandblockId.Landblock == 0x9202)
                    {
                        player.HandleActionTeleToLifestone();
                        player.LastTime = 0;
                        player.SpeedRunning = false;
                        CommandHandlerHelper.WriteOutputInfo(player.Session, $"Times Up!!! You've been disqualified!", ChatMessageType.Broadcast);
                        EventManager.StopEvent("SR300Active", player, null);
                        player.QuestManager.Erase("PrimordialMatronKilled");
                        player.QuestManager.Erase("PartOneDone");
                        player.QuestManager.Erase("PartTwoDone");
                        player.QuestManager.Erase("haroldinggemtimer");
                        player.QuestManager.Erase("GotTheGem");
                        player.QuestManager.Erase("RoomCleared");
                        player.QuestManager.Erase("SRRatKilled");
                        player.QuestManager.Erase("SRRatKilled2");
                        player.QuestManager.Erase("wildmushroompickup");
                    }
                }
            }
            if (player.SpeedRunning == true && !player.IsOnSpeedRunLandblock && player.QuestManager.HasQuest("PrimordialMatronKilled"))
            {
                player.SpeedrunEndTime = currentUnixTime;
                var milTime = (double)(player.SpeedrunEndTime - player.SpeedrunStartTime);
                                               
                player.PlaySoundEffect(Sound.UI_Bell, player.Guid, 1.0f);
                player.ApplyVisualEffects(PlayScript.VisionUpWhite);
                player.ApplyVisualEffects(PlayScript.RestrictionEffectBlue);
                var span = new TimeSpan(0, 0, (int)milTime); //Or TimeSpan.FromSeconds(seconds);
                var formatedTime = string.Format("{0}:{1:00}", (int)span.TotalMinutes, span.Seconds);                

                if (milTime < player.BestTime)
                {
                    player.BestTime = (int)milTime;
                    CommandHandlerHelper.WriteOutputInfo(player.Session, $"New Record!!!", ChatMessageType.Broadcast);
                    player.ApplyVisualEffects(PlayScript.WeddingBliss);
                }
                else if (milTime > player.BestTime)
                {
                    CommandHandlerHelper.WriteOutputInfo(player.Session, $"Your completion time is: {formatedTime}", ChatMessageType.Broadcast);
                    player.SpeedRunning = false;
                    player.SpeedRunTime = $"0";
                    player.LastTime = (int)milTime;

                    if (player.BestTime == 0)
                    {
                        player.BestTime = player.LastTime;
                    }
                }                
            }
            if (player.SpeedRunning == true && !player.IsOnSpeedRunLandblock && !player.QuestManager.HasQuest("PrimordialMatronKilled"))
            {               
                    player.LastTime = 0;
                    player.SpeedRunning = false;
                    CommandHandlerHelper.WriteOutputInfo(player.Session, $"You've been disqualified!", ChatMessageType.Broadcast);
                    player.QuestManager.Erase("PrimordialMatronKilled");
                    player.QuestManager.Erase("PartOneDone");
                    player.QuestManager.Erase("PartTwoDone");
                    player.QuestManager.Erase("haroldinggemtimer");
                    player.QuestManager.Erase("GotTheGem");
                    player.QuestManager.Erase("RoomCleared");
                    player.QuestManager.Erase("SRRatKilled");
                    player.QuestManager.Erase("SRRatKilled2");
                    player.QuestManager.Erase("wildmushroompickup");
            }
        }
        
        public static float MaxSpeed = 50;
        public static float MaxSpeedSq = MaxSpeed * MaxSpeed;

        public static bool DebugPlayerMoveToStatePhysics = false;

        /// <summary>
        /// Flag indicates if player is doing full physics simulation
        /// </summary>
        public bool FastTick => IsPKType;

        /// <summary>
        /// For advanced spellcasting / players glitching around during powersliding,
        /// the reason for this retail bug is from 2 different functions for player movement
        /// 
        /// The client's self-player uses DoMotion/StopMotion
        /// The server and other players on the client use apply_raw_movement
        ///
        /// When a 3+ button powerslide is performed, this bugs out apply_raw_movement,
        /// and causes the player to spin in place. With DoMotion/StopMotion, it performs a powerslide.
        ///
        /// With this option enabled (retail defaults to false), the player's position on the server
        /// will match up closely with the player's client during powerslides.
        ///
        /// Since the client uses apply_raw_movement to simulate the movement of nearby players,
        /// the other players will still glitch around on screen, even with this option enabled.
        ///
        /// If you wish for the positions of other players to be less glitchy, the 'MoveToState_UpdatePosition_Threshold'
        /// can be lowered to achieve that
        /// </summary>

        public void OnMoveToState(MoveToState moveToState)
        {
            if (!FastTick)
                return;

            if (DebugPlayerMoveToStatePhysics)
                Console.WriteLine(moveToState.RawMotionState);

            if (RecordCast.Enabled)
                RecordCast.OnMoveToState(moveToState);

            if (!PhysicsObj.IsMovingOrAnimating)
                PhysicsObj.UpdateTime = PhysicsTimer.CurrentTime;

            if (!PropertyManager.GetBool("client_movement_formula").Item || moveToState.StandingLongJump)
                OnMoveToState_ServerMethod(moveToState);
            else
                OnMoveToState_ClientMethod(moveToState);

            if (MagicState.IsCasting && MagicState.PendingTurnRelease && moveToState.RawMotionState.TurnCommand == 0)
                OnTurnRelease();
        }

        public void OnMoveToState_ClientMethod(MoveToState moveToState)
        {
            var rawState = moveToState.RawMotionState;
            var prevState = LastMoveToState?.RawMotionState ?? RawMotionState.None;

            var mvp = new Physics.Animation.MovementParameters();
            mvp.HoldKeyToApply = rawState.CurrentHoldKey;

            if (!PhysicsObj.IsMovingOrAnimating)
                PhysicsObj.UpdateTime = PhysicsTimer.CurrentTime;

            // ForwardCommand
            if (rawState.ForwardCommand != MotionCommand.Invalid)
            {
                // press new key
                if (prevState.ForwardCommand == MotionCommand.Invalid)
                {
                    PhysicsObj.DoMotion((uint)MotionCommand.Ready, mvp);
                    PhysicsObj.DoMotion((uint)rawState.ForwardCommand, mvp);
                }
                // press alternate key
                else if (prevState.ForwardCommand != rawState.ForwardCommand)
                {
                    PhysicsObj.DoMotion((uint)rawState.ForwardCommand, mvp);
                }
            }
            else if (prevState.ForwardCommand != MotionCommand.Invalid)
            {
                // release key
                PhysicsObj.StopMotion((uint)prevState.ForwardCommand, mvp, true);
            }

            // StrafeCommand
            if (rawState.SidestepCommand != MotionCommand.Invalid)
            {
                // press new key
                if (prevState.SidestepCommand == MotionCommand.Invalid)
                {
                    PhysicsObj.DoMotion((uint)rawState.SidestepCommand, mvp);
                }
                // press alternate key
                else if (prevState.SidestepCommand != rawState.SidestepCommand)
                {
                    PhysicsObj.DoMotion((uint)rawState.SidestepCommand, mvp);
                }
            }
            else if (prevState.SidestepCommand != MotionCommand.Invalid)
            {
                // release key
                PhysicsObj.StopMotion((uint)prevState.SidestepCommand, mvp, true);
            }

            // TurnCommand
            if (rawState.TurnCommand != MotionCommand.Invalid)
            {
                // press new key
                if (prevState.TurnCommand == MotionCommand.Invalid)
                {
                    PhysicsObj.DoMotion((uint)rawState.TurnCommand, mvp);
                }
                // press alternate key
                else if (prevState.TurnCommand != rawState.TurnCommand)
                {
                    PhysicsObj.DoMotion((uint)rawState.TurnCommand, mvp);
                }
            }
            else if (prevState.TurnCommand != MotionCommand.Invalid)
            {
                // release key
                PhysicsObj.StopMotion((uint)prevState.TurnCommand, mvp, true);
            }
        }

        public void OnMoveToState_ServerMethod(MoveToState moveToState)
        {
            var minterp = PhysicsObj.get_minterp();
            minterp.RawState.SetState(moveToState.RawMotionState);

            if (moveToState.StandingLongJump)
            {
                minterp.RawState.ForwardCommand = (uint)MotionCommand.Ready;
                minterp.RawState.SideStepCommand = 0;
            }

            var allowJump = minterp.motion_allows_jump(minterp.InterpretedState.ForwardCommand) == WeenieError.None;

            //PhysicsObj.cancel_moveto();

            minterp.apply_raw_movement(true, allowJump);
        }

        public bool InUpdate;

        public override bool UpdateObjectPhysics()
        {
            try
            {
                stopwatch.Restart();

                bool landblockUpdate = false;

                InUpdate = true;

                // update position through physics engine
                if (RequestedLocation != null)
                {
                    landblockUpdate = UpdatePlayerPosition(RequestedLocation);
                    RequestedLocation = null;
                }

                if (FastTick && PhysicsObj.IsMovingOrAnimating || PhysicsObj.Velocity != Vector3.Zero)
                {
                    UpdatePlayerPhysics();

                    if (MoveToParams?.Callback != null && !PhysicsObj.IsMovingOrAnimating)
                        HandleMoveToCallback();
                }

                InUpdate = false;

                return landblockUpdate;
            }
            finally
            {
                var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                ServerPerformanceMonitor.AddToCumulativeEvent(ServerPerformanceMonitor.CumulativeEventHistoryType.Player_Tick_UpdateObjectPhysics, elapsedSeconds);
                if (elapsedSeconds >= 1) // Yea, that ain't good....
                    log.Warn($"[PERFORMANCE][PHYSICS] {Guid}:{Name} took {(elapsedSeconds * 1000):N1} ms to process UpdateObjectPhysics() at loc: {Location}");
                else if (elapsedSeconds >= 0.010)
                    log.Debug($"[PERFORMANCE][PHYSICS] {Guid}:{Name} took {(elapsedSeconds * 1000):N1} ms to process UpdateObjectPhysics() at loc: {Location}");
            }
        }

        public void UpdatePlayerPhysics()
        {
            if (DebugPlayerMoveToStatePhysics)
                Console.WriteLine($"{Name}.UpdatePlayerPhysics({PhysicsObj.PartArray.Sequence.CurrAnim.Value.Anim.ID:X8})");

            //Console.WriteLine($"{PhysicsObj.Position.Frame.Origin}");
            //Console.WriteLine($"{PhysicsObj.Position.Frame.get_heading()}");

            PhysicsObj.update_object();

            // sync ace position?
            Location.Rotation = PhysicsObj.Position.Frame.Orientation;

            if (!FastTick) return;

            // ensure PKLogout position is synced up for other players
            if (PKLogout)
            {
                EnqueueBroadcast(new GameMessageUpdateMotion(this, new Motion(MotionStance.NonCombat, MotionCommand.Ready)));
                PhysicsObj.StopCompletely(true);

                if (!PhysicsObj.IsMovingOrAnimating)
                {
                    SyncLocation();
                    EnqueueBroadcast(new GameMessageUpdatePosition(this));
                }
            }

            // this fixes some differences between client movement (DoMotion/StopMotion) and server movement (apply_raw_movement)
            //
            // scenario: start casting a self-spell, and then immediately start holding the run forward key during the windup
            // on client: player will start running forward after the cast has completed
            // on server: player will stand still

            // this block of code can improve the sync between these 2 methods,
            // however there are some bugs that originate in acclient that cannot be resolved on the server
            // for example, equip a wand, and then start running forward in non-combat mode. switch to magic combat mode, and then release forward during the stance swap
            // the client will never send a 'client released forward' MoveToState in this scenario unfortunately.
            // because of this, it's better to have the 'client blip forward' bug without it, than to have the client invisibly running forward on the server.
            // commenting out this block because of this...

            /*if (!PhysicsObj.IsMovingOrAnimating && LastMoveToState != null)
            {
                // apply latest MoveToState, if applicable
                //if ((LastMoveToState.RawMotionState.Flags & (RawMotionFlags.ForwardCommand | RawMotionFlags.SideStepCommand | RawMotionFlags.TurnCommand)) != 0)
                if ((LastMoveToState.RawMotionState.Flags & RawMotionFlags.ForwardCommand) != 0 && LastMoveToState.RawMotionState.ForwardHoldKey == HoldKey.Invalid)
                {
                    if (DebugPlayerMoveToStatePhysics)
                        Console.WriteLine("Re-applying movement: " + LastMoveToState.RawMotionState.Flags);

                    OnMoveToState(LastMoveToState);

                    // re-broadcast MoveToState to other clients only
                    EnqueueBroadcast(false, new GameMessageUpdateMotion(this, CurrentMovementData));
                }
                LastMoveToState = null;
            }*/

            if (MagicState.IsCasting && MagicState.PendingTurnRelease)
                CheckTurn();
        }

        /// <summary>
        /// The maximum rate UpdatePosition packets from MoveToState will be broadcast for each player
        /// AutonomousPosition still always broadcasts UpdatePosition
        ///  
        /// The default value (1 second) was estimated from this retail video:
        /// https://youtu.be/o5lp7hWhtWQ?t=112
        /// 
        /// If you wish for players to glitch around less during powerslides, lower this value
        /// </summary>
        public static TimeSpan MoveToState_UpdatePosition_Threshold = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Used by physics engine to actually update a player position
        /// Automatically notifies clients of updated position
        /// </summary>
        /// <param name="newPosition">The new position being requested, before verification through physics engine</param>
        /// <returns>TRUE if object moves to a different landblock</returns>
        public bool UpdatePlayerPosition(ACE.Entity.Position newPosition, bool forceUpdate = false)
        {
            //Console.WriteLine($"{Name}.UpdatePlayerPhysics({newPosition}, {forceUpdate}, {Teleporting})");
            bool verifyContact = false;

            // possible bug: while teleporting, client can still send AutoPos packets from old landblock
            if (Teleporting && !forceUpdate) return false;

            // pre-validate movement
            if (!ValidateMovement(newPosition))
            {
                log.Error($"{Name}.UpdatePlayerPosition() - movement pre-validation failed from {Location} to {newPosition}");
                return false;
            }

            try
            {
                if (!forceUpdate) // This is needed beacuse this function might be called recursively
                    stopwatch.Restart();

                var success = true;

                if (PhysicsObj != null)
                {
                    var distSq = Location.SquaredDistanceTo(newPosition);

                    if (distSq > PhysicsGlobals.EpsilonSq)
                    {
                        /*var p = new Physics.Common.Position(newPosition);
                        var dist = PhysicsObj.Position.Distance(p);
                        Console.WriteLine($"Dist: {dist}");*/

                        if (newPosition.Landblock == 0x18A && Location.Landblock != 0x18A)
                            log.Info($"{Name} is getting swanky");

                        if (!Teleporting)
                        {
                            var blockDist = PhysicsObj.GetBlockDist(Location.Cell, newPosition.Cell);

                            // verify movement
                            if (distSq > MaxSpeedSq && blockDist > 1)
                            {
                                //Session.Network.EnqueueSend(new GameMessageSystemChat("Movement error", ChatMessageType.Broadcast));
                                log.Warn($"MOVEMENT SPEED: {Name} trying to move from {Location} to {newPosition}, speed: {Math.Sqrt(distSq)}");
                                return false;
                            }

                            // verify z-pos
                            if (blockDist == 0 && LastGroundPos != null && newPosition.PositionZ - LastGroundPos.PositionZ > 10 && DateTime.UtcNow - LastJumpTime > TimeSpan.FromSeconds(1) && GetCreatureSkill(Skill.Jump).Current < 1000)
                                verifyContact = true;
                        }

                        var curCell = LScape.get_landcell(newPosition.Cell);
                        if (curCell != null)
                        {
                            //if (PhysicsObj.CurCell == null || curCell.ID != PhysicsObj.CurCell.ID)
                                //PhysicsObj.change_cell_server(curCell);

                            PhysicsObj.set_request_pos(newPosition.Pos, newPosition.Rotation, curCell, Location.LandblockId.Raw);
                            if (FastTick)
                                success = PhysicsObj.update_object_server_new();
                            else
                                success = PhysicsObj.update_object_server();

                            if (PhysicsObj.CurCell == null && curCell.ID >> 16 != 0x18A)
                            {
                                PhysicsObj.CurCell = curCell;
                            }

                            if (verifyContact && IsJumping)
                            {
                                var blockDist = PhysicsObj.GetBlockDist(newPosition.Cell, LastGroundPos.Cell);

                                if (blockDist <= 1)
                                {
                                    log.Warn($"z-pos hacking detected for {Name}, lastGroundPos: {LastGroundPos.ToLOCString()} - requestPos: {newPosition.ToLOCString()}");
                                    Location = new ACE.Entity.Position(LastGroundPos);
                                    Sequences.GetNextSequence(SequenceType.ObjectForcePosition);
                                    SendUpdatePosition();
                                    return false;
                                }
                            }

                            CheckMonsters();
                        }
                    }
                    else
                        PhysicsObj.Position.Frame.Orientation = newPosition.Rotation;
                }

                // double update path: landblock physics update -> updateplayerphysics() -> update_object_server() -> Teleport() -> updateplayerphysics() -> return to end of original branch
                if (Teleporting && !forceUpdate) return true;

                if (!success) return false;

                var landblockUpdate = Location.Cell >> 16 != newPosition.Cell >> 16;

                Location = newPosition;

                if (RecordCast.Enabled)
                    RecordCast.Log($"CurPos: {Location.ToLOCString()}");

                if (RequestedLocationBroadcast || DateTime.UtcNow - LastUpdatePosition >= MoveToState_UpdatePosition_Threshold)
                    SendUpdatePosition();
                else
                    Session.Network.EnqueueSend(new GameMessageUpdatePosition(this));

                if (!InUpdate)
                    LandblockManager.RelocateObjectForPhysics(this, true);

                return landblockUpdate;
            }
            finally
            {
                if (!forceUpdate) // This is needed beacuse this function might be called recursively
                {
                    var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                    ServerPerformanceMonitor.AddToCumulativeEvent(ServerPerformanceMonitor.CumulativeEventHistoryType.Player_Tick_UpdateObjectPhysics, elapsedSeconds);
                    if (elapsedSeconds >= 0.100) // Yea, that ain't good....
                        log.Warn($"[PERFORMANCE][PHYSICS] {Guid}:{Name} took {(elapsedSeconds * 1000):N1} ms to process UpdatePlayerPosition() at loc: {Location}");
                    else if (elapsedSeconds >= 0.010)
                        log.Debug($"[PERFORMANCE][PHYSICS] {Guid}:{Name} took {(elapsedSeconds * 1000):N1} ms to process UpdatePlayerPosition() at loc: {Location}");
                }
            }
        }

        private static HashSet<uint> buggedCells = new HashSet<uint>()
        {
            0xD6990112,
            0xD599012C
        };

        public bool ValidateMovement(ACE.Entity.Position newPosition)
        {
            if (CurrentLandblock == null)
                return false;

            if (!Teleporting && Location.Landblock != newPosition.Cell >> 16)
            {
                if ((Location.Cell & 0xFFFF) >= 0x100 && (newPosition.Cell & 0xFFFF) >= 0x100)
                {
                    if (!buggedCells.Contains(Location.Cell) || !buggedCells.Contains(newPosition.Cell))
                        return false;
                }

                if (CurrentLandblock.IsDungeon)
                {
                    var destBlock = LScape.get_landblock(newPosition.Cell);
                    if (destBlock != null && destBlock.IsDungeon)
                        return false;
                }
            }
            return true;
        }


        public bool SyncLocationWithPhysics()
        {
            if (PhysicsObj.CurCell == null)
            {
                Console.WriteLine($"{Name}.SyncLocationWithPhysics(): CurCell is null!");
                return false;
            }

            var blockcell = PhysicsObj.Position.ObjCellID;
            var pos = PhysicsObj.Position.Frame.Origin;
            var rotate = PhysicsObj.Position.Frame.Orientation;

            var landblockUpdate = blockcell << 16 != CurrentLandblock.Id.Landblock;

            Location = new ACE.Entity.Position(blockcell, pos, rotate);

            return landblockUpdate;
        }

        private bool gagNoticeSent = false;

        public void GagsTick()
        {
            if (IsGagged)
            {
                if (!gagNoticeSent)
                {
                    SendGagNotice();
                    gagNoticeSent = true;
                }

                // check for gag expiration, if expired, remove gag.
                GagDuration -= CachedHeartbeatInterval;

                if (GagDuration <= 0)
                {
                    IsGagged = false;
                    GagTimestamp = 0;
                    GagDuration = 0;
                    SaveBiotaToDatabase();
                    SendUngagNotice();
                    gagNoticeSent = false;
                }
            }
        }

        /// <summary>
        /// Prepare new action to run on this player
        /// </summary>
        public override void EnqueueAction(IAction action)
        {
            actionQueue.EnqueueAction(action);
        }

        /// <summary>
        /// Called every ~5 secs for equipped mana consuming items
        /// </summary>
        public void ManaConsumersTick()
        {
            if (!EquippedObjectsLoaded) return;

            foreach (var item in EquippedObjects.Values)
            {
                if (!item.IsAffecting)
                    continue;

                if (item.ItemCurMana == null || item.ItemMaxMana == null || item.ManaRate == null)
                    continue;

                var burnRate = -item.ManaRate.Value;

                if (LumAugItemManaUsage != 0)
                    burnRate *= GetNegativeRatingMod(LumAugItemManaUsage * 5);

                item.ItemManaRateAccumulator += (float)(burnRate * CachedHeartbeatInterval);

                if (item.ItemManaRateAccumulator < 1)
                    continue;

                var manaToBurn = (int)Math.Floor(item.ItemManaRateAccumulator);

                if (manaToBurn > item.ItemCurMana)
                    manaToBurn = item.ItemCurMana.Value;

                item.ItemCurMana -= manaToBurn;

                item.ItemManaRateAccumulator -= manaToBurn;

                if (item.ItemCurMana > 0)
                    CheckLowMana(item, burnRate);
                else
                    HandleManaDepleted(item);
            }
        }

        private bool CheckLowMana(WorldObject item, double burnRate)
        {
            const int lowManaWarningSeconds = 120;

            var secondsUntilEmpty = item.ItemCurMana / burnRate;

            if (secondsUntilEmpty > lowManaWarningSeconds)
            {
                item.ItemManaDepletionMessage = false;
                return false;
            }
            if (!item.ItemManaDepletionMessage)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"Your {item.Name} is low on Mana.", ChatMessageType.Magic));
                item.ItemManaDepletionMessage = true;
            }
            return true;
        }

        private void HandleManaDepleted(WorldObject item)
        {
            var msg = new GameMessageSystemChat($"Your {item.Name} is out of Mana.", ChatMessageType.Magic);
            var sound = new GameMessageSound(Guid, Sound.ItemManaDepleted);
            Session.Network.EnqueueSend(msg, sound);

            // unsure if these messages / sounds were ever sent in retail,
            // or if it just purged the enchantments invisibly
            // doing a delay here to prevent 'SpellExpired' sounds from overlapping with 'ItemManaDepleted'
            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(2.0f);
            actionChain.AddAction(this, () =>
            {
                foreach (var spellId in item.Biota.GetKnownSpellsIds(item.BiotaDatabaseLock))
                    RemoveItemSpell(item, (uint)spellId);
            });
            actionChain.EnqueueChain();

            item.OnSpellsDeactivated();
        }

        public override void HandleMotionDone(uint motionID, bool success)
        {
            //Console.WriteLine($"{Name}.HandleMotionDone({(MotionCommand)motionID}, {success})");

            if (FastTick && MagicState.IsCasting)
                HandleMotionDone_Magic(motionID, success);
        }
    }
}
