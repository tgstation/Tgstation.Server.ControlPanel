﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Models.Request;
using Tgstation.Server.Api.Models.Response;
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
			Delete,
			AddChannel
		}

		public string Title => ChatBot.Name;

		public string Icon => Refreshing ? "resm:Tgstation.Server.ControlPanel.Assets.hourglass.png" : (ChatBot?.Provider == ChatProvider.Discord ? "resm:Tgstation.Server.ControlPanel.Assets.discord.png" : "resm:Tgstation.Server.ControlPanel.Assets.chat.png");

		public bool IsExpanded { get; set; }

		public IReadOnlyList<ITreeNode> Children => null;

		public EnumCommand<ChatBotCommand> Close { get; }
		public EnumCommand<ChatBotCommand> Refresh { get; }
		public EnumCommand<ChatBotCommand> Update { get; }
		public EnumCommand<ChatBotCommand> Delete { get; }
		public EnumCommand<ChatBotCommand> AddChannel { get; }

		public bool CanConnectionString => rightsProvider.ChatBotRights.HasFlag(ChatBotRights.WriteConnectionString);
		public bool CanChannels => rightsProvider.ChatBotRights.HasFlag(ChatBotRights.WriteChannels);
		public bool CanName => rightsProvider.ChatBotRights.HasFlag(ChatBotRights.WriteName);
		public bool CanProvider => rightsProvider.ChatBotRights.HasFlag(ChatBotRights.WriteProvider);
		public bool CanEnable => rightsProvider.ChatBotRights.HasFlag(ChatBotRights.WriteEnabled);
		public bool CanChannelLimit => rightsProvider.ChatBotRights.HasFlag(ChatBotRights.WriteChannelLimit);

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
				AddChannel.Recheck();
			}
		}

		public IReadOnlyList<ChatChannelViewModel> Channels
		{
			get => channels;
			set
			{
				this.RaiseAndSetIfChanged(ref channels, value);
				Update?.Recheck();
			}
		}

		public string Enabled => (ChatBot?.Enabled).HasValue ? ChatBot.Enabled.ToString() : "Unknown";
		public string Provider => (ChatBot?.Provider).HasValue ? ChatBot.Provider.ToString() : "Unknown";

		public ChatBotResponse ChatBot
		{
			get => chatBot;
			set
			{
				using (DelayChangeNotifications())
				{
					this.RaiseAndSetIfChanged(ref chatBot, value);
					this.RaisePropertyChanged(nameof(Title));
					this.RaisePropertyChanged(nameof(HasConnectionString));
					this.RaisePropertyChanged(nameof(Icon));
					this.RaisePropertyChanged(nameof(Enabled));
					this.RaisePropertyChanged(nameof(Provider));
					NewEnabled = chatBot.Enabled ?? false;
					Channels = chatBot.Channels.Select(x => new ChatChannelViewModel(x, chatBot.Provider.Value, () => OnChannelDelete(x), () => Update?.Recheck())).ToList();
				}
			}
		}

		public bool NewEnabled
		{
			get => newEnabled;
			set => this.RaiseAndSetIfChanged(ref newEnabled, value);
		}

		public bool HasConnectionString => ChatBot?.ConnectionString != null;

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

		public ushort NewChannelLimit
		{
			get => newChannelLimit;
			set => this.RaiseAndSetIfChanged(ref newChannelLimit, value);
		}

		public string DeleteText => confirmingDelete ? "Confirm?" : "Delete";

		readonly PageContextViewModel pageContext;
		readonly IChatBotsClient chatBotsClient;
		readonly IInstanceUserRightsProvider rightsProvider;
		readonly ChatRootViewModel chatRootViewModel;

		IReadOnlyList<ChatChannelViewModel> channels;

		string newConnectionString;
		string newName;

		ChatBotResponse chatBot;
		ushort newChannelLimit;
		bool refreshing;
		bool confirmingDelete;
		bool newEnabled;

		void OnChannelDelete(ChatChannel model) => Channels = new List<ChatChannelViewModel>(Channels.Where(y => y.Model != model));

		public ChatBotViewModel(PageContextViewModel pageContext, IChatBotsClient chatBotsClient, ChatBotResponse chatBot, IInstanceUserRightsProvider rightsProvider, ChatRootViewModel chatRootViewModel)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.chatBotsClient = chatBotsClient ?? throw new ArgumentNullException(nameof(chatBotsClient));
			ChatBot = chatBot ?? throw new ArgumentNullException(nameof(chatBot));
			NewChannelLimit = ChatBot.ChannelLimit.Value;
			this.rightsProvider = rightsProvider ?? throw new ArgumentNullException(nameof(rightsProvider));
			this.chatRootViewModel = chatRootViewModel ?? throw new ArgumentNullException(nameof(chatRootViewModel));

			Close = new EnumCommand<ChatBotCommand>(ChatBotCommand.Close, this);
			Update = new EnumCommand<ChatBotCommand>(ChatBotCommand.Update, this);
			Refresh = new EnumCommand<ChatBotCommand>(ChatBotCommand.Refresh, this);
			Delete = new EnumCommand<ChatBotCommand>(ChatBotCommand.Delete, this);
			AddChannel = new EnumCommand<ChatBotCommand>(ChatBotCommand.AddChannel, this);
		}

		public Task HandleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}

		public bool CanRunCommand(ChatBotCommand command)
		{
			return command switch
			{
				ChatBotCommand.Close => true,
				ChatBotCommand.Update => !Refreshing && (CanChannels || CanName || CanProvider || CanConnectionString || CanChannels) && !Channels.Any(x => x.BadForm),
				ChatBotCommand.Delete => !Refreshing && rightsProvider.ChatBotRights.HasFlag(ChatBotRights.Delete),
				ChatBotCommand.Refresh => !Refreshing,
				ChatBotCommand.AddChannel => !Refreshing && CanChannels,
				_ => throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!"),
			};
		}

		public async Task RunCommand(ChatBotCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case ChatBotCommand.Close:
					pageContext.ActiveObject = null;
					break;
				case ChatBotCommand.Update:
					var update = new ChatBotUpdateRequest
					{
						Id = ChatBot.Id,
						ConnectionString = CanConnectionString && !string.IsNullOrEmpty(NewConnectionString) && NewConnectionString != ChatBot.ConnectionString ? NewConnectionString : null,
						ChannelLimit = CanChannelLimit ? (ushort?)NewChannelLimit : null,
						Enabled = CanEnable && ChatBot.Enabled != NewEnabled ? (bool?)NewEnabled : null
					};

					if (channels.Count != ChatBot.Channels.Count || channels.Any(x => x.Modified))
						update.Channels = channels.Select(x => x.Model).ToList();

					Refreshing = true;
					try
					{
						ChatBot = await chatBotsClient.Update(update, cancellationToken).ConfigureAwait(true);
						NewChannelLimit = ChatBot.ChannelLimit.Value;
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
							await Task.Delay(TimeSpan.FromSeconds(3), default).ConfigureAwait(true);
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
						ChatBot = await chatBotsClient.GetId(ChatBot, cancellationToken).ConfigureAwait(true);
						NewChannelLimit = ChatBot.ChannelLimit.Value;
					}
					finally
					{
						Refreshing = false;
					}
					break;
				case ChatBotCommand.AddChannel:
					var model = new ChatChannel()
					{
						IsAdminChannel = false,
						IsUpdatesChannel = false,
						IsWatchdogChannel = false,
						IsSystemChannel = false,
					};
					Channels = new List<ChatChannelViewModel>(Channels)
					{
						new ChatChannelViewModel(model, ChatBot.Provider.Value, () => OnChannelDelete(model), Update.Recheck)
						{
							Modified = true,
							BadForm = true
						}
					};
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}
