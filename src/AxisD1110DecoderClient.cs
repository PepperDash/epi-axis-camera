using System;
using Newtonsoft.Json.Linq;

namespace PepperDash.Essentials.Plugins.Camera.Axis
{
	/// <summary>
	/// Result of one Decoder API call
	/// </summary>
	public class AxisD1110Result
	{
		/// <summary>
		/// True when the decoder answered with a data object (the request was applied)
		/// </summary>
		public bool Success { get; set; }

		/// <summary>
		/// Active view configuration id, when the decoder returned one
		/// </summary>
		public string ConfigurationId { get; set; }

		/// <summary>
		/// First Decoder API error code. Negative values are transport errors (-1 no answer, -2 bad response).
		/// HTTP failures report their HTTP status code. 0 when there was no error
		/// </summary>
		public int ErrorCode { get; set; }

		/// <summary>
		/// Error message that goes with ErrorCode
		/// </summary>
		public string ErrorMessage { get; set; }
	}

	/// <summary>
	/// Minimal client for the Axis VAPIX Decoder API (POST /axis-cgi/decoder.cgi)
	/// </summary>
	public class AxisD1110DecoderClient : IDisposable
	{
		private const string ApiVersion = "1.0";
		private const string DecoderPath = "/axis-cgi/decoder.cgi";

		private readonly AxisHttpClient http;
		private readonly string context;

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="config">Device configuration</param>
		/// <param name="context">Echoed back by the decoder, used to tell our requests apart in its logs</param>
		/// <param name="log">Receives one line per request and response. Passwords are masked. May be null</param>
		public AxisD1110DecoderClient(AxisD1110Config config, string context, Action<string> log = null)
		{
			this.context = context;
			http = new AxisHttpClient(config.ToConnection(), log);
		}

		/// <summary>
		/// Asks the decoder for its active view configuration id. Used as a poll
		/// </summary>
		public AxisD1110Result GetViewConfiguration()
		{
			return Send("getViewConfiguration", null);
		}

		/// <summary>
		/// Shows one stream full screen. A null stream sends an empty configuration, which blanks the output
		/// </summary>
		/// <param name="stream">Stream to show, or null to blank</param>
		public AxisD1110Result SetViewConfiguration(AxisD1110StreamConfig stream)
		{
			return Send("setViewConfiguration", BuildViewConfiguration(stream));
		}

		/// <summary>
		/// Builds the params object for setViewConfiguration: one full screen pane, one stream, one view
		/// </summary>
		public static JObject BuildViewConfiguration(AxisD1110StreamConfig stream)
		{
			if (stream == null)
			{
				return new JObject
				{
					{ "panes", new JArray() },
					{ "streams", new JArray() },
					{ "views", new JArray() }
				};
			}

			return new JObject
			{
				{
					"panes", new JArray
					{
						new JObject { { "paneId", 0 }, { "left", 0.0 }, { "top", 0.0 }, { "right", 1.0 }, { "bottom", 1.0 } }
					}
				},
				{
					"streams", new JArray
					{
						new JObject
						{
							{ "streamId", 0 },
							{ "url", stream.Url },
							{ "videoCodec", string.IsNullOrEmpty(stream.VideoCodec) ? "H264" : stream.VideoCodec },
							{ "audioCodec", stream.AudioCodec ?? string.Empty },
							{ "username", stream.Username ?? string.Empty },
							{ "password", stream.Password ?? string.Empty }
						}
					}
				},
				{
					"views", new JArray
					{
						new JObject
						{
							{ "viewId", 0 },
							{ "duration", 0 },
							{ "segments", new JArray { new JObject { { "stream", 0 }, { "pane", 0 } } } }
						}
					}
				}
			};
		}

		/// <summary>
		/// Sends any Decoder API method and returns the parsed result. Used by the device's DEVJSON commands to probe the decoder
		/// </summary>
		/// <param name="method">Decoder API method name, for example getCapabilities</param>
		/// <param name="parameters">The params object, or null for methods that take none</param>
		public AxisD1110Result Send(string method, JObject parameters)
		{
			var request = new JObject
			{
				{ "apiVersion", ApiVersion },
				{ "context", context },
				{ "method", method }
			};

			if (parameters != null)
			{
				request.Add("params", parameters);
			}

			var response = http.PostJson(method, DecoderPath, request.ToString(Newtonsoft.Json.Formatting.None));

			if (!response.GotResponse)
			{
				return new AxisD1110Result { ErrorCode = -1, ErrorMessage = response.Error };
			}

			if (!response.IsSuccessStatusCode)
			{
				return new AxisD1110Result { ErrorCode = response.StatusCode, ErrorMessage = response.Describe() };
			}

			return ParseResponse(response.Body);
		}

		/// <summary>
		/// Reads a decoder.cgi response. "data" means the request was applied. "errors" can accompany data
		/// (for example a stream that did not connect) and always carries the first error code and message
		/// </summary>
		public static AxisD1110Result ParseResponse(string body)
		{
			JObject json;
			try
			{
				json = JObject.Parse(body);
			}
			catch (Exception ex)
			{
				return new AxisD1110Result { ErrorCode = -2, ErrorMessage = "Unreadable response: " + ex.Message };
			}

			var result = new AxisD1110Result();

			var data = json["data"] as JObject;
			if (data != null)
			{
				result.Success = true;
				result.ConfigurationId = data.Value<string>("configurationId");
			}

			var errors = json["errors"] as JArray;
			if (errors != null && errors.Count > 0)
			{
				result.ErrorCode = errors[0].Value<int?>("code") ?? 0;
				result.ErrorMessage = errors[0].Value<string>("message");
			}
			else if (data == null)
			{
				result.ErrorCode = -2;
				result.ErrorMessage = "Response had no data and no errors";
			}

			return result;
		}

		/// <summary>
		/// Replaces the value of every password property in a JSON text, so request bodies can be logged
		/// </summary>
		public static string MaskSecrets(string json)
		{
			return AxisHttpClient.MaskSecrets(json);
		}

		/// <summary>
		/// Hides the password in an rtsp://user:password@host style URL, so it can be logged
		/// </summary>
		public static string MaskUrl(string url)
		{
			return AxisHttpClient.MaskUrl(url);
		}

		/// <summary>
		/// Releases the HTTP client
		/// </summary>
		public void Dispose()
		{
			http.Dispose();
		}
	}
}
