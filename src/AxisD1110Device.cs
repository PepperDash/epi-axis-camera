using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Queues;
using Newtonsoft.Json.Linq;

namespace PepperDash.Essentials.Plugins.Camera.Axis
{
	/// <summary>
	/// Axis D1110 video decoder. Tells the decoder which camera stream to show through the VAPIX Decoder API
	/// </summary>
	/// <remarks>
	/// The decoder has no "select input" command. Selecting a stream sends a complete view configuration
	/// (one full screen pane showing one stream), which replaces whatever the decoder was showing.
	/// The decoder does not report which stream it is showing, so the selected stream feedback is the last
	/// stream this device sent. A change in the decoder's configuration id that this device did not cause
	/// clears the feedback.
	/// </remarks>
	public class AxisD1110Device : EssentialsBridgeableDevice
	{
		private const long DefaultPollTimeMs = 30000;
		private const int MaxStreams = 16;
		private const int DecoderConfigurationChangedCode = 100;

		private const string SelectPrefix = "select:";
		private const string PollCommand = "poll";

		private readonly AxisD1110Config config;
		private readonly AxisD1110DecoderClient client;
		private readonly GenericQueue commandQueue;
		private readonly Dictionary<int, AxisD1110StreamConfig> streams = new Dictionary<int, AxisD1110StreamConfig>();
		private readonly Dictionary<int, string> bridgeUrls = new Dictionary<int, string>();

		private CTimer pollTimer;
		private bool online;
		private int selectedStream;
		private bool selectionMade;
		private string configurationId = string.Empty;
		private int lastErrorCode;
		private string lastError = string.Empty;
		private bool pollFailureLogged;

		/// <summary>
		/// Decoder answered the last request
		/// </summary>
		public BoolFeedback OnlineFeedback { get; private set; }

		/// <summary>
		/// Stream last sent to the decoder (1 to 16). 0 means blank or unknown
		/// </summary>
		public IntFeedback SelectedStreamFeedback { get; private set; }

		/// <summary>
		/// Name of the selected stream
		/// </summary>
		public StringFeedback SelectedStreamNameFeedback { get; private set; }

		/// <summary>
		/// Last Decoder API error code. 0 when the last request had no error
		/// </summary>
		public IntFeedback LastErrorCodeFeedback { get; private set; }

		/// <summary>
		/// Last Decoder API error message
		/// </summary>
		public StringFeedback LastErrorFeedback { get; private set; }

		/// <summary>
		/// Decoder view configuration id
		/// </summary>
		public StringFeedback ConfigurationIdFeedback { get; private set; }

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="key">Device key</param>
		/// <param name="name">Device name</param>
		/// <param name="config">Device properties</param>
		public AxisD1110Device(string key, string name, AxisD1110Config config)
			: base(key, name)
		{
			this.LogInformation("Constructing new {0} instance", name);

			this.config = config;

			foreach (var pair in config.Streams)
			{
				int number;
				if (!int.TryParse(pair.Key, out number) || number < 1 || number > MaxStreams)
				{
					this.LogWarning("Ignoring stream key {key}. Stream keys must be numbers from 1 to {max}", pair.Key, MaxStreams);
					continue;
				}

				streams[number] = pair.Value;
			}

			client = new AxisD1110DecoderClient(config, key, message => this.LogDebug("{message}", message));
			commandQueue = new GenericQueue(key + "-queue");

			this.LogInformation(
				"Configured for {host}:{port} https={https} auth={auth} streamSource={source} resolution={resolution} streams={count} defaultStream={default} pollTimeMs={poll}",
				config.Host,
				config.Port > 0 ? config.Port : (config.UseHttps ? 443 : 80),
				config.UseHttps,
				string.IsNullOrEmpty(config.AuthMode) ? "digest" : config.AuthMode,
				config.IsBridgeSource ? "bridge" : "config",
				string.IsNullOrEmpty(config.Resolution) ? "as received" : config.Resolution,
				streams.Count,
				config.DefaultStream,
				config.PollTimeMs == 0 ? DefaultPollTimeMs : config.PollTimeMs);

			OnlineFeedback = new BoolFeedback("online", () => online);
			SelectedStreamFeedback = new IntFeedback("selectedStream", () => selectedStream);
			SelectedStreamNameFeedback = new StringFeedback("selectedStreamName", () => StreamName(selectedStream));
			LastErrorCodeFeedback = new IntFeedback("lastErrorCode", () => lastErrorCode);
			LastErrorFeedback = new StringFeedback("lastError", () => lastError);
			ConfigurationIdFeedback = new StringFeedback("configurationId", () => configurationId);

			AddPostActivationAction(StartPolling);
		}

