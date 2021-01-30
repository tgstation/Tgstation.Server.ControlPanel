using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace Tgstation.Server.ControlPanel.Views
{
	public class Console : UserControl
	{
		public Console()
		{
			this.InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);

			var scrollViewer = this.FindControl<TextBox>("_scrollViewer");
			DispatcherTimer timer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(1)
			};
			timer.Tick += ((sender, e) =>
			{
				scrollViewer.CaretIndex = int.MaxValue;
			});
			timer.Start();
		}
	}
}
