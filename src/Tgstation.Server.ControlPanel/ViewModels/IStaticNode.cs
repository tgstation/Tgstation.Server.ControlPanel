using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	interface IStaticNode : ITreeNode
	{
		string Path { get; }

		void RemoveChild(IStaticNode child);

		Task RefreshContents(CancellationToken cancellationToken);
		void DirectAdd(ConfigurationFile file);
	}
}