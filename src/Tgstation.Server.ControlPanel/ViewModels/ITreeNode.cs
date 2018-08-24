using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	public interface ITreeNode
	{
		string Title { get; }

		string Icon { get; }
		IReadOnlyList<ITreeNode> Children { get; }

		Task HandleDoubleClick(CancellationToken cancellationToken);
	}
}
