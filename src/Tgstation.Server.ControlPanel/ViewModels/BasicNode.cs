using ReactiveUI;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class BasicNode : ViewModelBase, ITreeNode
	{
		public string Title { get; set; }
		public bool IsExpanded { get; set; }

		public string Icon
		{
			get => icon;
			set => this.RaiseAndSetIfChanged(ref icon, value);
		}

		string icon;

		public IReadOnlyList<ITreeNode> Children => null;

		public Task HandleDoubleClick(CancellationToken cancellationToken) => Task.CompletedTask;
	}
}
