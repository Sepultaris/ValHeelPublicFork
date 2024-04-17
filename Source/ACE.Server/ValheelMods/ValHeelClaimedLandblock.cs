
using ACE.Server.Command.Handlers;
using ACE.Entity.Enum;
using ACE.Entity;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network;
using ACE.Database.SQLFormatters.World;
using ACE.Server.Managers;
using ACE.Entity.Enum.Properties;
using System.Collections.Generic;
using ACE.Server.WorldObjects;
using ACE.Server.Entity;
using ACE.Server.Command.Handlers.Processors;

namespace ACE.Server.ShalebridgeMods
{
    public class Claimed_Landblock
    {
        public static LandblockInstanceWriter LandblockInstanceWriter;

        // This is the method that is called when the player types /si cl
        public static void HandleClaimLandblock(Session session)
        {
            var loc = new Position(session.Player.Location);

            var landblock = session.Player.CurrentLandblock.Id.Landblock;

            var player = session.Player;

            var playerLandblockId = session.Player.GetProperty(PropertyInt.ClaimedLandblockId);

            if (playerLandblockId == landblock)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.x1B));
                session.Network.EnqueueSend(new GameMessageSystemChat($"You have already claimed this landblock.", ChatMessageType.x1B));
                session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.x1B));
                return;
            }
            if (player.HasClaimedLandblock && landblock != playerLandblockId)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.x1B));
                session.Network.EnqueueSend(new GameMessageSystemChat($"You have already claimed a landblock.", ChatMessageType.x1B));
                session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.x1B));
                return;
            }
            if (LandblockAlreadyClaimed(landblock))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.x1B));
                session.Network.EnqueueSend(new GameMessageSystemChat($"A player has already claimed this landblock.", ChatMessageType.x1B));
                session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.x1B));
                return;
            }

            if (!player.HasClaimedLandblock && !LandblockAlreadyClaimed(landblock))
            {
                if (player.BankedPyreals < 5000000 || player.BankedPyreals == null)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.x1B));
                    session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have enough pyreals in your bank account.", ChatMessageType.x1B));
                    session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.x1B));
                    return;
                }
                else if (player.GetNumInventoryItemsOfWCID(803296) == 0)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.x1B));
                    session.Network.EnqueueSend(new GameMessageSystemChat($"You do not have a Land Attunment Crystal in your inventory.", ChatMessageType.x1B));
                    session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.x1B));
                    return;
                }
                else
                    HandleClaimConfirmation(player);
            }
        }

        // This method is called when the player has confirmed that they want to claim the landblock
        public static void HandleClaimedLandblock(Session session)
        {
            var player = session.Player;
            var landblock = session.Player.CurrentLandblock.Id.Landblock;
            var item = player.GetInventoryItemsOfWCID(803296);

            session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.x1B));
            session.Network.EnqueueSend(new GameMessageSystemChat($"You have laid claim to this landblock.", ChatMessageType.x1B));
            session.Network.EnqueueSend(new GameMessageSystemChat($"You may now place structures and set a recall point within this landblock.", ChatMessageType.x1B));
            session.Network.EnqueueSend(new GameMessageSystemChat($"You can set a recall location by using the /si srl.", ChatMessageType.x1B));
            session.Network.EnqueueSend(new GameMessageSystemChat($"---------------------------", ChatMessageType.x1B));

            // This is where the player is given the landblock and the Attunement Crystal is consumed plus the pyreals are removed from the bank account
            player.ClaimedLandblockId += landblock;
            player.BankedPyreals -= 5000000;

            if (item.Count != 0)
            {
                var wo = item[0];
                var guid = wo.Guid;

                player.TryRemoveFromInventoryWithNetworking(guid, out wo, Player.RemoveFromInventoryAction.ConsumeItem);
                
            }
            
            player.UpdateProperty(player, PropertyInt64.BankedPyreals, player.BankedPyreals);
            player.UpdateProperty(player, PropertyInt.ClaimedLandblockId, landblock);
            player.HasClaimedLandblock = true;
            player.UpdateProperty(player, PropertyBool.HasClaimedLandblock, player.HasClaimedLandblock);
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.ClaimedLandblockId, landblock));
            player.SaveBiotaToDatabase();

            foreach (var obj in session.Player.PhysicsObj.ObjMaint.GetVisibleObjectsValues())
            {
                var wo = obj.WeenieObj.WorldObject.Generator;

                DeveloperContentCommands.RemoveLandblockInstances(session, wo);
                DeveloperContentCommands.HandleRemoveEnc(session);
            }

            DeveloperCommands.HandleReloadLandblocks(session);
        }

        // This method is called to confirm that they want to claim the landblock
        public static void HandleClaimConfirmation(Player player, bool confirmed = false)
        {
            var msg = $"Are you sure you want to claim this landblock? This will cost five million (5,000,000) pyreals";
            if (!confirmed)
            {
                player.Session.Player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Session.Player.Guid, () => HandleClaimedLandblock(player.Session)), msg);
                player.Session.Player.SendWeenieError(WeenieError.ConfirmationInProgress);
                player.SaveBiotaToDatabase();
            }

            return;
        }

        // This method checks to see if the current landblock is already claimed by another player
        public static bool LandblockAlreadyClaimed(uint landblock)
        {
            List<OfflinePlayer> offlinePlayers = new List<OfflinePlayer>();

            List<Player> onlinePlayers = new List<Player>();

            foreach (var p in PlayerManager.GetAllOffline())
                offlinePlayers.Add(p);
            
            foreach (var p in offlinePlayers)
            {
                if (p.ClaimedLandblockId == landblock)
                    return true;
            }

            foreach (var p in PlayerManager.GetAllOnline())
                onlinePlayers.Add(p);

            foreach (var p in onlinePlayers)
            {
                if (p.ClaimedLandblockId == landblock)
                    return true;
            }

            return false;
        }
    }
}
