using System.Threading;
using System.Threading.Tasks;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	interface IStaticNode
	{
		string Path { get; }

		void RemoveChild(IStaticNode child);

		Task RefreshContents(CancellationToken cancellationToken);
	}
}