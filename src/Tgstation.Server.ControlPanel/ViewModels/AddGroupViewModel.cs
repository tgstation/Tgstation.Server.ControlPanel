using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using Tgstation.Server.Api.Models.Request;
using Tgstation.Server.Api.Models.Response;
using Tgstation.Server.Client;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class AddGroupViewModel : ViewModelBase, ITreeNode, ICommandReceiver<AddGroupViewModel.AddGroupCommand>
	{
		public enum AddGroupCommand
		{
			Add,
			Close
		}

		public string Title => "Add Group";

		public string Icon => "resm:Tgstation.Server.ControlPanel.Assets.plus.jpg";

		public bool IsExpanded { get; set; }

		public IReadOnlyList<ITreeNode> Children => null;

		public string GroupName
		{
			get => groupName;
			set
			{
				this.RaiseAndSetIfChanged(ref groupName, value);
				Add.Recheck();
			}
		}

		public EnumCommand<AddGroupCommand> Add { get; }
		public EnumCommand<AddGroupCommand> Close { get; }

		readonly PageContextViewModel pageContext;
		readonly ServerInformationResponse serverInformation;
		readonly IUserGroupsClient groupsClient;
		readonly IUserRightsProvider userRightsProvider;
		readonly UserGroupRootViewModel userGroupRootViewModel;

		string groupName;

		public AddGroupViewModel(PageContextViewModel pageContext, ServerInformationResponse serverInformation, IUserGroupsClient groupsClient, IUserRightsProvider userRightsProvider, UserGroupRootViewModel userGroupRootViewModel)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.serverInformation = serverInformation ?? throw new ArgumentNullException(nameof(serverInformation));
			this.groupsClient = groupsClient ?? throw new ArgumentNullException(nameof(groupsClient));
			this.userRightsProvider = userRightsProvider ?? throw new ArgumentNullException(nameof(userRightsProvider));
			this.userGroupRootViewModel = userGroupRootViewModel ?? throw new ArgumentNullException(nameof(userGroupRootViewModel));

			Add = new EnumCommand<AddGroupCommand>(AddGroupCommand.Add, this);
			Close = new EnumCommand<AddGroupCommand>(AddGroupCommand.Close, this);

			GroupName = string.Empty;
		}

		public Task HandleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}

		public bool CanRunCommand(AddGroupCommand command)
		{
			return command switch
			{
				AddGroupCommand.Add => userRightsProvider.AdministrationRights.HasFlag(Api.Rights.AdministrationRights.WriteUsers) && !string.IsNullOrWhiteSpace(GroupName),
				AddGroupCommand.Close => true,
				_ => false,
			};
		}

		public async Task RunCommand(AddGroupCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case AddGroupCommand.Add:
					var group = await groupsClient.Create(new UserGroupCreateRequest
					{
						Name = GroupName,
					},
					cancellationToken);

					GroupName = string.Empty;

					userGroupRootViewModel.DirectAdd(group);
					break;
				case AddGroupCommand.Close:
					pageContext.ActiveObject = null;
					break;
			}
		}
	}
}
