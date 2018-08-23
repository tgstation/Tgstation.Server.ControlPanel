using System;
using System.Windows.Input;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class EnumCommand<TCommand> : ICommand where TCommand : Enum
	{
		readonly ICommandReceiver<TCommand> commandReceiver;
		readonly TCommand command;
		public EnumCommand(TCommand command, ICommandReceiver<TCommand> commandReceiver)
		{
			this.command = command;
			this.commandReceiver = commandReceiver ?? throw new ArgumentNullException(nameof(commandReceiver));
		}

		public event EventHandler CanExecuteChanged;

		public bool CanExecute(object parameter) => commandReceiver.CanRunCommand(command);

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
