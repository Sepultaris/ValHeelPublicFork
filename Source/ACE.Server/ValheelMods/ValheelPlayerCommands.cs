using System;
using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Entity.Enum.Properties;
using ACE.Server.DuskfallMods;
using ACE.Server.WorldObjects.Entity;
using log4net.Core;
using ACE.Entity;

namespace ACE.Server.Command.Handlers
{
    public static class ValheelPlayerCommands
    {
        [CommandHandler("reputation", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Refunds costs associated with /raise.")]
        public static void Reputation(Session session, params string[] parameters)
        {
            Player player = session.Player;

            if (!player.QuestManager.HasQuestCompletes("Reputation"))
            {
                return;
            }

            var numberOfSolves = player.QuestManager.GetCurrentSolves("Reputation");

            ChatPacket.SendServerMessage(session, $"You currently have {numberOfSolves} reputation.", ChatMessageType.Broadcast);

        }

        [CommandHandler("prestige", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Refunds costs associated with /raise.")]
        public static void Prestige(Session session, params string[] parameters)
        {
            Player player = session.Player;

            if (!player.QuestManager.HasQuest("Prestige") || !player.QuestManager.HasQuest("Reputation"))
            {
                return;
            }

            var prestige = player.QuestManager.GetCurrentSolves("Prestige");
            var reputation = player.QuestManager.GetCurrentSolves("Reputation");

            if (prestige >= 1 && prestige <= 9)
                player.QuestManager.Stamp("PrestigeComplete");
            if (prestige >= 10 && prestige <= 24)
                player.QuestManager.Stamp("PrestigeComplete10");
            if (prestige >= 25 && prestige <= 49)
                player.QuestManager.Stamp("PrestigeComplete25");
            if (prestige >= 50 && prestige <= 74)
                player.QuestManager.Stamp("PrestigeComplete50");
            if (prestige >= 75 && prestige <= 99)
                player.QuestManager.Stamp("PrestigeComplete75");
            if (prestige >= 100)
                player.QuestManager.Stamp("PrestigeComplete100");
            if (prestige >= 150)
                player.QuestManager.Stamp("PrestigeComplete150");

            ChatPacket.SendServerMessage(session, string.Format("You are currently Prestige level {0}.", prestige), ChatMessageType.Broadcast);
            return;                     
        }

        [CommandHandler("raiserefund", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 0, "Refunds costs associated with /raise.")]
        public static void HandleRaiseRefund(Session session, params string[] parameters)
        {
            ValheelRaise.RaiseRefundToPlayer(session.Player);
        }

        [CommandHandler("raise", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Allows you to raise attributes past maximum at the cost of 10 billion XP. Allows you to raise Luminance Augmentation for Damage Rating (Destruction), Damage Reduction (Invulnerability), Critical Damage (Glory) and Critical Damage Reduction (Temperance) and Max Health, Stamina, and Mana (Vitality) for the cost of 10,000,000 Luminance.", "")]
        public static void HandleRaise(Session session, params string[] parameters)
        {
            Player player = session.Player;
            if (parameters.Length < 1)
            {
                ChatPacket.SendServerMessage(session, "Usage: /raise <str/end/coord/quick/focus/self/hp/stam/mp/mana> (for 10 billion exp) <destruction/invulnerability/glory/temperance/vitality> (for 10,000,000 luminance) [number of points to purchase (default: 1)]", ChatMessageType.Broadcast);
                return;
            }
            int result = 1;
            if (parameters.Length > 1 && !int.TryParse(parameters[1], out result))
            {
                ChatPacket.SendServerMessage(session, "Invalid value, values must be valid integers", ChatMessageType.Broadcast);
                return;
            }
            if (result <= 0)
            {
                ChatPacket.SendServerMessage(session, "Invalid value, values must be valid integers", ChatMessageType.Broadcast);
                return;
            }
            parameters[0] = parameters[0].ToLower();
            if (parameters[0].Equals("hp"))
            {
                parameters[0] = "health";
            }
            if (parameters[0].Equals("health"))
            {
                if (player.Level < 500)
                {
                    for (int i = 0; i < result; i++)
                    {
                        if (10000000000L > player.AvailableExperience)
                        {
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxHealth]));
                            ChatPacket.SendServerMessage(session, string.Format("Your Maximum Health has been increased by {0}.", i), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough experience for remaining points, you require 10 billion(10,000,000,000) XP per point.", ChatMessageType.Broadcast);
                            return;
                        }
                        if (!player.SpendXP(10000000000L))
                        {
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxHealth]));
                            ChatPacket.SendServerMessage(session, string.Format("Your Maximum Health has been increased by {0}.", i), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough experience for remaining points, you require 10 billion(10,000,000,000) XP per point.", ChatMessageType.Broadcast);
                            return;
                        }
                        CreatureVital creatureVital = new CreatureVital(player, PropertyAttribute2nd.MaxHealth);
                        creatureVital.Ranks = Math.Clamp(creatureVital.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital, creatureVital.MaxValue);
                        CreatureVital creatureVital2 = new CreatureVital(player, PropertyAttribute2nd.Health);
                        creatureVital2.Ranks = Math.Clamp(creatureVital2.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital2, creatureVital2.MaxValue);
                        player.RaisedHealth++;
                    }
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxHealth]));
                    ChatPacket.SendServerMessage(session, string.Format("Your Maximum Health has been increased by {0}.", result), ChatMessageType.Broadcast);
                    return;
                }
                if (player.Level >= 500)
                {
                    for (int i = 0; i < result; i++)
                    {
                        if (500000000000L > player.AvailableExperience)
                        {
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxHealth]));
                            ChatPacket.SendServerMessage(session, string.Format("Your Maximum Health has been increased by {0}.", i), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough experience for remaining points, you require 500 billion(500,000,000,000) XP per point.", ChatMessageType.Broadcast);
                            return;
                        }
                        if (!player.SpendXP(500000000000L))
                        {
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxHealth]));
                            ChatPacket.SendServerMessage(session, string.Format("Your Maximum Health has been increased by {0}.", i), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough experience for remaining points, you require 500 billion(500,000,000,000) XP per point.", ChatMessageType.Broadcast);
                            return;
                        }
                        CreatureVital creatureVital = new CreatureVital(player, PropertyAttribute2nd.MaxHealth);
                        creatureVital.Ranks = Math.Clamp(creatureVital.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital, creatureVital.MaxValue);
                        CreatureVital creatureVital2 = new CreatureVital(player, PropertyAttribute2nd.Health);
                        creatureVital2.Ranks = Math.Clamp(creatureVital2.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital2, creatureVital2.MaxValue);
                        player.RaisedHealth++;
                    }
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxHealth]));
                    ChatPacket.SendServerMessage(session, string.Format("Your Maximum Health has been increased by {0}.", result), ChatMessageType.Broadcast);
                    return;
                }
                
            }
            if (parameters[0].Equals("pres"))
            {
                parameters[0] = "prestige";
            }
            if (parameters[0].Equals("prestige"))
            {
                var prestige = player.QuestManager.GetCurrentSolves("Prestige");
                var reputation = player.QuestManager.GetCurrentSolves("Reputation");
                var lumAugDamageRating = player.GetProperty(PropertyInt.LumAugDamageRating);
                var lumAugDamageReductionRating = player.GetProperty(PropertyInt.LumAugDamageReductionRating);
                var lumAugCritDamageRating = player.GetProperty(PropertyInt.LumAugCritDamageRating);
                var lumAugCritReductionRating = player.GetProperty(PropertyInt.LumAugCritReductionRating);

                if (prestige <= 9)
                {
                    for (int i = 0; i < result; i++)
                    {
                        if (reputation <5000)
                        {
                            ChatPacket.SendServerMessage(session, string.Format("Your Prestige has been increased by {0}.", i), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough reputation, 5000 reputation to increase prestige.", ChatMessageType.Broadcast);
                            return;
                        }
                        if (player.QuestManager.GetCurrentSolves("Reputation") < 5000)
                        {
                            ChatPacket.SendServerMessage(session, string.Format("Your Prestige has been increased by {0}.", i), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough reputation, 5000 reputation to increase prestige.", ChatMessageType.Broadcast);
                            return;
                        }
                        player.QuestManager.Decrement("Reputation", 5000);
                        player.QuestManager.Increment("Prestige", 1);
                        player.LumAugDamageRating += 5;
                        player.LumAugDamageReductionRating += 5;
                        player.LumAugCritDamageRating += 5;
                        player.LumAugCritReductionRating += 5;
                    }
                    var newreputation = player.QuestManager.GetCurrentSolves("Reputation");
                    var newprestige = player.QuestManager.GetCurrentSolves("Prestige");
                    player.QuestManager.Stamp("PrestigeComplete");
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageRating, player.LumAugDamageRating));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageReductionRating, player.LumAugDamageReductionRating));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritDamageRating, player.LumAugCritDamageRating));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritReductionRating, player.LumAugCritReductionRating));
                    ChatPacket.SendServerMessage(session, string.Format("Your Prestige has been increased by {0}. And is now {1}. You have {2} Reputation remaining.", result, newprestige, newreputation), ChatMessageType.Broadcast);
                    player.PlayParticleEffect(PlayScript.TransUpWhite, player.Guid);
                    player.PlayParticleEffect(PlayScript.AetheriaLevelUp, player.Guid);
                    player.PlayParticleEffect(PlayScript.VitaeUpWhite, player.Guid);
                    return;
                }
                if (prestige >= 10 && prestige <= 24)
                {                                            
                    for (int i = 0; i < result; i++)
                    {
                        if (reputation < 10000)
                        {
                            ChatPacket.SendServerMessage(session, string.Format("Your Prestige has been increased by {0}.", i), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough reputation, 10000 reputation to increase prestige.", ChatMessageType.Broadcast);
                            return;
                        }
                        if (player.QuestManager.GetCurrentSolves("Reputation") < 10000)
                        {
                            ChatPacket.SendServerMessage(session, string.Format("Your Prestige has been increased by {0}.", i), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough reputation, 10000 reputation to increase prestige.", ChatMessageType.Broadcast);
                            return;
                        }   
                        player.QuestManager.Decrement("Reputation", 10000);
                        player.QuestManager.Increment("Prestige", 1);
                        player.LumAugDamageRating += 10;
                        player.LumAugDamageReductionRating += 10;
                        player.LumAugCritDamageRating += 10;
                        player.LumAugCritReductionRating += 10;

                    }
                    var newreputation = player.QuestManager.GetCurrentSolves("Reputation");
                    var newprestige = player.QuestManager.GetCurrentSolves("Prestige");
                    player.QuestManager.Stamp("PrestigeComplete10");
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageRating, player.LumAugDamageRating));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageReductionRating, player.LumAugDamageReductionRating));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritDamageRating, player.LumAugCritDamageRating));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritReductionRating, player.LumAugCritReductionRating));
                    ChatPacket.SendServerMessage(session, string.Format("Your Prestige has been increased by {0}. And is now {1}. You have {2} Reputation remaining.", result, newprestige, newreputation), ChatMessageType.Broadcast);
                    player.PlayParticleEffect(PlayScript.TransUpWhite, player.Guid);
                    player.PlayParticleEffect(PlayScript.AetheriaLevelUp, player.Guid);
                    player.PlayParticleEffect(PlayScript.VitaeUpWhite, player.Guid);                    
                    return;
                }
                if (prestige >= 25 && prestige <= 49)
                {                  
                    for (int i = 0; i < result; i++)
                    {
                        if (reputation < 25000)
                        {
                            ChatPacket.SendServerMessage(session, string.Format("Your Prestige has been increased by {0}.", i), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough reputation, 25000 reputation to increase prestige.", ChatMessageType.Broadcast);
                            return;
                        }
                        if (player.QuestManager.GetCurrentSolves("Reputation") < 25000)
                        {
                            ChatPacket.SendServerMessage(session, string.Format("Your Prestige has been increased by {0}.", i), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough reputation, 25000 reputation to increase prestige.", ChatMessageType.Broadcast);
                            return;
                        }
                        player.QuestManager.Decrement("Reputation", 25000);
                        player.QuestManager.Increment("Prestige", 1);
                        player.LumAugDamageRating += 25;
                        player.LumAugDamageReductionRating += 25;
                        player.LumAugCritDamageRating += 25;
                        player.LumAugCritReductionRating += 25;
                    }
                    var newreputation = player.QuestManager.GetCurrentSolves("Reputation");
                    var newprestige = player.QuestManager.GetCurrentSolves("Prestige");
                    player.QuestManager.Stamp("PrestigeComplete25");
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageRating, player.LumAugDamageRating));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageReductionRating, player.LumAugDamageReductionRating));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritDamageRating, player.LumAugCritDamageRating));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritReductionRating, player.LumAugCritReductionRating));
                    ChatPacket.SendServerMessage(session, string.Format("Your Prestige has been increased by {0}. And is now {1}. You have {2} Reputation remaining.", result, newprestige, newreputation), ChatMessageType.Broadcast);
                    player.PlayParticleEffect(PlayScript.TransUpWhite, player.Guid);
                    player.PlayParticleEffect(PlayScript.AetheriaLevelUp, player.Guid);
                    player.PlayParticleEffect(PlayScript.VitaeUpWhite, player.Guid);
                    return;
                }
                if (prestige >= 50 && prestige <= 74)
                {
                    for (int i = 0; i < result; i++)
                    {
                        if (reputation < 35000)
                        {
                            ChatPacket.SendServerMessage(session, string.Format("Your Prestige has been increased by {0}.", i), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough reputation, 35000 reputation to increase prestige.", ChatMessageType.Broadcast);
                            return;
                        }
                        if (player.QuestManager.GetCurrentSolves("Reputation") < 35000)
                        {
                            ChatPacket.SendServerMessage(session, string.Format("Your Prestige has been increased by {0}.", i), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough reputation, 35000 reputation to increase prestige.", ChatMessageType.Broadcast);
                            return;
                        }
                        player.QuestManager.Decrement("Reputation", 35000);
                        player.QuestManager.Increment("Prestige", 1);
                        player.LumAugDamageRating += 50;
                        player.LumAugDamageReductionRating += 50;
                        player.LumAugCritDamageRating += 50;
                        player.LumAugCritReductionRating += 50;
                    }
                    var newreputation = player.QuestManager.GetCurrentSolves("Reputation");
                    var newprestige = player.QuestManager.GetCurrentSolves("Prestige");
                    player.QuestManager.Stamp("PrestigeComplete50");
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageRating, player.LumAugDamageRating));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageReductionRating, player.LumAugDamageReductionRating));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritDamageRating, player.LumAugCritDamageRating));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritReductionRating, player.LumAugCritReductionRating));
                    ChatPacket.SendServerMessage(session, string.Format("Your Prestige has been increased by {0}. And is now {1}. You have {2} Reputation remaining.", result, newprestige, newreputation), ChatMessageType.Broadcast);
                    player.PlayParticleEffect(PlayScript.TransUpWhite, player.Guid);
                    player.PlayParticleEffect(PlayScript.AetheriaLevelUp, player.Guid);
                    player.PlayParticleEffect(PlayScript.VitaeUpWhite, player.Guid);
                    return;
                }
                if (prestige >= 75 && prestige <= 99)
                {
                    for (int i = 0; i < result; i++)
                    {
                        if (reputation < 50000)
                        {
                            ChatPacket.SendServerMessage(session, string.Format("Your Prestige has been increased by {0}.", i), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough reputation, 50000 reputation to increase prestige.", ChatMessageType.Broadcast);
                            return;
                        }
                        if (player.QuestManager.GetCurrentSolves("Reputation") < 50000)
                        {
                            ChatPacket.SendServerMessage(session, string.Format("Your Prestige has been increased by {0}.", i), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough reputation, 50000 reputation to increase prestige.", ChatMessageType.Broadcast);
                            return;
                        }
                        player.QuestManager.Decrement("Reputation", 50000);
                        player.QuestManager.Increment("Prestige", 1);
                        player.LumAugDamageRating += 75;
                        player.LumAugDamageReductionRating += 75;
                        player.LumAugCritDamageRating += 75;
                        player.LumAugCritReductionRating += 75;
                    }
                    var newreputation = player.QuestManager.GetCurrentSolves("Reputation");
                    var newprestige = player.QuestManager.GetCurrentSolves("Prestige");
                    player.QuestManager.Stamp("PrestigeComplete75");
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageRating, player.LumAugDamageRating));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageReductionRating, player.LumAugDamageReductionRating));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritDamageRating, player.LumAugCritDamageRating));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritReductionRating, player.LumAugCritReductionRating));
                    ChatPacket.SendServerMessage(session, string.Format("Your Prestige has been increased by {0}. And is now {1}. You have {2} Reputation remaining.", result, newprestige, newreputation), ChatMessageType.Broadcast);
                    player.PlayParticleEffect(PlayScript.TransUpWhite, player.Guid);
                    player.PlayParticleEffect(PlayScript.AetheriaLevelUp, player.Guid);
                    player.PlayParticleEffect(PlayScript.VitaeUpWhite, player.Guid);
                    return;
                }
                if (prestige >= 100)
                {
                    for (int i = 0; i < result; i++)
                    {
                        if (reputation < 100000)
                        {
                            ChatPacket.SendServerMessage(session, string.Format("Your Prestige has been increased by {0}.", i), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough reputation, 100000 reputation to increase prestige.", ChatMessageType.Broadcast);
                            return;
                        }
                        if (player.QuestManager.GetCurrentSolves("Reputation") < 100000)
                        {
                            ChatPacket.SendServerMessage(session, string.Format("Your Prestige has been increased by {0}.", i), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough reputation, 100000 reputation to increase prestige.", ChatMessageType.Broadcast);
                            return;
                        }
                        player.QuestManager.Decrement("Reputation", 100000);
                        player.QuestManager.Increment("Prestige", 1);
                        player.LumAugDamageRating += 100;
                        player.LumAugDamageReductionRating += 100;
                        player.LumAugCritDamageRating += 100;
                        player.LumAugCritReductionRating += 100;
                    }
                    var newreputation = player.QuestManager.GetCurrentSolves("Reputation");
                    var newprestige = player.QuestManager.GetCurrentSolves("Prestige");
                    player.QuestManager.Stamp("PrestigeComplete100");
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageRating, player.LumAugDamageRating));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageReductionRating, player.LumAugDamageReductionRating));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritDamageRating, player.LumAugCritDamageRating));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritReductionRating, player.LumAugCritReductionRating));
                    ChatPacket.SendServerMessage(session, string.Format("Your Prestige has been increased by {0}. And is now {1}. You have {2} Reputation remaining.", result, newprestige, newreputation), ChatMessageType.Broadcast);
                    player.PlayParticleEffect(PlayScript.TransUpWhite, player.Guid);
                    player.PlayParticleEffect(PlayScript.AetheriaLevelUp, player.Guid);
                    player.PlayParticleEffect(PlayScript.VitaeUpWhite, player.Guid);
                    return;
                }
                if (prestige >= 150)
                {
                    for (int i = 0; i < result; i++)
                    {
                        if (reputation < 250000)
                        {
                            ChatPacket.SendServerMessage(session, string.Format("Your Prestige has been increased by {0}.", i), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough reputation, 100000 reputation to increase prestige.", ChatMessageType.Broadcast);
                            return;
                        }
                        if (player.QuestManager.GetCurrentSolves("Reputation") < 250000)
                        {
                            ChatPacket.SendServerMessage(session, string.Format("Your Prestige has been increased by {0}.", i), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough reputation, 100000 reputation to increase prestige.", ChatMessageType.Broadcast);
                            return;
                        }
                        player.QuestManager.Decrement("Reputation", 250000);
                        player.QuestManager.Increment("Prestige", 1);
                        player.LumAugDamageRating += 100;
                        player.LumAugDamageReductionRating += 100;
                        player.LumAugCritDamageRating += 100;
                        player.LumAugCritReductionRating += 100;
                    }
                    var newreputation = player.QuestManager.GetCurrentSolves("Reputation");
                    var newprestige = player.QuestManager.GetCurrentSolves("Prestige");
                    player.QuestManager.Stamp("PrestigeComplete150");
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageRating, player.LumAugDamageRating));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageReductionRating, player.LumAugDamageReductionRating));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritDamageRating, player.LumAugCritDamageRating));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritReductionRating, player.LumAugCritReductionRating));
                    ChatPacket.SendServerMessage(session, string.Format("Your Prestige has been increased by {0}. And is now {1}. You have {2} Reputation remaining.", result, newprestige, newreputation), ChatMessageType.Broadcast);
                    player.PlayParticleEffect(PlayScript.TransUpWhite, player.Guid);
                    player.PlayParticleEffect(PlayScript.AetheriaLevelUp, player.Guid);
                    player.PlayParticleEffect(PlayScript.VitaeUpWhite, player.Guid);
                    return;
                }
                return;
            }
            if (parameters[0].Equals("stam"))
            {
                parameters[0] = "stamina";
            }
            if (parameters[0].Equals("stamina"))
            {
                if (player.Level < 500)
                {
                    for (int j = 0; j < result; j++)
                    {
                        if (10000000000L > player.AvailableExperience)
                        {
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxStamina]));
                            ChatPacket.SendServerMessage(session, string.Format("Your Maximum Stamina has been increased by {0}.", j), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough experience for remaining points, you require 10 billion(10,000,000,000) XP per point.", ChatMessageType.Broadcast);
                            return;
                        }
                        if (!player.SpendXP(10000000000L))
                        {
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxStamina]));
                            ChatPacket.SendServerMessage(session, string.Format("Your Maximum Stamina has been increased by {0}.", j), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough experience for remaining points, you require 10 billion(10,000,000,000) XP per point.", ChatMessageType.Broadcast);
                            return;
                        }
                        CreatureVital creatureVital3 = new CreatureVital(player, PropertyAttribute2nd.MaxStamina);
                        creatureVital3.Ranks = Math.Clamp(creatureVital3.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital3, creatureVital3.MaxValue);
                        CreatureVital creatureVital4 = new CreatureVital(player, PropertyAttribute2nd.Stamina);
                        creatureVital4.Ranks = Math.Clamp(creatureVital4.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital4, creatureVital4.MaxValue);
                        player.RaisedStamina++;
                    }
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxStamina]));
                    ChatPacket.SendServerMessage(session, string.Format("Your Maximum Stamina has been increased by {0}.", result), ChatMessageType.Broadcast);
                    return;
                }
                if (player.Level >= 500)
                {
                    for (int j = 0; j < result; j++)
                    {
                        if (500000000000L > player.AvailableExperience)
                        {
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxStamina]));
                            ChatPacket.SendServerMessage(session, string.Format("Your Maximum Stamina has been increased by {0}.", j), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough experience for remaining points, you require 500 billion(500,000,000,000) XP per point.", ChatMessageType.Broadcast);
                            return;
                        }
                        if (!player.SpendXP(500000000000L))
                        {
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxStamina]));
                            ChatPacket.SendServerMessage(session, string.Format("Your Maximum Stamina has been increased by {0}.", j), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough experience for remaining points, you require 500 billion(500,000,000,000) XP per point.", ChatMessageType.Broadcast);
                            return;
                        }
                        CreatureVital creatureVital3 = new CreatureVital(player, PropertyAttribute2nd.MaxStamina);
                        creatureVital3.Ranks = Math.Clamp(creatureVital3.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital3, creatureVital3.MaxValue);
                        CreatureVital creatureVital4 = new CreatureVital(player, PropertyAttribute2nd.Stamina);
                        creatureVital4.Ranks = Math.Clamp(creatureVital4.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital4, creatureVital4.MaxValue);
                        player.RaisedStamina++;
                    }
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxStamina]));
                    ChatPacket.SendServerMessage(session, string.Format("Your Maximum Stamina has been increased by {0}.", result), ChatMessageType.Broadcast);
                    return;
                }

            }

            int _luminanceRating = 0;


            // allows spending of luminance to increase luminance damage rating
            if (parameters[0].ToLowerInvariant().Equals("destruction"))
            {
                
                _luminanceRating = player.LumAugDamageRating;
                if (_luminanceRating == 0)
                    player.LumAugDamageRating = 1;
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageRating, _luminanceRating));

                for (int j = 0; j < result; j++)
                {
                    _luminanceRating = player.LumAugDamageRating;

                    var destruction = player.GetProperty(PropertyInt.LumAugDamageRating);
                    var destructioncost = Math.Round((double)(10000000 * (1 + (destruction * 0.149))));

                    // while looping through the number of increases requested - if the total available is not enough to keep looping
                    // break out of the loop and inform the player of how many increases they received
                    if (destructioncost > player.AvailableLuminance || !player.SpendLuminance((long)destructioncost))
                    {
                        player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageRating, _luminanceRating));
                        ChatPacket.SendServerMessage(session, string.Format("Your Luminance Augmentation Damage Rating has been increased by {0}.", j), ChatMessageType.Broadcast);
                        ChatPacket.SendServerMessage(session, $"Not enough Luminance for remaining points, you require {destructioncost} luminance.", ChatMessageType.Broadcast);
                        return;
                    }
                    player.LumAugDamageRating++;
                }
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageRating, player.LumAugDamageRating));
                ChatPacket.SendServerMessage(session, string.Format("Your Luminance Augmentation Damage Rating has been increased by {0}.", result), ChatMessageType.Broadcast);
                return;
            }


            // allows spending of luminance to increase luminance damage reduction rating (Invulnerability)
            if (parameters[0].ToLowerInvariant().Equals("invulnerability"))
            {
                for (int j = 0; j < result; j++)
                {
                    _luminanceRating = player.LumAugDamageReductionRating;
                    if (_luminanceRating == 0)
                        player.LumAugDamageReductionRating = 1;
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageReductionRating, _luminanceRating));

                    var invulnerability = player.GetProperty(PropertyInt.LumAugDamageReductionRating);
                    var invulnerabilitycost = Math.Round((double)(10000000 * (1 + (invulnerability * 0.149))));

                    // while looping through the number of increases requested - if the total available is not enough to keep looping
                    // break out of the loop and inform the player of how many increases they received
                    if (invulnerabilitycost > player.AvailableLuminance || !player.SpendLuminance((long)invulnerabilitycost))
                    {
                        player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageReductionRating, _luminanceRating));
                        ChatPacket.SendServerMessage(session, string.Format("Your Luminance Augmentation Damage Reduction Rating has been increased by {0}.", j), ChatMessageType.Broadcast);
                        ChatPacket.SendServerMessage(session, $"Not enough Luminance for remaining points, you require {invulnerabilitycost} luminance.", ChatMessageType.Broadcast);
                        return;
                    }
                    player.LumAugDamageReductionRating++;
                }
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageReductionRating, player.LumAugDamageReductionRating));
                ChatPacket.SendServerMessage(session, string.Format("Your Luminance Augmentation Damage Reduction Rating has been increased by {0}.", result), ChatMessageType.Broadcast);
                return;
            }

            
            // allows spending of luminance to increase luminance critical damage rating (Glory)
            if (parameters[0].ToLowerInvariant().Equals("glory"))
            {
                for (int j = 0; j < result; j++)
                {
                    _luminanceRating = player.LumAugCritDamageRating;
                    if (_luminanceRating == 0)
                        player.LumAugCritDamageRating = 1;
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritDamageRating, _luminanceRating));

                    var glory = player.GetProperty(PropertyInt.LumAugCritDamageRating);
                    var glorycost = Math.Round((double)(10000000 * (1 + (glory * 0.149))));

                    // while looping through the number of increases requested - if the total available is not enough to keep looping
                    // break out of the loop and inform the player of how many increases they received
                    if (glorycost > player.AvailableLuminance || !player.SpendLuminance((long)glorycost))
                    {
                        player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritDamageRating, _luminanceRating));
                        ChatPacket.SendServerMessage(session, string.Format("Your Luminance Augmentation Critical Damage Rating has been increased by {0}.", j), ChatMessageType.Broadcast);
                        ChatPacket.SendServerMessage(session, $"Not enough Luminance for remaining points, you require {glorycost} luminance.", ChatMessageType.Broadcast);
                        return;
                    }
                    player.LumAugCritDamageRating++;
                }
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritDamageRating, player.LumAugCritDamageRating));
                ChatPacket.SendServerMessage(session, string.Format("Your Luminance Augmentation Critical Damage Rating has been increased by {0}.", result), ChatMessageType.Broadcast);
                return;
            }

            
            // allows spending of luminance to increase luminance critical damage reduction rating (Temperance)
            if (parameters[0].ToLowerInvariant().Equals("temperance"))
            {
                for (int j = 0; j < result; j++)
                {
                    _luminanceRating = player.LumAugCritReductionRating;
                    if (_luminanceRating == 0)
                        player.LumAugCritReductionRating = 1;
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritReductionRating, _luminanceRating));

                    var temperance = player.GetProperty(PropertyInt.LumAugCritReductionRating);
                    var temperancecost = Math.Round((double)(10000000 * (1 + (temperance * 0.149))));

                    // while looping through the number of increases requested - if the total available is not enough to keep looping
                    // break out of the loop and inform the player of how many increases they received
                    if (temperancecost > player.AvailableLuminance || !player.SpendLuminance((long)temperancecost))
                    {
                        player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritReductionRating, _luminanceRating));
                        ChatPacket.SendServerMessage(session, string.Format("Your Luminance Augmentation Critical Damage Reduction Rating has been increased by {0}.", j), ChatMessageType.Broadcast);
                        ChatPacket.SendServerMessage(session, $"Not enough Luminance for remaining points, you require {temperancecost} luminance.", ChatMessageType.Broadcast);
                        return;
                    }
                    player.LumAugCritReductionRating++;
                }
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritReductionRating, player.LumAugCritReductionRating));
                ChatPacket.SendServerMessage(session, string.Format("Your Luminance Augmentation Critical Damage Reduction Rating has been increased by {0}.", result), ChatMessageType.Broadcast);
                return;
            }

            CreatureVital maxHealth = new CreatureVital(player, PropertyAttribute2nd.MaxHealth);

            if (parameters[0].Equals("vitality"))
            {
                if (maxHealth.Ranks < 10000)
                {
                    for (int j = 0; j < result; j++)
                    {
                        if (10000000L > player.AvailableLuminance)
                        {
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxHealth]));
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxStamina]));
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxMana]));
                            ChatPacket.SendServerMessage(session, string.Format("Your Vitality has been increased by {0}.", j), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough Luminance for remaining points, you require 10 million (10,000,000) Luminance per point.", ChatMessageType.Broadcast);
                            return;
                        }
                        if (!player.SpendLuminance(10000000L))
                        {
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxHealth]));
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxStamina]));
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxMana]));
                            ChatPacket.SendServerMessage(session, string.Format("Your Vitality has been increased by {0}.", j), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough Luminance for remaining points, you require 10 million (10,000,000) Luminance per point.", ChatMessageType.Broadcast);
                            return;
                        }
                        CreatureVital creatureVital1 = new CreatureVital(player, PropertyAttribute2nd.MaxHealth);
                        creatureVital1.Ranks = Math.Clamp(creatureVital1.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital1, creatureVital1.MaxValue);
                        CreatureVital creatureVital2 = new CreatureVital(player, PropertyAttribute2nd.Health);
                        creatureVital2.Ranks = Math.Clamp(creatureVital2.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital2, creatureVital2.MaxValue);
                        CreatureVital creatureVital3 = new CreatureVital(player, PropertyAttribute2nd.MaxStamina);
                        creatureVital3.Ranks = Math.Clamp(creatureVital3.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital3, creatureVital3.MaxValue);
                        CreatureVital creatureVital4 = new CreatureVital(player, PropertyAttribute2nd.Stamina);
                        creatureVital4.Ranks = Math.Clamp(creatureVital4.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital4, creatureVital4.MaxValue);
                        CreatureVital creatureVital5 = new CreatureVital(player, PropertyAttribute2nd.MaxMana);
                        creatureVital5.Ranks = Math.Clamp(creatureVital5.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital5, creatureVital5.MaxValue);
                        CreatureVital creatureVital6 = new CreatureVital(player, PropertyAttribute2nd.Mana);
                        creatureVital6.Ranks = Math.Clamp(creatureVital6.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital6, creatureVital6.MaxValue);
                    }
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxHealth]));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxStamina]));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxMana]));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxStamina]));
                    return;
                }                                

                if (maxHealth.Ranks >= 10000)
                {
                    for (int j = 0; j < result; j++)
                    {
                        if (500000000L > player.AvailableLuminance)
                        {
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxHealth]));
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxStamina]));
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxMana]));
                            ChatPacket.SendServerMessage(session, string.Format("Your Vitality has been increased by {0}.", j), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough Luminance for remaining points, you require 500 million (500,000,000) Luminance per point.", ChatMessageType.Broadcast);
                            return;
                        }
                        if (!player.SpendLuminance(500000000L))
                        {
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxHealth]));
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxStamina]));
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxMana]));
                            ChatPacket.SendServerMessage(session, string.Format("Your Vitality has been increased by {0}.", j), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough Luminance for remaining points, you require 500 million (500,000,000) Luminance per point.", ChatMessageType.Broadcast);
                            return;
                        }
                        CreatureVital creatureVital1 = new CreatureVital(player, PropertyAttribute2nd.MaxHealth);
                        creatureVital1.Ranks = Math.Clamp(creatureVital1.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital1, creatureVital1.MaxValue);
                        CreatureVital creatureVital2 = new CreatureVital(player, PropertyAttribute2nd.Health);
                        creatureVital2.Ranks = Math.Clamp(creatureVital2.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital2, creatureVital2.MaxValue);
                        CreatureVital creatureVital3 = new CreatureVital(player, PropertyAttribute2nd.MaxStamina);
                        creatureVital3.Ranks = Math.Clamp(creatureVital3.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital3, creatureVital3.MaxValue);
                        CreatureVital creatureVital4 = new CreatureVital(player, PropertyAttribute2nd.Stamina);
                        creatureVital4.Ranks = Math.Clamp(creatureVital4.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital4, creatureVital4.MaxValue);
                        CreatureVital creatureVital5 = new CreatureVital(player, PropertyAttribute2nd.MaxMana);
                        creatureVital5.Ranks = Math.Clamp(creatureVital5.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital5, creatureVital5.MaxValue);
                        CreatureVital creatureVital6 = new CreatureVital(player, PropertyAttribute2nd.Mana);
                        creatureVital6.Ranks = Math.Clamp(creatureVital6.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital6, creatureVital6.MaxValue);
                    }
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxHealth]));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxStamina]));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxMana]));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxStamina]));
                    return;
                }

            }

            if (parameters[0].Equals("nextlevel"))
            {
                var remainingXP = player.GetRemainingXP((uint)player.Level).Value;
                ChatPacket.SendServerMessage(session, string.Format($"You require {remainingXP} to reach your next level."), ChatMessageType.Broadcast);
                return;
            }
                
            if (parameters[0].Equals("mana"))
            {
                if (player.Level < 500)
                {
                    for (int k = 0; k < result; k++)
                    {
                        if (10000000000L > player.AvailableExperience)
                        {
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxMana]));
                            ChatPacket.SendServerMessage(session, string.Format("Your Maximum Mana has been increased by {0}.", k), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough experience for remaining points, you require 10 billion(10,000,000,000) XP per point.", ChatMessageType.Broadcast);
                            return;
                        }
                        if (!player.SpendXP(10000000000L))
                        {
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxMana]));
                            ChatPacket.SendServerMessage(session, string.Format("Your Maximum Mana has been increased by {0}.", k), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough experience for remaining points, you require 10 billion(10,000,000,000) XP per point.", ChatMessageType.Broadcast);
                            return;
                        }
                        CreatureVital creatureVital5 = new CreatureVital(player, PropertyAttribute2nd.MaxMana);
                        creatureVital5.Ranks = Math.Clamp(creatureVital5.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital5, creatureVital5.MaxValue);
                        CreatureVital creatureVital6 = new CreatureVital(player, PropertyAttribute2nd.Mana);
                        creatureVital6.Ranks = Math.Clamp(creatureVital6.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital6, creatureVital6.MaxValue);
                        player.RaisedMana++;
                    }
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxMana]));
                    ChatPacket.SendServerMessage(session, string.Format("Your Maximum Mana has been increased by {0}.", result), ChatMessageType.Broadcast);
                    return;
                }
                if (player.Level >= 500)
                {
                    for (int k = 0; k < result; k++)
                    {
                        if (500000000000L > player.AvailableExperience)
                        {
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxMana]));
                            ChatPacket.SendServerMessage(session, string.Format("Your Maximum Mana has been increased by {0}.", k), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough experience for remaining points, you require 500 billion(500,000,000,000) XP per point.", ChatMessageType.Broadcast);
                            return;
                        }
                        if (!player.SpendXP(500000000000L))
                        {
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxMana]));
                            ChatPacket.SendServerMessage(session, string.Format("Your Maximum Mana has been increased by {0}.", k), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough experience for remaining points, you require 500 billion(500,000,000,000) XP per point.", ChatMessageType.Broadcast);
                            return;
                        }
                        CreatureVital creatureVital5 = new CreatureVital(player, PropertyAttribute2nd.MaxMana);
                        creatureVital5.Ranks = Math.Clamp(creatureVital5.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital5, creatureVital5.MaxValue);
                        CreatureVital creatureVital6 = new CreatureVital(player, PropertyAttribute2nd.Mana);
                        creatureVital6.Ranks = Math.Clamp(creatureVital6.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital6, creatureVital6.MaxValue);
                        player.RaisedMana++;
                    }
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxMana]));
                    ChatPacket.SendServerMessage(session, string.Format("Your Maximum Mana has been increased by {0}.", result), ChatMessageType.Broadcast);
                    return;
                }

            }
            if (parameters[0].Equals("str") || parameters[0].Equals("strength"))
            {
                parameters[0] = "Strength";
            }
            else if (parameters[0].Equals("end") || parameters[0].Equals("endurance"))
            {
                parameters[0] = "Endurance";
            }
            else if (parameters[0].Equals("coord") || parameters[0].Equals("coordination"))
            {
                parameters[0] = "Coordination";
            }
            else if (parameters[0].Equals("quick") || parameters[0].Equals("quickness"))
            {
                parameters[0] = "Quickness";
            }
            else if (parameters[0].Equals("focus") || parameters[0].Equals("focus"))
            {
                parameters[0] = "Focus";
            }
            else if (parameters[0].Equals("self") || parameters[0].Equals("self"))
            {
                parameters[0] = "Self";
            }
            PropertyAttribute result2;
            if (!Enum.TryParse<PropertyAttribute>(parameters[0], out result2))
            {
                ChatPacket.SendServerMessage(session, "Invalid Attribute, valid values are: Strength,Endurance,Coordination,Quickness,Focus,Self,Health,Stamina,Mana", ChatMessageType.Broadcast);
                return;
            }
            if (player.Level >= 500)
            {
                for (int l = 0; l < result; l++)
                {
                    if (500000000000L > player.AvailableExperience)
                    {
                        player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, player.Attributes[result2]));
                        ChatPacket.SendServerMessage(session, string.Format("Your {0} has been increased by {1}.", result2.ToString(), l), ChatMessageType.Broadcast);
                        ChatPacket.SendServerMessage(session, "Not enough experience for remaining points, you require 500 billion(500,000,000,000) XP per point.", ChatMessageType.Broadcast);
                        return;
                    }
                    if (!player.SpendXP(500000000000L))
                    {
                        player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, player.Attributes[result2]));
                        ChatPacket.SendServerMessage(session, string.Format("Your {0} has been increased by {1}.", result2.ToString(), l), ChatMessageType.Broadcast);
                        ChatPacket.SendServerMessage(session, "Not enough experience for remaining points, you require 500 billion(500,000,000,000) XP per point.", ChatMessageType.Broadcast);
                        player.RaisedStr++;
                        return;
                    }
                    player.Attributes[result2].StartingValue++;

                }
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, player.Attributes[result2]));
                ChatPacket.SendServerMessage(session, string.Format("Your {0} has been increased by {1}.", result2.ToString(), result), ChatMessageType.Broadcast);

                for (int l = 0; l < result; l++)
                {
                    if (parameters[0].Equals("Strength"))
                    {
                        player.RaisedStr++;
                    }
                    else if (parameters[0].Equals("Endurance"))
                    {
                        player.RaisedEnd++;
                    }
                    else if (parameters[0].Equals("Coordination"))
                    {
                        player.RaisedCoord++;
                    }
                    else if (parameters[0].Equals("Quickness"))
                    {
                        player.RaisedQuick++;
                    }
                    else if (parameters[0].Equals("Focus"))
                    {
                        player.RaisedFocus++;
                    }
                    else if (parameters[0].Equals("Self"))
                    {
                        player.RaisedSelf++;
                    }
                }
            }
            if (player.Level < 500)
            {
                for (int l = 0; l < result; l++)
                {
                    if (10000000000L > player.AvailableExperience)
                    {
                        player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, player.Attributes[result2]));
                        ChatPacket.SendServerMessage(session, string.Format("Your {0} has been increased by {1}.", result2.ToString(), l), ChatMessageType.Broadcast);
                        ChatPacket.SendServerMessage(session, "Not enough experience for remaining points, you require 10 billion(10,000,000,000) XP per point.", ChatMessageType.Broadcast);
                        return;
                    }
                    if (!player.SpendXP(10000000000L))
                    {
                        player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, player.Attributes[result2]));
                        ChatPacket.SendServerMessage(session, string.Format("Your {0} has been increased by {1}.", result2.ToString(), l), ChatMessageType.Broadcast);
                        ChatPacket.SendServerMessage(session, "Not enough experience for remaining points, you require 10 billion(10,000,000,000) XP per point.", ChatMessageType.Broadcast);
                        player.RaisedStr++;
                        return;
                    }
                    player.Attributes[result2].StartingValue++;

                }
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, player.Attributes[result2]));
                ChatPacket.SendServerMessage(session, string.Format("Your {0} has been increased by {1}.", result2.ToString(), result), ChatMessageType.Broadcast);

                for (int l = 0; l < result; l++)
                {
                    if (parameters[0].Equals("Strength"))
                    {
                        player.RaisedStr++;
                    }
                    else if (parameters[0].Equals("Endurance"))
                    {
                        player.RaisedEnd++;
                    }
                    else if (parameters[0].Equals("Coordination"))
                    {
                        player.RaisedCoord++;
                    }
                    else if (parameters[0].Equals("Quickness"))
                    {
                        player.RaisedQuick++;
                    }
                    else if (parameters[0].Equals("Focus"))
                    {
                        player.RaisedFocus++;
                    }
                    else if (parameters[0].Equals("Self"))
                    {
                        player.RaisedSelf++;
                    }
                }
            }


        }

        [CommandHandler("vassalxp", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Shows full experience from vassals.")]
        public static void HandleShowVassalXp(Session session, params string[] parameters)
        {
            CommandHandlerHelper.WriteOutputInfo(session, "Experience from vassals:", ChatMessageType.Broadcast);
            foreach (var vassalNode in AllegianceManager.GetAllegianceNode(session?.Player).Vassals.Values)
            {
                var vassal = vassalNode.Player;
                CommandHandlerHelper.WriteOutputInfo(session, $"{vassal.Name,-30}{vassal.AllegianceXPGenerated,-20:N0}", ChatMessageType.Broadcast);
            }
        }
    }
}
