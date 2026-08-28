using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace FavoriteHelper
{
    internal sealed class Hotkey
    {
        public readonly bool Ctrl, Shift;
        public readonly uint VirtualKey;
        public readonly string Text;
        public Hotkey(bool ctrl, bool shift, uint virtualKey, string text) { Ctrl = ctrl; Shift = shift; VirtualKey = virtualKey; Text = text; }
    }

    internal sealed class AppConfig
    {
        public readonly Hotkey Open, Favorite, Unfavorite;
        public readonly bool EnableNotification;
        private AppConfig(Hotkey open, Hotkey favorite, Hotkey unfavorite, bool enableNotification)
        { Open = open; Favorite = favorite; Unfavorite = unfavorite; EnableNotification = enableNotification; }

        public static AppConfig Defaults()
        {
            return new AppConfig(ParseHotkey("Ctrl+Shift+P"), ParseHotkey("Ctrl+F"), ParseHotkey("Ctrl+Shift+U"), true);
        }

        public static AppConfig Load(string path, out string warning)
        {
            warning = null;
            if (!File.Exists(path))
            {
                AppConfig value = Defaults();
                File.WriteAllText(path, "{\r\n  \"open_hotkey\": \"Ctrl+Shift+P\",\r\n  \"favorite_hotkey\": \"Ctrl+F\",\r\n  \"unfavorite_hotkey\": \"Ctrl+Shift+U\",\r\n  \"enable_notification\": true\r\n}\r\n");
                return value;
            }
            try
            {
                string json = File.ReadAllText(path);
                Dictionary<string, string> strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (Match match in Regex.Matches(json, "\\\"([^\\\"]+)\\\"\\s*:\\s*\\\"([^\\\"]*)\\\"")) strings[match.Groups[1].Value] = match.Groups[2].Value;
                Match enabled = Regex.Match(json, "\\\"enable_notification\\\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
                if (!strings.ContainsKey("open_hotkey") || !strings.ContainsKey("favorite_hotkey") || !strings.ContainsKey("unfavorite_hotkey") || !enabled.Success)
                    throw new FormatException("required configuration field is missing or malformed");
                Hotkey open = ParseHotkey(strings["open_hotkey"]), favorite = ParseHotkey(strings["favorite_hotkey"]), unfavorite = ParseHotkey(strings["unfavorite_hotkey"]);
                if (Same(open, favorite) || Same(open, unfavorite) || Same(favorite, unfavorite)) throw new FormatException("hotkeys must be distinct");
                return new AppConfig(open, favorite, unfavorite, Boolean.Parse(enabled.Groups[1].Value));
            }
            catch (Exception ex)
            {
                warning = "Unsafe config rejected; safe defaults loaded: " + ex.Message;
                return Defaults();
            }
        }

        private static Hotkey ParseHotkey(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) throw new FormatException("empty hotkey");
            bool ctrl = false, shift = false; uint key = 0;
            string[] parts = text.Split('+');
            foreach (string raw in parts)
            {
                string part = raw.Trim().ToUpperInvariant();
                if (part == "CTRL" && !ctrl) ctrl = true;
                else if (part == "SHIFT" && !shift) shift = true;
                else if (part.Length == 1 && part[0] >= 'A' && part[0] <= 'Z' && key == 0) key = part[0];
                else throw new FormatException("unsupported hotkey: " + text);
            }
            // Ctrl is mandatory. This keeps ordinary typing, Alt combinations,
            // modifier tracking, repeat handling, and passthrough unambiguous.
            if (!ctrl || key == 0) throw new FormatException("hotkey requires Ctrl and one A-Z key: " + text);
            return new Hotkey(true, shift, key, (shift ? "Ctrl+Shift+" : "Ctrl+") + (char)key);
        }
        private static bool Same(Hotkey a, Hotkey b) { return a.Ctrl == b.Ctrl && a.Shift == b.Shift && a.VirtualKey == b.VirtualKey; }
    }
}
