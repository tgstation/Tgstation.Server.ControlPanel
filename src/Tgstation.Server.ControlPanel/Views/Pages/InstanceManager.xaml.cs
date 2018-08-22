using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Tgstation.Server.ControlPanel.Views.Pages
{
    public class InstanceManager : UserControl
    {
        public InstanceManager()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
