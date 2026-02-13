using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace PS2MemoryLane
{
    /// <summary>
    /// Persistent plugin settings stored by Playnite.
    /// </summary>
    public class PS2MemoryLaneSettings : ObservableObject
    {
        private Guid m_PlatformId = Guid.Empty;
        private string m_TemplateMemoryCardPath = string.Empty;
        private string m_OutputFolderPath = string.Empty;

        /// <summary>
        /// Selected Playnite platform identifier.
        /// </summary>
        public Guid PlatformId { get => m_PlatformId; set => SetValue(ref m_PlatformId, value); }

        /// <summary>
        /// Full path to the template memory card file.
        /// </summary>
        public string TemplateMemoryCardPath { get => m_TemplateMemoryCardPath; set => SetValue(ref m_TemplateMemoryCardPath, value); }

        /// <summary>
        /// Output folder where memory cards will be created.
        /// </summary>
        public string OutputFolderPath { get => m_OutputFolderPath; set => SetValue(ref m_OutputFolderPath, value); }
    }

    /// <summary>
    /// Lightweight platform entry for UI selection.
    /// </summary>
    public sealed class PlatformItem
    {
        public PlatformItem(Guid id, string name)
        {
            Id = id;
            Name = name ?? string.Empty;
        }

        public Guid Id { get; }
        public string Name { get; }
    }

    /// <summary>
    /// Result summary for a memory card creation run.
    /// </summary>
    public sealed class MemoryCardCreationResult
    {
        private readonly List<string> m_Errors = new List<string>();
        private readonly List<string> m_Notes = new List<string>();

        public int TotalGames { get; set; }
        public int CreatedCount { get; set; }
        public int SkippedCount { get; set; }
        public int FailedCount { get; set; }

        public IReadOnlyList<string> Errors => m_Errors;
        public IReadOnlyList<string> Notes => m_Notes;

        public void AddError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                m_Errors.Add(message);
            }
        }

        public void AddNote(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                m_Notes.Add(message);
            }
        }

        /// <summary>
        /// Builds a human-readable summary of the creation run.
        /// </summary>
        public string BuildSummaryMessage()
        {
            var builder = new StringBuilder();
            AppendCounts(builder);
            AppendNotes(builder);
            AppendErrors(builder);
            return builder.ToString().Trim();
        }

        private void AppendCounts(StringBuilder builder)
        {
            builder.AppendLine($"Games scanned: {TotalGames}");
            builder.AppendLine($"Created: {CreatedCount}");
            builder.AppendLine($"Skipped (already exists): {SkippedCount}");
            builder.AppendLine($"Failed: {FailedCount}");
        }

        private void AppendNotes(StringBuilder builder)
        {
            if (m_Notes.Count == 0)
            {
                return;
            }

            builder.AppendLine();
            builder.AppendLine("Notes:");
            AppendLines(builder, m_Notes);
        }

        private void AppendErrors(StringBuilder builder)
        {
            if (m_Errors.Count == 0)
            {
                return;
            }

            builder.AppendLine();
            builder.AppendLine("Errors:");
            AppendLines(builder, m_Errors);
        }

        private void AppendLines(StringBuilder builder, List<string> items)
        {
            foreach (var item in items)
            {
                builder.AppendLine(item);
            }
        }
    }

    /// <summary>
    /// Performs memory card creation and path resolution.
    /// </summary>
    public sealed class MemoryCardManager
    {
        private static readonly char[] m_InvalidFileNameChars = Path.GetInvalidFileNameChars();

        private readonly IPlayniteAPI m_PlayniteApi;

        public MemoryCardManager(IPlayniteAPI playniteApi)
        {
            m_PlayniteApi = playniteApi;
        }

        /// <summary>
        /// Creates a memory card per game for the selected platform.
        /// </summary>
        public MemoryCardCreationResult CreateMemoryCards(Guid platformId, string templatePath, string outputFolder)
        {
            var result = new MemoryCardCreationResult();
            if (!ValidateInputs(platformId, templatePath, outputFolder, result))
            {
                return result;
            }

            var games = GetGamesForPlatform(platformId);
            if (!TrySetGameCount(games, result))
            {
                return result;
            }

            EnsureOutputFolder(outputFolder);
            CreateCardsForGames(games, templatePath, outputFolder, result);
            return result;
        }

        private bool ValidateInputs(Guid platformId, string templatePath, string outputFolder, MemoryCardCreationResult result)
        {
            if (platformId == Guid.Empty)
            {
                result.AddError("Please select a platform.");
            }

            if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
            {
                result.AddError("Template memory card file is missing or invalid.");
            }

            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                result.AddError("Please select an output folder.");
            }

            return result.Errors.Count == 0;
        }

        private bool TrySetGameCount(List<Game> games, MemoryCardCreationResult result)
        {
            result.TotalGames = games.Count;
            if (games.Count > 0)
            {
                return true;
            }

            result.AddNote("No games found for the selected platform.");
            return false;
        }

        private List<Game> GetGamesForPlatform(Guid platformId)
        {
            return m_PlayniteApi.Database.Games
                .Where(game => game.PlatformIds?.Contains(platformId) == true)
                .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(game => game.Id)
                .ToList();
        }

        private void EnsureOutputFolder(string outputFolder)
        {
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }
        }

        private void CreateCardsForGames(List<Game> games, string templatePath, string outputFolder, MemoryCardCreationResult result)
        {
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var extension = GetTemplateExtension(templatePath);

            foreach (var game in games)
            {
                ProcessGame(game, templatePath, outputFolder, extension, usedNames, result);
            }
        }

        private void ProcessGame(
            Game game,
            string templatePath,
            string outputFolder,
            string extension,
            HashSet<string> usedNames,
            MemoryCardCreationResult result)
        {
            var fileName = BuildMemoryCardFileName(game, extension, usedNames);
            var destinationPath = Path.Combine(outputFolder, fileName);

            if (File.Exists(destinationPath))
            {
                result.SkippedCount++;
                return;
            }

            TryCopyTemplate(templatePath, destinationPath, game.Name, result);
        }

        private string BuildMemoryCardFileName(Game game, string extension, HashSet<string> usedNames)
        {
            var safeName = GetSafeFileName(game.Name);
            if (string.IsNullOrWhiteSpace(safeName))
            {
                safeName = game.Id.ToString();
            }

            var fileName = $"{safeName}{extension}";
            if (usedNames.Add(fileName))
            {
                return fileName;
            }

            var uniqueName = $"{safeName}_{GetShortId(game.Id)}{extension}";
            usedNames.Add(uniqueName);
            return uniqueName;
        }

        private string GetSafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var trimmed = name.Trim();
            var builder = new StringBuilder(trimmed.Length);
            foreach (var character in trimmed)
            {
                if (Array.IndexOf(m_InvalidFileNameChars, character) >= 0)
                {
                    builder.Append('_');
                }
                else
                {
                    builder.Append(character);
                }
            }

            return builder.ToString().Trim();
        }

        private string GetShortId(Guid id)
        {
            return id.ToString("N").Substring(0, 8);
        }

        private string GetTemplateExtension(string templatePath)
        {
            var extension = Path.GetExtension(templatePath);
            return string.IsNullOrWhiteSpace(extension) ? ".ps2" : extension;
        }

        private void TryCopyTemplate(string templatePath, string destinationPath, string gameName, MemoryCardCreationResult result)
        {
            try
            {
                File.Copy(templatePath, destinationPath);
                result.CreatedCount++;
            }
            catch (Exception ex)
            {
                result.FailedCount++;
                result.AddError($"Failed to create memory card for \"{gameName}\": {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Settings view model with commands and validation.
    /// </summary>
    public class PS2MemoryLaneSettingsViewModel : ObservableObject, ISettings
    {
        private readonly PS2MemoryLanePlugin m_Plugin;
        private readonly MemoryCardManager m_MemoryCardManager;
        private PS2MemoryLaneSettings m_EditingClone;
        private PS2MemoryLaneSettings m_Settings;
        private ObservableCollection<PlatformItem> m_Platforms;
        private PlatformItem m_SelectedPlatform;
        private RelayCommand m_BrowseTemplateCommand;
        private RelayCommand m_BrowseOutputFolderCommand;
        private RelayCommand m_CreateMemoryCardsCommand;

        /// <summary>
        /// Backing settings instance bound to the view.
        /// </summary>
        public PS2MemoryLaneSettings Settings
        {
            get => m_Settings;
            set
            {
                SetValue(ref m_Settings, value);
            }
        }

        public PS2MemoryLaneSettingsViewModel(PS2MemoryLanePlugin plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            m_Plugin = plugin;
            m_MemoryCardManager = new MemoryCardManager(plugin.PlayniteApi);

            LoadSettings();
            CreateCommands();
        }

        /// <summary>
        /// Available platform list for the dropdown.
        /// </summary>
        public ObservableCollection<PlatformItem> Platforms
        {
            get => m_Platforms;
            private set => SetValue(ref m_Platforms, value);
        }

        /// <summary>
        /// Currently selected platform.
        /// </summary>
        public PlatformItem SelectedPlatform
        {
            get => m_SelectedPlatform;
            set
            {
                if (Equals(m_SelectedPlatform, value))
                {
                    return;
                }

                SetValue(ref m_SelectedPlatform, value);
                if (Settings != null)
                {
                    Settings.PlatformId = value?.Id ?? Guid.Empty;
                }
            }
        }

        /// <summary>
        /// Command to choose a template memory card file.
        /// </summary>
        public RelayCommand BrowseTemplateCommand => m_BrowseTemplateCommand;

        /// <summary>
        /// Command to choose the output folder.
        /// </summary>
        public RelayCommand BrowseOutputFolderCommand => m_BrowseOutputFolderCommand;

        /// <summary>
        /// Command to create memory cards for the selected platform.
        /// </summary>
        public RelayCommand CreateMemoryCardsCommand => m_CreateMemoryCardsCommand;

        /// <summary>
        /// Reloads platform data from the Playnite database.
        /// </summary>
        public void RefreshPlatforms()
        {
            LoadPlatforms();
        }

        private void LoadSettings()
        {
            var savedSettings = m_Plugin.LoadPluginSettings<PS2MemoryLaneSettings>();
            Settings = savedSettings ?? new PS2MemoryLaneSettings();
        }

        private void LoadPlatforms()
        {
            var platforms = m_Plugin.PlayniteApi?.Database?.Platforms;
            if (platforms == null)
            {
                Platforms = new ObservableCollection<PlatformItem>();
                SelectedPlatform = null;
                return;
            }

            var items = platforms
                .Select(platform => new PlatformItem(platform.Id, platform.Name))
                .OrderBy(platform => platform.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Platforms = new ObservableCollection<PlatformItem>(items);
            SyncSelectedPlatformFromSettings();
        }

        private void SyncSelectedPlatformFromSettings()
        {
            if (Settings == null || Platforms == null)
            {
                return;
            }

            if (Settings.PlatformId == Guid.Empty)
            {
                SelectedPlatform = null;
                return;
            }

            SelectedPlatform = Platforms.FirstOrDefault(item => item.Id == Settings.PlatformId);
        }

        private void CreateCommands()
        {
            m_BrowseTemplateCommand = new RelayCommand(BrowseTemplate);
            m_BrowseOutputFolderCommand = new RelayCommand(BrowseOutputFolder);
            m_CreateMemoryCardsCommand = new RelayCommand(CreateMemoryCards);
        }

        private void BrowseTemplate()
        {
            var selectedPath = m_Plugin.PlayniteApi.Dialogs.SelectFile("PS2 Memory Card (*.ps2)|*.ps2|All files (*.*)|*.*");
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                Settings.TemplateMemoryCardPath = selectedPath;
            }
        }

        private void BrowseOutputFolder()
        {
            var selectedPath = m_Plugin.PlayniteApi.Dialogs.SelectFolder();
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                Settings.OutputFolderPath = selectedPath;
            }
        }

        private void CreateMemoryCards()
        {
            var result = m_MemoryCardManager.CreateMemoryCards(
                Settings.PlatformId,
                Settings.TemplateMemoryCardPath,
                Settings.OutputFolderPath);

            m_Plugin.PlayniteApi.Dialogs.ShowMessage(result.BuildSummaryMessage(), "PS2 Memory Lane");
        }

        /// <summary>
        /// Called when the settings view begins editing.
        /// </summary>
        public void BeginEdit()
        {
            // Code executed when settings view is opened and user starts editing values.
            m_EditingClone = Serialization.GetClone(Settings);
            LoadPlatforms();
        }

        /// <summary>
        /// Called when the user cancels settings edits.
        /// </summary>
        public void CancelEdit()
        {
            // Code executed when user decides to cancel any changes made since BeginEdit was called.
            // This method reverts any settings changes made since BeginEdit.
            Settings = m_EditingClone;
            SyncSelectedPlatformFromSettings();
        }

        /// <summary>
        /// Called when the user confirms settings edits.
        /// </summary>
        public void EndEdit()
        {
            // Code executed when user decides to confirm changes made since BeginEdit was called.
            // This method saves confirmed settings.
            m_Plugin.SavePluginSettings(Settings);
        }

        /// <summary>
        /// Called before saving to validate settings.
        /// </summary>
        public bool VerifySettings(out List<string> errors)
        {
            // Code execute when user decides to confirm changes made since BeginEdit was called.
            // Executed before EndEdit is called and EndEdit is not called if false is returned.
            // List of errors is presented to user if verification fails.
            errors = new List<string>();
            return true;
        }
    }
}
