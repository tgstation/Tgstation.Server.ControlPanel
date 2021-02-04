using System;
using System.Net;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Tgstation.Server.ControlPanel.ViewModels;
using Tgstation.Server.ControlPanel.Views;

namespace Tgstation.Server.ControlPanel
{
	public sealed class App : Application, IDisposable
	{
		public MainWindowViewModel MainWindowViewModel { get; }

		public App()
		{
			MainWindowViewModel = new MainWindowViewModel(new NotificationUpdater());
		}

		public override void Initialize() => AvaloniaXamlLoader.Load(this);

		public override void OnFrameworkInitializationCompleted()
		{
			// Add Tls1.2 to the existing enabled protocols
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

			MainWindowViewModel.AsyncStart();

			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				desktop.MainWindow = new MainWindow()
				{
					DataContext = MainWindowViewModel
				};
			}
			else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
			{
				singleView.MainView = new MainView()
				{
					DataContext = MainWindowViewModel
				};
			}

			if (ApplicationLifetime is IControlledApplicationLifetime controlledApplicationLifetime)
				controlledApplicationLifetime.Exit += (a, b) => Dispose();
			base.OnFrameworkInitializationCompleted();
		}

		public void Dispose() => MainWindowViewModel.Dispose();
	}
}
