using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.IO;

namespace PS2MemoryLane
{
    /// <summary>
    /// Updates PCSX2 configuration to point to the per-game memory card.
    /// </summary>
    public sealed class Pcsx2MemoryCardSwitcher
    {
        private static readonly ILogger m_Logger = LogManager.GetLogger();
        private static readonly string[] m_KnownMemoryCardKeys = { "Slot1_Filename", "Mcd001" };
        private static readonly string[] m_KnownSlotEnableKeys = { "Slot1_Enable" };
        private const string MemoryCardSectionName = "MemoryCards";

        private readonly MemoryCardManager m_MemoryCardManager;

        private Guid? m_LastGameId;
        private string m_LastConfigPath;
        private string m_LastSection;
        private string m_LastKey;
        private string m_PreviousValue;
        private bool m_PreviousValueFound;

        public Pcsx2MemoryCardSwitcher(MemoryCardManager memoryCardManager)
        {
            m_MemoryCardManager = memoryCardManager;
        }

        public void SwitchMemoryCard(Game game, PS2MemoryLaneSettings settings)
        {
            if (!ShouldHandleGame(game, settings, out var platformId))
            {
                return;
            }

            if (!TryResolveIniKey(settings, out var iniSection, out var iniKey, out var previousValueFound, out var previousValue))
            {
                return;
            }

            if (!TryResolveCardValue(game, settings, platformId, out var cardValue))
            {
                return;
            }

            if (!TryUpdateMemoryCardsFolderSetting(settings))
            {
                return;
            }

            if (!IniFileEditor.TryWriteValue(settings.Pcsx2ConfigPath, iniSection, iniKey, cardValue, out var error))
            {
                m_Logger.Error($"Failed to update PCSX2 config: {error}");
                return;
            }

            EnsurePrimaryKeysUpdated(settings, iniSection, iniKey, cardValue);
            EnsureSlotEnabled(settings, iniSection);

            m_LastGameId = game.Id;
            m_LastConfigPath = settings.Pcsx2ConfigPath;
            m_LastSection = iniSection;
            m_LastKey = iniKey;
            m_PreviousValue = previousValue;
            m_PreviousValueFound = previousValueFound;
        }

        public void RestorePreviousCardIfNeeded(Game game, PS2MemoryLaneSettings settings)
        {
            if (game == null || settings == null)
            {
                return;
            }

            if (!settings.EnableAutoSwitch || !settings.RestoreOnExit)
            {
                return;
            }

            if (m_LastGameId == null || m_LastGameId.Value != game.Id)
            {
                return;
            }

            if (!m_PreviousValueFound)
            {
                ClearLastSwitch();
                return;
            }

            if (string.IsNullOrWhiteSpace(m_LastConfigPath) ||
                string.IsNullOrWhiteSpace(m_LastSection) ||
                string.IsNullOrWhiteSpace(m_LastKey))
            {
                ClearLastSwitch();
                return;
            }

            if (!IniFileEditor.TryWriteValue(m_LastConfigPath, m_LastSection, m_LastKey, m_PreviousValue, out var error))
            {
                m_Logger.Error($"Failed to restore PCSX2 config: {error}");
            }

            ClearLastSwitch();
        }

