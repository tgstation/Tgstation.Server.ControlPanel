using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	interface ICommandReceiver<TCommand> where TCommand : Enum
	{
		bool CanRunCommand(TCommand command);
		Task RunCommand(TCommand command, CancellationToken cancellationToken);
	}
}
