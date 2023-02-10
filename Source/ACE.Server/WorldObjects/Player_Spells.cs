using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.DatLoader;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network.Structure;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Entity.Enum.Properties;
using ACE.Server.Factories.Tables;
using MySqlX.XDevAPI.Common;
using ACE.Common;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        public bool SpellIsKnown(uint spellId)
        {
            return Biota.SpellIsKnown((int)spellId, BiotaDatabaseLock);
        }

        /// <summary>
        /// Will return true if the spell was added, or false if the spell already exists.
        /// </summary>
        public bool AddKnownSpell(uint spellId)
        {
            Biota.GetOrAddKnownSpell((int)spellId, BiotaDatabaseLock, out var spellAdded);

            if (spellAdded)
                ChangesDetected = true;

            return spellAdded;
        }

        /// <summary>
        /// Removes a known spell from the player's spellbook
        /// </summary>
        public bool RemoveKnownSpell(uint spellId)
        {
            return Biota.TryRemoveKnownSpell((int)spellId, BiotaDatabaseLock);
        }

        public void LearnSpellWithNetworking(uint spellId, bool uiOutput = true)
        {
            var spells = DatManager.PortalDat.SpellTable;

            if (!spells.Spells.ContainsKey(spellId))
            {
                GameMessageSystemChat errorMessage = new GameMessageSystemChat("SpellID not found in Spell Table", ChatMessageType.Broadcast);
                Session.Network.EnqueueSend(errorMessage);
                return;
            }

            if (!AddKnownSpell(spellId))
            {
                GameMessageSystemChat errorMessage = new GameMessageSystemChat("That spell is already known", ChatMessageType.Broadcast);
                Session.Network.EnqueueSend(errorMessage);
                return;
            }

            GameEventMagicUpdateSpell updateSpellEvent = new GameEventMagicUpdateSpell(Session, (ushort)spellId);
            Session.Network.EnqueueSend(updateSpellEvent);

            //Check to see if we echo output to the client
            if (uiOutput == true)
            {
                // Always seems to be this SkillUpPurple effect
                ApplyVisualEffects(PlayScript.SkillUpPurple);

                string message = $"You learn the {spells.Spells[spellId].Name} spell.\n";
                GameMessageSystemChat learnMessage = new GameMessageSystemChat(message, ChatMessageType.Broadcast);
                Session.Network.EnqueueSend(learnMessage);
            }
        }

        /// <summary>
        ///  Learns spells in bulk, without notification, filtered by school and level
        /// </summary>
        public void LearnSpellsInBulk(MagicSchool school, uint spellLevel, bool withNetworking = true)
        {
            var spellTable = DatManager.PortalDat.SpellTable;

            foreach (var spellID in PlayerSpellTable)
            {
                if (!spellTable.Spells.ContainsKey(spellID))
                {
                    Console.WriteLine($"Unknown spell ID in PlayerSpellID table: {spellID}");
                    continue;
                }
                var spell = new Spell(spellID, false);
                if (spell.School == school && spell.Formula.Level == spellLevel)
                {
                    if (withNetworking)
                        LearnSpellWithNetworking(spell.Id, false);
                    else
                        AddKnownSpell(spell.Id);
                }
            }
        }

        public void HandleActionMagicRemoveSpellId(uint spellId)
        {
            if (!Biota.TryRemoveKnownSpell((int)spellId, BiotaDatabaseLock))
            {
                log.Error("Invalid spellId passed to Player.RemoveSpellFromSpellBook");
                return;
            }

            ChangesDetected = true;

            GameEventMagicRemoveSpell removeSpellEvent = new GameEventMagicRemoveSpell(Session, (ushort)spellId);
            Session.Network.EnqueueSend(removeSpellEvent);
        }

        public void EquipItemFromSet(WorldObject item)
        {
            if (!item.HasItemSet) return;

            var setItems = EquippedObjects.Values.Where(i => i.HasItemSet && i.EquipmentSetId == item.EquipmentSetId).ToList();

            var spells = GetSpellSet(setItems);

            // get the spells from before / without this item
            setItems.Remove(item);
            var prevSpells = GetSpellSet(setItems);

            EquipDequipItemFromSet(item, spells, prevSpells);
        }

        public void EquipDequipItemFromSet(WorldObject item, List<Spell> spells, List<Spell> prevSpells, WorldObject surrogateItem = null)
        {
            // compare these 2 spell sets -
            // see which spells are being added, and which are being removed
            var addSpells = spells.Except(prevSpells);
            var removeSpells = prevSpells.Except(spells);

            // set spells are not affected by mana
            // if it's equipped, it's active.

            foreach (var spell in removeSpells)
                EnchantmentManager.Dispel(EnchantmentManager.GetEnchantment(spell.Id, item.EquipmentSetId.Value));

            var addItem = surrogateItem ?? item;

            foreach (var spell in addSpells)
                CreateItemSpell(addItem, spell.Id);
        }

        public void DequipItemFromSet(WorldObject item)
        {
            if (!item.HasItemSet) return;

            var setItems = EquippedObjects.Values.Where(i => i.HasItemSet && i.EquipmentSetId == item.EquipmentSetId).ToList();

            // for better bookkeeping, and to avoid a rarish error with AuditItemSpells detecting -1 duration item enchantments where
            // the CasterGuid is no longer in the player's possession
            var surrogateItem = setItems.LastOrDefault();

            var spells = GetSpellSet(setItems);

            // get the spells from before / with this item
            setItems.Add(item);
            var prevSpells = GetSpellSet(setItems);

            if (surrogateItem == null)
            {
                var addSpells = spells.Except(prevSpells);

                if (addSpells.Count() != 0)
                    log.Error($"{Name}.DequipItemFromSet({item.Name}) -- last item in set dequipped, but addSpells still contains {string.Join(", ", addSpells.Select(i => i.Name))} -- this shouldn't happen!");
            }

            EquipDequipItemFromSet(item, spells, prevSpells, surrogateItem);
        }

        public void OnItemLevelUp(WorldObject item, int prevItemLevel)
        {
            if (!item.HasItemLevel) return;

            var setItems = EquippedObjects.Values.Where(i => i.HasItemSet && i.EquipmentSetId == item.EquipmentSetId).ToList();

            var levelDiff = prevItemLevel - (item.ItemLevel ?? 0);

            var levelDiff1 = (item.ItemLevel) - prevItemLevel;

            var prevSpells = GetSpellSet(setItems, levelDiff);

            var spells = GetSpellSet(setItems);

            EquipDequipItemFromSet(item, spells, prevSpells);

            // grant level up bonus
            for (int j = 0; j < levelDiff1; j++)
            {
                var actionChain = new ActionChain();
                actionChain.AddAction(this, () =>


                {
                    var baseRatingIncrease = 10;
                    var itemtype = item.GetProperty(PropertyInt.ItemType) ?? 0;

                    EnqueueBroadcast(new GameMessageScript(Guid, PlayScript.AetheriaLevelUp));

                    // decide what do with each ItemType
                    var proto = item.GetProperty(PropertyBool.Proto);
                    var itemLevel = item.ItemLevel;

                    if (itemtype == 256 && proto == false && item.Arramoran != true || itemtype == 256 && proto == null && item.Arramoran != true) // MissileWeapon
                    {

                        item.ElementalDamageBonus++;

                        var name = item.GetProperty(PropertyString.Name);
                        var damagebonus = item.GetProperty(PropertyInt.ElementalDamageBonus) ?? 0;
                        var damagebonusupdate = new GameMessagePrivateUpdatePropertyInt(item, PropertyInt.ElementalDamageBonus, ElementalDamageBonus ?? 0);
                        var geardamagebonus = new GameMessagePrivateUpdatePropertyInt(item, PropertyInt.GearDamage, GearDamage ?? 0);
                        var bonusmessage = $"Your {name}'s Damage Bonus has increased by 1. And is now {damagebonus} !";
                        Session.Network.EnqueueSend(new GameMessageSystemChat(bonusmessage, ChatMessageType.Broadcast));
                        Session.Network.EnqueueSend(damagebonusupdate);
                        Session.Network.EnqueueSend(geardamagebonus);

                    }

                    if (itemtype == 2 && proto == false && item.Arramoran != true || itemtype == 2 && proto == null && item.Arramoran != true) // Armor
                    {

                        item.ArmorLevel++;

                        var name = item.GetProperty(PropertyString.Name);
                        var armorlevel = item.GetProperty(PropertyInt.ArmorLevel) ?? 0;
                        var armorbonusupdate = new GameMessagePrivateUpdatePropertyInt(item, PropertyInt.ArmorLevel, ArmorLevel ?? 0);
                        var gearvitalitybonus = new GameMessagePrivateUpdatePropertyInt(item, PropertyInt.GearMaxHealth, GearMaxHealth ?? 0);
                        var bonusmessage = $"Your {name}'s Armor Level has increased by 1. And is now {armorlevel} !";
                        Session.Network.EnqueueSend(new GameMessageSystemChat(bonusmessage, ChatMessageType.Broadcast));
                        Session.Network.EnqueueSend(armorbonusupdate);
                        Session.Network.EnqueueSend(gearvitalitybonus);

                    }

                    if (itemtype == 1 && proto == false && item.Arramoran != true || itemtype == 1 && proto == null && item.Arramoran != true) // MelleeWeapon
                    {

                        item.Damage++;

                        var name = item.GetProperty(PropertyString.Name);
                        var damagebonus = item.GetProperty(PropertyInt.Damage) ?? 0;
                        var damagebonusupdate = new GameMessagePrivateUpdatePropertyInt(item, PropertyInt.Damage, Damage ?? 0);
                        var geardamagebonus = new GameMessagePrivateUpdatePropertyInt(item, PropertyInt.GearDamage, GearDamage ?? 0);
                        var bonusmessage = $"Your {name}'s Damage has increased by 1. And is now {damagebonus} !";
                        Session.Network.EnqueueSend(new GameMessageSystemChat(bonusmessage, ChatMessageType.Broadcast));
                        Session.Network.EnqueueSend(damagebonusupdate);
                        Session.Network.EnqueueSend(geardamagebonus);

                    }

                    if (itemtype == 32768 && proto == false && item.Arramoran != true || itemtype == 32768 && proto == null && item.Arramoran != true) // Caster
                    {
                        var weapondamage = item.GetProperty(PropertyFloat.ElementalDamageMod);
                        float increment = 0.01f;
                        float newweapondamage = (float)(weapondamage + increment);
                        item.SetProperty(PropertyFloat.ElementalDamageMod, (float)newweapondamage);



                        var name = item.GetProperty(PropertyString.Name);
                        var damagebonusupdate = new GameMessagePrivateUpdatePropertyFloat(item, PropertyFloat.ElementalDamageMod, ElementalDamageMod ?? 0);
                        var geardamagebonus = new GameMessagePrivateUpdatePropertyInt(item, PropertyInt.GearDamage, GearDamage ?? 0);
                        var bonusmessage = $"Your {name}'s Elemental Damage Bonus has increased by 1%. And is now {newweapondamage} !";
                        Session.Network.EnqueueSend(new GameMessageSystemChat(bonusmessage, ChatMessageType.Broadcast));
                        Session.Network.EnqueueSend(damagebonusupdate);
                        Session.Network.EnqueueSend(geardamagebonus);

                    }

                    //Proto Weapons
                    var damageType = item.GetProperty(PropertyInt.DamageType);

                    if (itemtype == 256 && proto == true && item.Arramoran != true) // MissileWeapon
                    {

                        item.ElementalDamageBonus++;

                        var name = item.GetProperty(PropertyString.Name);
                        var damagebonus = item.GetProperty(PropertyInt.ElementalDamageBonus) ?? 0;
                        var damagebonusupdate = new GameMessagePrivateUpdatePropertyInt(item, PropertyInt.ElementalDamageBonus, ElementalDamageBonus ?? 0);
                        var geardamagebonus = new GameMessagePrivateUpdatePropertyInt(item, PropertyInt.GearDamage, GearDamage ?? 0);
                        var bonusmessage = $"Your {name}'s Damage Bonus has increased by 1. And is now {damagebonus} !";
                        Session.Network.EnqueueSend(new GameMessageSystemChat(bonusmessage, ChatMessageType.Broadcast));
                        Session.Network.EnqueueSend(damagebonusupdate);
                        Session.Network.EnqueueSend(geardamagebonus);

                        // Proto Evolution
                        // Item "wakes up" and becomes Attuned and Bonded
                        if (itemLevel >= 1)
                        {
                            item.SetProperty(PropertyInt.Bonded, 1);
                            item.SetProperty(PropertyInt.Attuned, 1);

                            if (damageType == 1)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 8);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x600335C);
                            }
                            if (damageType == 2)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 16);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x600335B);
                            }

                            if (damageType == 4)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 32);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x600335A);
                            }

                            if (damageType == 8)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 128);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x6003353);
                            }
                            if (damageType == 16)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 512);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x6003359);
                            }

                            if (damageType == 32)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 64);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x6003355);
                            }

                            if (damageType == 64)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 256);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x6003354);
                            }
                            if (damageType == 1024)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 16384);
                            }
                        }
                        // Add cleaving at level 200
                        if (itemLevel >= 200)
                        {
                           
                            item.SetProperty(PropertyInt.Cleaving, 3); 

                        }
                        // Add spell proc at level 100
                        if (itemLevel >= 100)
                        {
                            if (damageType == 1) // Slash
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4457);
                            }
                            if (damageType == 2) // Pierce
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4443);
                            }

                            if (damageType == 4) // Bludgeon
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4455);
                            }

                            if (damageType == 8) // Cold
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4447);
                            }
                            if (damageType == 16) // Fire
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4439);
                            }

                            if (damageType == 32) // Acid
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4433);
                            }

                            if (damageType == 64) // Electric
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4451);
                            }
                            if (damageType == 1024) // Nether
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 5356);
                            }

                        }
                        // Increase SpellProcRate every 100 levels from level 300 - 500
                        if (itemLevel >= 300)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 0.6);

                        }
                        if (itemLevel >= 400)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 0.8);

                        }
                        if (itemLevel >= 500)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 1.0);

                        }


                    }
                    if (itemtype == 2 && proto == true && !Arramoran) // Armor
                    {

                        item.ArmorLevel++;

                        var name = item.GetProperty(PropertyString.Name);
                        var armorlevel = item.GetProperty(PropertyInt.ArmorLevel) ?? 0;
                        var armorbonusupdate = new GameMessagePrivateUpdatePropertyInt(item, PropertyInt.ArmorLevel, ArmorLevel ?? 0);
                        var gearvitalitybonus = new GameMessagePrivateUpdatePropertyInt(item, PropertyInt.GearMaxHealth, GearMaxHealth ?? 0);
                        var bonusmessage = $"Your {name}'s Armor Level has increased by 1. And is now {armorlevel} !";
                        Session.Network.EnqueueSend(new GameMessageSystemChat(bonusmessage, ChatMessageType.Broadcast));
                        Session.Network.EnqueueSend(armorbonusupdate);
                        Session.Network.EnqueueSend(gearvitalitybonus);
                        if (itemLevel == 1)
                        {
                            item.SetProperty(PropertyInt.Bonded, 1);
                            item.SetProperty(PropertyInt.Attuned, 1);

                            var protoFirstLevel = $"Your {name}'s has awakened! And is now symbiotically bonded to you!";
                            Session.Network.EnqueueSend(new GameMessageSystemChat(protoFirstLevel, ChatMessageType.Broadcast));
                        }

                    }
                    if (itemtype == 1 && proto == true && !Arramoran) // MelleeWeapon
                    {

                        item.Damage++;

                        var name = item.GetProperty(PropertyString.Name);
                        var damagebonus = item.GetProperty(PropertyInt.Damage) ?? 0;
                        var damagebonusupdate = new GameMessagePrivateUpdatePropertyInt(item, PropertyInt.Damage, Damage ?? 0);
                        var geardamagebonus = new GameMessagePrivateUpdatePropertyInt(item, PropertyInt.GearDamage, GearDamage ?? 0);
                        var bonusmessage = $"Your {name}'s Damage has increased by 1. And is now {damagebonus} !";
                        Session.Network.EnqueueSend(new GameMessageSystemChat(bonusmessage, ChatMessageType.Broadcast));
                        Session.Network.EnqueueSend(damagebonusupdate);
                        Session.Network.EnqueueSend(geardamagebonus);

                        // Proto Evolution
                        // Item "wakes up" and becomes Attuned and Bonded
                        var attacktype = item.GetProperty(PropertyInt.AttackType);
                        if (itemLevel >= 1)
                        {
                            item.SetProperty(PropertyInt.Bonded, 1);
                            item.SetProperty(PropertyInt.Attuned, 1);

                            if (damageType == 1)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 8);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x600335C);
                            }
                            if (damageType == 2)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 16);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x600335B);
                            }
                            if (damageType == 3)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 16);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x600335B);
                            }
                            if (damageType == 4)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 32);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x600335A);
                            }

                            if (damageType == 8)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 128);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x6003353);
                            }
                            if (damageType == 16)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 512);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x6003359);
                            }

                            if (damageType == 32)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 64);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x6003355);
                            }

                            if (damageType == 64)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 256);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x6003354);
                            }
                            if (damageType == 1024)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 16384);
                            }
                        }
                        // Add cleaving at level 200
                        if (itemLevel >= 200)
                        {
                            if (attacktype > 34)
                            {
                                item.SetProperty(PropertyInt.Cleaving, 5);
                            }
                            else 
                                item.SetProperty(PropertyInt.Cleaving, 3);


                        }
                        // Add spell proc at level 100
                        if (itemLevel >= 100)
                        {
                            if (damageType == 1) // Slash
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4457);
                            }
                            if (damageType == 2) // Pierce
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4443);
                            }

                            if (damageType == 4) // Bludgeon
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4455);
                            }

                            if (damageType == 8) // Cold
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4447);
                            }
                            if (damageType == 16) // Fire
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4439);
                            }

                            if (damageType == 32) // Acid
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4433);
                            }

                            if (damageType == 64) // Electric
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4451);
                            }
                            if (damageType == 1024) // Nether
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 5356);
                            }

                        }
                        // Increase SpellProcRate every 100 levels from level 300 - 500
                        if (itemLevel >= 300)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 0.6);

                        }
                        if (itemLevel >= 400)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 0.8);

                        }
                        if (itemLevel >= 500)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 1.0);

                        }
                    }
                    if (itemtype == 32768 && proto == true && item.Arramoran != true) // Caster
                    {
                        var weapondamage = item.GetProperty(PropertyFloat.ElementalDamageMod);
                        float increment = 0.005f;
                        float newweapondamage = (float)(weapondamage + increment);
                        item.SetProperty(PropertyFloat.ElementalDamageMod, (float)newweapondamage);



                        var name = item.GetProperty(PropertyString.Name);
                        var damagebonusupdate = new GameMessagePrivateUpdatePropertyFloat(item, PropertyFloat.ElementalDamageMod, ElementalDamageMod ?? 0);
                        var geardamagebonus = new GameMessagePrivateUpdatePropertyInt(item, PropertyInt.GearDamage, GearDamage ?? 0);
                        var bonusmessage = $"Your {name}'s Elemental Damage Bonus has increased by 1%. And is now {newweapondamage} !";
                        Session.Network.EnqueueSend(new GameMessageSystemChat(bonusmessage, ChatMessageType.Broadcast));
                        Session.Network.EnqueueSend(damagebonusupdate);
                        Session.Network.EnqueueSend(geardamagebonus);

                        // Proto Evolution
                        // Item "wakes up" and becomes Attuned and Bonded                      
                        if (itemLevel >= 1)
                        {
                            item.SetProperty(PropertyInt.Bonded, 1);
                            item.SetProperty(PropertyInt.Attuned, 1);

                            if (damageType == 1)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 8);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x600335C);
                            }
                            if (damageType == 2)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 16);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x600335B);
                            }

                            if (damageType == 4)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 32);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x600335A);
                            }

                            if (damageType == 8)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 128);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x6003353);
                            }
                            if (damageType == 16)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 512);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x6003359);
                            }

                            if (damageType == 32)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 64);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x6003355);
                            }

                            if (damageType == 64)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 256);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x6003354);
                            }
                            if (damageType == 1024)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 16384);
                            }
                        }
                        // Add cleaving at level 200
                        if (itemLevel >= 200)
                        {                            
                                item.SetProperty(PropertyInt.Cleaving, 3);
                        }
                        // Add spell proc at level 100
                        if (itemLevel >= 100)
                        {
                            if (damageType == 1) // Slash
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4457);
                            }
                            if (damageType == 2) // Pierce
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4443);
                            }

                            if (damageType == 4) // Bludgeon
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4455);
                            }

                            if (damageType == 8) // Cold
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4447);
                            }
                            if (damageType == 16) // Fire
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4439);
                            }

                            if (damageType == 32) // Acid
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4433);
                            }

                            if (damageType == 64) // Electric
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4451);
                            }
                            if (damageType == 1024) // Nether
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 5356);
                            }

                        }
                        // Increase SpellProcRate every 100 levels from level 300 - 500
                        if (itemLevel >= 300)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 0.6);

                        }
                        if (itemLevel >= 400)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 0.8);

                        }
                        if (itemLevel >= 500)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 1.0);

                        }

                    }
                    if (itemtype == 8 && proto == true) // Jewelry
                    {                             
                        // Proto Evolution
                        // Item "wakes up" and becomes Attuned and Bonded                      
                        if (itemLevel >= 1)
                        {
                            item.SetProperty(PropertyInt.Bonded, 1);
                            item.SetProperty(PropertyInt.Attuned, 1);

                            if (itemLevel >= 1 && !item.HasProc)
                            {
                                var spellProc = ThreadSafeRandom.Next(0.0f, 1.0f);

                                if (spellProc <= 0.3f)
                                {
                                    item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                    item.SetProperty(PropertyFloat.ProcSpellRate, 0.05f);
                                    item.SetProperty(PropertyDataId.ProcSpell, 4643);
                                }
                                else if (spellProc > 0.3f && spellProc < 0.6f)
                                {
                                    item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                    item.SetProperty(PropertyFloat.ProcSpellRate, 0.05f);
                                    item.SetProperty(PropertyDataId.ProcSpell, 4644);
                                }
                                else if (spellProc >= 0.3f)
                                {
                                    item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                    item.SetProperty(PropertyFloat.ProcSpellRate, 0.05f);
                                    item.SetProperty(PropertyDataId.ProcSpell, 4645);
                                }
                            }
                            item.SetProperty(PropertyFloat.ProcSpellRate, 0.05f);
                        }
                       
                        // Increase to proc rate
                        if (itemLevel >= 200)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 0.09f);
                        }
                        // Increase to proc rate
                        if (itemLevel >= 100)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 0.07f);
                        }
                        // Increase to proc rate
                        if (itemLevel >= 300)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 0.11);
                        }
                        // Increase to proc rate
                        if (itemLevel >= 400)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 0.13);
                        }
                        // Increase to proc rate
                        if (itemLevel >= 500)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 0.15);
                        }

                        var procrate = item.GetProperty(PropertyFloat.ProcSpellRate);
                        var name = item.GetProperty(PropertyString.Name);
                        var bonusmessage = $"Your {name}'s spell proc rate has increased. And is now {procrate} !";
                        Session.Network.EnqueueSend(new GameMessageSystemChat(bonusmessage, ChatMessageType.Broadcast));
                    }

                    // T10 Arramoran gear
                    if (itemtype == 256 && item.Arramoran == true && Proto == false) // MissileWeapon
                    {

                        item.ElementalDamageBonus++;

                        var name = item.GetProperty(PropertyString.Name);
                        var damagebonus = item.GetProperty(PropertyInt.ElementalDamageBonus) ?? 0;
                        var damagebonusupdate = new GameMessagePrivateUpdatePropertyInt(item, PropertyInt.ElementalDamageBonus, ElementalDamageBonus ?? 0);
                        var geardamagebonus = new GameMessagePrivateUpdatePropertyInt(item, PropertyInt.GearDamage, GearDamage ?? 0);
                        var bonusmessage = $"Your {name}'s Damage Bonus has increased by 1. And is now {damagebonus} !";
                        Session.Network.EnqueueSend(new GameMessageSystemChat(bonusmessage, ChatMessageType.Broadcast));
                        Session.Network.EnqueueSend(damagebonusupdate);
                        Session.Network.EnqueueSend(geardamagebonus);

                        // Proto Evolution
                        // Item "wakes up" and becomes Attuned and Bonded
                        if (itemLevel >= 1)
                        {
                            item.SetProperty(PropertyInt.Bonded, 1);
                            item.SetProperty(PropertyInt.Attuned, 1);

                            if (damageType == 1)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 8);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x600335C);
                            }
                            if (damageType == 2)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 16);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x600335B);
                            }

                            if (damageType == 4)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 32);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x600335A);
                            }

                            if (damageType == 8)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 128);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x6003353);
                            }
                            if (damageType == 16)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 512);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x6003359);
                            }

                            if (damageType == 32)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 64);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x6003355);
                            }

                            if (damageType == 64)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 256);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x6003354);
                            }
                            if (damageType == 1024)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 16384);
                            }
                        }
                        // Add cleaving at level 200
                        if (itemLevel >= 200)
                        {

                            item.SetProperty(PropertyInt.Cleaving, 3);
                            

                        }
                        // Add spell proc at level 100
                        if (itemLevel >= 100)
                        {
                            if (damageType == 1) // Slash
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4457);
                            }
                            if (damageType == 2) // Pierce
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4443);
                            }

                            if (damageType == 4) // Bludgeon
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4455);
                            }

                            if (damageType == 8) // Cold
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4447);
                            }
                            if (damageType == 16) // Fire
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4439);
                            }

                            if (damageType == 32) // Acid
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4433);
                            }

                            if (damageType == 64) // Electric
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4451);
                            }
                            if (damageType == 1024) // Nether
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 5356);
                            }

                            

                        }
                        // Increase SpellProcRate every 100 levels from level 300 - 500
                        if (itemLevel >= 300)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 0.6);
                            

                        }
                        if (itemLevel >= 400)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 0.8);
                            

                        }
                        if (itemLevel >= 500)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 1.0);
                            

                        }


                    }
                    if (itemtype == 2 && item.Arramoran || item.Arramoran && HasArmorLevel()) // Armor
                    {

                        item.ArmorLevel++;

                        var name = item.GetProperty(PropertyString.Name);
                        var armorlevel = item.GetProperty(PropertyInt.ArmorLevel) ?? 0;
                        var armorbonusupdate = new GameMessagePrivateUpdatePropertyInt(item, PropertyInt.ArmorLevel, ArmorLevel ?? 0);
                        var gearvitalitybonus = new GameMessagePrivateUpdatePropertyInt(item, PropertyInt.GearMaxHealth, GearMaxHealth ?? 0);
                        var bonusmessage = $"Your {name}'s Armor Level has increased by 1. And is now {armorlevel} !";
                        var gearHealingBoost = item.GearHealingBoost;
                        var gearMaxHealth = item.GearMaxHealth;
                        var gearCritDamage = item.GearCritDamage;
                        var gearCritDamageResist = item.GearCritDamageResist;
                        var gearDamage = item.GearDamage;
                        var gearDamageResist = item.GearDamageResist;
                        var ratingIncrease = ThreadSafeRandom.Next(1, 5);                       
                        Session.Network.EnqueueSend(new GameMessageSystemChat(bonusmessage, ChatMessageType.Broadcast));
                        Session.Network.EnqueueSend(armorbonusupdate);
                        Session.Network.EnqueueSend(gearvitalitybonus);
                        if (itemLevel == 1)
                        {
                            item.SetProperty(PropertyInt.Bonded, 1);
                            item.SetProperty(PropertyInt.Attuned, 1);

                            var protoFirstLevel = $"Your {name}'s has awakened! And is now symbiotically bonded to you!";
                            Session.Network.EnqueueSend(new GameMessageSystemChat(protoFirstLevel, ChatMessageType.Broadcast));
                        }
                        if (itemLevel >= 100)
                        {
                            item.GearHealingBoost += 1;
                            item.GearMaxHealth += 1;
                            item.GearCritDamage += 1;
                            item.GearCritDamageResist += 1;
                            item.GearDamage += 1;
                            item.GearDamageResist += 1;
                            item.ArmorModVsPierce += 0.02f;
                            item.ArmorModVsSlash += 0.02f;
                            item.ArmorModVsBludgeon += 0.02f;
                            item.ArmorModVsAcid += 0.02f;
                            item.ArmorModVsFire += 0.02f;
                            item.ArmorModVsCold += 0.02f;
                            item.ArmorModVsElectric += 0.02f;
                            return;
                        }
                        if (itemLevel >= 200)
                        {
                            item.GearHealingBoost += 1;
                            item.GearMaxHealth += 1;
                            item.GearCritDamage += 1;
                            item.GearCritDamageResist += 1;
                            item.GearDamage += 1;
                            item.GearDamageResist += 1;
                            item.ArmorModVsPierce += 0.02f;
                            item.ArmorModVsSlash += 0.02f;
                            item.ArmorModVsBludgeon += 0.02f;
                            item.ArmorModVsAcid += 0.02f;
                            item.ArmorModVsFire += 0.02f;
                            item.ArmorModVsCold += 0.02f;
                            item.ArmorModVsElectric += 0.02f;
                            return;
                        }
                        if (itemLevel >= 300)
                        {
                            item.GearHealingBoost += 1;
                            item.GearMaxHealth += 1;
                            item.GearCritDamage += 1;
                            item.GearCritDamageResist += 1;
                            item.GearDamage += 1;
                            item.GearDamageResist += 1;
                            item.ArmorModVsPierce += 0.02f;
                            item.ArmorModVsSlash += 0.02f;
                            item.ArmorModVsBludgeon += 0.02f;
                            item.ArmorModVsAcid += 0.02f;
                            item.ArmorModVsFire += 0.02f;
                            item.ArmorModVsCold += 0.02f;
                            item.ArmorModVsElectric += 0.02f;
                            return;
                        }
                        if (itemLevel >= 400)
                        {
                            item.GearHealingBoost += 1;
                            item.GearMaxHealth += 1;
                            item.GearCritDamage += 1;
                            item.GearCritDamageResist += 1;
                            item.GearDamage += 1;
                            item.GearDamageResist += 1;
                            item.ArmorModVsPierce += 0.02f;
                            item.ArmorModVsSlash += 0.02f;
                            item.ArmorModVsBludgeon += 0.02f;
                            item.ArmorModVsAcid += 0.02f;
                            item.ArmorModVsFire += 0.02f;
                            item.ArmorModVsCold += 0.02f;
                            item.ArmorModVsElectric += 0.02f;
                            return;
                        }
                        if (itemLevel >= 500)
                        {
                            item.GearHealingBoost += 1;
                            item.GearMaxHealth += 1;
                            item.GearCritDamage += 1;
                            item.GearCritDamageResist += 1;
                            item.GearDamage += 1;
                            item.GearDamageResist += 1;
                            item.ArmorModVsPierce += 0.02f;
                            item.ArmorModVsSlash += 0.02f;
                            item.ArmorModVsBludgeon += 0.02f;
                            item.ArmorModVsAcid += 0.02f;
                            item.ArmorModVsFire += 0.02f;
                            item.ArmorModVsCold += 0.02f;
                            item.ArmorModVsElectric += 0.02f;
                            return;
                        }

                        /*if (item.Level % 100 == 0)
                        {
                            item.GearHealingBoost = gearHealingBoost + ratingIncrease;
                            item.GearMaxHealth = gearMaxHealth + ratingIncrease;
                            item.GearCritDamage = gearCritDamage + ratingIncrease;
                            item.GearCritDamageResist = gearCritDamageResist + ratingIncrease;
                            item.GearDamage = gearDamage + ratingIncrease;
                            item.GearDamageResist = gearDamageResist + ratingIncrease;
                        }*/
                    }
                    if (itemtype == 1 && item.Arramoran) // MelleeWeapon
                    {

                        item.Damage++;

                        var name = item.GetProperty(PropertyString.Name);
                        var damagebonus = item.GetProperty(PropertyInt.Damage) ?? 0;
                        var damagebonusupdate = new GameMessagePrivateUpdatePropertyInt(item, PropertyInt.Damage, Damage ?? 0);
                        var geardamagebonus = new GameMessagePrivateUpdatePropertyInt(item, PropertyInt.GearDamage, GearDamage ?? 0);
                        var bonusmessage = $"Your {name}'s Damage has increased by 1. And is now {damagebonus} !";
                        Session.Network.EnqueueSend(new GameMessageSystemChat(bonusmessage, ChatMessageType.Broadcast));
                        Session.Network.EnqueueSend(damagebonusupdate);
                        Session.Network.EnqueueSend(geardamagebonus);

                        // Proto Evolution
                        // Item "wakes up" and becomes Attuned and Bonded
                        var attacktype = item.GetProperty(PropertyInt.AttackType);
                        if (itemLevel >= 1)
                        {
                            item.SetProperty(PropertyInt.Bonded, 1);
                            item.SetProperty(PropertyInt.Attuned, 1);

                            if (damageType == 1)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 8);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x600335C);
                            }
                            if (damageType == 2)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 16);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x600335B);
                            }
                            if (damageType == 3)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 16);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x600335B);
                            }

                            if (damageType == 4)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 32);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x600335A);
                            }

                            if (damageType == 8)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 128);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x6003353);
                            }
                            if (damageType == 16)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 512);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x6003359);
                            }

                            if (damageType == 32)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 64);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x6003355);
                            }

                            if (damageType == 64)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 256);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x6003354);
                            }
                            if (damageType == 1024)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 16384);
                            }
                        }
                        // Add cleaving at level 200
                        if (itemLevel >= 200)
                        {
                            if (attacktype > 34)
                            {
                                item.SetProperty(PropertyInt.Cleaving, 5);                                
                            }
                            else
                                item.SetProperty(PropertyInt.Cleaving, 3);

                            item.GearHealingBoost += (ThreadSafeRandom.Next(5, 15));
                            item.GearMaxHealth += (ThreadSafeRandom.Next(5, 15));
                            item.GearCritDamage += (ThreadSafeRandom.Next(5, 15));
                            item.GearCritDamageResist += (ThreadSafeRandom.Next(5, 15));
                            item.GearDamage += (ThreadSafeRandom.Next(5, 15));
                            item.GearDamageResist += (ThreadSafeRandom.Next(5, 15));


                        }
                        // Add spell proc at level 100
                        if (itemLevel >= 100)
                        {
                            if (damageType == 1) // Slash
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4457);
                            }
                            if (damageType == 2) // Pierce
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4443);
                            }

                            if (damageType == 4) // Bludgeon
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4455);
                            }

                            if (damageType == 8) // Cold
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4447);
                            }
                            if (damageType == 16) // Fire
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4439);
                            }

                            if (damageType == 32) // Acid
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4433);
                            }

                            if (damageType == 64) // Electric
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4451);
                            }
                            if (damageType == 1024) // Nether
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 5356);
                            }

                            item.GearHealingBoost += (ThreadSafeRandom.Next(5, 15));
                            item.GearMaxHealth += (ThreadSafeRandom.Next(5, 15));
                            item.GearCritDamage += (ThreadSafeRandom.Next(5, 15));
                            item.GearCritDamageResist += (ThreadSafeRandom.Next(5, 15));
                            item.GearDamage += (ThreadSafeRandom.Next(5, 15));
                            item.GearDamageResist += (ThreadSafeRandom.Next(5, 15));
                        }
                        // Increase SpellProcRate every 100 levels from level 300 - 500
                        if (itemLevel >= 300)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 0.6);
                            item.GearHealingBoost += (ThreadSafeRandom.Next(5, 15));
                            item.GearMaxHealth += (ThreadSafeRandom.Next(5, 15));
                            item.GearCritDamage += (ThreadSafeRandom.Next(5, 15));
                            item.GearCritDamageResist += (ThreadSafeRandom.Next(5, 15));
                            item.GearDamage += (ThreadSafeRandom.Next(5, 15));
                            item.GearDamageResist += (ThreadSafeRandom.Next(5, 15));

                        }
                        if (itemLevel >= 400)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 0.8);
                            item.GearHealingBoost += (ThreadSafeRandom.Next(5, 15));
                            item.GearMaxHealth += (ThreadSafeRandom.Next(5, 15));
                            item.GearCritDamage += (ThreadSafeRandom.Next(5, 15));
                            item.GearCritDamageResist += (ThreadSafeRandom.Next(5, 15));
                            item.GearDamage += (ThreadSafeRandom.Next(5, 15));
                            item.GearDamageResist += (ThreadSafeRandom.Next(5, 15));

                        }
                        if (itemLevel >= 500)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 1.0);
                            item.GearHealingBoost += (ThreadSafeRandom.Next(5, 15));
                            item.GearMaxHealth += (ThreadSafeRandom.Next(5, 15));
                            item.GearCritDamage += (ThreadSafeRandom.Next(5, 15));
                            item.GearCritDamageResist += (ThreadSafeRandom.Next(5, 15));
                            item.GearDamage += (ThreadSafeRandom.Next(5, 15));
                            item.GearDamageResist += (ThreadSafeRandom.Next(5, 15));

                        }
                    }
                    if (itemtype == 32768 && item.Arramoran == true) // Caster
                    {
                        var weapondamage = item.GetProperty(PropertyFloat.ElementalDamageMod);
                        float increment = 0.005f;
                        float newweapondamage = (float)(weapondamage + increment);
                        item.SetProperty(PropertyFloat.ElementalDamageMod, (float)newweapondamage);



                        var name = item.GetProperty(PropertyString.Name);
                        var damagebonusupdate = new GameMessagePrivateUpdatePropertyFloat(item, PropertyFloat.ElementalDamageMod, ElementalDamageMod ?? 0);
                        var geardamagebonus = new GameMessagePrivateUpdatePropertyInt(item, PropertyInt.GearDamage, GearDamage ?? 0);
                        var bonusmessage = $"Your {name}'s Elemental Damage Bonus has increased by {increment}. And is now {newweapondamage} !";
                        Session.Network.EnqueueSend(new GameMessageSystemChat(bonusmessage, ChatMessageType.Broadcast));
                        Session.Network.EnqueueSend(damagebonusupdate);
                        Session.Network.EnqueueSend(geardamagebonus);

                        // Proto Evolution
                        // Item "wakes up" and becomes Attuned and Bonded                      
                        if (itemLevel >= 1)
                        {
                            item.SetProperty(PropertyInt.Bonded, 1);
                            item.SetProperty(PropertyInt.Attuned, 1);

                            if (damageType == 1)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 8);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x600335C);
                            }
                            if (damageType == 2)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 16);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x600335B);
                            }

                            if (damageType == 4)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 32);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x600335A);
                            }

                            if (damageType == 8)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 128);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x6003353);
                            }
                            if (damageType == 16)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 512);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x6003359);
                            }

                            if (damageType == 32)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 64);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x6003355);
                            }

                            if (damageType == 64)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 256);
                                item.SetProperty(PropertyDataId.IconUnderlay, 0x6003354);
                            }
                            if (damageType == 1024)
                            {
                                item.SetProperty(PropertyInt.ImbuedEffect, 16384);
                            }
                        }
                        // Add cleaving at level 200
                        if (itemLevel >= 200)
                        {
                            item.SetProperty(PropertyInt.Cleaving, 3);
                            item.GearHealingBoost += (ThreadSafeRandom.Next(5, 15));
                            item.GearMaxHealth += (ThreadSafeRandom.Next(5, 15));
                            item.GearCritDamage += (ThreadSafeRandom.Next(5, 15));
                            item.GearCritDamageResist += (ThreadSafeRandom.Next(5, 15));
                            item.GearDamage += (ThreadSafeRandom.Next(5, 15));
                            item.GearDamageResist += (ThreadSafeRandom.Next(5, 15));
                        }
                        // Add spell proc at level 100
                        if (itemLevel >= 100)
                        {
                            if (damageType == 1) // Slash
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4457);
                            }
                            if (damageType == 2) // Pierce
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4443);
                            }

                            if (damageType == 4) // Bludgeon
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4455);
                            }

                            if (damageType == 8) // Cold
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4447);
                            }
                            if (damageType == 16) // Fire
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4439);
                            }

                            if (damageType == 32) // Acid
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4433);
                            }

                            if (damageType == 64) // Electric
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 4451);
                            }
                            if (damageType == 1024) // Nether
                            {
                                item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                item.SetProperty(PropertyFloat.ProcSpellRate, 0.3);
                                item.SetProperty(PropertyDataId.ProcSpell, 5356);
                            }

                            item.GearHealingBoost += (ThreadSafeRandom.Next(5, 15));
                            item.GearMaxHealth += (ThreadSafeRandom.Next(5, 15));
                            item.GearCritDamage += (ThreadSafeRandom.Next(5, 15));
                            item.GearCritDamageResist += (ThreadSafeRandom.Next(5, 15));
                            item.GearDamage += (ThreadSafeRandom.Next(5, 15));
                            item.GearDamageResist += (ThreadSafeRandom.Next(5, 15));
                        }
                        // Increase SpellProcRate every 100 levels from level 300 - 500
                        if (itemLevel >= 300)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 0.6);
                            item.GearHealingBoost += (ThreadSafeRandom.Next(5, 15));
                            item.GearMaxHealth += (ThreadSafeRandom.Next(5, 15));
                            item.GearCritDamage += (ThreadSafeRandom.Next(5, 15));
                            item.GearCritDamageResist += (ThreadSafeRandom.Next(5, 15));
                            item.GearDamage += (ThreadSafeRandom.Next(5, 15));
                            item.GearDamageResist += (ThreadSafeRandom.Next(5, 15));

                        }
                        if (itemLevel >= 400)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 0.8);
                            item.GearHealingBoost += (ThreadSafeRandom.Next(5, 15));
                            item.GearMaxHealth += (ThreadSafeRandom.Next(5, 15));
                            item.GearCritDamage += (ThreadSafeRandom.Next(5, 15));
                            item.GearCritDamageResist += (ThreadSafeRandom.Next(5, 15));
                            item.GearDamage += (ThreadSafeRandom.Next(5, 15));
                            item.GearDamageResist += (ThreadSafeRandom.Next(5, 15));

                        }
                        if (itemLevel >= 500)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 1.0);
                            item.GearHealingBoost += (ThreadSafeRandom.Next(5, 15));
                            item.GearMaxHealth += (ThreadSafeRandom.Next(5, 15));
                            item.GearCritDamage += (ThreadSafeRandom.Next(5, 15));
                            item.GearCritDamageResist += (ThreadSafeRandom.Next(5, 15));
                            item.GearDamage += (ThreadSafeRandom.Next(5, 15));
                            item.GearDamageResist += (ThreadSafeRandom.Next(5, 15));

                        }

                    }
                    if (itemtype == 8 && item.Arramoran) // Jewelry
                    {
                        // Proto Evolution
                        // Item "wakes up" and becomes Attuned and Bonded

                        var gearHealingBoost = item.GearHealingBoost;
                        var gearMaxHealth = item.GearMaxHealth;
                        var gearCritDamage = item.GearCritDamage;
                        var gearCritDamageResist = item.GearCritDamageResist;
                        var gearDamage = item.GearDamage;
                        var gearDamageResist = item.GearDamageResist;
                        var ratingIncrease = ThreadSafeRandom.Next(5, 15);

                        if (itemLevel >= 1)
                        {
                            item.SetProperty(PropertyInt.Bonded, 1);
                            item.SetProperty(PropertyInt.Attuned, 1);


                            if (itemLevel >= 1 && !item.HasProc)
                            {
                                var spellProc = ThreadSafeRandom.Next(0.0f, 1.0f);

                                if (spellProc <= 0.3f)
                                {
                                    item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                    item.SetProperty(PropertyFloat.ProcSpellRate, 0.05f);
                                    item.SetProperty(PropertyDataId.ProcSpell, 4643);
                                }
                                else if (spellProc > 0.3f && spellProc < 0.6f)
                                {
                                    item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                    item.SetProperty(PropertyFloat.ProcSpellRate, 0.05f);
                                    item.SetProperty(PropertyDataId.ProcSpell, 4644);
                                }
                                else if (spellProc >= 0.3f)
                                {
                                    item.SetProperty(PropertyInt.ItemSpellcraft, 999);
                                    item.SetProperty(PropertyFloat.ProcSpellRate, 0.05f);
                                    item.SetProperty(PropertyDataId.ProcSpell, 4645);
                                }
                            }
                            item.SetProperty(PropertyFloat.ProcSpellRate, 0.05f);
                        }

                        // Increase to proc rate
                        if (itemLevel >= 200)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 0.09f);
                            item.GearHealingBoost += 1;
                            item.GearMaxHealth += 1;
                            item.GearCritDamage += 1;
                            item.GearCritDamageResist += 1;
                            item.GearDamage += 1;
                            item.GearDamageResist += 1;
                            return;
                        }
                        // Increase to proc rate
                        if (itemLevel >= 100)
                        {
                            item.GearHealingBoost += 1;
                            item.GearMaxHealth += 1;
                            item.GearCritDamage += 1;
                            item.GearCritDamageResist += 1;
                            item.GearDamage += 1;
                            item.GearDamageResist += 1;
                            return;
                        }
                        // Increase to proc rate
                        if (itemLevel >= 300)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 0.11);
                            item.GearHealingBoost += 1;
                            item.GearMaxHealth += 1;
                            item.GearCritDamage += 1;
                            item.GearCritDamageResist += 1;
                            item.GearDamage += 1;
                            item.GearDamageResist += 1;
                            return;
                        }
                        // Increase to proc rate
                        if (itemLevel >= 400)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 0.13);
                            item.GearHealingBoost += 1;
                            item.GearMaxHealth += 1;
                            item.GearCritDamage += 1;
                            item.GearCritDamageResist += 1;
                            item.GearDamage += 1;
                            item.GearDamageResist += 1;
                            return;
                        }
                        // Increase to proc rate
                        if (itemLevel >= 500)
                        {
                            item.SetProperty(PropertyFloat.ProcSpellRate, 0.15);
                            item.GearHealingBoost += 1;
                            item.GearMaxHealth += 1;
                            item.GearCritDamage += 1;
                            item.GearCritDamageResist += 1;
                            item.GearDamage += 1;
                            item.GearDamageResist += 1;
                            return;
                        }

                        var procrate = item.GetProperty(PropertyFloat.ProcSpellRate);
                        var name = item.GetProperty(PropertyString.Name);
                        var bonusmessage = $"Your {name}'s spell proc rate has increased. And is now {procrate} !";
                        Session.Network.EnqueueSend(new GameMessageSystemChat(bonusmessage, ChatMessageType.Broadcast));
                    }

                    if (item.ArmorType == 1 && item.Arramoran && item.HasArmorLevel()) // Armor
                    {
                        // Proto Evolution
                        // Item "wakes up" and becomes Attuned and Bonded

                        var gearHealingBoost = item.GearHealingBoost;
                        var gearMaxHealth = item.GearMaxHealth;
                        var gearCritDamage = item.GearCritDamage;
                        var gearCritDamageResist = item.GearCritDamageResist;
                        var gearDamage = item.GearDamage;
                        var gearDamageResist = item.GearDamageResist;
                        var ratingIncrease = ThreadSafeRandom.Next(5, 15);

                        if (itemLevel >= 1)
                        {
                            item.SetProperty(PropertyInt.Bonded, 1);
                            item.SetProperty(PropertyInt.Attuned, 1);
                            
                        }

                        // Increase to proc rate
                        if (itemLevel >= 100)
                        {
                            item.GearHealingBoost += 1;
                            item.GearMaxHealth += 1;
                            item.GearCritDamage += 1;
                            item.GearCritDamageResist += 1;
                            item.GearDamage += 1;
                            item.GearDamageResist += 1;
                            item.ArmorModVsPierce += 0.02f;
                            item.ArmorModVsSlash += 0.02f;
                            item.ArmorModVsBludgeon += 0.02f;
                            item.ArmorModVsAcid += 0.02f;
                            item.ArmorModVsFire += 0.02f;
                            item.ArmorModVsCold += 0.02f;
                            item.ArmorModVsElectric += 0.02f;
                            return;
                        }
                        if (itemLevel >= 200)
                        {
                            item.GearHealingBoost += 1;
                            item.GearMaxHealth += 1;
                            item.GearCritDamage += 1;
                            item.GearCritDamageResist += 1;
                            item.GearDamage += 1;
                            item.GearDamageResist += 1;
                            item.ArmorModVsPierce += 0.02f;
                            item.ArmorModVsSlash += 0.02f;
                            item.ArmorModVsBludgeon += 0.02f;
                            item.ArmorModVsAcid += 0.02f;
                            item.ArmorModVsFire += 0.02f;
                            item.ArmorModVsCold += 0.02f;
                            item.ArmorModVsElectric += 0.02f;
                            return;
                        }
                        if (itemLevel >= 300)
                        {
                            item.GearHealingBoost += 1;
                            item.GearMaxHealth += 1;
                            item.GearCritDamage += 1;
                            item.GearCritDamageResist += 1;
                            item.GearDamage += 1;
                            item.GearDamageResist += 1;
                            item.ArmorModVsPierce += 0.02f;
                            item.ArmorModVsSlash += 0.02f;
                            item.ArmorModVsBludgeon += 0.02f;
                            item.ArmorModVsAcid += 0.02f;
                            item.ArmorModVsFire += 0.02f;
                            item.ArmorModVsCold += 0.02f;
                            item.ArmorModVsElectric += 0.02f;
                            return;
                        }
                        if (itemLevel >= 400)
                        {
                            item.GearHealingBoost += 1;
                            item.GearMaxHealth += 1;
                            item.GearCritDamage += 1;
                            item.GearCritDamageResist += 1;
                            item.GearDamage += 1;
                            item.GearDamageResist += 1;
                            item.ArmorModVsPierce += 0.02f;
                            item.ArmorModVsSlash += 0.02f;
                            item.ArmorModVsBludgeon += 0.02f;
                            item.ArmorModVsAcid += 0.02f;
                            item.ArmorModVsFire += 0.02f;
                            item.ArmorModVsCold += 0.02f;
                            item.ArmorModVsElectric += 0.02f;
                            return;
                        }
                        if (itemLevel >= 500)
                        {
                            item.GearHealingBoost += 1;
                            item.GearMaxHealth += 1;
                            item.GearCritDamage += 1;
                            item.GearCritDamageResist += 1;
                            item.GearDamage += 1;
                            item.GearDamageResist += 1;
                            item.ArmorModVsPierce += 0.02f;
                            item.ArmorModVsSlash += 0.02f;
                            item.ArmorModVsBludgeon += 0.02f;
                            item.ArmorModVsAcid += 0.02f;
                            item.ArmorModVsFire += 0.02f;
                            item.ArmorModVsCold += 0.02f;
                            item.ArmorModVsElectric += 0.02f;
                            return;
                        }

                        var procrate = item.GetProperty(PropertyFloat.ProcSpellRate);
                        var name = item.GetProperty(PropertyString.Name);
                        var bonusmessage = $"Your {name}'s ratings and defensive bonuses have increased!";
                        Session.Network.EnqueueSend(new GameMessageSystemChat(bonusmessage, ChatMessageType.Broadcast));
                    }
                    if (item.ArmorType == 1 && item.Arramoran && Proto == false && !item.HasArmorLevel()) // Clothing
                    {
                        // Proto Evolution
                        // Item "wakes up" and becomes Attuned and Bonded

                        var gearHealingBoost = item.GearHealingBoost;
                        var gearMaxHealth = item.GearMaxHealth;
                        var gearCritDamage = item.GearCritDamage;
                        var gearCritDamageResist = item.GearCritDamageResist;
                        var gearDamage = item.GearDamage;
                        var gearDamageResist = item.GearDamageResist;
                        var ratingIncrease = ThreadSafeRandom.Next(5, 15);

                        if (itemLevel >= 1)
                        {
                            item.SetProperty(PropertyInt.Bonded, 1);
                            item.SetProperty(PropertyInt.Attuned, 1);

                        }

                        // Increase to proc rate
                        if (itemLevel >= 200)
                        {

                            item.GearHealingBoost += 1;
                            item.GearMaxHealth += 1;
                            item.GearCritDamage += 1;
                            item.GearCritDamageResist += 1;
                            item.GearDamage += 1;
                            item.GearDamageResist += 1;
                            return;
                        }
                        // Increase to proc rate
                        if (itemLevel >= 100)
                        {
                            item.GearHealingBoost += 1;
                            item.GearMaxHealth += 1;
                            item.GearCritDamage += 1;
                            item.GearCritDamageResist += 1;
                            item.GearDamage += 1;
                            item.GearDamageResist += 1;
                            return;
                        }
                        // Increase to proc rate
                        if (itemLevel >= 300)
                        {

                            item.GearHealingBoost += 1;
                            item.GearMaxHealth += 1;
                            item.GearCritDamage += 1;
                            item.GearCritDamageResist += 1;
                            item.GearDamage += 1;
                            item.GearDamageResist += 1;
                            return;
                        }
                        // Increase to proc rate
                        if (itemLevel >= 400)
                        {

                            item.GearHealingBoost += 1;
                            item.GearMaxHealth += 1;
                            item.GearCritDamage += 1;
                            item.GearCritDamageResist += 1;
                            item.GearDamage += 1;
                            item.GearDamageResist += 1;
                            return;
                        }
                        // Increase to proc rate
                        if (itemLevel >= 500)
                        {

                            item.GearHealingBoost += 1;
                            item.GearMaxHealth += 1;
                            item.GearCritDamage += 1;
                            item.GearCritDamageResist += 1;
                            item.GearDamage += 1;
                            item.GearDamageResist += 1;
                            return;
                        }

                        var procrate = item.GetProperty(PropertyFloat.ProcSpellRate);
                        var name = item.GetProperty(PropertyString.Name);
                        var bonusmessage = $"Your {name}'s ratings have increased!";
                        Session.Network.EnqueueSend(new GameMessageSystemChat(bonusmessage, ChatMessageType.Broadcast));
                    }
                    if (item.GetProperty(PropertyInt.ValidLocations) == 0x8000000 && item.Arramoran && Proto == false && !item.HasArmorLevel()) // Cloaks
                    {
                        // Proto Evolution
                        // Item "wakes up" and becomes Attuned and Bonded

                        var gearHealingBoost = item.GearHealingBoost;
                        var gearMaxHealth = item.GearMaxHealth;
                        var gearCritDamage = item.GearCritDamage;
                        var gearCritDamageResist = item.GearCritDamageResist;
                        var gearDamage = item.GearDamage;
                        var gearDamageResist = item.GearDamageResist;
                        var ratingIncrease = ThreadSafeRandom.Next(5, 15);

                        if (itemLevel >= 1)
                        {
                            item.SetProperty(PropertyInt.Bonded, 1);
                            item.SetProperty(PropertyInt.Attuned, 1);

                        }

                        // Increase to proc rate
                        if (itemLevel >= 200)
                        {

                            item.GearHealingBoost += 1;
                            item.GearMaxHealth += 1;
                            item.GearCritDamage += 1;
                            item.GearCritDamageResist += 1;
                            item.GearDamage += 1;
                            item.GearDamageResist += 1;
                            return;
                        }
                        // Increase to proc rate
                        if (itemLevel >= 100)
                        {
                            item.GearHealingBoost += 1;
                            item.GearMaxHealth += 1;
                            item.GearCritDamage += 1;
                            item.GearCritDamageResist += 1;
                            item.GearDamage += 1;
                            item.GearDamageResist += 1;
                            return;
                        }
                        // Increase to proc rate
                        if (itemLevel >= 300)
                        {

                            item.GearHealingBoost += 1;
                            item.GearMaxHealth += 1;
                            item.GearCritDamage += 1;
                            item.GearCritDamageResist += 1;
                            item.GearDamage += 1;
                            item.GearDamageResist += 1;
                            return;
                        }
                        // Increase to proc rate
                        if (itemLevel >= 400)
                        {

                            item.GearHealingBoost += 1;
                            item.GearMaxHealth += 1;
                            item.GearCritDamage += 1;
                            item.GearCritDamageResist += 1;
                            item.GearDamage += 1;
                            item.GearDamageResist += 1;
                            return;
                        }
                        // Increase to proc rate
                        if (itemLevel >= 500)
                        {

                            item.GearHealingBoost += 1;
                            item.GearMaxHealth += 1;
                            item.GearCritDamage += 1;
                            item.GearCritDamageResist += 1;
                            item.GearDamage += 1;
                            item.GearDamageResist += 1;
                            return;
                        }

                        var procrate = item.GetProperty(PropertyFloat.ProcSpellRate);
                        var name = item.GetProperty(PropertyString.Name);
                        var bonusmessage = $"Your {name}'s spell proc rate has increased. And is now {procrate} !";
                        Session.Network.EnqueueSend(new GameMessageSystemChat(bonusmessage, ChatMessageType.Broadcast));
                    }
                });
                actionChain.EnqueueChain();                
            }
                


        }

        public void CreateSentinelBuffPlayers(IEnumerable<Player> players, bool self = false, ulong maxLevel = 8)
        {
            if (!(Session.AccessLevel >= AccessLevel.Sentinel)) return;

            var SelfOrOther = self ? "Self" : "Other";

            // ensure level 8s are installed
            var maxSpellLevel = Math.Clamp(maxLevel, 1, 8);
            if (maxSpellLevel == 8 && DatabaseManager.World.GetCachedSpell((uint)SpellId.ArmorOther8) == null)
                maxSpellLevel = 7;

            var tySpell = typeof(SpellId);
            List<BuffMessage> buffMessages = new List<BuffMessage>();
            // prepare messages
            List<string> buffsNotImplementedYet = new List<string>();
            foreach (var spell in Buffs)
            {
                var spellNamPrefix = spell;
                bool isBane = false;
                if (spellNamPrefix.StartsWith("@"))
                {
                    isBane = true;
                    spellNamPrefix = spellNamPrefix.Substring(1);
                }
                string fullSpellEnumName = spellNamPrefix + ((isBane) ? string.Empty : SelfOrOther) + maxSpellLevel;
                string fullSpellEnumNameAlt = spellNamPrefix + ((isBane) ? string.Empty : ((SelfOrOther == "Self") ? "Other" : "Self")) + maxSpellLevel;
                uint spellID = (uint)Enum.Parse(tySpell, fullSpellEnumName);
                var buffMsg = BuildBuffMessage(spellID);

                if (buffMsg == null)
                {
                    spellID = (uint)Enum.Parse(tySpell, fullSpellEnumNameAlt);
                    buffMsg = BuildBuffMessage(spellID);
                }

                if (buffMsg != null)
                {
                    buffMsg.Bane = isBane;
                    buffMessages.Add(buffMsg);
                }
                else
                {
                    buffsNotImplementedYet.Add(fullSpellEnumName);
                }
            }
            // buff each player
            players.ToList().ForEach(targetPlayer =>
            {
                if (buffMessages.Any(k => !k.Bane))
                {
                    // bake player into the messages
                    buffMessages.Where(k => !k.Bane).ToList().ForEach(k => k.SetTargetPlayer(targetPlayer));
                    // update client-side enchantments
                    targetPlayer.Session.Network.EnqueueSend(buffMessages.Where(k => !k.Bane).Select(k => k.SessionMessage).ToArray());
                    // run client-side effect scripts, omitting duplicates
                    targetPlayer.EnqueueBroadcast(buffMessages.Where(k => !k.Bane).ToList().GroupBy(m => m.Spell.TargetEffect).Select(a => a.First().LandblockMessage).ToArray());
                    // update server-side enchantments

                    var buffsForPlayer = buffMessages.Where(k => !k.Bane).ToList().Select(k => k.Enchantment);

                    var lifeBuffsForPlayer = buffsForPlayer.Where(k => k.Spell.School == MagicSchool.LifeMagic).ToList();
                    var critterBuffsForPlayer = buffsForPlayer.Where(k => k.Spell.School == MagicSchool.CreatureEnchantment).ToList();
                    var itemBuffsForPlayer = buffsForPlayer.Where(k => k.Spell.School == MagicSchool.ItemEnchantment).ToList();

                    uint dmg = 0;
                    EnchantmentStatus ec;
                    lifeBuffsForPlayer.ForEach(spl =>
                    {
                        bool casted = targetPlayer.LifeMagic(spl.Spell, out dmg, out ec, targetPlayer);
                    });
                    critterBuffsForPlayer.ForEach(spl =>
                    {
                        ec = targetPlayer.CreatureMagic(targetPlayer, spl.Spell);
                    });
                    itemBuffsForPlayer.ForEach(spl =>
                    {
                        ec = targetPlayer.ItemMagic(targetPlayer, spl.Spell);
                    });
                }
                if (buffMessages.Any(k => k.Bane))
                {
                    // Impen/bane
                    var items = targetPlayer.EquippedObjects.Values.ToList();
                    var itembuffs = buffMessages.Where(k => k.Bane).ToList();
                    foreach (var itemBuff in itembuffs)
                    {
                        foreach (var item in items)
                        {
                            if ((item.WeenieType == WeenieType.Clothing || item.IsShield) && item.IsEnchantable)
                            {
                                itemBuff.SetLandblockMessage(item.Guid);
                                var enchantmentStatus = targetPlayer.ItemMagic(item, itemBuff.Spell, this);
                                targetPlayer?.EnqueueBroadcast(itemBuff.LandblockMessage);
                            }
                        }
                    }
                }
            });
        }

        // TODO: switch this over to SpellProgressionTables
        private static string[] Buffs = new string[] {
#region spells
            // @ indicates impenetrability or a bane
            "Strength",
            "Invulnerability",
            "FireProtection",
            "Armor",
            "Rejuvenation",
            "Regeneration",
            "ManaRenewal",
            "Impregnability",
            "MagicResistance",
            //"AxeMastery",    // light weapons
            "LightWeaponsMastery",
            //"DaggerMastery", // finesse weapons
            "FinesseWeaponsMastery",
            //"MaceMastery",
            //"SpearMastery",
            //"StaffMastery",
            //"SwordMastery",  // heavy weapons
            "HeavyWeaponsMastery",
            //"UnarmedCombatMastery",
            //"BowMastery",    // missile weapons
            "MissileWeaponsMastery",
            //"CrossbowMastery",
            //"ThrownWeaponMastery",
            "AcidProtection",
            "CreatureEnchantmentMastery",
            "ItemEnchantmentMastery",
            "LifeMagicMastery",
            "WarMagicMastery",
            "ManaMastery",
            "ArcaneEnlightenment",
            "ArcanumSalvaging",
            "ArmorExpertise",
            "ItemExpertise",
            "MagicItemExpertise",
            "WeaponExpertise",
            "MonsterAttunement",
            "PersonAttunement",
            "DeceptionMastery",
            "HealingMastery",
            "LeadershipMastery",
            "LockpickMastery",
            "Fealty",
            "JumpingMastery",
            "Sprint",
            "BludgeonProtection",
            "ColdProtection",
            "LightningProtection",
            "BladeProtection",
            "PiercingProtection",
            "Endurance",
            "Coordination",
            "Quickness",
            "Focus",
            "Willpower",
            "CookingMastery",
            "FletchingMastery",
            "AlchemyMastery",
            "VoidMagicMastery",
            "SummoningMastery",
            "SwiftKiller",
            "Defender",
            "BloodDrinker",
            "HeartSeeker",
            "HermeticLink",
            "SpiritDrinker",
            "DualWieldMastery",
            "TwoHandedMastery",
            "DirtyFightingMastery",
            "RecklessnessMastery",
            "SneakAttackMastery",
            "@Impenetrability",
            "@PiercingBane",
            "@BludgeonBane",
            "@BladeBane",
            "@AcidBane",
            "@FlameBane",
            "@FrostBane",
            "@LightningBane",
#endregion
            };

        private class BuffMessage
        {
            public bool Bane { get; set; } = false;
            public GameEventMagicUpdateEnchantment SessionMessage { get; set; } = null;
            public GameMessageScript LandblockMessage { get; set; } = null;
            public Spell Spell { get; set; } = null;
            public Enchantment Enchantment { get; set; } = null;
            public void SetTargetPlayer(Player p)
            {
                Enchantment.Target = p;
                SessionMessage = new GameEventMagicUpdateEnchantment(p.Session, Enchantment);
                SetLandblockMessage(p.Guid);
            }
            public void SetLandblockMessage(ObjectGuid target)
            {
                LandblockMessage = new GameMessageScript(target, Spell.TargetEffect, 1f);
            }
        }

        private static BuffMessage BuildBuffMessage(uint spellID)
        {
            BuffMessage buff = new BuffMessage();
            buff.Spell = new Spell(spellID);
            if (buff.Spell.NotFound) return null;
            buff.Enchantment = new Enchantment(null, 0, spellID, 1, (EnchantmentMask)buff.Spell.StatModType, buff.Spell.StatModVal);
            return buff;
        }

        public void HandleSpellbookFilters(SpellBookFilterOptions filters)
        {
            Character.SpellbookFilters = (uint)filters;
        }

        public void HandleSetDesiredComponentLevel(uint component_wcid, uint amount)
        {
            // ensure wcid is spell component
            if (!SpellComponent.IsValid(component_wcid))
            {
                log.Warn($"{Name}.HandleSetDesiredComponentLevel({component_wcid}, {amount}): invalid spell component wcid");
                return;
            }
            if (amount > 0)
            {
                var existing = Character.GetFillComponent(component_wcid, CharacterDatabaseLock);

                if (existing == null)
                    Character.AddFillComponent(component_wcid, amount, CharacterDatabaseLock, out bool exists);
                else
                    existing.QuantityToRebuy = (int)amount;
            }
            else
                Character.TryRemoveFillComponent(component_wcid, out var _, CharacterDatabaseLock);

            CharacterChangesDetected = true;
        }

        public static Dictionary<MagicSchool, uint> FociWCIDs = new Dictionary<MagicSchool, uint>()
        {
            { MagicSchool.CreatureEnchantment, 15268 },   // Foci of Enchantment
            { MagicSchool.ItemEnchantment,     15269 },   // Foci of Artifice
            { MagicSchool.LifeMagic,           15270 },   // Foci of Verdancy
            { MagicSchool.WarMagic,            15271 },   // Foci of Strife
            { MagicSchool.VoidMagic,           43173 },   // Foci of Shadow
        };

        public bool HasFoci(MagicSchool school)
        {
            switch (school)
            {
                case MagicSchool.CreatureEnchantment:
                    if (AugmentationInfusedCreatureMagic > 0)
                        return true;
                    break;
                case MagicSchool.ItemEnchantment:
                    if (AugmentationInfusedItemMagic > 0)
                        return true;
                    break;
                case MagicSchool.LifeMagic:
                    if (AugmentationInfusedLifeMagic > 0)
                        return true;
                    break;
                case MagicSchool.VoidMagic:
                    if (AugmentationInfusedVoidMagic > 0)
                        return true;
                    break;
                case MagicSchool.WarMagic:
                    if (AugmentationInfusedWarMagic > 0)
                        return true;
                    break;
            }

            var wcid = FociWCIDs[school];
            return Inventory.Values.FirstOrDefault(i => i.WeenieClassId == wcid) != null;
        }

        public void HandleSpellHooks(Spell spell)
        {
            HandleMaxVitalUpdate(spell);

            // unsure if spell hook was here in retail,
            // but this has the potential to take the client out of autorun mode
            // which causes them to stop if they hit a turn key afterwards
            if (PropertyManager.GetBool("runrate_add_hooks").Item)
                HandleRunRateUpdate(spell);
        }

        /// <summary>
        /// Called when an enchantment is added or removed,
        /// checks if the spell affects the max vitals,
        /// and if so, updates the client immediately
        /// </summary>
        public void HandleMaxVitalUpdate(Spell spell)
        {
            var maxVitals = spell.UpdatesMaxVitals;

            if (maxVitals.Count == 0)
                return;

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(1.0f);      // client needs time for primary attribute updates
            actionChain.AddAction(this, () =>
            {
                foreach (var maxVital in maxVitals)
                {
                    var playerVital = Vitals[maxVital];

                    Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute2ndLevel(this, playerVital.ToEnum(), playerVital.Current));
                }
            });
            actionChain.EnqueueChain();
        }

        public bool HandleRunRateUpdate(Spell spell)
        {
            if (!spell.UpdatesRunRate)
                return false;

            return HandleRunRateUpdate();
        }

        public void AuditItemSpells()
        {
            // cleans up bugged chars with dangling item set spells
            // from previous bugs

            var allPossessions = GetAllPossessions().ToDictionary(i => i.Guid, i => i);

            // this is a legacy method, but is still a decent failsafe to catch any existing issues

            // get active item enchantments
            var enchantments = Biota.PropertiesEnchantmentRegistry.Clone(BiotaDatabaseLock).Where(i => i.Duration == -1 && i.SpellId != (int)SpellId.Vitae).ToList();

            foreach (var enchantment in enchantments)
            {
                var table = enchantment.HasSpellSetId ? allPossessions : EquippedObjects;

                // if this item is not equipped, remove enchantment
                if (!table.TryGetValue(new ObjectGuid(enchantment.CasterObjectId), out var item))
                {
                    var spell = new Spell(enchantment.SpellId, false);
                    log.Error($"{Name}.AuditItemSpells(): removing spell {spell.Name} from {(enchantment.HasSpellSetId ? "non-possessed" : "non-equipped")} item");

                    EnchantmentManager.Dispel(enchantment);
                    continue;
                }

                // is this item part of a set?
                if (!item.HasItemSet)
                    continue;

                // get all of the equipped items in this set
                var setItems = EquippedObjects.Values.Where(i => i.HasItemSet && i.EquipmentSetId == item.EquipmentSetId).ToList();

                // get all of the spells currently active from this set
                var currentSpells = GetSpellSet(setItems);

                // get all of the spells possible for this item set
                var possibleSpells = GetSpellSetAll((EquipmentSet)item.EquipmentSetId);

                // get the difference between them
                var inactiveSpells = possibleSpells.Except(currentSpells).ToList();

                // remove any item set spells that shouldn't be active
                foreach (var inactiveSpell in inactiveSpells)
                {
                    var removeSpells = enchantments.Where(i => i.SpellSetId == item.EquipmentSetId && i.SpellId == inactiveSpell.Id).ToList();

                    foreach (var removeSpell in removeSpells)
                    {
                        log.Error($"{Name}.AuditItemSpells(): removing spell {inactiveSpell.Name} from {item.EquipmentSetId}");

                        EnchantmentManager.Dispel(removeSpell);
                    }
                }
            }
        }
    }
}
