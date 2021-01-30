using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class StaticFileViewModel : ViewModelBase, IStaticNode, ICommandReceiver<StaticFileViewModel.StaticFileCommand>
	{
		public enum StaticFileCommand
		{
			Close,
			Refresh,
			Write,
			Upload,
			BrowseUpload,
			BrowseDownload,
			Delete,
			Download,
			EnableEditor
		}

		public bool Refreshing
		{
			get => refreshing;
			set
			{
				this.RaiseAndSetIfChanged(ref refreshing, value);
				this.RaisePropertyChanged(nameof(Icon));
				EditorEnabled = false;
				Refresh.Recheck();
				Write.Recheck();
				Delete.Recheck();
				Upload.Recheck();
				Download.Recheck();
				BrowseDownload.Recheck();
				BrowseUpload.Recheck();
				EnableEditor.Recheck();
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

		public bool EditorEnabled
		{
			get => editorEnabled;
			set
			{
				this.RaiseAndSetIfChanged(ref editorEnabled, value);
			}
		}

		public string TextBlob
		{
			get => textBlob;
			set
			{
				this.RaiseAndSetIfChanged(ref textBlob, value.Replace("\r", string.Empty));
				this.RaisePropertyChanged(nameof(IsText));
				textChanged = true;
				Write.Recheck();
			}
		}

		public string UploadPath
		{
			get => uploadPath;
			set
			{
				this.RaiseAndSetIfChanged(ref uploadPath, value);
				Upload.Recheck();
			}
		}

		public string DeleteText => confirmingDelete ? "Confirm?" : "Delete";

		public string DownloadPath
		{
			get => downloadPath;
			set
			{
				this.RaiseAndSetIfChanged(ref downloadPath, value);
				Download.Recheck();
			}
		}

		public bool IsText { get; set; } = true;

		public ConfigurationFile ConfigurationFile
		{
			get => configurationFile;
			set
			{
				this.RaiseAndSetIfChanged(ref configurationFile, value);
				textChanged = false;
				Write.Recheck();
				Delete.Recheck();
				Download.Recheck();
				Upload.Recheck();
				EnableEditor.Recheck();
			}
		}

		public EnumCommand<StaticFileCommand> Close { get; }
		public EnumCommand<StaticFileCommand> Refresh { get; }
		public EnumCommand<StaticFileCommand> Delete { get; }
		public EnumCommand<StaticFileCommand> Write { get; }
		public EnumCommand<StaticFileCommand> Upload { get; }
		public EnumCommand<StaticFileCommand> Download { get; }
		public EnumCommand<StaticFileCommand> BrowseUpload { get; }
		public EnumCommand<StaticFileCommand> BrowseDownload { get; }
		public EnumCommand<StaticFileCommand> EnableEditor { get; }

		public string Title => System.IO.Path.GetFileName(Path);

		public string Icon => Refreshing ? "resm:Tgstation.Server.ControlPanel.Assets.hourglass.png" : Denied ? "resm:Tgstation.Server.ControlPanel.Assets.denied.jpg" : "resm:Tgstation.Server.ControlPanel.Assets.file.png";

		public bool IsExpanded { get; set; }
		public IReadOnlyList<ITreeNode> Children => null;

		readonly PageContextViewModel pageContext;
		readonly IConfigurationClient configurationClient;
		readonly IInstanceUserRightsProvider rightsProvider;
		readonly IStaticNode parent;

		ConfigurationFile configurationFile;

		string errorMessage;
		string textBlob;
		string uploadPath;
		string downloadPath;
		bool refreshing;
		bool denied;
		bool firstLoad;
		bool textChanged;
		bool editorEnabled;

		bool confirmingDelete;

		public StaticFileViewModel(PageContextViewModel pageContext, IConfigurationClient configurationClient, IInstanceUserRightsProvider rightsProvider, IStaticNode parent, string path)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.configurationClient = configurationClient ?? throw new ArgumentNullException(nameof(configurationClient));
			this.rightsProvider = rightsProvider ?? throw new ArgumentNullException(nameof(rightsProvider));
			this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
			Path = path ?? throw new ArgumentNullException(nameof(path));

			Close = new EnumCommand<StaticFileCommand>(StaticFileCommand.Close, this);
			Refresh = new EnumCommand<StaticFileCommand>(StaticFileCommand.Refresh, this);
			Write = new EnumCommand<StaticFileCommand>(StaticFileCommand.Write, this);
			Delete = new EnumCommand<StaticFileCommand>(StaticFileCommand.Delete, this);
			Upload = new EnumCommand<StaticFileCommand>(StaticFileCommand.Upload, this);
			Download = new EnumCommand<StaticFileCommand>(StaticFileCommand.Download, this);
			BrowseDownload = new EnumCommand<StaticFileCommand>(StaticFileCommand.BrowseDownload, this);
			BrowseUpload = new EnumCommand<StaticFileCommand>(StaticFileCommand.BrowseUpload, this);
			EnableEditor = new EnumCommand<StaticFileCommand>(StaticFileCommand.EnableEditor, this);

			rightsProvider.OnUpdated += (a, b) =>
			{
				Refresh.Recheck();
				Write.Recheck();
				Delete.Recheck();
				Upload.Recheck();
				Download.Recheck();
				BrowseUpload.Recheck();
				BrowseDownload.Recheck();
			};
		}

		public void DirectLoad(ConfigurationFile file)
		{
			ConfigurationFile = file;
			Denied = file.AccessDenied ?? false;
			if (Denied)
				ErrorMessage = "Read access to this file is denied!";
		}

		public void RemoveChild(IStaticNode child) => throw new NotSupportedException();

		public async Task RefreshContents(CancellationToken cancellationToken)
		{
			Refreshing = true;
			ErrorMessage = null;
			Denied = false;
			firstLoad = true;
			try
			{
				var fileTuple = await configurationClient.Read(new ConfigurationFile
				{
					Path = Path
				}, cancellationToken).ConfigureAwait(true);
				await fileTuple.Item2.DisposeAsync();
				DirectLoad(fileTuple.Item1);
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

		public async Task HandleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			if (!firstLoad)
				await RefreshContents(cancellationToken).ConfigureAwait(true);
		}

		public bool CanRunCommand(StaticFileCommand command)
		{
			switch (command)
			{
				case StaticFileCommand.Close:
					return true;
				case StaticFileCommand.Refresh:
					return !Refreshing && rightsProvider.ConfigurationRights.HasFlag(ConfigurationRights.Read);
				case StaticFileCommand.Write:
					return !Refreshing && ConfigurationFile != null && IsText && textChanged && rightsProvider.ConfigurationRights.HasFlag(ConfigurationRights.Write);
				case StaticFileCommand.Upload:
					return !Refreshing && ConfigurationFile != null && File.Exists(UploadPath) && rightsProvider.ConfigurationRights.HasFlag(ConfigurationRights.Write);
				case StaticFileCommand.Delete:
					return !Refreshing && ConfigurationFile != null && rightsProvider.ConfigurationRights.HasFlag(ConfigurationRights.Write);
				case StaticFileCommand.Download:
					try
					{
						return !Refreshing && ConfigurationFile != null && Directory.Exists(System.IO.Path.GetDirectoryName(DownloadPath));
					}
					catch (IOException)
					{
						return false;
					}
				case StaticFileCommand.BrowseDownload:
				case StaticFileCommand.BrowseUpload:
				case StaticFileCommand.EnableEditor:
					return !Refreshing;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}

		public async Task RunCommand(StaticFileCommand command, CancellationToken cancellationToken)
		{
			async Task WriteGeneric(Stream data)
			{
				using (data)
				{
					var update = new ConfigurationFile
					{
						Path = Path,
						LastReadHash = ConfigurationFile.LastReadHash
					};

					Refreshing = true;
					try
					{
						var newConfig = await configurationClient.Write(update, data, cancellationToken).ConfigureAwait(true);
						ConfigurationFile = newConfig;
						Denied = newConfig.AccessDenied ?? false;
						if (!Denied && data == null)
							parent.RemoveChild(this);
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
			}

			switch (command)
			{
				case StaticFileCommand.Close:
					pageContext.ActiveObject = null;
					break;
				case StaticFileCommand.Refresh:
					await RefreshContents(cancellationToken).ConfigureAwait(true);
					break;
				case StaticFileCommand.Download:
					var fileTuple = await configurationClient.Read(ConfigurationFile, cancellationToken);
					using (fileTuple.Item2)
					using (var fileStream = new FileStream(DownloadPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, 8192, true))
						await fileTuple.Item2.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
					ControlPanel.OpenFolder(System.IO.Path.GetDirectoryName(DownloadPath));
					break;
				case StaticFileCommand.Upload:
					await WriteGeneric(new FileStream(UploadPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 8192, true)).ConfigureAwait(true);
					await RefreshContents(cancellationToken).ConfigureAwait(true);
					break;
				case StaticFileCommand.Write:
					await WriteGeneric(new MemoryStream(Encoding.UTF8.GetBytes(TextBlob))).ConfigureAwait(true);
					break;
				case StaticFileCommand.Delete:
					if (confirmingDelete)
						await WriteGeneric(null).ConfigureAwait(true);
					else
					{
						async void ResetDelete()
						{
							await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(true);
							confirmingDelete = false;
							this.RaisePropertyChanged(nameof(DeleteText));
						}
						confirmingDelete = true;
						this.RaisePropertyChanged(nameof(DeleteText));
						ResetDelete();
					}
					break;
				case StaticFileCommand.BrowseDownload:
					var sfd = new SaveFileDialog
					{
						Title = string.Format(CultureInfo.InvariantCulture, "Save {0}", Path),
						InitialFileName = System.IO.Path.GetFileName(Path)
					};
					var ext = System.IO.Path.GetExtension(Path);
					if (!string.IsNullOrEmpty(ext))
						sfd.DefaultExtension = ext;

					if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime1)
						DownloadPath = (await sfd.ShowAsync(lifetime1.MainWindow).ConfigureAwait(true)) ?? DownloadPath;
					break;
				case StaticFileCommand.BrowseUpload:
					var ofd = new OpenFileDialog
					{
						Title = string.Format(CultureInfo.InvariantCulture, "Upload to {0}", Path),
						InitialFileName = System.IO.Path.GetFileName(Path),
						AllowMultiple = false
					};
					if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime2)
						UploadPath = (await ofd.ShowAsync(lifetime2.MainWindow).ConfigureAwait(true))[0] ?? UploadPath;
					break;
				case StaticFileCommand.EnableEditor:
					lock (this)
					{
						if (EditorEnabled)
							return;
						EditorEnabled = true;
					}

					try
					{
						using var memoryStream = new MemoryStream();
						var fileTuple2 = await configurationClient.Read(ConfigurationFile, cancellationToken);
						using (fileTuple2.Item2)
							await fileTuple2.Item2.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
						try
						{
							TextBlob = Encoding.UTF8.GetString(memoryStream.ToArray());
						}
						catch
						{
							MainWindowViewModel.Singleton.AddToConsole($"Unable to turn remote file \"{ConfigurationFile.Path}\" into text!");
							IsText = false;
						}
					}
					catch
					{
						EditorEnabled = false;
						throw;
					}

					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
		public void DirectAdd(ConfigurationFile file) => throw new NotSupportedException();
	}
}
