using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace PepperDash.Essentials.Plugins.Camera.Axis
{
	/// <summary>
	/// Where and how to reach an Axis device over HTTP. Immutable, so a device that learns a new address
	/// (for example from the bridge) builds a new one and a new client.
	/// </summary>
	public sealed class AxisConnection
	{
		/// <summary>
		/// IP address or hostname
		/// </summary>
		public string Host { get; set; }

		/// <summary>
		/// HTTP(S) port. 0 means 80, or 443 with UseHttps
		/// </summary>
		public int Port { get; set; }

		/// <summary>
		/// Use HTTPS
		/// </summary>
		public bool UseHttps { get; set; }

		/// <summary>
		/// Accept a self signed certificate
		/// </summary>
		public bool IgnoreCertificateErrors { get; set; }

		/// <summary>
		/// Login
		/// </summary>
		public string Username { get; set; }

		/// <summary>
		/// Password
		/// </summary>
		public string Password { get; set; }

		/// <summary>
		/// "digest" (default) answers whatever the device challenges with. "basic" sends a Basic header every time
		/// </summary>
		public string AuthMode { get; set; }

		/// <summary>
		/// Request timeout in milliseconds. 0 means 5000
		/// </summary>
		public int TimeoutMs { get; set; }

		/// <summary>
		/// True when there is a host to talk to
		/// </summary>
		public bool IsUsable
		{
			get { return !string.IsNullOrWhiteSpace(Host); }
		}

		/// <summary>
		/// Port to use, with the default filled in
		/// </summary>
		public int EffectivePort
		{
			get { return Port > 0 ? Port : (UseHttps ? 443 : 80); }
		}

		/// <summary>
		/// Builds the URL for a path (and optional query) on this device
		/// </summary>
		/// <param name="pathAndQuery">For example /axis-cgi/com/ptz.cgi?query=position</param>
		public Uri BuildUri(string pathAndQuery)
		{
			var path = pathAndQuery.StartsWith("/", StringComparison.Ordinal) ? pathAndQuery : "/" + pathAndQuery;

			return new Uri(string.Format("{0}://{1}:{2}{3}", UseHttps ? "https" : "http", Host.Trim(), EffectivePort, path));
		}

		/// <summary>
		/// True when both connections would reach the same device the same way
		/// </summary>
		public bool SameAs(AxisConnection other)
		{
			return other != null
				&& string.Equals(Host, other.Host, StringComparison.OrdinalIgnoreCase)
				&& EffectivePort == other.EffectivePort
				&& UseHttps == other.UseHttps
				&& IgnoreCertificateErrors == other.IgnoreCertificateErrors
				&& Username == other.Username
				&& Password == other.Password
				&& string.Equals(AuthMode, other.AuthMode, StringComparison.OrdinalIgnoreCase)
				&& TimeoutMs == other.TimeoutMs;
		}
	}

	/// <summary>
	/// What came back from an HTTP request
	/// </summary>
	public sealed class AxisHttpResult
	{
		/// <summary>
		/// True when the device answered. False for connection failures, timeouts and so on
		/// </summary>
		public bool GotResponse { get; set; }

		/// <summary>
		/// HTTP status code when GotResponse is true
		/// </summary>
		public int StatusCode { get; set; }

		/// <summary>
		/// HTTP reason phrase
		/// </summary>
		public string ReasonPhrase { get; set; }

		/// <summary>
		/// Response body
		/// </summary>
		public string Body { get; set; }

		/// <summary>
		/// The exception message when there was no response
		/// </summary>
		public string Error { get; set; }

		/// <summary>
		/// True for a 2xx answer
		/// </summary>
		public bool IsSuccessStatusCode
		{
			get { return GotResponse && StatusCode >= 200 && StatusCode < 300; }
		}

		/// <summary>
		/// One line describing a failure, for logs and error feedback
		/// </summary>
		public string Describe()
		{
			if (!GotResponse) return Error;

			return string.Format("HTTP {0} {1}", StatusCode, ReasonPhrase);
		}
	}

	/// <summary>
	/// HTTP client for Axis devices (VAPIX). Handles Digest and Basic authentication, timeouts and logging.
	/// Passwords in request bodies are masked in the log.
	/// </summary>
	public sealed class AxisHttpClient : IDisposable
	{
		private const int DefaultTimeoutMs = 5000;
		private const int MaxLoggedBodyLength = 600;

		private readonly HttpClient client;
		private readonly Action<string> log;

		/// <summary>
		/// The connection this client was built for
		/// </summary>
		public AxisConnection Connection { get; private set; }

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="connection">Where to connect and the login</param>
		/// <param name="log">Receives one line per request and response. May be null</param>
		public AxisHttpClient(AxisConnection connection, Action<string> log)
		{
			Connection = connection;
			this.log = log ?? (message => { });

			var handler = new HttpClientHandler();
			var basic = string.Equals(connection.AuthMode, "basic", StringComparison.OrdinalIgnoreCase);

			// Credentials on the handler answer a Digest or Basic challenge from the device
			if (!basic && !string.IsNullOrEmpty(connection.Username))
			{
				handler.Credentials = new NetworkCredential(connection.Username, connection.Password);
				handler.PreAuthenticate = true;
			}

			if (connection.UseHttps && connection.IgnoreCertificateErrors)
			{
				handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
			}

			client = new HttpClient(handler)
			{
				Timeout = TimeSpan.FromMilliseconds(connection.TimeoutMs > 0 ? connection.TimeoutMs : DefaultTimeoutMs)
			};

			if (basic && !string.IsNullOrEmpty(connection.Username))
			{
				var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(connection.Username + ":" + connection.Password));
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
			}
		}

		/// <summary>
		/// Sends a GET request
		/// </summary>
		/// <param name="label">Names the request in the log, for example the command</param>
		/// <param name="pathAndQuery">For example /axis-cgi/com/ptz.cgi?query=position</param>
		public AxisHttpResult Get(string label, string pathAndQuery)
		{
			var uri = Connection.BuildUri(pathAndQuery);
			log(string.Format("GET {0}", uri));

			return Send(label, uri, () => client.GetAsync(uri));
		}

		/// <summary>
		/// Sends a POST request with a JSON body
		/// </summary>
		/// <param name="label">Names the request in the log, for example the Decoder API method</param>
		/// <param name="pathAndQuery">For example /axis-cgi/decoder.cgi</param>
		/// <param name="json">The request body</param>
		public AxisHttpResult PostJson(string label, string pathAndQuery, string json)
		{
			var uri = Connection.BuildUri(pathAndQuery);
			log(string.Format("POST {0} {1}", uri, Truncate(MaskSecrets(json))));

			// the content must stay alive until the request has been sent, so it is disposed after Send returns
			using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
			{
				return Send(label, uri, () => client.PostAsync(uri, content));
			}
		}

		private AxisHttpResult Send(string label, Uri uri, Func<System.Threading.Tasks.Task<HttpResponseMessage>> request)
		{
			var timer = Stopwatch.StartNew();

			try
			{
				using (var response = request().GetAwaiter().GetResult())
				{
					var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

					log(string.Format("{0} answered HTTP {1} in {2} ms: {3}", label, (int)response.StatusCode, timer.ElapsedMilliseconds, Truncate(MaskSecrets(body))));

					return new AxisHttpResult
					{
						GotResponse = true,
						StatusCode = (int)response.StatusCode,
						ReasonPhrase = response.ReasonPhrase,
						Body = body
					};
				}
			}
			catch (Exception ex)
			{
				log(string.Format("{0} to {1} failed after {2} ms: {3}: {4}", label, uri, timer.ElapsedMilliseconds, ex.GetType().Name, ex.Message));

				return new AxisHttpResult { Error = ex.Message };
			}
		}

		/// <summary>
		/// Replaces the value of every password property in a JSON text, so request bodies can be logged
		/// </summary>
		public static string MaskSecrets(string json)
		{
			return string.IsNullOrEmpty(json)
				? json
				: Regex.Replace(json, @"(""password""\s*:\s*)""(?:[^""\\]|\\.)*""", "$1\"***\"", RegexOptions.IgnoreCase);
		}

		/// <summary>
		/// Hides the password in an rtsp://user:password@host style URL, so it can be logged
		/// </summary>
		public static string MaskUrl(string url)
		{
			return string.IsNullOrEmpty(url) ? url : Regex.Replace(url, @"(://[^/:@]+:)[^@/]*@", "$1***@");
		}

		private static string Truncate(string text)
		{
			if (text == null || text.Length <= MaxLoggedBodyLength) return text;

			return text.Substring(0, MaxLoggedBodyLength) + "...(" + (text.Length - MaxLoggedBodyLength) + " more characters)";
		}

		/// <summary>
		/// Releases the HTTP client
		/// </summary>
		public void Dispose()
		{
			client.Dispose();
		}
	}
}
