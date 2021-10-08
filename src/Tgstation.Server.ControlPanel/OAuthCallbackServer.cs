using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Tgstation.Server.ControlPanel
{
	sealed class OAuthCallbackServer : IAsyncDisposable
	{
		public Task<string> Response => responseTcs.Task;

		readonly TaskCompletionSource<string> responseTcs;
		readonly IWebHost webHost;
		readonly CancellationTokenSource cancellationTokenSource;

		Task runTask;

		public OAuthCallbackServer(int callbackPort)
		{
			webHost = new WebHostBuilder()
				.UseKestrel(kestrelOptions =>
				{
					kestrelOptions.ListenLocalhost(
						callbackPort);
				})
				.SuppressStatusMessages(true)
				.ConfigureServices(services => services.AddRouting())
				.Configure(appBuilder =>
				{
					appBuilder.UseRouting();
					appBuilder.UseEndpoints(endpoints =>
					{
						endpoints.MapGet("/", HandleGet);
					});
				})
				.Build();

			cancellationTokenSource = new CancellationTokenSource();
			responseTcs = new TaskCompletionSource<string>();
		}

		public async Task Start(CancellationToken startCancellation)
		{
			var appLifetime = webHost.Services.GetRequiredService<IHostApplicationLifetime>();
			runTask = webHost.RunAsync(cancellationTokenSource.Token);

			var tcs = new TaskCompletionSource();
			using (appLifetime.ApplicationStarted.Register(() => tcs.TrySetResult()))
			using (startCancellation.Register(() => tcs.TrySetCanceled()))
				await tcs.Task;
		}

		async Task HandleGet(HttpContext context)
		{
			if(!context.Request.Query.TryGetValue("code", out var values) || values.Count > 1)
				return;

			var code = values.First();
			responseTcs.TrySetResult(code);

			context.Response.StatusCode = 200;
			await context.Response.WriteAsync("<script type=\"text/javascript\">window.close();</script>", context.RequestAborted);
			await context.Response.CompleteAsync();
		}

		public async ValueTask DisposeAsync()
		{
			responseTcs.TrySetCanceled();
			cancellationTokenSource.Cancel();
			cancellationTokenSource.Dispose();
			if (runTask != null)
				await runTask;
			webHost.Dispose();
		}
	}
}
