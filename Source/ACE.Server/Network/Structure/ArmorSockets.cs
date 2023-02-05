using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ACE.Server.Entity;
using ACE.Entity.Enum.Properties;
using ACE.Server.WorldObjects;

namespace ACE.Server.Network.Structure
{
    /// <summary>
    /// Handles the per-body part AL display for the character panel
    /// </summary>
    public class ArmorSockets
    {
        public int Sockets;
        

        public ArmorSockets(WorldObject armor)
        {
            // get base + enchanted AL per body part
            Sockets = GetArmorSockets(armor);
            
        }

        public int GetArmorSockets(WorldObject armor)
        {
            if (armor.Sockets == null)
                return 0;
            var armorSockets = armor.Sockets;
            
            return (int)armorSockets;
        }
    }

    public static class ArmorSocketsExtensions
    {
        public static void Write(this BinaryWriter writer, ArmorSockets armorSockets)
        {
            writer.Write(armorSockets);          
        }
    }
}
