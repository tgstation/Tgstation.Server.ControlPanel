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
			Link
		}

		public string Title => String.Format(CultureInfo.InvariantCulture, "#{0} - {1}", TestMerge.Number, TestMerge.TitleAtMerge);

		public bool Selected
		{
			get => selected;
			set
			{
				this.RaiseAndSetIfChanged(ref selected, value);
				onActivate();
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
				TestMerge.PullRequestRevision = Commits[SelectedIndex].Substring(0, 7);
			}
		}

		public IReadOnlyList<string> Commits { get; }

		public string ActiveCommit => TestMerge.PullRequestRevision;

		public EnumCommand<TestMergeCommand> Link { get; }

		readonly Action onActivate;
		int selectedIndex;
		bool canEdit;
		bool selected;

		TestMergeViewModel(Action onActivate)
		{
			this.onActivate = onActivate ?? throw new ArgumentNullException(nameof(onActivate));
			
			Link = new EnumCommand<TestMergeCommand>(TestMergeCommand.Link, this);
		}

		public TestMergeViewModel(Issue pullRequest, IReadOnlyList<PullRequestCommit> commits, Action onActivate, int? activeCommit = null) : this(onActivate)
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
			CanEdit = true;
		}

		public TestMergeViewModel(TestMerge testMerge, Action onActivate) : this(onActivate)
		{
			TestMerge = testMerge ?? throw new ArgumentNullException(nameof(testMerge));

			Commits = new List<string> { TestMerge.PullRequestRevision.Substring(0, 7) };

			FontWeight = FontWeight.Normal;
			Selected = true;
		}

		public bool CanRunCommand(TestMergeCommand command)
		{
			switch (command)
			{
				case TestMergeCommand.Link:
					return true;
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
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}