        private bool ShouldHandleGame(Game game, PS2MemoryLaneSettings settings, out Guid platformId)
        {
            platformId = Guid.Empty;
            if (game == null || settings == null)
            {
                return false;
            }

            if (!settings.EnableAutoSwitch)
            {
                return false;
            }

            if (!m_MemoryCardManager.TryResolvePlatformId(settings.PlatformId, out platformId))
            {
                m_Logger.Warn("Auto-switch is enabled but platform is not configured and auto-detect failed.");
                return false;
            }

            if (game.PlatformIds?.Contains(platformId) != true)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(settings.OutputFolderPath))
            {
                m_Logger.Warn("Auto-switch is enabled but output folder is not configured.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(settings.Pcsx2ConfigPath) || !File.Exists(settings.Pcsx2ConfigPath))
            {
                m_Logger.Warn("Auto-switch is enabled but PCSX2 config path is missing or invalid.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(settings.Pcsx2IniSection) || string.IsNullOrWhiteSpace(settings.Pcsx2IniKey))
            {
                m_Logger.Warn("Auto-switch is enabled but INI section/key are not configured.");
                return false;
            }

            return true;
        }

        private bool TryResolveCardValue(Game game, PS2MemoryLaneSettings settings, Guid platformId, out string cardValue)
        {
            cardValue = null;
            if (!m_MemoryCardManager.TryGetMemoryCardFileName(platformId, game, settings.TemplateMemoryCardPath, out var fileName))
            {
                m_Logger.Warn("Unable to resolve memory card file name for game.");
                return false;
            }

            var outputFolder = settings.OutputFolderPath;
            var fullPath = Path.Combine(outputFolder, fileName);

            if (!File.Exists(fullPath))
            {
                if (!settings.AutoCreateMissingCard)
                {
                    m_Logger.Warn($"Memory card does not exist for \"{game.Name}\".");
                    return false;
                }

                if (!TryCreateMemoryCard(fullPath, settings.TemplateMemoryCardPath))
                {
                    return false;
                }
            }

            cardValue = fileName;
            return true;
        }

        private bool TryCreateMemoryCard(string destinationPath, string templatePath)
        {
            if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
            {
                m_Logger.Warn("Auto-create is enabled but the template memory card path is invalid.");
                return false;
            }

            var folder = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            try
            {
                File.Copy(templatePath, destinationPath);
                return true;
            }
            catch (Exception ex)
            {
                m_Logger.Error(ex, "Failed to create memory card from template.");
                return false;
            }
        }

        private bool TryResolveIniKey(
            PS2MemoryLaneSettings settings,
            out string iniSection,
            out string iniKey,
            out bool previousValueFound,
            out string previousValue)
        {
            iniSection = MemoryCardSectionName;
            previousValueFound = false;
            previousValue = null;

            if (IniFileEditor.TryFindKeyInSection(settings.Pcsx2ConfigPath, iniSection, m_KnownMemoryCardKeys, out var detectedKey))
            {
                iniKey = detectedKey;
                previousValueFound = IniFileEditor.TryReadValue(
                    settings.Pcsx2ConfigPath,
                    iniSection,
                    iniKey,
                    out previousValue);
                return true;
            }

            iniKey = m_KnownMemoryCardKeys[0];
            return true;
        }

        private bool TryUpdateMemoryCardsFolderSetting(PS2MemoryLaneSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.OutputFolderPath))
            {
                m_Logger.Warn("Memory card folder is missing.");
                return false;
            }

            if (!IniFileEditor.TryWriteValue(settings.Pcsx2ConfigPath, "Folders", "MemoryCards", settings.OutputFolderPath, out var error))
            {
                m_Logger.Error($"Failed to update PCSX2 folder config: {error}");
                return false;
            }

            return true;
        }

        private void EnsureSlotEnabled(PS2MemoryLaneSettings settings, string iniSection)
        {
            if (!IniFileEditor.TryFindKeyInSection(settings.Pcsx2ConfigPath, iniSection, m_KnownSlotEnableKeys, out var enableKey))
            {
                return;
            }

            IniFileEditor.TryWriteValue(settings.Pcsx2ConfigPath, iniSection, enableKey, "true", out _);
        }

        private void EnsurePrimaryKeysUpdated(PS2MemoryLaneSettings settings, string iniSection, string activeKey, string cardValue)
        {
            foreach (var key in m_KnownMemoryCardKeys)
            {
                if (string.Equals(key, activeKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                IniFileEditor.TryWriteValue(settings.Pcsx2ConfigPath, iniSection, key, cardValue, out _);
            }
        }

        private void ClearLastSwitch()
        {
            m_LastGameId = null;
            m_LastConfigPath = null;
            m_LastSection = null;
            m_LastKey = null;
            m_PreviousValue = null;
            m_PreviousValueFound = false;
        }
    }
}
