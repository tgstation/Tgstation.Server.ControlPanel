using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Client;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class AdministrationViewModel : ViewModelBase, ITreeNode, ICommandReceiver<AdministrationViewModel.AdministrationCommand>
	{
		public enum AdministrationCommand
		{
			Close,
			Restart,
			Update
		}

		public string Title => "Administration";

		public string Icon => "resm:Tgstation.Server.ControlPanel.Assets.gear.png";

		public IReadOnlyList<ITreeNode> Children => null;

		readonly PageContextViewModel pageContext;
		readonly IAdministrationClient administrationClient;
		readonly IUserRightsProvider userRightsProvider;

		public AdministrationViewModel(PageContextViewModel pageContext, IAdministrationClient administrationClient, IUserRightsProvider userRightsProvider)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.administrationClient = administrationClient ?? throw new ArgumentNullException(nameof(administrationClient));
			this.userRightsProvider = userRightsProvider ?? throw new ArgumentNullException(nameof(userRightsProvider));
		}

		public Task HandleDoubleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}

		public bool CanRunCommand(AdministrationCommand command)
		{
			switch (command)
			{
				case AdministrationCommand.Close:
				case AdministrationCommand.Restart:
					return true;
				case AdministrationCommand.Update:
					return false;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}

		public Task RunCommand(AdministrationCommand command, CancellationToken cancellationToken)
		{
		}
	}
}
