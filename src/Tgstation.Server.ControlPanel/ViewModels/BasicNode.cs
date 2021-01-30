using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class BasicNode : ViewModelBase, ITreeNode
	{
		public string Title
		{
			get => title;
			set => this.RaiseAndSetIfChanged(ref title, value);
		}

		public bool IsExpanded { get; set; }

		public string Icon
		{
			get => icon;
			set => this.RaiseAndSetIfChanged(ref icon, value);
		}

		string icon;
		string title;

		public IReadOnlyList<ITreeNode> Children => null;

		public Task HandleClick(CancellationToken cancellationToken) => Task.CompletedTask;
	}
}
