using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Tgstation.Server.ControlPanel.ViewModels;

namespace Tgstation.Server.ControlPanel.Views
{
	public class ObjectBrowserItem : UserControl
	{
		public ObjectBrowserItem()
		{
			AvaloniaXamlLoader.Load(this);
			Tapped += HandleTap;
		}

		async void HandleTap(object sender, RoutedEventArgs eventArgs) => await ((ITreeNode)DataContext).HandleClick(default).ConfigureAwait(false);
	}
}
