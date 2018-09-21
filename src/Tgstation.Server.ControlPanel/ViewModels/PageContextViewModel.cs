using ReactiveUI;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	public sealed class PageContextViewModel : ViewModelBase
	{
		public object ActiveObject
		{
			get => activeObject;
			set
			{
				using (DelayChangeNotifications())
				{
					this.RaiseAndSetIfChanged(ref activeObject, value);
					this.RaisePropertyChanged(nameof(IsConnectionManager));
					this.RaisePropertyChanged(nameof(IsUser));
					this.RaisePropertyChanged(nameof(IsAddUser));
					this.RaisePropertyChanged(nameof(IsAdministration));
					this.RaisePropertyChanged(nameof(IsAddInstance));
					this.RaisePropertyChanged(nameof(IsInstance));
					this.RaisePropertyChanged(nameof(IsInstanceUser));
					this.RaisePropertyChanged(nameof(IsAddInstanceUser));
					this.RaisePropertyChanged(nameof(IsRepository));
					this.RaisePropertyChanged(nameof(IsByond));
					this.RaisePropertyChanged(nameof(IsCompiler));
					this.RaisePropertyChanged(nameof(IsDreamDaemon));
					this.RaisePropertyChanged(nameof(IsAddChatBot));
					this.RaisePropertyChanged(nameof(IsChatBot));
					this.RaisePropertyChanged(nameof(IsStaticFolder));
					this.RaisePropertyChanged(nameof(IsStaticAdd));
					this.RaisePropertyChanged(nameof(IsStaticFile));
				}
			}
		}

		object activeObject;

		public bool IsConnectionManager => activeObject is ConnectionManagerViewModel;
		public bool IsUser => activeObject is UserViewModel;
		public bool IsAddUser => activeObject is AddUserViewModel;
		public bool IsAdministration => activeObject is AdministrationViewModel;
		public bool IsAddInstance => activeObject is AddInstanceViewModel;
		public bool IsInstance => activeObject is InstanceViewModel;
		public bool IsInstanceUser => activeObject is InstanceUserViewModel;
		public bool IsAddInstanceUser => activeObject is AddInstanceUserViewModel;
		public bool IsRepository => activeObject is RepositoryViewModel;
		public bool IsByond => activeObject is ByondViewModel;
		public bool IsCompiler => activeObject is CompilerViewModel;
		public bool IsDreamDaemon => activeObject is DreamDaemonViewModel;
		public bool IsAddChatBot => activeObject is AddChatBotViewModel;
		public bool IsChatBot => activeObject is ChatBotViewModel;
		public bool IsStaticFolder => activeObject is StaticFolderViewModel;
		public bool IsStaticAdd => activeObject is AddStaticItemViewModel;
		public bool IsStaticFile => activeObject is StaticFileViewModel;
	}
}
