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
				}
			}
		}

		object activeObject;

		public bool IsConnectionManager => activeObject is ConnectionManagerViewModel;
		public bool IsUser => activeObject is UserViewModel;
	}
}
