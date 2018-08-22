using System.Collections.Generic;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	public interface ITreeNode
	{
		string Title { get; }

		string Icon { get; }
		IReadOnlyList<ITreeNode> Children { get; }
	}
}
