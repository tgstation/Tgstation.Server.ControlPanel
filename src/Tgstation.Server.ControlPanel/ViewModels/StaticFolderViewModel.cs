﻿using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class StaticFolderViewModel : ViewModelBase, ITreeNode, ICommandReceiver<StaticFolderViewModel.StaticFolderCommand>, IStaticNode
	{
		public enum StaticFolderCommand
		{
			Close,
			Refresh,
			Delete
		}

		public string Title => System.IO.Path.GetFileName(Path);

		public string Icon => Refreshing ? "resm:Tgstation.Server.ControlPanel.Assets.hourglass.png" : Denied ? "resm:Tgstation.Server.ControlPanel.Assets.denied.jpg" : "resm:Tgstation.Server.ControlPanel.Assets.folder.png";

		public bool IsExpanded { get; set; }

		public IReadOnlyList<ITreeNode> Children
		{
			get => children;
			set => this.RaiseAndSetIfChanged(ref children, value);
		}

		public bool Refreshing
		{
			get => refreshing;
			set
			{
				this.RaiseAndSetIfChanged(ref refreshing, value);
				this.RaisePropertyChanged(nameof(Icon));
				Refresh.Recheck();
				Delete.Recheck();
			}
		}

		public string Path { get; }

		public bool Errored => ErrorMessage != null;

		public string ErrorMessage
		{
			get => errorMessage;
			set
			{
				this.RaiseAndSetIfChanged(ref errorMessage, value);
				this.RaisePropertyChanged(nameof(Errored));
			}
		}

		public bool Denied
		{
			get => denied;
			set
			{
				this.RaiseAndSetIfChanged(ref denied, value);
				this.RaisePropertyChanged(nameof(Icon));
			}
		}

		public EnumCommand<StaticFolderCommand> Close { get; }
		public EnumCommand<StaticFolderCommand> Refresh { get; }
		public EnumCommand<StaticFolderCommand> Delete { get; }

		readonly PageContextViewModel pageContext;
		readonly IConfigurationClient configurationClient;
		readonly IInstanceUserRightsProvider rightsProvider;

		readonly IStaticNode parent;

		IReadOnlyList<ITreeNode> children;

		string errorMessage;

		bool refreshing;
		bool denied;
		bool firstLoad;

		public StaticFolderViewModel(PageContextViewModel pageContext, IConfigurationClient configurationClient, IInstanceUserRightsProvider rightsProvider, IStaticNode parent, string path)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.configurationClient = configurationClient ?? throw new ArgumentNullException(nameof(configurationClient));
			this.rightsProvider = rightsProvider ?? throw new ArgumentNullException(nameof(rightsProvider));
			this.parent = parent;
			Path = path ?? throw new ArgumentNullException(nameof(path));

			Close = new EnumCommand<StaticFolderCommand>(StaticFolderCommand.Close, this);
			Refresh = new EnumCommand<StaticFolderCommand>(StaticFolderCommand.Refresh, this);
			Delete = new EnumCommand<StaticFolderCommand>(StaticFolderCommand.Delete, this);

			rightsProvider.OnUpdated += (a, b) => Delete.Recheck();
		}

		public async Task HandleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			if (!firstLoad)
				await RefreshContents(cancellationToken).ConfigureAwait(true);
		}

		public void RemoveChild(IStaticNode child)
		{
			Children = Children?.Where(x => x != child).ToList();
		}

		public async Task RefreshContents(CancellationToken cancellationToken)
		{
			firstLoad = true;
			Refreshing = true;
			Denied = false;
			ErrorMessage = null;
			Children = null;
			try
			{
				Task<IReadOnlyList<ConfigurationFile>> childrenTask;
				using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
				{
					childrenTask = configurationClient.List(Path, cts.Token);
					var self = await configurationClient.Read(new ConfigurationFile
					{
						Path = Path
					}, cancellationToken).ConfigureAwait(true);

					if (!self.IsDirectory.Value || self.Path != Path)
					{
						cts.Cancel();
						await parent.RefreshContents(cancellationToken).ConfigureAwait(false);
					}

					if (self.AccessDenied.Value)
					{
						ErrorMessage = "Access denied while listing this directory's contents!";
						Denied = true;
						cts.Cancel();
					}
				}

				var dirs = await childrenTask.ConfigureAwait(false);

				var newChildren = new List<ITreeNode>();
				if (rightsProvider.ConfigurationRights.HasFlag(ConfigurationRights.Write))
					//newChildren.Add(new AddItemViewModel(pageContext, configurationClient, rightsProvider, this, Path));
					newChildren.Add(new BasicNode
					{
						Title = "TODO: Add Item",
						Icon = "resm:Tgstation.Server.ControlPanel.Assets.plus.jpg"
					});
				newChildren.AddRange(dirs.Select(x =>
				{
					if (x.IsDirectory.Value)
						return new StaticFolderViewModel(pageContext, configurationClient, rightsProvider, this, x.Path);
					//return new StaticFileViewModel(pageContext, configurationClient, rightsProvider, this, x.Path);
					return (ITreeNode)new BasicNode
					{
						Title = "TODO: " + System.IO.Path.GetFileName(x.Path),
						Icon = "resm:Tgstation.Server.ControlPanel.Assets.file.png"
					};
				}));
				Children = newChildren;
			}
			catch(ClientException e)
			{
				ErrorMessage = e.Message;
				Denied = e is InsufficientPermissionsException;
			}
			finally
			{
				Refreshing = false;
			}
		}

		public bool CanRunCommand(StaticFolderCommand command)
		{
			switch (command)
			{
				case StaticFolderCommand.Close:
					return true;
				case StaticFolderCommand.Refresh:
					return !Refreshing;
				case StaticFolderCommand.Delete:
					return rightsProvider.ConfigurationRights.HasFlag(ConfigurationRights.Delete) && !Children.Where(x => x is IStaticNode).Any();
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}

		public async Task RunCommand(StaticFolderCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case StaticFolderCommand.Close:
					pageContext.ActiveObject = null;
					break;
				case StaticFolderCommand.Refresh:
					await RefreshContents(cancellationToken).ConfigureAwait(true);
					break;
				case StaticFolderCommand.Delete:
					Refreshing = true;
					try
					{
						await configurationClient.DeleteEmptyDirectory(new ConfigurationFile
						{
							Path = Path
						}, cancellationToken).ConfigureAwait(false);
						parent.RemoveChild(this);
					}
					catch (ClientException e)
					{
						ErrorMessage = e.Message;
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
