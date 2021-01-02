using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Client;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class AddInstanceViewModel : ViewModelBase, ITreeNode, ICommandReceiver<AddInstanceViewModel.AddInstanceCommand>
	{
		const string DefaultInstancePath = "/tgs4_instances/";

		public enum AddInstanceCommand
		{
			Close,
			Add
		}

		public string Title => "Add Instance";

		public string Icon => "resm:Tgstation.Server.ControlPanel.Assets.plus.jpg";

		public bool IsExpanded { get; set; }

		public string Name
		{
			get => name;
			set
			{
				var simpleMode = Path.StartsWith(DefaultInstancePath, StringComparison.Ordinal) == true && Path.EndsWith(name ?? String.Empty, StringComparison.Ordinal) == true;
				this.RaiseAndSetIfChanged(ref name, value);
				if (simpleMode)
					Path = DefaultInstancePath + name;
				Add.Recheck();
			}
		}

		public string ValidPaths => serverInformation.ValidInstancePaths?.Any() == true ? $"\"{String.Join("\", \"", serverInformation.ValidInstancePaths)}\"" : "Anywhere!";

		public string Path
		{
			get => path;
			set
			{
				this.RaiseAndSetIfChanged(ref path, value);
				Add.Recheck();
			}
		}

		public ICommand Close { get; }
		EnumCommand<AddInstanceCommand> Add { get; }

		public IReadOnlyList<ITreeNode> Children => null;

		readonly PageContextViewModel pageContext;
		readonly IInstanceManagerClient instanceManagerClient;
		readonly InstanceRootViewModel instanceRootViewModel;
		readonly ServerInformation serverInformation;

		string name;
		string path;

		bool adding;

		public AddInstanceViewModel(PageContextViewModel pageContext, ServerInformation serverInformation, IInstanceManagerClient instanceManagerClient, InstanceRootViewModel instanceRootViewModel)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.serverInformation = serverInformation ?? throw new ArgumentNullException(nameof(serverInformation));
			this.instanceManagerClient = instanceManagerClient ?? throw new ArgumentNullException(nameof(instanceManagerClient));
			this.instanceRootViewModel = instanceRootViewModel ?? throw new ArgumentNullException(nameof(instanceRootViewModel));

			Close = new EnumCommand<AddInstanceCommand>(AddInstanceCommand.Close, this);
			Add = new EnumCommand<AddInstanceCommand>(AddInstanceCommand.Add, this);

			Path = serverInformation.ValidInstancePaths?.FirstOrDefault() ?? DefaultInstancePath;
			Name = String.Empty;
		}

		public Task HandleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}

		public bool CanRunCommand(AddInstanceCommand command)
		{
			switch (command)
			{
				case AddInstanceCommand.Close:
					return true;
				case AddInstanceCommand.Add:
					return !adding && !String.IsNullOrWhiteSpace(Name) && !String.IsNullOrWhiteSpace(Path);
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}

		public async Task RunCommand(AddInstanceCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case AddInstanceCommand.Close:
					pageContext.ActiveObject = null;
					break;
				case AddInstanceCommand.Add:
					adding = true;
					Add.Recheck();
					try
					{
						var newInstance = await instanceManagerClient.CreateOrAttach(new Instance
						{
							Name = Name,
							Path = Path
						}, cancellationToken).ConfigureAwait(true);
						instanceRootViewModel.DirectAddInstance(newInstance);
					}
					finally
					{
						adding = false;
						Add.Recheck();
					}
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}