using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Octokit;
using ReactiveUI;
using Tgstation.Server.Api.Models;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class TestMergeViewModel : ViewModelBase, ICommandReceiver<TestMergeViewModel.TestMergeCommand>
	{
		public enum TestMergeCommand
		{
			Link,
			LoadCommits
		}

		public string Title => string.Format(CultureInfo.InvariantCulture, "#{0} - {1}", TestMerge.Number, TestMerge.TitleAtMerge);

		public string MergedBy => string.Format(CultureInfo.InvariantCulture, "{0} ({1})", TestMerge.MergedBy.Name, TestMerge.MergedBy.Id);
		public string MergedAt => TestMerge.MergedAt.ToLocalTime().ToString("g");

		public bool Selected
		{
			get => selected;
			set
			{
				this.RaiseAndSetIfChanged(ref selected, value);
				onActivate(TestMerge.Number);
			}
		}

		public bool CanEdit
		{
			get => canEdit;
			set => this.RaiseAndSetIfChanged(ref canEdit, value);
		}

		public string Comment
		{
			get => TestMerge.Comment;
			set => TestMerge.Comment = value;
		}

		public TestMerge TestMerge { get; }

		public FontWeight FontWeight { get; }

		public int SelectedIndex
		{
			get => selectedIndex;
			set
			{
				this.RaiseAndSetIfChanged(ref selectedIndex, value);
				if (CommitsLoaded)
					TestMerge.TargetCommitSha = Commits[SelectedIndex].Substring(0, 7);
			}
		}

		public bool CommitsLoaded => Commits != null;
		public IReadOnlyList<string> Commits { get; private set; }

		public string ActiveCommit => TestMerge.TargetCommitSha.Substring(0, 7);

		public EnumCommand<TestMergeCommand> Link { get; }
		public EnumCommand<TestMergeCommand> LoadCommits { get; }

		readonly Action<int> onActivate;

		readonly Func<CancellationToken, Task> onLoadCommits;
		int selectedIndex;
		bool canEdit;
		bool selected;

		TestMergeViewModel(Action<int> onActivate)
		{
			this.onActivate = onActivate ?? throw new ArgumentNullException(nameof(onActivate));

			Link = new EnumCommand<TestMergeCommand>(TestMergeCommand.Link, this);
			LoadCommits = new EnumCommand<TestMergeCommand>(TestMergeCommand.LoadCommits, this);
		}

		public TestMergeViewModel(Issue pullRequest, IReadOnlyList<PullRequestCommit> commits, Action<int> onActivate, Func<CancellationToken, Task> onLoadCommits, int? activeCommit = null, bool selected = false) : this(onActivate)
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
			this.onLoadCommits = onLoadCommits;
			LoadCommitsAction(commits);
			if (activeCommit.HasValue)
				SelectedIndex = activeCommit.Value;
			FontWeight = pullRequest.Labels.Any(x => x.Name.ToUpperInvariant().Contains("TEST MERGE")) ? FontWeight.Bold : FontWeight.Normal;
			CanEdit = true;
			this.selected = selected; // The use of the private property is intended; for avoiding calling activation function again
		}

		public TestMergeViewModel(TestMerge testMerge, Action<int> onActivate) : this(onActivate)
		{
			TestMerge = testMerge ?? throw new ArgumentNullException(nameof(testMerge));

			Commits = new List<string> { TestMerge.TargetCommitSha.Substring(0, 7) };

			FontWeight = FontWeight.Normal;
			selected = true;    //do not use the property here or you'll cause a StackOverflow
		}

		public void LoadCommitsAction(IReadOnlyList<PullRequestCommit> commits)
		{
			if (commits == null)
				return;
			Commits = commits?.Select(x => string.Format(CultureInfo.InvariantCulture, "{0} - {1}", x.Sha.Substring(0, 7), x.Commit.Message.Split('\n').First())).ToList();
			this.RaisePropertyChanged(nameof(Commits));
			this.RaisePropertyChanged(nameof(CommitsLoaded));
			LoadCommits.Recheck();
			SelectedIndex = Commits.Count - 1;
		}

		public bool CanRunCommand(TestMergeCommand command)
		{
			return command switch
			{
				TestMergeCommand.Link => true,
				TestMergeCommand.LoadCommits => !CommitsLoaded,
				_ => throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!"),
			};
		}
		public async Task RunCommand(TestMergeCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case TestMergeCommand.Link:
					ControlPanel.LaunchUrl(TestMerge.Url);
					break;
				case TestMergeCommand.LoadCommits:
					if (onLoadCommits != null)
						await onLoadCommits(cancellationToken).ConfigureAwait(true);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}
