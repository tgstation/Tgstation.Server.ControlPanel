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
				this.RaiseAndSetIfChanged(ref activeObject, value);
				this.RaisePropertyChanged(nameof(IsConnectionManager));
			}
		}

		object activeObject;

		public bool IsConnectionManager => activeObject is ConnectionManagerViewModel;
	}
}
