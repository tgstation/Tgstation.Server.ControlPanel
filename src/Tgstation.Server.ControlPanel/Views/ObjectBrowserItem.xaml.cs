using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using Tgstation.Server.ControlPanel.ViewModels;

namespace Tgstation.Server.ControlPanel.Views
{
	public class ObjectBrowserItem : UserControl
	{
		public ObjectBrowserItem() => AvaloniaXamlLoader.Load(this);
	}
}
