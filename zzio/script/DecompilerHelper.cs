using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace zzio.script
{
    public static class DecompilerHelper
    {
        public static string getCameraPosDescription(uint id)
        {
            string[] behindPlyr = {
                "left top", "left bottom", "left center", "right top", "right bottom", "right center", "directly"
            };
            uint type = id / 100;
            uint data = id % 100;
            switch(type)
            {
                case (0): { return "at trigger " + data + " (look at player)"; }
                case (10):
                    {
                        if (data == 7)
                            return "front of plyr (directly)";
                        else if (data < 7)
                            return "behind plyr (" + behindPlyr[data] + ")";
                        else
                            return null;
                    }
                case (20): { return data < 6 ? "behind npc ( " + behindPlyr[data] + ")" : null; }
                case (21): { return data == 0 ? "follow npc" : null; }
                case (30): { return "at trigger " + data + " (use trigger direction)"; }
                case (40): { return data == 0 ? "look at npc" : null; }
                case (50): { return "at trigger " + data + " (look at npc)"; }
                default: { return null; }
            }
        }

        public static string getModifyWizformDescription(uint id)
        {
            string[] descr =
            {
                "Increase Hitpoints", "Increase Experience Points", "Remove Condition", "reserved", "reserved", "reserved",
                "reserved", "Execute Evolution", "Execute Level Jump", "Attach Damage Modifier", "Attach Shield Modifier",
                "Attach Reloadtime Modifier", "Attach Jumppower Modifier", "Attach CriticalHitRatio Modifier", "Modify Happiness",
                "Increase Deckoperations", "Revive WizForm", "Increase Mana", "Rename Wizform"
            };
            return id < 19 ? descr[id] : null;
        }
    }
}
