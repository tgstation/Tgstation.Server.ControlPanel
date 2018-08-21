using Avalonia;
using Avalonia.Markup.Xaml;

namespace Tgstation.Server.ControlPanel
{
    public class App : Application
    {
        public override void Initialize() => AvaloniaXamlLoaderPortableXaml.Load(this);
    }
}
