using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class InstanceRootViewModel : ViewModelBase, ITreeNode
	{
		public string Title => "Instances";

		public bool IsExpanded
		{
			get => isExpanded;
			set => this.RaiseAndSetIfChanged(ref isExpanded, value);
		}

		public string Icon
		{
			get => icon;
			private set => this.RaiseAndSetIfChanged(ref icon, value);
		}

		public IReadOnlyList<ITreeNode> Children
		{
			get => children;
			set => this.RaiseAndSetIfChanged(ref children, value);
		}

		readonly PageContextViewModel pageContext;
		readonly IInstanceManagerClient instanceManagerClient;
		readonly IUserRightsProvider userRightsProvider;
		readonly IUserProvider userProvider;
		readonly IServerJobSink serverJobSink;

		IReadOnlyList<ITreeNode> children;
		string icon;
		bool loading;
		bool isExpanded;

		public InstanceRootViewModel(PageContextViewModel pageContext, IInstanceManagerClient instanceManagerClient, IUserRightsProvider userRightsProvider, IUserProvider userProvider, IServerJobSink serverJobSink)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.instanceManagerClient = instanceManagerClient ?? throw new ArgumentNullException(nameof(instanceManagerClient));
			this.userRightsProvider = userRightsProvider ?? throw new ArgumentNullException(nameof(userRightsProvider));
			this.userProvider = userProvider ?? throw new ArgumentNullException(nameof(userProvider));
			this.serverJobSink = serverJobSink ?? throw new ArgumentNullException(nameof(serverJobSink));

			async void InitalLoad() => await Refresh(default).ConfigureAwait(false);
			InitalLoad();
			userRightsProvider.OnUpdated += (a, b) => InitalLoad();
		}

		public async Task Refresh(CancellationToken cancellationToken)
		{
			lock (this)
			{
				if (loading)
					return;
				loading = true;
			}

			try
			{
				var hasReadRight = userRightsProvider.InstanceManagerRights.HasFlag(InstanceManagerRights.List) || userRightsProvider.InstanceManagerRights.HasFlag(InstanceManagerRights.Read);
				var hasCreateRight = userRightsProvider.InstanceManagerRights.HasFlag(InstanceManagerRights.Create);

				if (!hasReadRight && !hasCreateRight)
				{
					Icon = "resm:Tgstation.Server.ControlPanel.Assets.denied.jpg";
					Children = null;
					return;
				}

				AddInstanceViewModel auvm = null;
				var newChildren = new List<ITreeNode>();
				if (hasCreateRight)
				{
					auvm = new AddInstanceViewModel(pageContext, instanceManagerClient, this);
					newChildren.Add(auvm);
				}

				BasicNode basic = null;
				if (hasReadRight)
				{
					basic = new BasicNode()
					{
						Title = "Loading...",
						Icon = "resm:Tgstation.Server.ControlPanel.Assets.hourglass.png"
					};
					newChildren.Add(basic);
				}

				Children = newChildren;

				if (hasReadRight)
					try
					{
						var instances = await instanceManagerClient.List(cancellationToken).ConfigureAwait(false);

						newChildren = new List<ITreeNode>();
						if (hasCreateRight)
							newChildren.Add(auvm);
						newChildren.AddRange(instances.Select(x => new InstanceViewModel(instanceManagerClient, pageContext, x, userRightsProvider, this, userProvider, serverJobSink)));
						if (instances.Count == 1)
							newChildren[1].IsExpanded = true;
						Children = newChildren;
						if (pageContext.IsInstance)
						{
							int? index = null;
							var pcID = ((InstanceViewModel)pageContext.ActiveObject).Instance.Id;

							for (var I = 0; I < instances.Count; ++I)
								if (instances[I].Id == pcID)
								{
									index = hasCreateRight ? I + 1 : I;
									break;
								}

							if (index.HasValue)
								pageContext.ActiveObject = Children[index.Value];
							else
								pageContext.ActiveObject = null;
						}

						Icon = "resm:Tgstation.Server.ControlPanel.Assets.folder.png";
					}
					catch
					{
						basic.Title = "Error!";
						basic.Icon = "resm:Tgstation.Server.ControlPanel.Assets.error.png";
					}
				else if (pageContext.IsInstance)
					pageContext.ActiveObject = null;
			}
			finally
			{
				loading = false;
				IsExpanded = true;
			}
		}

		public void DirectAddInstance(Instance instance)
		{
			var newChildren = new List<ITreeNode>(Children);
			var newThing = new InstanceViewModel(instanceManagerClient, pageContext, instance, userRightsProvider, this, userProvider, serverJobSink);
			newChildren.Add(newThing);
			Children = newChildren;
			pageContext.ActiveObject = newThing;
		}

		public Task HandleDoubleClick(CancellationToken cancellationToken) => Refresh(cancellationToken);
	}
}
