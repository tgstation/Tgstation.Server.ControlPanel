using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Tgstation.Server.ControlPanel.Views.Pages
{
	public class CompileJobList : UserControl
	{
		public CompileJobList()
		{
			this.InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
