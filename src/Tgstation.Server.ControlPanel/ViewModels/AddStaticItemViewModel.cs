﻿using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class AddStaticItemViewModel : ViewModelBase, ITreeNode, ICommandReceiver<AddStaticItemViewModel.AddStaticItemCommand>
	{
		public enum AddStaticItemCommand
		{
			Close,
			Browse,
			Add
		}

		public enum ItemType
		{
			File,
			Folder
		}

		public string Title => "Add Item";

		public string Icon => "resm:Tgstation.Server.ControlPanel.Assets.plus.jpg";

		public bool IsExpanded { get; set; }

		public string ItemName
		{
			get => itemName;
			set
			{
				this.RaiseAndSetIfChanged(ref itemName, value);
				Add.Recheck();
			}
		}
		public string ItemText
		{
			get => itemText;
			set
			{
				this.RaiseAndSetIfChanged(ref itemText, value);
				Add.Recheck();
			}
		}

		public string ItemPath
		{
			get => itemPath;
			set
			{
				this.RaiseAndSetIfChanged(ref itemPath, value);
				Add.Recheck();
			}
		}
		public string ErrorMessage
		{
			get => errorMessage;
			set
			{
				this.RaiseAndSetIfChanged(ref errorMessage, value);
				this.RaisePropertyChanged(nameof(Errored));
			}
		}

		public bool Errored => ErrorMessage != null;

		public ItemType Type
		{
			get => type;
			set => this.RaiseAndSetIfChanged(ref type, value);
		}

		public bool Refreshing
		{
			get => refreshing;
			set
			{
				this.RaiseAndSetIfChanged(ref refreshing, value);
				Browse.Recheck();
				Add.Recheck();
			}
		}

		public IReadOnlyList<ITreeNode> Children => null;

		public EnumCommand<AddStaticItemCommand> Close { get; }
		public EnumCommand<AddStaticItemCommand> Browse { get; }
		public EnumCommand<AddStaticItemCommand> Add { get; }

		readonly PageContextViewModel pageContext;
		readonly IConfigurationClient configurationClient;
		readonly IInstanceUserRightsProvider rightsProvider;
		readonly IStaticNode parent;

		ItemType type;

		string errorMessage;
		string itemText;
		string itemPath;
		string itemName;

		bool refreshing;

		public AddStaticItemViewModel(PageContextViewModel pageContext, IConfigurationClient configurationClient, IInstanceUserRightsProvider rightsProvider, IStaticNode parent)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.configurationClient = configurationClient ?? throw new ArgumentNullException(nameof(configurationClient));
			this.rightsProvider = rightsProvider ?? throw new ArgumentNullException(nameof(rightsProvider));
			this.parent = parent ?? throw new ArgumentNullException(nameof(parent));

			Close = new EnumCommand<AddStaticItemCommand>(AddStaticItemCommand.Close, this);
			Browse = new EnumCommand<AddStaticItemCommand>(AddStaticItemCommand.Browse, this);
			Add = new EnumCommand<AddStaticItemCommand>(AddStaticItemCommand.Add, this);

			rightsProvider.OnUpdated += (a, b) => Add.Recheck();
		}

		public Task HandleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}

		public bool CanRunCommand(AddStaticItemCommand command)
		{
			switch (command)
			{
				case AddStaticItemCommand.Close:
					return true;
				case AddStaticItemCommand.Browse:
					return !Refreshing;
				case AddStaticItemCommand.Add:
					try
					{
						return !Refreshing
							&& rightsProvider.ConfigurationRights.HasFlag(ConfigurationRights.Write)
							&& !String.IsNullOrEmpty(ItemName)
							&& !ItemName.Contains("/")
							&& !ItemName.Contains("\\")
							&& (Type == ItemType.Folder
							|| ((String.IsNullOrWhiteSpace(ItemText) ^ String.IsNullOrWhiteSpace(ItemPath))
							&& (String.IsNullOrEmpty(ItemPath) || File.Exists(ItemPath))));
					}
					catch (IOException)
					{
						return false;
					}
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}

		public async Task RunCommand(AddStaticItemCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case AddStaticItemCommand.Close:
					pageContext.ActiveObject = null;
					break;
				case AddStaticItemCommand.Browse:
					var ofd = new OpenFileDialog
					{
						Title = String.Format(CultureInfo.InvariantCulture, "Upload to {0}", ItemName),
						InitialFileName = ItemName,
						AllowMultiple = false
					};
					ItemPath = (await ofd.ShowAsync(Application.Current.MainWindow).ConfigureAwait(true))[0] ?? ItemPath;
					break;
				case AddStaticItemCommand.Add:
					Refreshing = true;
					ErrorMessage = null;
					try
					{
						var newPath = parent.Path + '/' + ItemName;
						ConfigurationFile file;
						if (Type == ItemType.Folder)
							file = await configurationClient.CreateDirectory(newPath, cancellationToken).ConfigureAwait(true);
						else
						{
							byte[] data;
							if (ItemPath != null)
								data = File.ReadAllBytes(ItemPath);
							else
								data = Encoding.UTF8.GetBytes(ItemText);
							file = new ConfigurationFile
							{
								Content = data,
								Path = newPath
							};

							file = await configurationClient.Write(file, cancellationToken).ConfigureAwait(true);
							file.Content = data;
						}
						parent.DirectAdd(file);

						ItemPath = String.Empty;
						ItemText = String.Empty;
						ItemName = String.Empty;
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