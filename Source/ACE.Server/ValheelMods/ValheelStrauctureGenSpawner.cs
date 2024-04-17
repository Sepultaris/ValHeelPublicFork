using System;
using System.Collections.Generic;
using System.Linq;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity;
using ACE.Server.Factories;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Database;
using ACE.Server.Command.Handlers;
using ACE.Database.SQLFormatters.World;
using Microsoft.EntityFrameworkCore;
using System.IO;
using ACE.Server.Managers;

namespace ACE.Server.ValheelMods
{
    public class ValheelStrauctureGenSpawner : WorldObject
    {
        public static LandblockInstanceWriter LandblockInstanceWriter;

        // Handles creating the landblock instance of the player structure generator
        public static void HandleAllegStructure(Session session, Player player, int? generatorWCID)
        {
            var loc = new Position(session.Player.Location);

            uint? parentGuid = null;

            var landblock = session.Player.CurrentLandblock.Id.Landblock;

            var firstStaticGuid = 0x70000000 | (uint)landblock << 12;

            var weenie = DatabaseManager.World.GetWeenie((uint)generatorWCID);   // wcid

            // clear any cached instances for this landblock
            DatabaseManager.World.ClearCachedInstancesByLandblock(landblock);

            var instances = DatabaseManager.World.GetCachedInstancesByLandblock(landblock);

            // for link mode, ensure parent guid instance exists
            WorldObject parentObj = null;
            LandblockInstance parentInstance = null;

            if (parentGuid != null)
            {
                parentInstance = instances.FirstOrDefault(i => i.Guid == parentGuid);

                if (parentInstance == null)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find landblock instance for parent guid 0x{parentGuid:X8}", ChatMessageType.Broadcast));
                    return;
                }

                parentObj = session.Player.CurrentLandblock.GetObject(parentGuid.Value);

                if (parentObj == null)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find parent object 0x{parentGuid:X8}", ChatMessageType.Broadcast));
                    return;
                }
            }

            var nextStaticGuid = GetNextStaticGuid(landblock, instances, session);

            var maxStaticGuid = firstStaticGuid | 0xFFF;