		/// <summary>
		/// Shows a configured stream on the decoder
		/// </summary>
		/// <param name="stream">Stream number (1 to 16). 0 blanks the output</param>
		public void SelectStream(int stream)
		{
			this.LogInformation("Select stream {stream} requested ({name})", stream, StreamName(stream));
			commandQueue.Enqueue(new ProcessStringMessage(SelectPrefix + stream, ProcessCommand));
		}

		/// <summary>
		/// Blanks the decoder output
		/// </summary>
		public void Clear()
		{
			SelectStream(0);
		}

		/// <summary>
		/// Polls the decoder now
		/// </summary>
		public void Poll()
		{
			this.LogDebug("Poll requested");
			commandQueue.Enqueue(new ProcessStringMessage(PollCommand, ProcessCommand));
		}

		/// <summary>
		/// Shows a configured stream by its name. For DEVJSON testing
		/// </summary>
		/// <param name="name">Stream name from the config, not case sensitive</param>
		public void SelectStreamByName(string name)
		{
			foreach (var pair in streams)
			{
				if (string.Equals(pair.Value.Name, name, StringComparison.OrdinalIgnoreCase))
				{
					SelectStream(pair.Key);
					return;
				}
			}

			this.LogWarning("No stream named {name} is configured", name);
		}

		/// <summary>
		/// Logs the current state of the device. For DEVJSON testing
		/// </summary>
		public void PrintStatus()
		{
			this.LogInformation(
				"Status: online={online} selectedStream={stream} ({name}) configurationId={id} lastError={code} {error} polling={polling}",
				online, selectedStream, StreamName(selectedStream), configurationId, lastErrorCode, lastError, pollTimer != null);

			for (var number = 1; number <= MaxStreams; number++)
			{
				AxisD1110StreamConfig configured;
				streams.TryGetValue(number, out configured);
				string bridgeUrl;
				lock (bridgeUrls)
				{
					bridgeUrls.TryGetValue(number, out bridgeUrl);
				}

				if (configured != null && !string.IsNullOrEmpty(configured.CameraKey))
				{
					this.LogInformation("Stream {number}: {name} is camera {camera}", number, StreamName(number), configured.CameraKey);
					continue;
				}

				var source = config.IsBridgeSource ? bridgeUrl : (configured == null ? null : configured.Url);
				if (string.IsNullOrEmpty(source)) continue;

				this.LogInformation("Stream {number}: {name} source url {url}", number, StreamName(number), AxisD1110DecoderClient.MaskUrl(source));
			}
		}

		/// <summary>
		/// Stores the URL SIMPL sent for a stream. Used when streamSource is bridge. If it is the stream on screen
		/// and the URL changed, the new URL is sent to the decoder
		/// </summary>
		/// <param name="number">Stream number (1 to 16)</param>
		/// <param name="url">RTSP URL, may carry the login</param>
		public void SetBridgeStreamUrl(int number, string url)
		{
			if (number < 1 || number > MaxStreams) return;

			url = url == null ? string.Empty : url.Trim();

			string previous;
			lock (bridgeUrls)
			{
				bridgeUrls.TryGetValue(number, out previous);
				if (previous == url) return;
				bridgeUrls[number] = url;
			}

			this.LogInformation("Bridge sent URL for stream {number}: {url}", number, string.IsNullOrEmpty(url) ? "(empty)" : AxisD1110DecoderClient.MaskUrl(url));

			if (number == selectedStream)
			{
				this.LogInformation("Stream {number} is on screen and its URL changed, sending it again", number);
				SelectStream(number);
			}
		}

