using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;

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
        internal const string DefaultFavoriteFolderName = FavoriteService.FavoritesDirectoryName;
        public readonly Hotkey Open, Favorite, Unfavorite;
        public readonly bool EnableNotification;
        public readonly string FavoriteFolderName;
        internal static Action BeforeConfigCommit;
        private AppConfig(Hotkey open, Hotkey favorite, Hotkey unfavorite, bool enableNotification, string favoriteFolderName)
        { Open = open; Favorite = favorite; Unfavorite = unfavorite; EnableNotification = enableNotification; FavoriteFolderName = favoriteFolderName; }

        public static AppConfig Defaults()
        {
            return new AppConfig(ParseHotkey("Ctrl+Shift+P"), ParseHotkey("Ctrl+F"), ParseHotkey("Ctrl+Shift+U"), true, DefaultFavoriteFolderName);
        }

        public static AppConfig Load(string path, out string warning)
        {
            warning = null;
            if (!File.Exists(path))
            {
                AppConfig value = Defaults();
                WriteSafely(path, value.ToJson());
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
                string folder = strings.ContainsKey("favorite_folder_name") ? strings["favorite_folder_name"] : DefaultFavoriteFolderName;
                string validation;
                if (!TryValidateFavoriteFolderName(folder, out validation)) throw new FormatException(validation);
                return new AppConfig(open, favorite, unfavorite, Boolean.Parse(enabled.Groups[1].Value), folder);
            }
            catch (Exception ex)
            {
                warning = "Unsafe config rejected; safe defaults loaded: " + ex.Message;
                return Defaults();
            }
        }

        public AppConfig WithFavoriteFolderName(string value)
        {
            string error;
            if (!TryValidateFavoriteFolderName(value, out error)) throw new ArgumentException(error, "value");
            return new AppConfig(Open, Favorite, Unfavorite, EnableNotification, value);
        }

        public void Save(string path) { WriteSafely(path, ToJson()); }

        internal static bool TryValidateFavoriteFolderName(string value, out string error)
        {
            error = null;
            if (String.IsNullOrWhiteSpace(value)) error = "Favorite folder name cannot be empty.";
            else if (value == "." || value == "..") error = "Favorite folder name cannot be . or ...";
            else if (value.EndsWith(" ", StringComparison.Ordinal) || value.EndsWith(".", StringComparison.Ordinal)) error = "Favorite folder name cannot end in a space or period.";
            else if (value.IndexOfAny(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' }) >= 0 || ContainsControl(value)) error = "Favorite folder name contains a character Windows does not allow.";
            else
            {
                string stem = value.Split('.')[0];
                if (Regex.IsMatch(stem, "^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])$", RegexOptions.IgnoreCase))
                    error = "Favorite folder name is a reserved Windows device name.";
            }
            return error == null;
        }

        private string ToJson()
        {
            return "{\r\n  \"open_hotkey\": \"" + Escape(Open.Text) + "\",\r\n  \"favorite_hotkey\": \"" + Escape(Favorite.Text) + "\",\r\n  \"unfavorite_hotkey\": \"" + Escape(Unfavorite.Text) + "\",\r\n  \"enable_notification\": " + EnableNotification.ToString().ToLowerInvariant() + ",\r\n  \"favorite_folder_name\": \"" + Escape(FavoriteFolderName) + "\"\r\n}\r\n";
        }

        private static void WriteSafely(string path, string contents)
        {
            string full = Path.GetFullPath(path), directory = Path.GetDirectoryName(full);
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, ".favoritehelper-config-" + Guid.NewGuid().ToString("N") + ".tmp");
            string backup = temporary + ".bak";
            try
            {
                File.WriteAllText(temporary, contents, new UTF8Encoding(false));
                if (BeforeConfigCommit != null) BeforeConfigCommit();
                if (File.Exists(full)) File.Replace(temporary, full, backup, true); else File.Move(temporary, full);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
                try { if (File.Exists(backup)) File.Delete(backup); } catch { }
            }
        }
        private static bool ContainsControl(string value) { foreach (char c in value) if (c < 32) return true; return false; }
        private static string Escape(string value) { return value.Replace("\\", "\\\\").Replace("\"", "\\\""); }

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
