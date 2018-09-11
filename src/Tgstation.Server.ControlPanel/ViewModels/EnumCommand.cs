using Avalonia.Threading;
using System;
using System.Globalization;
using System.Windows.Input;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	public sealed class EnumCommand<TCommand> : ICommand where TCommand : Enum
	{
		readonly ICommandReceiver<TCommand> commandReceiver;
		readonly TCommand command;

		public event EventHandler CanExecuteChanged;
		public EnumCommand(TCommand command, ICommandReceiver<TCommand> commandReceiver)
		{
			this.command = command;
			this.commandReceiver = commandReceiver ?? throw new ArgumentNullException(nameof(commandReceiver));
		}

		public bool CanExecute(object parameter) => commandReceiver.CanRunCommand(command);

		public void Recheck()
		{
			void Invoke() => CanExecuteChanged?.Invoke(this, new EventArgs());
			if (Dispatcher.UIThread == null)
				Invoke();
			else
				Dispatcher.UIThread.Post(Invoke);
		}

		public async void Execute(object parameter)
		{
			try
			{
				await commandReceiver.RunCommand(command, default).ConfigureAwait(false);
			}
			catch (Exception e)
			{
				lock (MainWindowViewModel.Singleton)
					MainWindowViewModel.Singleton.ConsoleContent = String.Format(CultureInfo.InvariantCulture, "{0}{1}[{2}]: UNCAUGHT COMMAND EXCEPTION! Type: {3} Action: {4} Exception: {5}", MainWindowViewModel.Singleton.ConsoleContent, Environment.NewLine, DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture), typeof(TCommand).Name, command, e);
			}
		}
	}
}
