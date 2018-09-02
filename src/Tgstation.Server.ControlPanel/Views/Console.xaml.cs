using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;

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

			var scrollViewer = this.FindControl<ScrollViewer>("_scrollViewer");
			DispatcherTimer timer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(1)
			};
			timer.Tick += ((sender, e) =>
			{
				scrollViewer.Offset = new Vector(0, scrollViewer.Extent.Height);
			});
			timer.Start();
		}
    }
}