		/// <summary>
		/// Sends any Decoder API method and logs the answer. For DEVJSON testing of the decoder, for example
		/// {"deviceKey":"d1110-1","methodName":"SendRequest","params":["getCapabilities",""]}
		/// </summary>
		/// <param name="method">Decoder API method, for example getCapabilities</param>
		/// <param name="paramsJson">The params object as JSON text. Empty for methods that take none</param>
		public void SendRequest(string method, string paramsJson)
		{
			this.LogInformation("Raw request {method} {params}", method, AxisD1110DecoderClient.MaskSecrets(paramsJson));

			commandQueue.Enqueue(new ProcessStringMessage(method, m =>
			{
				try
				{
					var parameters = string.IsNullOrWhiteSpace(paramsJson) ? null : JObject.Parse(paramsJson);
					var result = client.Send(m, parameters);
					this.LogInformation(
						"Raw {method} result: success={success} configurationId={id} errorCode={code} {error}",
						m, result.Success, result.ConfigurationId, result.ErrorCode, result.ErrorMessage);
				}
				catch (Exception ex)
				{
					this.LogError(ex, "Raw request {method} failed", m);
				}
			}));
		}

		private void StartPolling()
		{
			var pollTimeMs = config.PollTimeMs == 0 ? DefaultPollTimeMs : config.PollTimeMs;

			this.LogInformation("Starting polling. First poll in 1000 ms, then every {interval} ms", pollTimeMs > 0 ? pollTimeMs : 0);

			// first poll shortly after activation, then every pollTimeMs. A negative pollTimeMs polls once
			pollTimer = new CTimer(o => Poll(), null, 1000, pollTimeMs > 0 ? pollTimeMs : Timeout.Infinite);
		}

		private void ProcessCommand(string command)
		{
			this.LogDebug("Processing command {command}", command);

			try
			{
				if (command == PollCommand)
				{
					ProcessPoll();
				}
				else if (command.StartsWith(SelectPrefix, StringComparison.Ordinal))
				{
					ProcessSelect(int.Parse(command.Substring(SelectPrefix.Length)));
				}
			}
			catch (Exception ex)
			{
				this.LogError(ex, "Error processing command {command}", command);
			}
		}

		private void ProcessSelect(int number)
		{
			AxisD1110StreamConfig stream = null;

			if (number != 0)
			{
				AxisD1110StreamConfig configured;
				streams.TryGetValue(number, out configured);

				string bridgeUrl;
				lock (bridgeUrls)
				{
					bridgeUrls.TryGetValue(number, out bridgeUrl);
				}

				string error = null;
				if (number < 1 || number > MaxStreams)
				{
					error = "stream numbers are 1 to " + MaxStreams;
				}
				else
				{
					var camera = configured == null || string.IsNullOrEmpty(configured.CameraKey)
						? null
						: DeviceManager.GetDeviceForKey(configured.CameraKey) as IAxisCameraSource;

					AxisD1110StreamBuilder.TryBuild(config, configured, bridgeUrl, camera, out stream, out error);
				}

				if (stream == null)
				{
					this.LogWarning("Stream {number} cannot be used: {error}", number, error);
					SetError(-3, string.Format("Stream {0}: {1}", number, error));
					return;
				}

				this.LogInformation("Selecting stream {number} {name}: {url} login={login} resolution={resolution}",
					number, StreamName(number), stream.Url, string.IsNullOrEmpty(stream.Username) ? "none" : stream.Username, ResolutionOf(stream.Url));
			}
			else
			{
				this.LogInformation("Blanking the decoder output");
			}

			var result = client.SetViewConfiguration(stream);
			RecordResult(result);

			if (!result.Success)
			{
				this.LogWarning("Select stream {number} failed: {code} {message}", number, result.ErrorCode, result.ErrorMessage);
				return;
			}

			this.LogInformation("Stream {number} ({name}) applied. Decoder configurationId={id}. Decoder reported error {code} {message}",
				number, StreamName(number), result.ConfigurationId, result.ErrorCode, result.ErrorMessage);

			selectionMade = true;
			selectedStream = number;
			configurationId = result.ConfigurationId ?? string.Empty;
			SetOnline(true);
			SelectedStreamFeedback.FireUpdate();
			SelectedStreamNameFeedback.FireUpdate();
			ConfigurationIdFeedback.FireUpdate();
		}