            if (nextStaticGuid > maxStaticGuid)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Landblock {landblock:X4} has reached the maximum # of static guids", ChatMessageType.Broadcast));
                return;
            }

            // create and spawn object
            var entityWeenie = Database.Adapter.WeenieConverter.ConvertToEntityWeenie(weenie);

            var wo = WorldObjectFactory.CreateWorldObject(entityWeenie, new ObjectGuid(nextStaticGuid));

            if (wo == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to create new object for {weenie.ClassId} - {weenie.ClassName}", ChatMessageType.Broadcast));
                return;
            }

            var isLinkChild = parentInstance != null;

            if (!wo.Stuck && !isLinkChild)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"{weenie.ClassId} - {weenie.ClassName} is missing PropertyBool.Stuck, cannot spawn as landblock instance unless it is a child object", ChatMessageType.Broadcast));
                return;
            }

            // spawn as ethereal temporarily, to spawn directly on player position
            wo.Ethereal = true;
            wo.Location = new Position(loc);

            // even on flat ground, objects can sometimes fail to spawn at the player's current Z
            // Position.Z has some weird thresholds when moving around, but i guess the same logic doesn't apply when trying to spawn in...
            wo.Location.PositionZ += 0.05f;

            session.Network.EnqueueSend(new GameMessageSystemChat($"Creating new landblock instance {(isLinkChild ? "child object " : "")}@ {loc.ToLOCString()}\n{wo.WeenieClassId} - {wo.Name} ({nextStaticGuid:X8})", ChatMessageType.Broadcast));

            if (!wo.EnterWorld())
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Failed to spawn new object at this location", ChatMessageType.Broadcast));
                return;
            }

            // create new landblock instance
            var instance = CreateLandblockInstance(wo, isLinkChild);

            instances.Add(instance);

            if (isLinkChild)
            {
                var link = new LandblockInstanceLink();

                link.ParentGuid = parentGuid.Value;
                link.ChildGuid = wo.Guid.Full;
                link.LastModified = DateTime.Now;

                parentInstance.LandblockInstanceLink.Add(link);

                parentObj.LinkedInstances.Add(instance);

                // ActivateLinks?
                parentObj.SetLinkProperties(wo);
                parentObj.ChildLinks.Add(wo);
                wo.ParentLink = parentObj;
            }

            SyncInstances(session, landblock, instances);
        }

        public static uint GetNextStaticGuid(ushort landblock, List<LandblockInstance> instances, Session session)
        {
            int guidCount = instances.Count(i => i.Landblock == landblock);
            var firstGuid = 0x70000000 | ((uint)landblock << 12);
            var lastGuid = firstGuid | 0xFFF;

            var highestLandblockInst = instances.Where(i => i.Landblock == landblock).OrderByDescending(i => i.Guid).FirstOrDefault();

            if (highestLandblockInst == null)
                return firstGuid;

            var nextGuid = highestLandblockInst.Guid + 1;

            if (nextGuid <= lastGuid)
                return nextGuid;

            // try more exhaustive search
            return GetNextStaticGuid_GapFinder(landblock, instances) ?? nextGuid;
        }

        public static void SyncInstances(Session session, ushort landblock, List<LandblockInstance> instances)
        {
            // serialize to .sql file
            var contentFolder = VerifyContentFolder(session, false);

            var sep = Path.DirectorySeparatorChar;
            var folder = new DirectoryInfo($"{contentFolder.FullName}{sep}sql{sep}landblocks{sep}");

            if (!folder.Exists)
                folder.Create();

            var sqlFilename = $"{folder.FullName}{sep}{landblock:X4}.sql";

            if (instances.Count > 0)
            {
                var fileWriter = new StreamWriter(sqlFilename);

                if (LandblockInstanceWriter == null)
                {
                    LandblockInstanceWriter = new LandblockInstanceWriter();
                    LandblockInstanceWriter.WeenieNames = DatabaseManager.World.GetAllWeenieNames();
                }

                LandblockInstanceWriter.CreateSQLDELETEStatement(instances, fileWriter);

                fileWriter.WriteLine();

                LandblockInstanceWriter.CreateSQLINSERTStatement(instances, fileWriter);

                fileWriter.Close();

                // import into db
                ImportSQL(sqlFilename);
            }
            else
            {
                // handle special case: deleting the last instance from landblock
                File.Delete(sqlFilename);

                using (var ctx = new WorldDbContext())
                    ctx.Database.ExecuteSqlRaw($"DELETE FROM landblock_instance WHERE landblock={landblock};");
            }

            // clear landblock instances for this landblock (again)
            DatabaseManager.World.ClearCachedInstancesByLandblock(landblock);
        }

        public static uint? GetNextStaticGuid_GapFinder(ushort landblock, List<LandblockInstance> instances)
        {
            var landblockGuids = instances.Where(i => i.Landblock == landblock).Select(i => i.Guid).ToHashSet();

            var firstGuid = 0x70000000 | ((uint)landblock << 12);
            var lastGuid = firstGuid | 0xFFF;

            for (var guid = firstGuid; guid <= lastGuid; guid++)
            {
                if (!landblockGuids.Contains(guid))
                    return guid;
            }
            return null;
        }

        public static LandblockInstance CreateLandblockInstance(WorldObject wo, bool isLinkChild = false)
        {
            var instance = new LandblockInstance();

            instance.Guid = wo.Guid.Full;

            instance.Landblock = (int)wo.Location.Landblock;

            instance.WeenieClassId = wo.WeenieClassId;

            instance.ObjCellId = wo.Location.Cell;

            instance.OriginX = wo.Location.PositionX;
            instance.OriginY = wo.Location.PositionY;
            instance.OriginZ = wo.Location.PositionZ;

            instance.AnglesW = wo.Location.RotationW;
            instance.AnglesX = wo.Location.RotationX;
            instance.AnglesY = wo.Location.RotationY;
            instance.AnglesZ = wo.Location.RotationZ;

            instance.IsLinkChild = isLinkChild;

            instance.LastModified = DateTime.Now;

            return instance;
        }

        private static DirectoryInfo VerifyContentFolder(Session session, bool showError = true)
        {
            var content_folder = PropertyManager.GetString("content_folder").Item;

            var sep = Path.DirectorySeparatorChar;

            // handle relative path
            if (content_folder.StartsWith("."))
            {
                var cwd = Directory.GetCurrentDirectory() + sep;
                content_folder = cwd + content_folder;
            }

            var di = new DirectoryInfo(content_folder);

            if (!di.Exists && showError)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find content folder: {di.FullName}");
                CommandHandlerHelper.WriteOutputInfo(session, "To set your content folder, /modifystring content_folder <path>");
            }
            return di;
        }

        public static void ImportSQL(string sqlFile)
        {
            var sqlCommands = File.ReadAllText(sqlFile);

            sqlCommands = sqlCommands.Replace("\r\n", "\n");

            // not sure why ExecuteSqlCommand doesn't parse this correctly..
            var idx = sqlCommands.IndexOf($"/* Lifestoned Changelog:");
            if (idx != -1)
                sqlCommands = sqlCommands.Substring(0, idx);

            using (var ctx = new WorldDbContext())
                ctx.Database.ExecuteSqlRaw(sqlCommands);
        }
    }
}
