using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class ChatChannelViewModel : ICommandReceiver<ChatChannelViewModel.ChatChannelCommand>
	{
		public enum ChatChannelCommand
		{
			Delete
		}

		public bool Modified { get; set; }

		public EnumCommand<ChatChannelCommand> Delete { get; }

		public ChatChannel Model { get; set; }

		public bool BadForm { get; set; }

		public bool IsAdminChannel
		{
			get => Model.IsAdminChannel.Value;
			set
			{
				Model.IsAdminChannel = value;
				Modified = true;
				onEdit();
			}
		}
		public bool IsWatchdogChannel
		{
			get => Model.IsWatchdogChannel.Value;
			set
			{
				Model.IsWatchdogChannel = value;
				Modified = true;
				onEdit();
			}
		}
		public bool IsUpdatesChannel
		{
			get => Model.IsUpdatesChannel.Value;
			set
			{
				Model.IsUpdatesChannel = value;
				Modified = true;
				onEdit();
			}
		}

		public string IrcChannelName
		{
			get => Model.IrcChannel?.Split(';').First();
			set
			{
				if (!IsIrc)
					return;

				var key = Model.IrcChannel?.Split(';').Skip(1).LastOrDefault();
				Model.IrcChannel = value;
				if (!String.IsNullOrWhiteSpace(key))
					Model.IrcChannel += ';' + key;
				BadForm = String.IsNullOrEmpty(Model.IrcChannel) || Model.IrcChannel[0] != '#';
				Modified = true;
				onEdit();
			}
		}

		public string IrcChannelKey
		{
			get => Model.IrcChannel?.Split(';').Skip(1).FirstOrDefault();
			set
			{
				if (!IsIrc)
					return;
				Model.IrcChannel = Model.IrcChannel?.Split(';').First() ?? String.Empty;
				if (!String.IsNullOrWhiteSpace(value))
					Model.IrcChannel += $";{value}";
				Modified = true;
				onEdit();
			}
		}

		public string Tag
		{
			get => Model.Tag;
			set
			{
				if(String.IsNullOrEmpty(value))
					Model.Tag = null;
				else
					Model.Tag = value;
				Modified = true;
				onEdit();
			}
		}
		public string DiscordChannelId
		{
			get => Model.DiscordChannelId.ToString();
			set
			{
				if (!IsDiscord)
					return;
				if (UInt64.TryParse(value, out var result))
				{
					Model.DiscordChannelId = result;
					BadForm = false;
					Modified = true;
					onEdit();
				}
				else
					BadForm = true;
			}
		}

		public bool IsIrc => provider == ChatProvider.Irc;
		public bool IsDiscord => provider == ChatProvider.Discord;

		readonly ChatProvider provider;
		readonly Action onDelete;
		readonly Action onEdit;

		public ChatChannelViewModel(ChatChannel model, ChatProvider provider, Action onDelete, Action onEdit)
		{
			Model = model ?? throw new ArgumentNullException(nameof(model));
			this.provider = provider;
			this.onDelete = onDelete ?? throw new ArgumentNullException(nameof(onDelete));
			this.onEdit = onEdit ?? throw new ArgumentNullException(nameof(onEdit));

			Delete = new EnumCommand<ChatChannelCommand>(ChatChannelCommand.Delete, this);
		}

		public bool CanRunCommand(ChatChannelCommand command) => true;

		public Task RunCommand(ChatChannelCommand command, CancellationToken cancellationToken)
		{
			onDelete();
			return Task.CompletedTask;
		}
	}
}