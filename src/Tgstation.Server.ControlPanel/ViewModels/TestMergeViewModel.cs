using Avalonia.Media;
using Octokit;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class TestMergeViewModel : ViewModelBase, ICommandReceiver<TestMergeViewModel.TestMergeCommand>
	{
		public enum TestMergeCommand
		{
			Activate,
			Link
		}

		public string Title => String.Format(CultureInfo.InvariantCulture, "#{0} - {1}", TestMerge.Number, TestMerge.TitleAtMerge);

		public TestMerge TestMerge { get; }

		public FontWeight FontWeight { get; }

		public int SelectedIndex
		{
			get => selectedIndex;
			set
			{
				this.RaiseAndSetIfChanged(ref selectedIndex, value);
				TestMerge.PullRequestRevision = Commits[SelectedIndex].Substring(0, 7);
			}
		}

		public IReadOnlyList<string> Commits { get; }

		public string ActiveCommit => TestMerge.PullRequestRevision;

		public EnumCommand<TestMergeCommand> Activate { get; }

		public EnumCommand<TestMergeCommand> Link { get; }

		readonly Func<int, CancellationToken, Task> onActivate;
		int selectedIndex;

		TestMergeViewModel(Func<int, CancellationToken, Task> onActivate)
		{
			this.onActivate = onActivate;

			Activate = new EnumCommand<TestMergeCommand>(TestMergeCommand.Activate, this);
			Link = new EnumCommand<TestMergeCommand>(TestMergeCommand.Link, this);
		}

		public TestMergeViewModel(Issue pullRequest, IReadOnlyList<PullRequestCommit> commits, Func<int, CancellationToken, Task> onActivate, int? activeCommit = null) : this(onActivate)
		{
			if (pullRequest == null)
				throw new ArgumentNullException(nameof(pullRequest));

			TestMerge = new TestMerge
			{
				Author = pullRequest.User.Login,
				BodyAtMerge = pullRequest.Body,
				Number = pullRequest.Number,
				TitleAtMerge = pullRequest.Title,
				Url = pullRequest.HtmlUrl
			};

			Commits = commits?.Select(x => String.Format(CultureInfo.InvariantCulture, "{0} - {1}", x.Sha.Substring(0, 7), x.Commit.Message.Split('\n').First())).ToList() ?? throw new ArgumentNullException(nameof(commits));
			FontWeight = pullRequest.Labels.Any(x => x.Name.ToUpperInvariant().Contains("TEST MERGE")) ? FontWeight.Bold : FontWeight.Normal;
			SelectedIndex = activeCommit ?? (Commits.Count - 1);
		}

		public TestMergeViewModel(TestMerge testMerge, Func<int, CancellationToken, Task> onActivate) : this(onActivate)
		{
			TestMerge = testMerge ?? throw new ArgumentNullException(nameof(testMerge));

			Commits = new List<string> { TestMerge.PullRequestRevision.Substring(0, 7) };

			FontWeight = FontWeight.Normal;
		}

		public bool CanRunCommand(TestMergeCommand command)
		{
			switch (command)
			{
				case TestMergeCommand.Link:
					return true;
				case TestMergeCommand.Activate:
					return onActivate != null;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
		public Task RunCommand(TestMergeCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case TestMergeCommand.Link:
					ControlPanel.LaunchUrl(TestMerge.Url);
					return Task.CompletedTask;
				case TestMergeCommand.Activate:
					return onActivate(TestMerge.Number.Value, cancellationToken);
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}
