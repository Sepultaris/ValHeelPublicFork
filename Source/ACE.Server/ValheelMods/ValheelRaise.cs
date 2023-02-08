using System;
using ACE.Entity.Enum;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Entity.Enum.Properties;
using ACE.Server.DuskfallMods;
using System.Collections.Generic;
using log4net.Core;


namespace ACE.Server.DuskfallMods
{
    public static class ValheelRaise
    {
        /// <summary>
        /// Refunds all resources used in the /raise command
        /// </summary>
        /// <param name="player">Player to refund</param>
        /// <returns>Update messages that would be sent to the player.</returns>
        public static List<string> RaiseRefundOfflinePlayer(Player player)
        {
            var playerMessages = new List<string>();

            try
            {
                //Try to refund every target
                foreach (var target in Enum.GetValues<RaiseTarget>())
                {
                    var level = target.GetLevel(player);
                    var startLevel = target.StartingLevel();
                    var timesLeveled = level - startLevel;                    

                    //Check if anything invested
                    if (timesLeveled < 1)
                    {
                        continue;
                    }

                    //Get resources spent
                    if (!target.TryGetCostToLevel(startLevel, timesLeveled, out long cost))
                    {
                        playerMessages.Add($"Failed to get cost for {timesLeveled} levels of {target}.");
                        continue;
                    }

                    //Refund the resource and update the player
                    if (target.TryGetAttribute(player, out var attribute))
                    {
                        player.AvailableExperience += cost;
                        playerMessages.Add($"Refunding {timesLeveled} levels of {target} for {cost:N0} xp.");
                        //session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, attribute));
                    }                    
                    /*else
                    {
                         player.AvailableLuminance += cost;
                         playerMessages.Add($"Refunding {timesLeveled} levels of {target} for {cost:N0} lum.");
                    }*/
                    //Finally, set the level to what it should be
                    target.SetLevel(player, startLevel);
                }
                ////Send player their updated ratings
                //session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugAllSkills, player.LumAugAllSkills));
                //session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageReductionRating, player.LumAugDamageReductionRating));
                //session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageRating, player.LumAugDamageRating));
                ////Send updated resources
                //session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.AvailableLuminance, player.AvailableLuminance ?? 0));
                //session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.AvailableExperience, player.AvailableExperience ?? 0));
            }
            catch (Exception ex)
            {
                playerMessages.Add($"Failed to reset /raise values for {player.Account}\\{player.Name}.");
            }
            return playerMessages;
        }

        public static List<string> RaiseRefundOfflinePlayerVital(Player player)
        {
            var playerVitalMessages = new List<string>();

            try
            {
                foreach (var target in Enum.GetValues<RaiseTargetVital>())
                {
                    var vitallevel = target.GetVitalLevel(player);
                    var startVitalLevel = target.StartingVitalLevel();
                    var timesVitalLeveled = vitallevel - startVitalLevel;

                    //Check if anything is invested
                    if (timesVitalLeveled > 1)
                    {
                        continue;
                    }

                    //Get resource spent
                    if (!target.TryGetCostToLevelVital(startVitalLevel, timesVitalLeveled, out long cost))
                    {
                        playerVitalMessages.Add($"Failed to get cost for {timesVitalLeveled} levels of {target}.");
                        continue;
                    }

                    //Refund the resource and update the player
                    if (target.TryGetVital(player, out var vital))
                    {
                        player.AvailableExperience += cost;
                        playerVitalMessages.Add($"Refunding {timesVitalLeveled} levels of {target} for {cost:N0} xp.");
                        //session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, attribute));
                    }
                    target.SetVitalLevel(player, startVitalLevel);
                }
            }
            catch (Exception ex)
            {
                playerVitalMessages.Add($"Failed to reset /raise values for {player.Account}\\{player.Name}.");
            }
            return playerVitalMessages;
        }



        /// <summary>
        /// Refunds all resources used in the /raise command and informs the refunded player if possible.
        /// </summary>
        /// <param name="player">Player to refund</param>
        /// <param name="session">Optional session used to send messages and updated properties to.</param>
        public static void RaiseRefundToPlayer(Player player, Session session = null)
        {
            if (player == null)
                return;

            //Refund the player
            var refundMessages = RaiseRefundOfflinePlayer(player);
            var refundMessagesVital = RaiseRefundOfflinePlayerVital(player);

            //Providing a session will use that for the messaging, in case an admin wants to specify using their session.
            //Falls back to the players session if available
            if (session == null)
            {
                if (player.Session != null)
                    session = player.Session;
                else
                    return;
            }

            //Send messages
            foreach (var msg in refundMessages)
                ChatPacket.SendServerMessage(session, msg, ChatMessageType.Broadcast);

            UpdatePlayerRaiseProperties(player, session);


            foreach (var msg in refundMessagesVital)
                ChatPacket.SendServerMessage(session, msg, ChatMessageType.Broadcast);

            UpdatePlayerRaiseVital(player, session); 
        }

        private static void UpdatePlayerRaiseProperties(Player player, Session session)
        {
            //Send updated attributes
            foreach (var target in Enum.GetValues<RaiseTarget>())
            {
                if (target.TryGetAttribute(player, out var attribute))
                    session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, attribute));               
            }           
            //Send player their updated ratings
           // session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugAllSkills, player.LumAugAllSkills));
           // session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageReductionRating, player.LumAugDamageReductionRating));
           // session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageRating, player.LumAugDamageRating));
           // session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritDamageRating, player.LumAugCritDamageRating));
           // session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritReductionRating, player.LumAugCritReductionRating));
            //Send updated resources
           // session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.AvailableLuminance, player.AvailableLuminance ?? 0));
            session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.AvailableExperience, player.AvailableExperience ?? 0));
        }

        private static void UpdatePlayerRaiseVital(Player player, Session session)
        {
            //Send updated vitals           
            foreach (var target in Enum.GetValues<RaiseTargetVital>())
            {
                if (target.TryGetVital(player, out var vital))
                    session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, vital));
            }
            
            session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(player, PropertyInt64.AvailableExperience, player.AvailableExperience ?? 0));
        }

    }
}
