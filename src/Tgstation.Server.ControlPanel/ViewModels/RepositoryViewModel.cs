using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using ReactiveUI;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Api.Models.Request;
using Tgstation.Server.Api.Models.Response;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Client;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class RepositoryViewModel : ViewModelBase, ITreeNode, ICommandReceiver<RepositoryViewModel.RepositoryCommand>
	{
		public enum RepositoryCommand
		{
			Close,
			Refresh,
			Clone,
			Delete,
			Update,
			RemoveCredentials,
			DirectAddManualPR,
			RefreshPRs,
			AddPR
		}

		const string NoCredentials = "(not set)";

		public string Title => "Repository";

		public string Icon
		{
			get => icon;
			private set => this.RaiseAndSetIfChanged(ref icon, value);
		}

		public IReadOnlyList<ITreeNode> Children => null;

		public bool IsExpanded { get; set; }

		public RepositoryResponse Repository
		{
			get => repository;
			set
			{
				this.RaiseAndSetIfChanged(ref repository, value);
				this.RaisePropertyChanged(nameof(Available));
				this.RaisePropertyChanged(nameof(HasCredentials));
			}
		}

		public bool Available => Repository.Origin != null;

		public string NewOrigin
		{
			get => newOrigin;
			set
			{
				this.RaiseAndSetIfChanged(ref newOrigin, value);
				Clone.Recheck();
			}
		}
		public string NewSha
		{
			get => newSha;
			set
			{
				this.RaiseAndSetIfChanged(ref newSha, value);
				this.RaisePropertyChanged(nameof(CanSetRef));
				this.RaisePropertyChanged(nameof(UpdateText));
				this.RaisePropertyChanged(nameof(CanUpdateMerge));
				this.RaisePropertyChanged(nameof(UpdateMerge));
			}
		}
		public string NewReference
		{
			get => newReference;
			set
			{
				this.RaiseAndSetIfChanged(ref newReference, value);
				this.RaisePropertyChanged(nameof(CanSetSha));
				this.RaisePropertyChanged(nameof(UpdateText));
				this.RaisePropertyChanged(nameof(CanUpdateMerge));
				this.RaisePropertyChanged(nameof(UpdateMerge));
			}
		}
		public string NewCommitterName
		{
			get => newCommitterName;
			set => this.RaiseAndSetIfChanged(ref newCommitterName, value);
		}
		public string NewCommitterEmail
		{
			get => newCommitterEmail;
			set => this.RaiseAndSetIfChanged(ref newCommitterEmail, value);
		}
		public string NewAccessUser
		{
			get => newAccessUser;
			set
			{
				this.RaiseAndSetIfChanged(ref newAccessUser, value);
				Update.Recheck();
				Clone.Recheck();
			}
		}
		public string NewAccessToken
		{
			get => newAccessToken;
			set
			{
				this.RaiseAndSetIfChanged(ref newAccessToken, value);
				Update.Recheck();
				Clone.Recheck();
			}
		}

		public bool UpdateMerge
		{
			get => updateMerge && CanUpdateMerge;
			set => this.RaiseAndSetIfChanged(ref updateMerge, value && !UpdateHard);
		}

		public bool UpdateHard
		{
			get => updateHard;
			set
			{
				using (DelayChangeNotifications())
				{
					this.RaiseAndSetIfChanged(ref updateHard, value);
					this.RaisePropertyChanged(nameof(UpdateMerge));
					this.RaisePropertyChanged(nameof(CanUpdateMerge));
					this.RaisePropertyChanged(nameof(CanSetRef));
					if (value)
					{
						if (string.IsNullOrEmpty(NewReference) && Repository != null)
							NewReference = Repository.Reference;
						NewSha = string.Empty;
					}
					else if (NewReference == Repository?.Reference)
						NewReference = string.Empty;
					RebuildTestMergeList();
				}
			}
		}

		public bool CanUpdateMerge => !UpdateHard && string.IsNullOrEmpty(NewReference) && string.IsNullOrEmpty(NewSha);

		public bool NewShowTestMergeCommitters
		{
			get => newShowTestMergeCommitters;
			set => this.RaiseAndSetIfChanged(ref newShowTestMergeCommitters, value);
		}

		public bool NewAutoUpdatesKeepTestMerges
		{
			get => newAutoUpdatesKeepTestMerges;
			set => this.RaiseAndSetIfChanged(ref newAutoUpdatesKeepTestMerges, value);
		}

		public bool NewAutoUpdatesSynchronize
		{
			get => newAutoUpdatesSynchronize;
			set => this.RaiseAndSetIfChanged(ref newAutoUpdatesSynchronize, value);
		}

		public bool RecurseSubmodules
		{
			get => recurseSubmodules;
			set => this.RaiseAndSetIfChanged(ref recurseSubmodules, value);
		}


		public bool NewPostTestMergeComment
		{
			get => newPostTestMergeComment;
			set => this.RaiseAndSetIfChanged(ref newPostTestMergeComment, value);
		}

		public bool NewGitHubDeployments
		{
			get => newGitHubDeployments;
			set => this.RaiseAndSetIfChanged(ref newGitHubDeployments, value);
		}

		public bool UpdateSubmodules { get; set; }

		public string DeleteText => confirmingDelete ? "Confirm?" : "Delete Repository";

		public bool CanClone => !Refreshing && rightsProvider.RepositoryRights.HasFlag(RepositoryRights.SetOrigin);
		public bool CanDelete => !Refreshing && rightsProvider.RepositoryRights.HasFlag(RepositoryRights.Delete);
		public bool CanSetRef => !Refreshing && rightsProvider.RepositoryRights.HasFlag(RepositoryRights.SetReference) && string.IsNullOrEmpty(NewSha) && !UpdateHard;
		public bool CanSetSha => !Refreshing && rightsProvider.RepositoryRights.HasFlag(RepositoryRights.SetSha) && string.IsNullOrEmpty(NewReference);
		public bool CanShowTMCommitters => !Refreshing && rightsProvider.RepositoryRights.HasFlag(RepositoryRights.ChangeTestMergeCommits);
		public bool CanUpdateSubmodules => !Refreshing && rightsProvider.RepositoryRights.HasFlag(RepositoryRights.ChangeSubmoduleUpdate);
		public bool CanChangeCommitter => !Refreshing && rightsProvider.RepositoryRights.HasFlag(RepositoryRights.ChangeCommitter);
		public bool CanAccess => !Refreshing && rightsProvider.RepositoryRights.HasFlag(RepositoryRights.ChangeCredentials);
		public bool CanAutoUpdate => !Refreshing && rightsProvider.RepositoryRights.HasFlag(RepositoryRights.ChangeAutoUpdateSettings);
		public bool CanUpdate => !Refreshing && rightsProvider.RepositoryRights.HasFlag(RepositoryRights.UpdateBranch);
		public bool CanTestMerge => !Refreshing && rightsProvider.RepositoryRights.HasFlag(RepositoryRights.MergePullRequest);
		public bool CanDeploy => rightsProvider.DreamMakerRights.HasFlag(DreamMakerRights.Compile);

		string manualPR;
		public string ManualPR
		{
			get => manualPR;
			set => this.RaiseAndSetIfChanged(ref manualPR, value);
		}

		public bool Error
		{
			get => error;
			set => this.RaiseAndSetIfChanged(ref error, value);
		}

		public bool CloneAvailable
		{
			get => cloneAvailable;
			set => this.RaiseAndSetIfChanged(ref cloneAvailable, value);
		}

		public bool Refreshing
		{
			get => refreshing;
			set => this.RaiseAndSetIfChanged(ref refreshing, value);
		}

		public string UpdateText => string.IsNullOrEmpty(NewSha) && (string.IsNullOrEmpty(NewReference) || NewReference == Repository.Reference) ? "Fetch and Hard Reset To Tracked Origin Reference" : "Fetch and Hard Reset to Target Origin Object";

		public string ErrorMessage
		{
			get => errorMessage;
			set => this.RaiseAndSetIfChanged(ref errorMessage, value);
		}

		public bool DeployAfter
		{
			get => deployAfter;
			set => this.RaiseAndSetIfChanged(ref deployAfter, value);
		}

		public IReadOnlyList<TestMergeViewModel> TestMerges
		{
			get => testMerges;
			set => this.RaiseAndSetIfChanged(ref testMerges, value);
		}

		IReadOnlyList<TestMergeViewModel> availableTestMerges;
		public IReadOnlyList<TestMergeViewModel> AvailableTestMerges
		{
			get => availableTestMerges;
			set => this.RaiseAndSetIfChanged(ref availableTestMerges, value);
		}

		IReadOnlyList<TestMergeViewModel> selectedTestMerges;
		public IReadOnlyList<TestMergeViewModel> SelectedTestMerges
		{
			get => selectedTestMerges;
			set => this.RaiseAndSetIfChanged(ref selectedTestMerges, value);
		}

		TestMergeViewModel currentTestMerge;
		public TestMergeViewModel CurrentTestMerge
		{
			get => currentTestMerge;
			set => this.RaiseAndSetIfChanged(ref currentTestMerge, value);
		}

		public bool HasCredentials => Repository?.AccessUser != null && Repository?.AccessUser != NoCredentials;

		public bool RateLimited
		{
			get => rateLimited;
			set => this.RaiseAndSetIfChanged(ref rateLimited, value);
		}
		public string RateLimitSeconds
		{
			get => rateLimitSeconds;
			set => this.RaiseAndSetIfChanged(ref rateLimitSeconds, value);
		}

		public EnumCommand<RepositoryCommand> Close { get; }
		public EnumCommand<RepositoryCommand> RefreshCommand { get; }
		public EnumCommand<RepositoryCommand> Clone { get; }
		public EnumCommand<RepositoryCommand> Delete { get; }
		public EnumCommand<RepositoryCommand> Update { get; }
		public EnumCommand<RepositoryCommand> RemoveCredentials { get; }
		public EnumCommand<RepositoryCommand> DirectAddPR { get; }
		public EnumCommand<RepositoryCommand> RefreshPRs { get; }
		public EnumCommand<RepositoryCommand> AddPR { get; }

		readonly PageContextViewModel pageContext;
		readonly IRepositoryClient repositoryClient;
		readonly IDreamMakerClient dreamMakerClient;
		readonly IJobsClient jobsClient;
		readonly IInstanceJobSink jobSink;
		readonly IInstanceUserRightsProvider rightsProvider;
		readonly Octokit.IGitHubClient gitHubClient;

		RepositoryResponse repository;

		IReadOnlyList<TestMergeViewModel> testMerges;

		Dictionary<int, Octokit.Issue> pullRequests;
		Dictionary<int, IReadOnlyList<Octokit.PullRequestCommit>> pullRequestCommits;

		string newOrigin;
		string newSha;
		string newReference;
		string newCommitterName;
		string newCommitterEmail;
		string newAccessUser;
		string newAccessToken;

		string rateLimitSeconds;

		bool updateMerge;
		bool updateHard;

		bool newShowTestMergeCommitters;
		bool newAutoUpdatesKeepTestMerges;
		bool newAutoUpdatesSynchronize;
		bool newPostTestMergeComment;
		bool newGitHubDeployments;
		bool rateLimited;

		string errorMessage;
		bool error;
		bool loadingPRs;

		string icon;

		bool refreshing;
		bool recurseSubmodules;
		bool cloneAvailable;
		bool deployAfter;

		bool modifiedPRList;

		bool confirmingDelete;

		public RepositoryViewModel(PageContextViewModel pageContext, IRepositoryClient repositoryClient, IDreamMakerClient dreamMakerClient, IJobsClient jobsClient, IInstanceJobSink jobSink, IInstanceUserRightsProvider rightsProvider, Octokit.IGitHubClient gitHubClient)
		{
			this.pageContext = pageContext ?? throw new ArgumentNullException(nameof(pageContext));
			this.repositoryClient = repositoryClient ?? throw new ArgumentNullException(nameof(repositoryClient));
			this.dreamMakerClient = dreamMakerClient ?? throw new ArgumentNullException(nameof(dreamMakerClient));
			this.jobsClient = jobsClient ?? throw new ArgumentNullException(nameof(jobsClient));
			this.jobSink = jobSink ?? throw new ArgumentNullException(nameof(jobSink));
			this.rightsProvider = rightsProvider ?? throw new ArgumentNullException(nameof(rightsProvider));
			this.gitHubClient = gitHubClient ?? throw new ArgumentNullException(nameof(gitHubClient));

			Close = new EnumCommand<RepositoryCommand>(RepositoryCommand.Close, this);
			RefreshCommand = new EnumCommand<RepositoryCommand>(RepositoryCommand.Refresh, this);
			Clone = new EnumCommand<RepositoryCommand>(RepositoryCommand.Clone, this);
			Delete = new EnumCommand<RepositoryCommand>(RepositoryCommand.Delete, this);
			Update = new EnumCommand<RepositoryCommand>(RepositoryCommand.Update, this);
			RemoveCredentials = new EnumCommand<RepositoryCommand>(RepositoryCommand.RemoveCredentials, this);
			DirectAddPR = new EnumCommand<RepositoryCommand>(RepositoryCommand.DirectAddManualPR, this);
			RefreshPRs = new EnumCommand<RepositoryCommand>(RepositoryCommand.RefreshPRs, this);
			AddPR = new EnumCommand<RepositoryCommand>(RepositoryCommand.AddPR, this);

			rightsProvider.OnUpdated += (a, b) => RecheckCommands();
			RecurseSubmodules = true;

			async void InitialLoad() => await Refresh(null, null, false, default).ConfigureAwait(false);
			if (CanRunCommand(RepositoryCommand.Refresh))
				InitialLoad();
		}

		void RecheckCommands()
		{
			RefreshCommand.Recheck();
			Delete.Recheck();
			Clone.Recheck();
			Update.Recheck();
			DirectAddPR.Recheck();
			RefreshPRs.Recheck();
			RemoveCredentials.Recheck();

			this.RaisePropertyChanged(nameof(CanAutoUpdate));
			this.RaisePropertyChanged(nameof(CanChangeCommitter));
			this.RaisePropertyChanged(nameof(CanAccess));
			this.RaisePropertyChanged(nameof(CanShowTMCommitters));
			this.RaisePropertyChanged(nameof(CanTestMerge));
			this.RaisePropertyChanged(nameof(CanSetRef));
			this.RaisePropertyChanged(nameof(CanSetSha));
			this.RaisePropertyChanged(nameof(CanUpdate));
			this.RaisePropertyChanged(nameof(CanDelete));
			this.RaisePropertyChanged(nameof(CanClone));
			this.RaisePropertyChanged(nameof(CanUpdateSubmodules));
		}

		async Task<JobResponse> Refresh(RepositoryUpdateRequest update, RepositoryCreateRequest clone, bool delete, CancellationToken cancellationToken)
		{
			Icon = "resm:Tgstation.Server.ControlPanel.Assets.hourglass.png";
			Refreshing = true;
			Error = false;
			ErrorMessage = null;
			CloneAvailable = false;
			RecheckCommands();
			JobResponse job = null;
			try
			{
				var oldRepo = Repository;
				RepositoryResponse newRepo;
				try
				{
					if (clone != null)
						newRepo = await repositoryClient.Clone(clone, cancellationToken).ConfigureAwait(true);
					else if (delete)
						newRepo = await repositoryClient.Delete(cancellationToken).ConfigureAwait(true);
					else if (update != null)
						newRepo = await repositoryClient.Update(update, cancellationToken).ConfigureAwait(true);
					else
						newRepo = await repositoryClient.Read(cancellationToken).ConfigureAwait(true);

					if (newRepo.ActiveJob != null)
					{
						jobSink.RegisterJob(newRepo.ActiveJob, ct => Refresh(null, null, false, ct));
						job = newRepo.ActiveJob;
					}
					if (newRepo.Reference == null)
						newRepo.Reference = "(unknown)";
					if (newRepo.AccessUser == null)
						newRepo.AccessUser = NoCredentials;


					Repository = newRepo;

					NewOrigin = string.Empty;
					NewSha = string.Empty;
					NewReference = string.Empty;
					NewCommitterEmail = string.Empty;
					NewCommitterName = string.Empty;
					NewAccessUser = string.Empty;
					NewAccessToken = string.Empty;
					UpdateSubmodules = newRepo.UpdateSubmodules.Value;

					UpdateHard = false;
					UpdateMerge = false;
					NewAutoUpdatesKeepTestMerges = Repository.AutoUpdatesKeepTestMerges ?? update?.AutoUpdatesKeepTestMerges ?? oldRepo?.AutoUpdatesKeepTestMerges ?? NewAutoUpdatesKeepTestMerges;
					NewAutoUpdatesSynchronize = Repository.AutoUpdatesSynchronize ?? update?.AutoUpdatesSynchronize ?? oldRepo?.AutoUpdatesSynchronize ?? NewAutoUpdatesSynchronize;
					NewShowTestMergeCommitters = Repository.ShowTestMergeCommitters ?? update?.ShowTestMergeCommitters ?? oldRepo?.ShowTestMergeCommitters ?? NewShowTestMergeCommitters;
					NewPostTestMergeComment = Repository.PostTestMergeComment ?? update?.PostTestMergeComment ?? oldRepo?.PostTestMergeComment ?? NewPostTestMergeComment;
					NewGitHubDeployments = Repository.CreateGitHubDeployments ?? update?.CreateGitHubDeployments ?? oldRepo?.CreateGitHubDeployments ?? NewGitHubDeployments;

					CloneAvailable = Repository.Origin == null;
				}
				catch (ClientException e)
				{
					Error = true;
					ErrorMessage = e.Message;
					return null;
				}

				if (pullRequests == null)
				{
					TestMerges = Repository?.RevisionInformation?.ActiveTestMerges == null ? new List<TestMergeViewModel>() : new List<TestMergeViewModel>(Repository.RevisionInformation.ActiveTestMerges.Select(x => new TestMergeViewModel(x, DeactivatePR)));
					await RefreshPRList().ConfigureAwait(true);
				}
				else
					RebuildTestMergeList();
				modifiedPRList = false;
			}
			finally
			{
				Icon = "resm:Tgstation.Server.ControlPanel.Assets.git.png";
				Refreshing = false;
				RecheckCommands();
			}



			return job;
		}

		void UpdateSelectedPRs()
		{
			SelectedTestMerges = TestMerges.Where(x => x.Selected).ToList();
			AvailableTestMerges = TestMerges.Where(x => !x.Selected).ToList();
			AddPR.Recheck();
		}

		void DigestPR(Octokit.Issue pr, IReadOnlyList<Octokit.PullRequestCommit> commits)
		{
			if (pullRequests == null)
			{
				pullRequests = new Dictionary<int, Octokit.Issue>();
				pullRequestCommits = new Dictionary<int, IReadOnlyList<Octokit.PullRequestCommit>>();
			}
			pullRequests.Add(pr.Number, pr);
			pullRequestCommits.Add(pr.Number, commits);
		}

		async Task RefreshPRList()
		{
			if (!rightsProvider.RepositoryRights.HasFlag(RepositoryRights.MergePullRequest) || Repository?.RemoteGitProvider != RemoteGitProvider.GitHub)
				return;

			loadingPRs = true;
			DirectAddPR.Recheck();
			RefreshPRs.Recheck();
			try
			{
				var prs = await gitHubClient.Search.SearchIssues(new Octokit.SearchIssuesRequest
				{
					Repos = new Octokit.RepositoryCollection { { Repository.RemoteRepositoryOwner, Repository.RemoteRepositoryName } },
					State = Octokit.ItemState.Open,
					Type = Octokit.IssueTypeQualifier.PullRequest
				}).ConfigureAwait(true);

				pullRequests = null;
				foreach (var I in prs.Items)
					DigestPR(I, null);

				RebuildTestMergeList();
			}
			catch (Octokit.RateLimitExceededException e)
			{
				HandleRateLimit(e);
			}
			catch (Exception ex)
			{
				MainWindowViewModel.HandleException(ex);
			}
			finally
			{
				loadingPRs = false;
				DirectAddPR.Recheck();
				RefreshPRs.Recheck();
			}
		}

		async void DeactivatePR(int number)
		{
			try
			{
				await DirectAdd(number, true).ConfigureAwait(true);
				modifiedPRList = true;
			}
			catch { }
		}

		void RebuildTestMergeList()
		{
			if (!rightsProvider.RepositoryRights.HasFlag(RepositoryRights.MergePullRequest) || Repository?.RemoteGitProvider != RemoteGitProvider.GitHub)
				return;

			var tmp = Repository?.RevisionInformation?.ActiveTestMerges == null ? new List<TestMergeViewModel>() : new List<TestMergeViewModel>(Repository.RevisionInformation.ActiveTestMerges.Select(x =>
			{
				if (!(updateHard && pullRequests?.ContainsKey(x.Number) == true))
					return new TestMergeViewModel(x, DeactivatePR);
				TestMergeViewModel result = null;
				async Task LoadCommits(CancellationToken cancellationToken)
				{
					result.LoadCommitsAction(await gitHubClient.PullRequest.Commits(Repository.RemoteRepositoryOwner, Repository.RemoteRepositoryName, x.Number).ConfigureAwait(false));
				}
				result = new TestMergeViewModel(pullRequests[x.Number], pullRequestCommits[x.Number], y => { }, LoadCommits)
				{
					Selected = true,
					Comment = x.Comment
				};
				if (result.CommitsLoaded)
					result.SelectedIndex = result.Commits.ToList().IndexOf(result.Commits.First(y => y.Substring(0, 7).ToUpperInvariant() == x.TargetCommitSha.Substring(0, 7).ToUpperInvariant()));
				return result;
			}));
			if (!RateLimited && pullRequests != null)
			{
				var enumerable = Enumerable.Zip(pullRequests, pullRequestCommits, (a, b) =>
				{
					if (tmp.Any(x => x.TestMerge.Number == a.Key))
						return null;

					TestMergeViewModel testMergeViewModel = null;
					async Task LoadCommits(CancellationToken cancellationToken)
					{
						testMergeViewModel.LoadCommitsAction(await gitHubClient.PullRequest.Commits(Repository.RemoteRepositoryOwner, Repository.RemoteRepositoryName, a.Key).ConfigureAwait(false));
					}

					testMergeViewModel = new TestMergeViewModel(a.Value, b.Value, x => modifiedPRList = true, LoadCommits);
					return testMergeViewModel;
				}).Where(x => x != null).ToList();
				tmp.AddRange(enumerable.Where(x => x.FontWeight == FontWeight.Bold));
				tmp.AddRange(enumerable.Where(x => x.FontWeight == FontWeight.Normal));
			}

			// Use new testmerge list, and enforce selected status
			TestMerges = tmp;
			UpdateSelectedPRs();
		}

		static readonly Regex PRNumberPattern = new Regex(@"[^0-9]?(?<pr_number>[0-9]+)[^0-9]*$", RegexOptions.Compiled);

		async Task DirectAdd(string input, bool forceRebuild)
		{
			// Attempt to extract PR number from the provided string, as it could be a link
			var matchResult = PRNumberPattern.Match(input);
			if (!matchResult.Success)
			{
				return;
				// do thing for no result
			}
			if (!int.TryParse(matchResult.Groups["pr_number"].Value, out var number))
			{
				return;
				// do thing for bad number
			}

			await DirectAdd(number, forceRebuild);
		}

		async Task DirectAdd(int number, bool forceRebuild)
		{
			if (!rightsProvider.RepositoryRights.HasFlag(RepositoryRights.MergePullRequest) || Repository?.RemoteGitProvider != RemoteGitProvider.GitHub)
				return;

			loadingPRs = true;
			DirectAddPR.Recheck();
			RefreshPRs.Recheck();
			var run = pullRequests?.ContainsKey(number) != true;
			try
			{
				if (run)
					try
					{
						var prTask = gitHubClient.Issue.Get(Repository.RemoteRepositoryOwner, Repository.RemoteRepositoryName, number);
						var commits = await gitHubClient.PullRequest.Commits(Repository.RemoteRepositoryOwner, Repository.RemoteRepositoryName, number).ConfigureAwait(true);
						var pr = await prTask.ConfigureAwait(true);
						DigestPR(pr, commits);
					}
					catch (Octokit.RateLimitExceededException e)
					{
						HandleRateLimit(e);
					}
					catch (Octokit.ApiException ex)
					{
						MainWindowViewModel.HandleException(ex);
					}
			}
			catch
			{
				// manual addition
				var manualPr = new Octokit.Issue(
					string.Empty,
					$"https://github.com/{Repository.RemoteRepositoryOwner}/{Repository.RemoteRepositoryName}/pull/{number}",
					string.Empty,
					string.Empty,
					number,
					Octokit.ItemState.Open,
					"Unable to Load PR Details",
					string.Empty,
					null,
					new Octokit.User(),
					Array.Empty<Octokit.Label>(),
					null,
					Array.Empty<Octokit.User>(),
					null,
					0,
					null,
					null,
					default,
					null,
					0,
					string.Empty,
					false,
					null,
					null);
				DigestPR(manualPr, null);
			}
			finally
			{
				if (run || forceRebuild)
					RebuildTestMergeList();
				loadingPRs = false;
				DirectAddPR.Recheck();
				RefreshPRs.Recheck();

				// Flag new addition as active if successful
				var newPr = TestMerges.FirstOrDefault(x => x.TestMerge.Number == number);
				if (newPr != null)
				{
					newPr.Selected = true;
					ManualPR = null;
					UpdateSelectedPRs();
				}
			}
		}

		void HandleRateLimit(Octokit.RateLimitExceededException e)
		{
			RateLimited = true;
			void UpdateSeconds() => RateLimitSeconds = string.Format(CultureInfo.InvariantCulture, "You have been rate limited by GitHub, add a personal access token on the Connection Manager to increase the limit. This will reset in {0}s...", Math.Floor((e.Reset - DateTimeOffset.Now).TotalSeconds));
			UpdateSeconds();
			async void ResetRate()
			{
				while (DateTimeOffset.Now < e.Reset)
				{
					await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(true);
					UpdateSeconds();
				}
				RateLimited = false;
			}
			ResetRate();
			RebuildTestMergeList();
		}

		public Task HandleClick(CancellationToken cancellationToken)
		{
			pageContext.ActiveObject = this;
			return Task.CompletedTask;
		}

		public bool CanRunCommand(RepositoryCommand command)
		{
			return command switch
			{
				RepositoryCommand.Close => true,
				RepositoryCommand.Refresh => !Refreshing && rightsProvider.RepositoryRights.HasFlag(RepositoryRights.Read),
				RepositoryCommand.Update => !Refreshing
					&& (CanAutoUpdate | CanChangeCommitter | CanAccess | CanShowTMCommitters | CanTestMerge | CanSetRef | CanSetSha | CanUpdate)
					&& !(!string.IsNullOrEmpty(NewAccessUser) ^ !string.IsNullOrEmpty(NewAccessToken)),
				RepositoryCommand.Delete => !Refreshing && CanDelete,
				RepositoryCommand.Clone => !Refreshing
					&& CanClone && !string.IsNullOrEmpty(NewOrigin)
					&& Uri.TryCreate(NewOrigin, UriKind.Absolute, out var _)
					&& !(!string.IsNullOrEmpty(NewAccessUser) ^ !string.IsNullOrEmpty(NewAccessToken)),
				RepositoryCommand.RemoveCredentials => !Refreshing && CanAccess,
				RepositoryCommand.DirectAddManualPR or RepositoryCommand.AddPR or RepositoryCommand.RefreshPRs => !Refreshing && CanTestMerge && !RateLimited && !loadingPRs,
				_ => throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!"),
			};
		}

		public async Task RunCommand(RepositoryCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case RepositoryCommand.Close:
					pageContext.ActiveObject = null;
					break;
				case RepositoryCommand.Refresh:
					await Refresh(null, null, false, cancellationToken).ConfigureAwait(true);
					break;
				case RepositoryCommand.Update:
					var update = new RepositoryUpdateRequest
					{
						CheckoutSha = string.IsNullOrEmpty(NewSha) ? null : NewSha,
						Reference = string.IsNullOrEmpty(NewReference) ? null : NewReference,
						UpdateFromOrigin = UpdateHard || UpdateMerge ? (bool?)true : null,

						AccessToken = string.IsNullOrEmpty(NewAccessToken) ? null : NewAccessToken,
						AccessUser = string.IsNullOrEmpty(NewAccessUser) ? null : NewAccessUser,

						AutoUpdatesKeepTestMerges = CanAutoUpdate ? (bool?)NewAutoUpdatesKeepTestMerges : null,
						AutoUpdatesSynchronize = CanAutoUpdate ? (bool?)NewAutoUpdatesSynchronize : null,

						PostTestMergeComment = CanShowTMCommitters ? (bool?)NewPostTestMergeComment : null,
						ShowTestMergeCommitters = CanShowTMCommitters ? (bool?)NewShowTestMergeCommitters : null,
						CreateGitHubDeployments = CanShowTMCommitters ? (bool?)NewGitHubDeployments : null,

						CommitterEmail = !string.IsNullOrEmpty(NewCommitterEmail) ? NewCommitterEmail : null,
						CommitterName = !string.IsNullOrEmpty(NewCommitterName) ? NewCommitterName : null,
						UpdateSubmodules = CanUpdateSubmodules ? UpdateSubmodules : null,
					};

					if (modifiedPRList || UpdateHard)
						update.NewTestMerges = TestMerges.Where(x => x.CanEdit && x.Selected).Select(x => new TestMergeParameters
						{
							Comment = x.TestMerge.Comment,
							Number = x.TestMerge.Number,
							TargetCommitSha = x.TestMerge.TargetCommitSha
						}).ToList();

					var job = await Refresh(update, null, false, cancellationToken).ConfigureAwait(true);
					if (DeployAfter)
					{
						// Wait until the job has progress
						if (job != null)
						{
							while (!job.StoppedAt.HasValue && !job.Progress.HasValue)
							{
								await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
								job = await jobsClient.GetId(job, cancellationToken).ConfigureAwait(false);
							}
						}

						job = await dreamMakerClient.Compile(cancellationToken).ConfigureAwait(true);
						jobSink.RegisterJob(job);
					}
					break;
				case RepositoryCommand.Delete:
					if (confirmingDelete)
						await Refresh(null, null, true, cancellationToken).ConfigureAwait(true);
					else
					{
						async void ResetDelete()
						{
							await Task.Delay(TimeSpan.FromSeconds(3), default).ConfigureAwait(true);
							confirmingDelete = false;
							this.RaisePropertyChanged(nameof(DeleteText));
						}
						confirmingDelete = true;
						this.RaisePropertyChanged(nameof(DeleteText));
						ResetDelete();
					}
					break;
				case RepositoryCommand.Clone:
					var clone = new RepositoryCreateRequest
					{
						Origin = new Uri(NewOrigin),
						Reference = string.IsNullOrEmpty(NewReference) ? null : NewReference,
						AccessToken = string.IsNullOrEmpty(NewAccessToken) || !CanAccess ? null : NewAccessToken,
						AccessUser = string.IsNullOrEmpty(NewAccessUser) || !CanAccess ? null : NewAccessUser,
						UpdateSubmodules = RecurseSubmodules
					};
					await Refresh(null, clone, false, cancellationToken).ConfigureAwait(true);
					break;
				case RepositoryCommand.RemoveCredentials:
					await Refresh(new RepositoryUpdateRequest
					{
						AccessUser = string.Empty,
						AccessToken = string.Empty
					}, null, false, cancellationToken).ConfigureAwait(true);
					break;
				case RepositoryCommand.DirectAddManualPR:
					await DirectAdd(ManualPR, false).ConfigureAwait(true);
					break;
				case RepositoryCommand.AddPR:
					CurrentTestMerge.Selected = true;
					UpdateSelectedPRs();
					break;
				case RepositoryCommand.RefreshPRs:
					await RefreshPRList().ConfigureAwait(true);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}
