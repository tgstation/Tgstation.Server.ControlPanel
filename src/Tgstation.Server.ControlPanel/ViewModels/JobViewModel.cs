using Avalonia.Media;
using ReactiveUI;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Client;
using Tgstation.Server.Client.Components;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	public sealed class JobViewModel : ViewModelBase, ICommandReceiver<JobViewModel.JobCommand>
	{
		public enum JobCommand
		{
			Remove,
			Cancel
		}

		public string Title => String.Format(CultureInfo.InvariantCulture, "Job #{0}", job.Id) + (Finished ? (Cancelled ? " (Cancelled)" : Error ? " (Failed)" : " (Done)") : String.Empty);

		public bool Finished => job.StoppedAt.HasValue;
		public bool Error => job.ExceptionDetails != null;

		public IBrush Background => new SolidColorBrush(Color.Parse(Finished ? (Error ? "#EA4638" : (Cancelled ? "#FFB900" : "#12BC00")) : "#FFFFFF"));

		public int Progress => Finished ? (Cancelled || Error ? job.Progress ?? 0 : 100) : job.Progress ?? 0;

		public bool HasProgress => job.Progress.HasValue || Finished;

		public string Description => job.Description;

		public string StartedBy => String.Format(CultureInfo.InvariantCulture, "{0} ({1})", job.StartedBy.Name, job.StartedBy.Id);
		public string StartedAt => job.StartedAt.Value.ToString("s");

		public bool Cancelled => job.Cancelled == true;

		public string ErrorDetails => job.ExceptionDetails;

		public EnumCommand<JobCommand> Remove { get; }
		public EnumCommand<JobCommand> Cancel { get; }

		readonly Action onRemove;

		Job job;
		IJobsClient jobsClient;
		bool canCancel;

		public JobViewModel(Job job, Action onRemove, IJobsClient jobsClient)
		{
			this.onRemove = onRemove ?? throw new ArgumentNullException(nameof(onRemove));
			canCancel = true;
			Remove = new EnumCommand<JobCommand>(JobCommand.Remove, this);
			Cancel = new EnumCommand<JobCommand>(JobCommand.Cancel, this);
			Update(job, jobsClient);
		}

		public void Update(Job job, IJobsClient jobsClient)
		{
			this.job = job ?? throw new ArgumentNullException(nameof(job));
			this.jobsClient = jobsClient ?? throw new ArgumentNullException(nameof(jobsClient));

			this.RaisePropertyChanged(nameof(Finished));
			this.RaisePropertyChanged(nameof(ErrorDetails));
			this.RaisePropertyChanged(nameof(Progress));
			this.RaisePropertyChanged(nameof(HasProgress));
			this.RaisePropertyChanged(nameof(Background));
			this.RaisePropertyChanged(nameof(Error));
			this.RaisePropertyChanged(nameof(Title));
			Remove.Recheck();
			Cancel.Recheck();
		}

		public bool CanRunCommand(JobCommand command)
		{
			switch (command)
			{
				case JobCommand.Remove:
					return Finished;
				case JobCommand.Cancel:
					return canCancel && !Finished && jobsClient != null;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}

		public async Task RunCommand(JobCommand command, CancellationToken cancellationToken)
		{
			switch (command)
			{
				case JobCommand.Remove:
					onRemove();
					break;
				case JobCommand.Cancel:
					try
					{
						await jobsClient.Cancel(job, cancellationToken).ConfigureAwait(false);  //still has to finish cancelling and propagate back to us
					}
					catch (InsufficientPermissionsException)
					{
						canCancel = false;
					}
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(command), command, "Invalid command!");
			}
		}
	}
}