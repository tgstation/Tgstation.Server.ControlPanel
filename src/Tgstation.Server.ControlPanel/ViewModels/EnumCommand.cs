using System;
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
			try
			{
				CanExecuteChanged?.Invoke(this, new EventArgs());
			}
			catch (InvalidOperationException)
			{
				System.Diagnostics.Debugger.Break();
			}	//main thread issue
		}


		public async void Execute(object parameter)
		{
			try
			{
				await commandReceiver.RunCommand(command, default).ConfigureAwait(false);
			}
			catch { }	//TODO
		}
	}
}
