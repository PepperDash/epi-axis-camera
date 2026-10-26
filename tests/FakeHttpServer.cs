using System.Net;
using System.Text;

namespace PepperDash.Essentials.Plugins.Camera.Axis.Tests;

/// <summary>
/// A local HTTP server that stands in for an Axis device. Records every request and answers with whatever the handler returns:
/// (status code, body, WWW-Authenticate challenge or null)
/// </summary>
public record RecordedRequest(string Method, string Path, string ContentType, string Authorization, string Body, string Query = "");

public sealed class FakeHttpServer : IDisposable
{
	private readonly HttpListener listener = new();
	private readonly Func<RecordedRequest, (int, string, string)> handler;
	private readonly object sync = new();

	public List<RecordedRequest> Requests { get; } = new();
	public int Port { get; }

	public FakeHttpServer(Func<RecordedRequest, (int, string, string)> handler)
	{
		this.handler = handler;
		Port = Start();
	}

	private int Start()
	{
		// Ask the OS for a free port, then bind the listener to it
		var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
		probe.Start();
		var port = ((IPEndPoint)probe.LocalEndpoint).Port;
		probe.Stop();

		listener.Prefixes.Add($"http://127.0.0.1:{port}/");
		listener.Start();
		Task.Run(Serve);
		return port;
	}

	private async Task Serve()
	{
		while (listener.IsListening)
		{
			HttpListenerContext context;
			try
			{
				context = await listener.GetContextAsync();
			}
			catch
			{
				return;
			}

			var req = context.Request;
			string body;
			using (var reader = new StreamReader(req.InputStream, Encoding.UTF8))
			{
				body = await reader.ReadToEndAsync();
			}

			var recorded = new RecordedRequest(req.HttpMethod, req.Url!.AbsolutePath, req.ContentType, req.Headers["Authorization"], body, req.Url.Query);
			lock (sync)
			{
				Requests.Add(recorded);
			}

			var (status, text, challenge) = handler(recorded);

			try
			{
				context.Response.StatusCode = status;
				if (challenge != null)
				{
					context.Response.AddHeader("WWW-Authenticate", challenge);
				}

				var bytes = Encoding.UTF8.GetBytes(text);
				context.Response.ContentType = "application/json";
				await context.Response.OutputStream.WriteAsync(bytes);
				context.Response.Close();
			}
			catch
			{
				// the client may already have given up (timeout test)
			}
		}
	}

	public void Dispose()
	{
		listener.Close();
	}
}