		private void ProcessPoll()
		{
			var result = client.GetViewConfiguration();
			RecordResult(result);

			var wasOnline = online;
			SetOnline(result.Success);

			if (!result.Success)
			{
				// warn on the first failure and when the decoder drops off, then stay at debug so a dead decoder does not flood the log
				if (wasOnline || !pollFailureLogged)
				{
					this.LogWarning("Poll failed, decoder is not answering: {code} {message}", result.ErrorCode, result.ErrorMessage);
				}
				else
				{
					this.LogDebug("Poll failed: {code} {message}", result.ErrorCode, result.ErrorMessage);
				}

				pollFailureLogged = true;
				return;
			}

			pollFailureLogged = false;
			this.LogDebug("Poll ok. configurationId={id}", result.ConfigurationId);

			var newId = result.ConfigurationId ?? string.Empty;

			if (configurationId.Length > 0 && newId != configurationId && selectedStream != 0)
			{
				this.LogWarning("Decoder configuration changed outside this plugin ({old} to {new}). Selected stream is now unknown", configurationId, newId);
				selectedStream = 0;
				SelectedStreamFeedback.FireUpdate();
				SelectedStreamNameFeedback.FireUpdate();
			}

			configurationId = newId;
			ConfigurationIdFeedback.FireUpdate();

			if (!wasOnline && !selectionMade && config.DefaultStream > 0)
			{
				ProcessSelect(config.DefaultStream);
			}
		}

		private string StreamName(int number)
		{
			if (number < 1) return string.Empty;

			AxisD1110StreamConfig stream;
			if (streams.TryGetValue(number, out stream))
			{
				if (!string.IsNullOrEmpty(stream.Name)) return stream.Name;

				// a stream that is a camera is called what the camera is called
				var camera = string.IsNullOrEmpty(stream.CameraKey) ? null : DeviceManager.GetDeviceForKey(stream.CameraKey) as IKeyName;
				if (camera != null && !string.IsNullOrEmpty(camera.Name)) return camera.Name;
			}

			return "Stream " + number;
		}

		private static string ResolutionOf(string url)
		{
			AxisStreamUrl parsed;
			if (!AxisStreamUrl.TryParse(url, out parsed)) return "unknown";

			foreach (var part in parsed.Query.Split('&'))
			{
				if (part.StartsWith("resolution=", StringComparison.OrdinalIgnoreCase)) return part.Substring("resolution=".Length);
			}

			return "not set";
		}

		private void SetOnline(bool value)
		{
			if (online == value) return;

			this.LogInformation("Decoder is now {state}", value ? "online" : "offline");

			online = value;
			OnlineFeedback.FireUpdate();
		}

		// Code 100 only tells us the configuration changed. It is not an error
		private void RecordResult(AxisD1110Result result)
		{
			if (result.ErrorCode == DecoderConfigurationChangedCode)
			{
				SetError(0, string.Empty);
				return;
			}

			SetError(result.ErrorCode, result.ErrorMessage);
		}

		private void SetError(int code, string message)
		{
			lastErrorCode = code;
			lastError = message ?? string.Empty;
			LastErrorCodeFeedback.FireUpdate();
			LastErrorFeedback.FireUpdate();
		}

		#region Overrides of EssentialsBridgeableDevice

