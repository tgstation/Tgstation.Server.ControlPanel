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
	}
}
