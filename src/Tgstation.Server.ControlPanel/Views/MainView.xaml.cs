using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Tgstation.Server.ControlPanel.Views
{
    public sealed class MainView : UserControl
    {
        public MainView() => InitializeComponent();

		void InitializeComponent() => AvaloniaXamlLoaderPortableXaml.Load(this);
    }
}
