using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using PS2MemoryLane.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace PS2MemoryLane
{
    /// <summary>
    /// Main Playnite plugin entry point.
    /// </summary>
    public class PS2MemoryLanePlugin : GenericPlugin
    {
        private static readonly ILogger m_Logger = LogManager.GetLogger();

        private readonly PS2MemoryLaneSettingsViewModel m_SettingsViewModel;

        public override Guid Id { get; } = Guid.Parse("9a87ed3b-7961-43a1-a473-5560257a5c12");

        public PS2MemoryLanePlugin(IPlayniteAPI api) : base(api)
        {
            m_SettingsViewModel = new PS2MemoryLaneSettingsViewModel(this);
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
        }

        public override void OnGameInstalled(OnGameInstalledEventArgs args)
        {
            // Add code to be executed when game is finished installing.
        }

        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
            // Add code to be executed when game is started running.
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            // Add code to be executed when game is preparing to be started.
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            // Add code to be executed when game is preparing to be started.
        }

        public override void OnGameUninstalled(OnGameUninstalledEventArgs args)
        {
            // Add code to be executed when game is uninstalled.
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            // Add code to be executed when Playnite is initialized.
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            // Add code to be executed when Playnite is shutting down.
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            // Add code to be executed when library is updated.
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return m_SettingsViewModel;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new PS2MemoryLaneSettingsView
            {
                DataContext = m_SettingsViewModel
            };
        }
    }
}
