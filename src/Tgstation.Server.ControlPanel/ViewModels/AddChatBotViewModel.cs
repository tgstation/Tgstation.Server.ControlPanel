using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class AddChatBotViewModel : ViewModelBase, ITreeNode, ICommandReceiver<AddChatBotViewModel.AddChatBotCommand>
	{
		public enum AddChatBotCommand
		{
			Close,
			Add
		}

		public string Title => "Add Bot";

		public string Icon => "resm:Tgstation.Server.ControlPanel.Assets.plus.jpg";

		public bool IsExpanded { get; set; }

		public IReadOnlyList<ITreeNode> Children => null;

		public EnumCommand<AddChatBotCommand> Close { get; }
		public EnumCommand<AddChatBotCommand> Add { get; }

		public ChatProvider Provider { get; set; }

		public string BotName
		{
			get => botName;
			set
			{
				this.RaiseAndSetIfChanged(ref botName, value);
				Add.Recheck();
			}
		}

		readonly PageContextViewModel pageContext;
		readonly IChatBotsClient chatBotsClient;
		readonly IInstanceUserRightsProvider rightsProvider;
		readonly ChatRootViewModel chatRootViewModel;

		bool loading;
		string botName;

		public AddChatBotViewModel(PageContextViewModel pageContext, IChatBotsClient chatBotsClient, IInstanceUserRightsProvider rightsProvider, ChatRootViewModel chatRootViewModel)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.chatBotsClient = chatBotsClient ?? throw new ArgumentNullException(nameof(chatBotsClient));
			this.rightsProvider = rightsProvider ?? throw new ArgumentNullException(nameof(rightsProvider));
			this.chatRootViewModel = chatRootViewModel ?? throw new ArgumentNullException(nameof(chatRootViewModel));
			rightsProvider.OnUpdated += (a, b) => Add.Recheck();
			Close = new EnumCommand<AddChatBotCommand>(AddChatBotCommand.Close, this);
			Add = new EnumCommand<AddChatBotCommand>(AddChatBotCommand.Add, this);
		}

		public Task HandleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}

		public bool CanRunCommand(AddChatBotCommand command)
		{
			switch (command)
			{
				case AddChatBotCommand.Close:
					return true;
				case AddChatBotCommand.Add:
					return !loading && rightsProvider.ChatBotRights.HasFlag(ChatBotRights.Create);
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}

		public async Task RunCommand(AddChatBotCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case AddChatBotCommand.Close:
					pageContext.ActiveObject = null;
					break;
				case AddChatBotCommand.Add:
					loading = true;
					Add.Recheck();
					try
					{
						var newBot = await chatBotsClient.Create(new ChatBot
						{
							Provider = Provider,
							Name = BotName
						}, cancellationToken).ConfigureAwait(true);
						chatRootViewModel.DirectAdd(newBot);
					}
					finally
					{
						loading = false;
						Add.Recheck();
					}
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}