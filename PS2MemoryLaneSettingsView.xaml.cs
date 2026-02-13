using PS2MemoryLane;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PS2MemoryLane.Views
{
    public partial class PS2MemoryLaneSettingsView : UserControl
    {
        public PS2MemoryLaneSettingsView()
        {
            InitializeComponent();
        }

        private void OnViewLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is PS2MemoryLaneSettingsViewModel viewModel)
            {
                viewModel.RefreshPlatforms();
            }
        }
    }
}
