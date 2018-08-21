using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Tgstation.Server.ControlPanel.Views
{
    public class MainView : UserControl
    {
        public MainView()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
