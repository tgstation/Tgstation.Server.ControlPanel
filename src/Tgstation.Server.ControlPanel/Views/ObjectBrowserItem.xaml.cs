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
			DoubleTapped += HandleDoubleTap;
		}

		async void HandleDoubleTap(object sender, RoutedEventArgs eventArgs) => await ((ITreeNode)DataContext).HandleDoubleClick(default).ConfigureAwait(false);
	}
}
