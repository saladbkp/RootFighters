using System.Collections.Generic;
using UnityEngine;

namespace CtfStage
{
    public struct CategoryInfo
    {
        public string key;
        public string label;
        public Color color;
        public Color glow;
        public string effect;   // pick your VFX/prefab by this name
    }

    /// <summary>
    /// Unity-side mirror of web/config.js — keep the two in sync so the web
    /// prototype and the Unity stage speak the same color/effect language.
    /// </summary>
    public static class StageConfig
    {
        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        static CategoryInfo C(string key, string label, string hex, string glow, string effect)
            => new CategoryInfo { key = key, label = label, color = Hex(hex), glow = Hex(glow), effect = effect };

        public static readonly Dictionary<string, CategoryInfo> Categories = new Dictionary<string, CategoryInfo>
        {
            { "pwn",       C("pwn",       "Pwn",       "#ff2d2d", "#ff7a7a", "explosion") }, // red
            { "web",       C("web",       "Web",       "#39ff14", "#a6ff96", "matrix")    }, // green
            { "wifi",      C("wifi",      "WiFi",      "#ffe600", "#fff59a", "lightning") }, // yellow
            { "reverse",   C("reverse",   "Reverse",   "#3b82f6", "#9ec1ff", "gears")     }, // blue
            { "forensics", C("forensics", "Forensics", "#a64bff", "#d3a6ff", "psychic")   }, // purple
            { "crypto",    C("crypto",    "Crypto",    "#ff8c00", "#ffc46b", "vortex")    }, // orange
            { "iot",       C("iot",       "IoT",       "#202028", "#7b5cff", "shadow")    }, // black (purple rim)
            { "osint",     C("osint",     "OSINT",     "#ffffff", "#bfe9ff", "flash")     }, // white
            { "b2r",       C("b2r",       "B2R",       "#ffd700", "#fff0a0", "root")      }, // gold (9th)
        };

        public static CategoryInfo Cat(string key)
        {
            if (key != null && Categories.TryGetValue(key, out var c)) return c;
            return new CategoryInfo { key = key, label = key, color = Color.gray, glow = Color.white, effect = "flash" };
        }

        // Team identity (left = A, right = B). Move colors come from the category.
        public static readonly Color TeamAColor = Hex("#33d6ff"); // cyan
        public static readonly Color TeamBColor = Hex("#ff4fd8"); // magenta

        public static Color TeamColor(string side) => side == "A" ? TeamAColor : TeamBColor;
    }
}
