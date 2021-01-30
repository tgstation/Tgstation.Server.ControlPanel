using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class StaticFolderViewModel : ViewModelBase, ICommandReceiver<StaticFolderViewModel.StaticFolderCommand>, IStaticNode
	{
		public enum StaticFolderCommand
		{
			Close,
			Refresh,
			Delete
		}

		public string Title
		{
			get
			{
				var fileName = System.IO.Path.GetFileName(Path);
				if (string.IsNullOrEmpty(fileName))
					return "Configuration";
				return fileName;
			}
		}

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
			if (pageContext.ActiveObject == child)
				pageContext.ActiveObject = null;
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
				var dirs = await configurationClient.List(null, Path, cancellationToken).ConfigureAwait(false);

				var newChildren = new List<ITreeNode>();
				if (rightsProvider.ConfigurationRights.HasFlag(ConfigurationRights.Write))
					newChildren.Add(new AddStaticItemViewModel(pageContext, configurationClient, rightsProvider, this));
				newChildren.AddRange(dirs.Select(x =>
				{
					if (x.IsDirectory.Value)
						return new StaticFolderViewModel(pageContext, configurationClient, rightsProvider, this, x.Path);
					return (IStaticNode)new StaticFileViewModel(pageContext, configurationClient, rightsProvider, this, x.Path);
				}));
				Children = newChildren;
				IsExpanded = true;
				this.RaisePropertyChanged(nameof(IsExpanded));
			}
			catch (ClientException e)
			{
				ErrorMessage = e.Message;
				Denied = e is InsufficientPermissionsException;
			}
			finally
			{
				Refreshing = false;
			}
		}

		public void DirectAdd(ConfigurationFile file)
		{
			IStaticNode vm;
			if (!file.IsDirectory.Value)
			{
				var sfvm = new StaticFileViewModel(pageContext, configurationClient, rightsProvider, this, file.Path);
				sfvm.DirectLoad(file);
				vm = sfvm;
			}
			else
				vm = new StaticFolderViewModel(pageContext, configurationClient, rightsProvider, this, file.Path);
			pageContext.ActiveObject = vm;
			Children = new List<ITreeNode>(Children)
			{
				vm
			};
		}
		public bool CanRunCommand(StaticFolderCommand command)
		{
			return command switch
			{
				StaticFolderCommand.Close => true,
				StaticFolderCommand.Refresh => !Refreshing,
				StaticFolderCommand.Delete => !Refreshing && rightsProvider.ConfigurationRights.HasFlag(ConfigurationRights.Delete) && (!Children?.Where(x => x is IStaticNode).Any() ?? true),
				_ => throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!"),
			};
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
