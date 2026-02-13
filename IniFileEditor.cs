using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace PS2MemoryLane
{
    /// <summary>
    /// Minimal INI reader/writer for updating PCSX2 config.
    /// </summary>
    public static class IniFileEditor
    {
        public static bool TryFindKeyInSection(string path, string section, IReadOnlyList<string> candidateKeys, out string foundKey)
        {
            foundKey = null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            if (candidateKeys == null || candidateKeys.Count == 0)
            {
                return false;
            }

            var lines = File.ReadAllLines(path);
            if (!TryFindSection(lines, section, out var sectionIndex))
            {
                return false;
            }

            for (var i = sectionIndex + 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (IsSectionHeader(line))
                {
                    break;
                }

                if (TryParseKeyValue(line, out var parsedKey, out _) &&
                    candidateKeys.Any(candidate => string.Equals(candidate, parsedKey, StringComparison.OrdinalIgnoreCase)))
                {
                    foundKey = parsedKey;
                    return true;
                }
            }

            return false;
        }
        public static bool TryReadValue(string path, string section, string key, out string value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            var lines = File.ReadAllLines(path);
            if (!TryFindSection(lines, section, out var sectionIndex))
            {
                return false;
            }

            for (var i = sectionIndex + 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (IsSectionHeader(line))
                {
                    break;
                }

                if (TryParseKeyValue(line, out var parsedKey, out var parsedValue) &&
                    string.Equals(parsedKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = parsedValue;
                    return true;
                }
            }

            return false;
        }

        public static bool TryWriteValue(string path, string section, string key, string value, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "INI path is empty.";
                return false;
            }

            if (!File.Exists(path))
            {
                error = "INI file does not exist.";
                return false;
            }

            var lines = new List<string>(File.ReadAllLines(path));
            if (!TryFindSection(lines, section, out var sectionIndex))
            {
                AppendSection(lines, section, key, value);
                File.WriteAllLines(path, lines);
                return true;
            }

            if (TryUpdateKey(lines, sectionIndex, key, value))
            {
                File.WriteAllLines(path, lines);
                return true;
            }

            InsertKey(lines, sectionIndex, key, value);
            File.WriteAllLines(path, lines);
            return true;
        }

        private static bool TryFindSection(IReadOnlyList<string> lines, string section, out int sectionIndex)
        {
            sectionIndex = -1;
            if (string.IsNullOrWhiteSpace(section))
            {
                return false;
            }

            var sectionHeader = $"[{section}]";
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i].Trim();
                if (string.Equals(line, sectionHeader, StringComparison.OrdinalIgnoreCase))
                {
                    sectionIndex = i;
                    return true;
                }
            }

            return false;
        }

        private static bool TryUpdateKey(List<string> lines, int sectionIndex, string key, string value)
        {
            for (var i = sectionIndex + 1; i < lines.Count; i++)
            {
                var line = lines[i];
                if (IsSectionHeader(line))
                {
                    return false;
                }

                if (TryParseKeyValue(line, out var parsedKey, out _) &&
                    string.Equals(parsedKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = $"{key}={value}";
                    return true;
                }
            }

            return false;
        }

        private static void InsertKey(List<string> lines, int sectionIndex, string key, string value)
        {
            var insertIndex = lines.Count;
            for (var i = sectionIndex + 1; i < lines.Count; i++)
            {
                if (IsSectionHeader(lines[i]))
                {
                    insertIndex = i;
                    break;
                }
            }

            lines.Insert(insertIndex, $"{key}={value}");
        }

        private static void AppendSection(List<string> lines, string section, string key, string value)
        {
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
            {
                lines.Add(string.Empty);
            }

            lines.Add($"[{section}]");
            lines.Add($"{key}={value}");
        }

        private static bool TryParseKeyValue(string line, out string key, out string value)
        {
            key = null;
            value = null;

            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            var trimmed = line.TrimStart();
            if (trimmed.StartsWith(";") || trimmed.StartsWith("#"))
            {
                return false;
            }

            var separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex <= 0)
            {
                return false;
            }

            key = trimmed.Substring(0, separatorIndex).Trim();
            value = trimmed.Substring(separatorIndex + 1).Trim();
            return !string.IsNullOrWhiteSpace(key);
        }

        private static bool IsSectionHeader(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            var trimmed = line.Trim();
            return trimmed.StartsWith("[") && trimmed.EndsWith("]");
        }
    }
}
