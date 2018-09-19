using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class ChatRootViewModel : ViewModelBase, ITreeNode
	{
		public string Title => "Chat Bots";

		public string Icon
		{
			get => icon;
			set => this.RaiseAndSetIfChanged(ref icon, value);
		}

		public bool IsExpanded { get; set; }

		public IReadOnlyList<ITreeNode> Children
		{
			get => children;
			set => this.RaiseAndSetIfChanged(ref children, value);
		}

		readonly PageContextViewModel pageContext;
		readonly IChatBotsClient chatBotsClient;
		readonly IInstanceUserRightsProvider rightsProvider;

		IReadOnlyList<ITreeNode> children;
		IReadOnlyList<ChatBot> chatBots;

		bool loading;
		string icon;


		public ChatRootViewModel(PageContextViewModel pageContext, IChatBotsClient chatBotsClient, IInstanceUserRightsProvider rightsProvider)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.chatBotsClient = chatBotsClient ?? throw new ArgumentNullException(nameof(chatBotsClient));
			this.rightsProvider = rightsProvider ?? throw new ArgumentNullException(nameof(rightsProvider));

			async void InitialLoad() => await Refresh(default).ConfigureAwait(false);
			InitialLoad();
		}

		public async Task Refresh(CancellationToken cancellationToken)
		{
			lock (this)
			{
				if (loading)
					return;
				loading = true;
			}
			var nullPage = true;
			try
			{
				if (rightsProvider.ChatBotRights == ChatBotRights.None)
				{
					Children = null;
					Icon = "resm:Tgstation.Server.ControlPanel.Assets.denied.jpg";
					return;
				}

				Children = null;
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.hourglass.png";
				this.RaisePropertyChanged(nameof(Icon));

				var newChildren = new List<ITreeNode>();

				var hasReadRight = rightsProvider.ChatBotRights.HasFlag(ChatBotRights.Read);

				if (hasReadRight)
					chatBots = await chatBotsClient.List(cancellationToken).ConfigureAwait(true);

				if (rightsProvider.InstanceUserRights.HasFlag(InstanceUserRights.WriteUsers))
					newChildren.Add(new AddChatBotViewModel(pageContext, chatBotsClient, rightsProvider, this));

				if (hasReadRight)
					newChildren.AddRange(chatBots
						.Select(x => new ChatBotViewModel(pageContext, chatBotsClient, x, rightsProvider)));

				Children = newChildren;
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.chat.png";
				nullPage = false;
			}
			catch
			{
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.error.png";
			}
			finally
			{
				loading = false;
				if (pageContext.IsAddChatBot || pageContext.IsChatBot)
					if (nullPage)
						pageContext.ActiveObject = false;
					else if (pageContext.IsAddInstanceUser)
						if (Children[0] is AddChatBotViewModel)
							pageContext.ActiveObject = Children[0];
						else
							pageContext.ActiveObject = null;
					else //instance user
					{
						var start = Children[0] is ChatBotViewModel ? 1 : 0;
						bool found = false;
						for (var I = start; I < Children.Count; ++I)
							if (((ChatBotViewModel)Children[I]).ChatBot.Id == ((ChatBotViewModel)pageContext.ActiveObject).ChatBot.Id)
							{
								pageContext.ActiveObject = Children[I];
								found = true;
							}
						if (!found)
							pageContext.ActiveObject = null;
					}
			}
		}

		public void DirectAdd(ChatBot bot)
		{
			var newModel = new ChatBotViewModel(pageContext, chatBotsClient, x, rightsProvider);
			var newChildren = new List<ITreeNode>(Children)
			{
				newModel
			};
			chatBots = new List<ChatBot>(chatBots)
			{
				bot
			};
			if (newChildren[0] is AddInstanceUserViewModel)
				newChildren[0] = new AddChatBotViewModel(pageContext, chatBotsClient, rightsProvider, this);
			Children = newChildren;
			pageContext.ActiveObject = rightsProvider.InstanceUserRights.HasFlag(InstanceUserRights.ReadUsers) ? newModel : null;
		}

		public Task HandleClick(CancellationToken cancellationToken) => Refresh(cancellationToken);
	}
}
