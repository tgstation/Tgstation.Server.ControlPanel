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
	sealed class ChatBotViewModel : ViewModelBase, ITreeNode, ICommandReceiver<ChatBotViewModel.ChatBotCommand>
	{
		public enum ChatBotCommand
		{
			Close,
			Refresh,
			Update,
			Delete
		}

		public string Title => ChatBot.Name;

		public string Icon => Refreshing ? "resm:Tgstation.Server.ControlPanel.Assets.hourglass.png" : "resm:Tgstation.Server.ControlPanel.Assets.chat.png";

		public bool IsExpanded { get; set; }

		public IReadOnlyList<ITreeNode> Children => null;

		public EnumCommand<ChatBotCommand> Close { get; }
		public EnumCommand<ChatBotCommand> Refresh { get; }
		public EnumCommand<ChatBotCommand> Update { get; }
		public EnumCommand<ChatBotCommand> Delete { get; }

		public bool CanConnectionString => rightsProvider.ChatBotRights.HasFlag(ChatBotRights.WriteConnectionString);
		public bool CanChannels => rightsProvider.ChatBotRights.HasFlag(ChatBotRights.WriteChannels);
		public bool CanName => rightsProvider.ChatBotRights.HasFlag(ChatBotRights.WriteName);
		public bool CanProvider => rightsProvider.ChatBotRights.HasFlag(ChatBotRights.WriteProvider);
		public bool CanEnable => rightsProvider.ChatBotRights.HasFlag(ChatBotRights.WriteEnabled);

		public bool Refreshing
		{
			get => refreshing;
			set
			{
				this.RaiseAndSetIfChanged(ref refreshing, value);
				this.RaisePropertyChanged(nameof(Icon));
				Update.Recheck();
				Delete.Recheck();
				Refresh.Recheck();
			}
		}

		public ChatBot ChatBot
		{
			get => chatBot;
			set
			{
				this.RaiseAndSetIfChanged(ref chatBot, value);
				this.RaisePropertyChanged(nameof(Title));
			}
		}

		public string NewConnectionString
		{
			get => newConnectionString;
			set => this.RaiseAndSetIfChanged(ref newConnectionString, value);
		}
		public string NewName
		{
			get => newName;
			set => this.RaiseAndSetIfChanged(ref newName, value);
		}

		public string DeleteText => confirmingDelete ? "Confirm?" : "Delete";

		readonly PageContextViewModel pageContext;
		readonly IChatBotsClient chatBotsClient;
		readonly IInstanceUserRightsProvider rightsProvider;
		readonly ChatRootViewModel chatRootViewModel;

		string newConnectionString;
		string newName;

		ChatBot chatBot;
		bool refreshing;
		bool confirmingDelete;

		public ChatBotViewModel(PageContextViewModel pageContext, IChatBotsClient chatBotsClient, ChatBot chatBot, IInstanceUserRightsProvider rightsProvider, ChatRootViewModel chatRootViewModel)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.chatBotsClient = chatBotsClient ?? throw new ArgumentNullException(nameof(chatBotsClient));
			ChatBot = chatBot ?? throw new ArgumentNullException(nameof(chatBot));
			this.rightsProvider = rightsProvider ?? throw new ArgumentNullException(nameof(rightsProvider));
			this.chatRootViewModel = chatRootViewModel ?? throw new ArgumentNullException(nameof(chatRootViewModel));

			Close = new EnumCommand<ChatBotCommand>(ChatBotCommand.Close, this);
			Update = new EnumCommand<ChatBotCommand>(ChatBotCommand.Update, this);
			Refresh = new EnumCommand<ChatBotCommand>(ChatBotCommand.Refresh, this);
			Delete = new EnumCommand<ChatBotCommand>(ChatBotCommand.Delete, this);
		}

		public Task HandleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}

		public bool CanRunCommand(ChatBotCommand command)
		{
			switch (command)
			{
				case ChatBotCommand.Close:
					return true;
				case ChatBotCommand.Update:
					return !Refreshing && (CanChannels || CanName || CanProvider || CanConnectionString || CanChannels);
				case ChatBotCommand.Delete:
					return !Refreshing && rightsProvider.ChatBotRights.HasFlag(ChatBotRights.Delete);
				case ChatBotCommand.Refresh:
					return !Refreshing;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}

		public async Task RunCommand(ChatBotCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case ChatBotCommand.Close:
					pageContext.ActiveObject = null;
					break;
				case ChatBotCommand.Update:
					var update = new ChatBot
					{
						Id = ChatBot.Id,
						Provider = CanProvider ? ChatBot.Provider : null,
						ConnectionString = CanConnectionString && !String.IsNullOrEmpty(NewConnectionString) ? NewConnectionString : null,
						Name = CanName && !String.IsNullOrEmpty(newName) ? NewName : null
					};

					//TODO: Channels

					Refreshing = true;
					try
					{
						ChatBot = await chatBotsClient.Update(update, cancellationToken).ConfigureAwait(true);
					}
					finally
					{
						Refreshing = false;
					}
					break;
				case ChatBotCommand.Delete:
					if (!confirmingDelete)
					{
						async void ResetDelete()
						{
							await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(true);
							confirmingDelete = true;
							this.RaisePropertyChanged(nameof(DeleteText));
						}
						confirmingDelete = true;
						this.RaisePropertyChanged(nameof(DeleteText));
						ResetDelete();
					}
					else
					{
						Refreshing = true;
						try
						{
							await chatBotsClient.Delete(ChatBot, cancellationToken).ConfigureAwait(true);
							pageContext.ActiveObject = null;
							await chatRootViewModel.Refresh(cancellationToken).ConfigureAwait(true);
						}
						finally
						{
							Refreshing = false;
						}
					}
					break;
				case ChatBotCommand.Refresh:
					Refreshing = true;
					try
					{
						//await chatBotsClient.GetId(ChatBot, cancellationToken).ConfigureAwait(true);
					}
					finally
					{
						Refreshing = false;
					}
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}