		/// <summary>
		/// Links the device to the EISC bridge
		/// </summary>
		public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
		{
			var indexed = config.IsIndexedLayout;

			if (indexed && (config.DecoderIndex < 1 || config.DecoderIndex > AxisD1110BridgeJoinMap.MaxIndexedDecoders))
			{
				this.LogWarning("joinLayout is indexed but decoderIndex is {index}. It must be 1 to {max}, using the contiguous layout",
					config.DecoderIndex, AxisD1110BridgeJoinMap.MaxIndexedDecoders);
				indexed = false;
			}

			if (indexed && config.IsBridgeSource)
			{
				this.LogWarning("streamSource bridge needs the stream URL joins, which the indexed layout does not have. Stream URLs from SIMPL will not arrive");
			}

			var joinMap = new AxisD1110BridgeJoinMap(joinStart, indexed ? config.DecoderIndex : 0);

			// This adds the join map to the collection on the bridge
			bridge?.AddJoinMap(Key, joinMap);

			var customJoins = JoinMapHelper.TryGetJoinMapAdvancedForDevice(joinMapKey);

			if (customJoins != null)
			{
				joinMap.SetCustomJoinData(customJoins);
			}

			this.LogDebug("Linking to Trilist {id} at join start {start}", trilist.ID.ToString("X"), joinStart);
			this.LogInformation("Linking to Bridge Type {type}", GetType().Name);

			trilist.SetString(joinMap.DeviceName.JoinNumber, Name);

			trilist.SetUShortSigAction(joinMap.SelectStream.JoinNumber, value => SelectStream(value));
			trilist.SetSigTrueAction(joinMap.Clear.JoinNumber, Clear);
			trilist.SetSigTrueAction(joinMap.Refresh.JoinNumber, Poll);

			this.LogInformation("Linked ({layout} layout): select stream is analog join {select}, online is digital join {online}",
				indexed ? "indexed" : "contiguous", joinMap.SelectStream.JoinNumber, joinMap.IsOnline.JoinNumber);

			if (config.IsBridgeSource && !indexed)
			{
				this.LogInformation("Stream URLs come from the bridge, serial joins {start} to {end}", joinMap.StreamUrls.JoinNumber, joinMap.StreamUrls.JoinNumber + MaxStreams - 1);

				for (var i = 0; i < MaxStreams; i++)
				{
					var number = i + 1;
					trilist.SetStringSigAction(joinMap.StreamUrls.JoinNumber + (uint)i, url => SetBridgeStreamUrl(number, url));
				}
			}

			OnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
			SelectedStreamFeedback.LinkInputSig(trilist.UShortInput[joinMap.SelectStream.JoinNumber]);
			LastErrorCodeFeedback.LinkInputSig(trilist.UShortInput[joinMap.LastErrorCode.JoinNumber]);
			SelectedStreamNameFeedback.LinkInputSig(trilist.StringInput[joinMap.SelectedStreamName.JoinNumber]);
			LastErrorFeedback.LinkInputSig(trilist.StringInput[joinMap.LastError.JoinNumber]);
			ConfigurationIdFeedback.LinkInputSig(trilist.StringInput[joinMap.ConfigurationId.JoinNumber]);

			UpdateFeedbacks(trilist, joinMap, indexed);

			trilist.OnlineStatusChange += (o, a) =>
			{
				if (!a.DeviceOnLine) return;

				UpdateFeedbacks(trilist, joinMap, indexed);
			};
		}

		private void UpdateFeedbacks(BasicTriList trilist, AxisD1110BridgeJoinMap joinMap, bool indexed)
		{
			trilist.SetString(joinMap.DeviceName.JoinNumber, Name);

			// the stream names have no joins in the indexed layout
			for (var number = 1; number <= MaxStreams && !indexed; number++)
			{
				AxisD1110StreamConfig configured;
				streams.TryGetValue(number, out configured);
				trilist.SetString(joinMap.StreamNames.JoinNumber + (uint)(number - 1), configured == null ? string.Empty : StreamName(number));
			}

			OnlineFeedback.FireUpdate();
			SelectedStreamFeedback.FireUpdate();
			SelectedStreamNameFeedback.FireUpdate();
			LastErrorCodeFeedback.FireUpdate();
			LastErrorFeedback.FireUpdate();
			ConfigurationIdFeedback.FireUpdate();
		}

		#endregion
	}
}
