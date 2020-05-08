using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Tgstation.Server.Api.Models;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	sealed class CompileJobViewModel : ViewModelBase
	{
		public CompileJob CompileJob { get; }

		public string User => String.Format(CultureInfo.InvariantCulture, "{0} ({1})", CompileJob.Job.StartedBy.Name, CompileJob.Job.StartedBy.Id);

		public string StoppedAt => CompileJob.Job.StoppedAt.Value.ToLocalTime().ToString("g");

		public string Duration => (CompileJob.Job.StoppedAt.Value - CompileJob.Job.StartedAt.Value).ToString("hh\\:mm\\:ss");

		public string Revision => CompileJob.RevisionInformation.CommitSha.Substring(0, 7);
		public string DMApiVersion => $"{CompileJob.DMApiVersion.Major}.{CompileJob.DMApiVersion.Minor}.{CompileJob.DMApiVersion.Build}";
		public string OriginRevision => CompileJob.RevisionInformation.OriginCommitSha.Substring(0, 7);

		public bool HasTestMerges => CompileJob.RevisionInformation.ActiveTestMerges.Count > 0 && IsExpanded;

		public bool IsExpanded
		{
			get => isExpanded;
			set
			{
				this.RaiseAndSetIfChanged(ref isExpanded, value);
				this.RaisePropertyChanged(nameof(HasTestMerges));
			}
		}

		public string MinimumSecurity => CompileJob.MinimumSecurityLevel.ToString();

		public IReadOnlyList<TestMergeViewModel> TestMerges { get; }

		bool isExpanded;

		public CompileJobViewModel(CompileJob compileJob)
		{
			CompileJob = compileJob ?? throw new ArgumentNullException(nameof(compileJob));
			TestMerges = compileJob.RevisionInformation.ActiveTestMerges.Select(x => new TestMergeViewModel(x, y => { })).ToList();
		}
	}
}
