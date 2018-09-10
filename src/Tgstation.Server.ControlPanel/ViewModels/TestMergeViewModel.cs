using Avalonia.Media;
using Octokit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class TestMergeViewModel : ViewModelBase, ICommandReceiver<TestMergeViewModel.TestMergeCommand>
	{
		public enum TestMergeCommand
		{
			Activate
		}
		public string Title { get; }

		public FontWeight FontWeight { get; }

		public int SelectedIndex { get; set; }

		public IReadOnlyList<string> Commits { get; }

		public IReadOnlyList<object> Children => null;
		
		readonly Func<CancellationToken, Task> onActivate;

		public TestMergeViewModel(Issue pullRequest, IReadOnlyList<PullRequestCommit> commits, Func<CancellationToken, Task> onActivate)
		{
			Title = String.Format(CultureInfo.InvariantCulture, "#{0} - {1}", pullRequest?.Number ?? throw new ArgumentNullException(nameof(pullRequest)), pullRequest.Title);
			Commits = commits?.Select(x => String.Format(CultureInfo.InvariantCulture, "{0} - {1}", x.Sha.Substring(0, 7), x.Commit.Message.Split('\n').First())).ToList() ?? throw new ArgumentNullException(nameof(commits));
			FontWeight = pullRequest.Labels.Any(x => x.Name.ToUpperInvariant().Contains("TEST MERGE")) ? FontWeight.Bold : FontWeight.Normal;
			SelectedIndex = Commits.Count - 1;
			
			this.onActivate = onActivate ?? throw new ArgumentNullException(nameof(onActivate));
		}

		public bool CanRunCommand(TestMergeCommand command) => true;
		public Task RunCommand(TestMergeCommand command, CancellationToken cancellationToken) => onActivate(cancellationToken);
	}
}
