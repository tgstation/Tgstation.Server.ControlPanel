using Octokit.Internal;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Client;

namespace Tgstation.Server.ControlPanel
{
	sealed class LoggingHandler : DelegatingHandler
	{
		readonly IRequestLogger requestLogger;

		public LoggingHandler(IRequestLogger requestLogger)
		{
			this.requestLogger = requestLogger ?? throw new ArgumentNullException(nameof(requestLogger));
			InnerHandler = HttpMessageHandlerFactory.CreateDefault();
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			await requestLogger.LogRequest(request, cancellationToken).ConfigureAwait(false);
			var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
			await requestLogger.LogResponse(response, cancellationToken).ConfigureAwait(false);
			return response;
		}
	}
}