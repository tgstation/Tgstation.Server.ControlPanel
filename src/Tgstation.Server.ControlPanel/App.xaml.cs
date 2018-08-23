using Avalonia;
using Avalonia.Markup.Xaml;

namespace Tgstation.Server.ControlPanel
{
    public sealed class App : Application
    {
        public override void Initialize() => AvaloniaXamlLoader.Load(this);
    }
}
