using System;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Entity.Actions;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        /// <summary>
        /// This is set by HandleActionUseItem / TryUseItem
        /// </summary>
        public ObjectGuid LastOpenedContainerId { get; set; }

        /// <summary>
        /// This is set by Hook.ActOnUse
        /// </summary>
        public ObjectGuid LasUsedHookId { get; set; }

        /// <summary>
        /// Handles the 'GameAction 0x35 - UseWithTarget' network message
        /// when player double clicks an inventory item resulting in a target indicator
        /// and then clicks another item
        /// </summary>
        public void HandleActionUseWithTarget(uint sourceObjectGuid, uint targetObjectGuid)
        {
            if (PKLogout)
            {
                SendUseDoneEvent(WeenieError.YouHaveBeenInPKBattleTooRecently);
                return;
            }

            StopExistingMoveToChains();

            // source item is always in our possession
            var sourceItem = FindObject(sourceObjectGuid, SearchLocations.MyInventory | SearchLocations.MyEquippedItems, out _, out _, out var sourceItemIsEquipped);

            if (sourceItem == null)
            {
                log.Warn($"{Name}.HandleActionUseWithTarget({sourceObjectGuid:X8}, {targetObjectGuid:X8}): couldn't find {sourceObjectGuid:X8}");
                SendUseDoneEvent();
                return;
            }

            // handle casters with built-in spells
            if (sourceItemIsEquipped)
            {
                if (sourceItem.SpellDID != null)
                {
                    // check activation requirements
                    var result = sourceItem.CheckUseRequirements(this);
                    if (!result.Success)
                    {
                        if (result.Message != null)
                            Session.Network.EnqueueSend(result.Message);

                        SendUseDoneEvent();
                    }
                    else
                        HandleActionCastTargetedSpell(targetObjectGuid, sourceItem.SpellDID ?? 0, true);
                }
                else
                {
                    SendUseDoneEvent();
                }

                return;
            }

            // Resolve the guid to an object that is either in our possession or on the Landblock
            var target = FindObject(targetObjectGuid, SearchLocations.MyInventory | SearchLocations.MyEquippedItems | SearchLocations.Landblock);

            if (target == null)
            {
                log.Warn($"{Name}.HandleActionUseWithTarget({sourceObjectGuid:X8}, {targetObjectGuid:X8}): couldn't find {targetObjectGuid:X8}");
                SendUseDoneEvent();
                return;
            }

            if (IsTrading)
            {
                if (ItemsInTradeWindow.Contains(sourceItem.Guid))
                {
                    SendUseDoneEvent(WeenieError.TradeItemBeingTraded);
                    //SendWeenieError(WeenieError.TradeItemBeingTraded);
                    return;
                }
                if (ItemsInTradeWindow.Contains(target.Guid))
                {
                    SendUseDoneEvent(WeenieError.TradeItemBeingTraded);
                    //SendWeenieError(WeenieError.TradeItemBeingTraded);
                    return;
                }
            }

            // re-verify client checks
            if (((sourceItem.TargetType ?? ItemType.None) & target.ItemType) == ItemType.None)
            {
                // ItemHolder::TargetCompatibleWithObject
                SendTransientError($"Cannot use the {sourceItem.Name} with the {target.Name}");
                SendUseDoneEvent();
                return;
            }

            if (target.CurrentLandblock != null && target != this)
            {
                // todo: verify target can be used remotely
                // move RecipeManager.VerifyUse logic into base Player_Use
                // this was avoided because i didn't want to deal with the ramifications of random items missing the correct ItemUseable flags,
                // and because there are still some ItemUseable flags with missing logic we haven't quite figured out yet

                if (IsBusy)
                {
                    SendUseDoneEvent(WeenieError.YoureTooBusy);
                    return;
                }

                CreateMoveToChain(target, (success) =>
                {
                    if (success)
                        sourceItem.HandleActionUseOnTarget(this, target);
                    else
                        SendUseDoneEvent();
                });
            }
            else
                sourceItem.HandleActionUseOnTarget(this, target);
        }

        /// <summary>
        /// Handles the 'GameAction 0x36 - UseItem' network message
        /// when player double clicks an item
        /// </summary>
        public void HandleActionUseItem(uint itemGuid)
        {
            if (PKLogout)
            {
                SendUseDoneEvent(WeenieError.YouHaveBeenInPKBattleTooRecently);
                return;
            }

            StopExistingMoveToChains();

            var item = FindObject(itemGuid, SearchLocations.MyInventory | SearchLocations.MyEquippedItems | SearchLocations.Landblock);

            if (IsTrading && ItemsInTradeWindow.Contains(item.Guid))
            {
                SendUseDoneEvent(WeenieError.TradeItemBeingTraded);
                //SendWeenieError(WeenieError.TradeItemBeingTraded);
                return;
            }

            if (item != null)
            {
                if (item.ItemType == ItemType.Portal)
                {
                    // kill pets
                    ActionChain killPets = new ActionChain();

                    killPets.AddAction(this, () =>
                    {
                        foreach (var monster in PhysicsObj.ObjMaint.GetVisibleObjectsValuesOfTypeCreature())
                        {
                            if (monster.IsCombatPet)
                            {
                                if (monster.PetOwner == Guid.Full)
                                {
                                    monster.Destroy();
                                    NumberOfPets--;
                                    if (NumberOfPets < 0)
                                    {
                                        NumberOfPets = 0;
                                    }
                                }
                            }
                        }
                    });

                    killPets.EnqueueChain();
                }

                // Ability Items
                if (item.IsAbilityItem)
                    DoAbility(this, item);

                if (item.CurrentLandblock != null && !item.Visibility && item.Guid != LastOpenedContainerId && !item.IsAbilityItem)
                {
                    if (IsBusy)
                    {
                        SendUseDoneEvent(WeenieError.YoureTooBusy);
                        return;
                    }

                    CreateMoveToChain(item, (success) => TryUseItem(item, success));
                }
                
                else
                    TryUseItem(item);
            }
            else
            {
                log.Debug($"{Name}.HandleActionUseItem({itemGuid:X8}): couldn't find object");
                SendUseDoneEvent();
            }
        }

        public DateTime NextUseTime { get; set; }
        public float LastUseTime { get; set; }

        /// <summary>
        /// Attempts to use an item - checks activation requirements
        /// </summary>
        public void TryUseItem(WorldObject item, bool success = true)
        {
            //Console.WriteLine($"{Name}.TryUseItem({item.Name}, {success})");
            LastUseTime = 0.0f;

            if (success)
                item.OnActivate(this);

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(LastUseTime);
            actionChain.AddAction(this, () => SendUseDoneEvent());
            actionChain.EnqueueChain();

            NextUseTime = DateTime.UtcNow + TimeSpan.FromSeconds(LastUseTime);
        }

        /// <summary>
        /// Sends the GameEventUseDone network message for a player
        /// </summary>
        /// <param name="errorType">An optional error message</param>
        public void SendUseDoneEvent(WeenieError errorType = WeenieError.None)
        {
            Session.Network.EnqueueSend(new GameEventUseDone(Session, errorType));
        }


        /// <summary>
        /// This method processes the Game Action (F7B1) No Longer Viewing Contents (0x0195)
        /// This is raised when we:
        /// - have a container open and open up a second container without closing the first container.
        /// </summary>
        public void HandleActionNoLongerViewingContents(uint objectGuid)
        {
            var container = CurrentLandblock?.GetObject(objectGuid) as Container;

            if (container != null && container.Viewer == Guid.Full)
                container.Close(this);
        }

        public Pet CurrentActivePet { get; set; }

        public void StartBarber()
        {
            BarberActive = true;
            Session.Network.EnqueueSend(new GameEventStartBarber(Session));
        }

        public void ApplyConsumable(MotionCommand useMotion, Action action, float animMod = 1.0f)
        {
            IsBusy = true;

            var actionChain = new ActionChain();

            // if something other that NonCombat.Ready,
            // manually send this swap
            var prevStance = CurrentMotionState.Stance;

            var animTime = 0.0f;

            if (prevStance != MotionStance.NonCombat)
                animTime = EnqueueMotion_Force(actionChain, MotionStance.NonCombat, MotionCommand.Ready, (MotionCommand)prevStance);

            // start the eat/drink motion
            var useAnimTime = EnqueueMotion_Force(actionChain, MotionStance.NonCombat, useMotion, null, 1.0f, animMod);
            animTime += useAnimTime;

            // apply consumable
            actionChain.AddAction(this, action);

            if (animMod == 1.0f)
            {
                // return to ready stance
                animTime += EnqueueMotion_Force(actionChain, MotionStance.NonCombat, MotionCommand.Ready, useMotion);
            }
            else
                actionChain.AddDelaySeconds(useAnimTime * (1.0f - animMod));

            if (prevStance != MotionStance.NonCombat)
                animTime += EnqueueMotion_Force(actionChain, prevStance, MotionCommand.Ready, MotionCommand.NonCombat);

            actionChain.AddAction(this, () => { IsBusy = false; });

            actionChain.EnqueueChain();

            LastUseTime = animTime;
        }
    }
}
