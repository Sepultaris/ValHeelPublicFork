using System;
using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Server.Factories;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.ValheelMods;
using System.Collections.Generic;
using ACE.Database;
using log4net;
using System.Linq;
using System.Text;

namespace ACE.Server.Command.Handlers
{
    public static class ValheelAdminCommands
    {
        [CommandHandler("updateleaderboard", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, "Forces an update to the Hc leaderboard in ValHeel Discord")]
        public static void HandleUpdateLeaderboard(Session session, params string[] parameters)
        {
            DiscordRelay.UpdateHcLeaderboard();
        }

        [CommandHandler("holtup", AccessLevel.Admin, CommandHandlerFlag.ConsoleInvoke, "Moves errybody (offline) to Holt")]
        public static void HandleVerifySkillCredits(Session session, params string[] parameters)
        {
            //Few ways of getting the position you want:
            //Negatives for switching between ns/ew, or you can construct with /loc data
            var holt = new ACE.Entity.Position(42.1f, 33.6f);

            //Similar but no guessing with negatives and checks for errors
            if (!CommandParameterHelpers.TryParsePosition(new string[] { "42.1N", "33.6E" }, out var e, out var position))
                return;

            //POI approach
            var teleportPOI = DatabaseManager.World.GetCachedPointOfInterest("holtburg");
            if (teleportPOI == null)
                return;
            var weenie = DatabaseManager.World.GetCachedWeenie(teleportPOI.WeenieClassId);
            var portalDest = new ACE.Entity.Position(weenie.GetPosition(PositionType.Destination));
            WorldObject.AdjustDungeon(portalDest);

            //Easiest to do when the server is closed.  Ordering by account for legibility
            var sb = new StringBuilder("\r\n");
            foreach (var p in PlayerManager.GetAllOffline().OrderBy(x => x.Account.AccountId))
            {
                //Only move regular players / restrict based on access level.  
                if (p.Account == null || p.Account.AccessLevel >= (uint)AccessLevel.Sentinel)
                    continue;

                //Get location position/other positions for player if you need it.  Not needed to move but using it to print position info
                sb.AppendLine($"{p.Account.AccountId} - {p.Name}:");
                foreach (var pt in Enum.GetValues<PositionType>())
                {
                    if (!p.Biota.PropertiesPosition.TryGetValue(PositionType.Location, out var pos))
                        continue;

                    sb.AppendLine($"   {pt,-20} - 0x{pos.ObjCellId:X} - {pos.PositionX},{pos.PositionY},{pos.PositionZ}");
                }

                //Set Location position for a player to holt and save
                p.Biota.SetPosition(PositionType.Location, holt, p.BiotaDatabaseLock);
                p.SaveBiotaToDatabase();
            }

            //Print out locations
            //ModManager.Log($"{sb}");
            return;
        }

        [CommandHandler("randomizecolor", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, "Totally randomizes the colors of the appraised armor.")]
        public static void HandleRandomizeColor(Session session, params string[] parameters)
        {
            var target = CommandHandlerHelper.GetLastAppraisedObject(session);
            var isArmor = target.GetProperty(PropertyInt.ItemType);
            string name = target.GetProperty(PropertyString.Name);

            if (target != null && isArmor == 2)
            {
                LootGenerationFactory.RandomizeColorTotallyRandom(target);
                ChatPacket.SendServerMessage(session, $"The color of the {name} has been randomized.", ChatMessageType.Broadcast);
                return;
            }
            if (isArmor != 2)
            {
                ChatPacket.SendServerMessage(session, $"The target must be an armor piece.", ChatMessageType.Broadcast);
                return;
            }
        }

        [CommandHandler("mutatecolor", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, "Randomly muatates the color of the appraised armor.")]
        public static void HandleMutateColor(Session session, params string[] parameters)
        {
            var target = CommandHandlerHelper.GetLastAppraisedObject(session);
            var isArmor = target.GetProperty(PropertyInt.ItemType);
            string name = target.GetProperty(PropertyString.Name);

            if (target != null && isArmor == 2)
            {
                LootGenerationFactory.MutateColor(target);
                ChatPacket.SendServerMessage(session, $"The color of the {name} has been mutated.", ChatMessageType.Broadcast);
                return;
            }
            if (isArmor != 2)
            {
                ChatPacket.SendServerMessage(session, $"The target must be an armor piece.", ChatMessageType.Broadcast);
                return;
            }
        }

        //TODO: Decide if refunding yourself should should be open to players
        [CommandHandler("raiserefund", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 0, "Refunds costs associated with /raise.")]
        public static void HandleRaiseRefund(Session session, params string[] parameters)
        {
            ValheelRaise.RaiseRefundToPlayer(session.Player);
        }

        [CommandHandler("raiserefundto", AccessLevel.Admin, CommandHandlerFlag.None, 1, "Refunds costs associated with /raise.", "/raiserefund [*|name|id]")]
        public static void HandleRaiseRefundTo(Session session, params string[] parameters)
        {
            //Todo: Handle offline players by adding properties directly to the helper?
            //Refund all players
            if (parameters[0] == "*")
            {
                //PlayerManager.GetAllPlayers().ForEach(p => DuskfallRaise.RaiseRefundToPlayer((Player)p));
                PlayerManager.GetAllOnline().ForEach(p => ValheelRaise.RaiseRefundToPlayer(p));
                return;
            }

            //Refund by name/ID
            //var player = PlayerManager.FindByName(parameters[0]) as Player;
            var player = PlayerManager.GetOnlinePlayer(parameters[0]);
            if (player == null)
            {
                ChatPacket.SendServerMessage(session, $"No player {parameters[0]} found.", ChatMessageType.Broadcast);
                return;
            }

            ValheelRaise.RaiseRefundToPlayer(player);
        }
    }
}
