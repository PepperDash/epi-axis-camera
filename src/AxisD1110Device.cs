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
	/// The decoder has no "select input" command. Selecting a stream sends a complete view configuration,
	/// which replaces whatever the decoder was showing. The decoder is driven with two panes, like a quad
	/// multiviewer following two touch panel video windows: one stream is shown full screen, two are shown
	/// side by side, none blanks the output. Selecting on either pane sends both again.
	/// The decoder does not report which stream it is showing, so the selected stream feedbacks are the last
	/// streams this device sent. A change in the decoder's configuration id that this device did not cause
	/// clears the feedbacks.
	/// </remarks>
	public class AxisD1110Device : EssentialsBridgeableDevice
	{
		private const long DefaultPollTimeMs = 30000;
		private const int MaxStreams = AxisD1110Select.MaxStreams;
		private const int MaxBridgeStreams = AxisD1110BridgeJoinMap.MaxBridgeStreams;
		private const int PaneCount = AxisD1110Layout.PaneCount;
		private const int DecoderConfigurationChangedCode = 100;

		private const string SelectPrefix = "select:";
		private const string Select2Prefix = "select2:";
		private const string ClearCommand = "clear";
		private const string ApplyCommand = "apply";
		private const string PollCommand = "poll";
		private const string PrivacyCommand = "privacy";

		private readonly AxisD1110Config config;
		private readonly AxisD1110DecoderClient client;
		private readonly GenericQueue commandQueue;
		private readonly Dictionary<int, AxisD1110StreamConfig> streams = new Dictionary<int, AxisD1110StreamConfig>();
		private readonly Dictionary<int, string> bridgeUrls = new Dictionary<int, string>();

		// per pane: the stream last sent (0 = none), and whether the pane was left out because its camera is in privacy
		private readonly int[] selectedStreams = new int[PaneCount];
		private readonly bool[] paneBlankedByPrivacy = new bool[PaneCount];

		private CTimer pollTimer;
		private bool online;
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
		/// Stream last sent to the decoder for the first pane (1 to 64). 0 means none or unknown
		/// </summary>
		public IntFeedback SelectedStreamFeedback { get; private set; }

		/// <summary>
		/// Stream last sent to the decoder for the second pane, shown side by side with the first (1 to 64). 0 means none or unknown
		/// </summary>
		public IntFeedback SelectedStream2Feedback { get; private set; }

		/// <summary>
		/// Name of the selected stream (first pane)
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
				if (!AxisD1110Select.TryParseStreamKey(pair.Key, out number))
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
			SelectedStreamFeedback = new IntFeedback("selectedStream", () => selectedStreams[0]);
			SelectedStream2Feedback = new IntFeedback("selectedStream2", () => selectedStreams[1]);
			SelectedStreamNameFeedback = new StringFeedback("selectedStreamName", () => StreamName(selectedStreams[0]));
			LastErrorCodeFeedback = new IntFeedback("lastErrorCode", () => lastErrorCode);
			LastErrorFeedback = new StringFeedback("lastError", () => lastError);
			ConfigurationIdFeedback = new StringFeedback("configurationId", () => configurationId);

			AddPostActivationAction(StartPolling);
			AddPostActivationAction(SubscribeToCameras);
		}

		/// <summary>
		/// Shows a configured stream in the first pane. With a second stream selected the two are shown side by side,
		/// otherwise this one is full screen
		/// </summary>
		/// <param name="stream">Stream number (1 to 64). 0 empties the first pane (blanks the output when the second is empty too)</param>
		public void SelectStream(int stream)
		{
			this.LogInformation("Select stream {stream} requested ({name})", stream, StreamName(stream));
			commandQueue.Enqueue(new ProcessStringMessage(SelectPrefix + stream, ProcessCommand));
		}

		/// <summary>
		/// Shows a configured stream in the second pane, side by side with the first. Alone it is full screen
		/// </summary>
		/// <param name="stream">Stream number (1 to 64). 0 empties the second pane</param>
		public void SelectStream2(int stream)
		{
			this.LogInformation("Select second stream {stream} requested ({name})", stream, StreamName(stream));
			commandQueue.Enqueue(new ProcessStringMessage(Select2Prefix + stream, ProcessCommand));
		}

		/// <summary>
		/// Empties both panes, which blanks the decoder output
		/// </summary>
		public void Clear()
		{
			this.LogInformation("Clear requested");
			commandQueue.Enqueue(new ProcessStringMessage(ClearCommand, ProcessCommand));
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
				"Status: online={online} selectedStream={stream} ({name}) selectedStream2={stream2} ({name2}) configurationId={id} lastError={code} {error} polling={polling}",
				online, selectedStreams[0], StreamName(selectedStreams[0]), selectedStreams[1], StreamName(selectedStreams[1]),
				configurationId, lastErrorCode, lastError, pollTimer != null);

			for (var pane = 0; pane < PaneCount; pane++)
			{
				this.LogInformation("Pane {pane}: stream {stream} ({name}) hiddenByPrivacy={hidden}",
					pane + 1, selectedStreams[pane], StreamName(selectedStreams[pane]), paneBlankedByPrivacy[pane]);
			}

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
		/// Stores the URL SIMPL sent for a stream. Used when streamSource is bridge. If the stream is selected in either
		/// pane and the URL changed, the layout is sent to the decoder again
		/// </summary>
		/// <param name="number">Stream number (1 to 64). The bridge has URL joins for streams 1 to 16</param>
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

			if (Array.IndexOf(selectedStreams, number) >= 0)
			{
				this.LogInformation("Stream {number} is on screen and its URL changed, sending it again", number);
				commandQueue.Enqueue(new ProcessStringMessage(ApplyCommand, ProcessCommand));
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

		// the select joins may be driven by something that also carries numbers that are not streams (a display's selected source)
		private void OnSelectJoin(ushort value)
		{
			if (!AxisD1110Select.ShouldSelect(value, config.SelectOnlyConfiguredStreams, IsConfiguredStream))
			{
				this.LogDebug("Ignoring select value {value}: it is not a configured stream", value);
				return;
			}

			SelectStream(value);
		}

		private void OnSelect2Join(ushort value)
		{
			if (!AxisD1110Select.ShouldSelect(value, config.SelectOnlyConfiguredStreams, IsConfiguredStream))
			{
				this.LogDebug("Ignoring second stream select value {value}: it is not a configured stream", value);
				return;
			}

			SelectStream2(value);
		}

		private bool IsConfiguredStream(int number)
		{
			if (streams.ContainsKey(number)) return true;

			string url;
			lock (bridgeUrls)
			{
				return bridgeUrls.TryGetValue(number, out url) && !string.IsNullOrEmpty(url);
			}
		}

		// privacy on a camera hides its video everywhere, so a decoder pane showing that camera is left out until privacy ends
		private void SubscribeToCameras()
		{
			var followed = new HashSet<string>();

			foreach (var stream in streams.Values)
			{
				if (string.IsNullOrEmpty(stream.CameraKey) || !followed.Add(stream.CameraKey)) continue;

				var camera = DeviceManager.GetDeviceForKey(stream.CameraKey) as IAxisCameraSource;
				if (camera == null)
				{
					this.LogWarning("Camera {key} was not found, so privacy on it cannot hide this decoder", stream.CameraKey);
					continue;
				}

				camera.PrivacyChanged += OnCameraPrivacyChanged;
				this.LogInformation("Following privacy of camera {key}", stream.CameraKey);
			}
		}

		private void OnCameraPrivacyChanged(object sender, EventArgs e)
		{
			commandQueue.Enqueue(new ProcessStringMessage(PrivacyCommand, ProcessCommand));
		}

		private void ProcessPrivacy()
		{
			var hides = new bool[PaneCount];
			for (var pane = 0; pane < PaneCount; pane++)
			{
				var camera = CameraOf(selectedStreams[pane]);
				hides[pane] = camera != null && camera.PrivacyHidesVideo;
			}

			if (!AxisPrivacy.PanesNeedApply(hides, paneBlankedByPrivacy)) return;

			for (var pane = 0; pane < PaneCount; pane++)
			{
				var action = AxisPrivacy.Plan(hides[pane], paneBlankedByPrivacy[pane]);
				if (action == AxisPrivacyAction.None) continue;

				this.LogInformation("Privacy changed on camera {key} (hides video: {hides}). Stream {number} in pane {pane}: {action}",
					streams[selectedStreams[pane]].CameraKey, hides[pane], selectedStreams[pane], pane + 1, action);
			}

			// sending the layout again leaves out the panes whose camera is in privacy and brings back the ones whose privacy is over
			ApplyPanes((int[])selectedStreams.Clone());
		}

		// the camera a stream names, or null when the stream has no cameraKey or the camera does not exist
		private IAxisCameraSource CameraOf(int number)
		{
			if (number < 1) return null;

			AxisD1110StreamConfig configured;
			if (!streams.TryGetValue(number, out configured) || string.IsNullOrEmpty(configured.CameraKey)) return null;

			return DeviceManager.GetDeviceForKey(configured.CameraKey) as IAxisCameraSource;
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
					ProcessSelect(0, int.Parse(command.Substring(SelectPrefix.Length)));
				}
				else if (command.StartsWith(Select2Prefix, StringComparison.Ordinal))
				{
					ProcessSelect(1, int.Parse(command.Substring(Select2Prefix.Length)));
				}
				else if (command == ClearCommand)
				{
					this.LogInformation("Blanking the decoder output");
					ApplyPanes(new int[PaneCount]);
				}
				else if (command == ApplyCommand)
				{
					ApplyPanes((int[])selectedStreams.Clone());
				}
				else if (command == PrivacyCommand)
				{
					ProcessPrivacy();
				}
			}
			catch (Exception ex)
			{
				this.LogError(ex, "Error processing command {command}", command);
			}
		}

		// pane is 0 for the first pane, 1 for the second. The other pane keeps its stream and both are sent
		private void ProcessSelect(int pane, int number)
		{
			if (number != 0 && !AxisD1110Select.IsStreamNumber(number))
			{
				var error = "stream numbers are 1 to " + MaxStreams;
				this.LogWarning("Stream {number} cannot be used: {error}", number, error);
				SetError(-3, string.Format("Stream {0}: {1}", number, error));
				return;
			}

			var requested = (int[])selectedStreams.Clone();
			requested[pane] = number;

			ApplyPanes(requested);
		}

		// sends the layout for the streams asked for in each pane. A pane that is 0, hidden by privacy or cannot be built is left out
		private void ApplyPanes(int[] requested)
		{
			var panes = new AxisD1110Pane[PaneCount];
			string buildError = null;

			for (var pane = 0; pane < PaneCount; pane++)
			{
				var number = requested[pane];
				var camera = CameraOf(number);

				panes[pane] = AxisD1110Layout.Resolve(number, camera != null && camera.PrivacyHidesVideo, BuildStream);

				switch (panes[pane].Status)
				{
					case AxisD1110PaneStatus.HiddenByPrivacy:
						this.LogInformation("Pane {pane}: stream {number} ({name}) is hidden: camera {key} is in privacy. Leaving it out",
							pane + 1, number, StreamName(number), streams[number].CameraKey);
						break;

					case AxisD1110PaneStatus.Unavailable:
						this.LogWarning("Stream {number} cannot be used: {error}", number, panes[pane].Error);
						if (buildError == null) buildError = string.Format("Stream {0}: {1}", number, panes[pane].Error);
						break;

					case AxisD1110PaneStatus.Shown:
						var stream = panes[pane].Stream;
						this.LogInformation("Pane {pane}: stream {number} {name}: {url} login={login} resolution={resolution}",
							pane + 1, number, StreamName(number), stream.Url, string.IsNullOrEmpty(stream.Username) ? "none" : stream.Username, ResolutionOf(stream.Url));
						break;
				}
			}

			var visible = AxisD1110Layout.Visible(panes);
			var layout = LayoutName(visible.Count);

			this.LogInformation("Sending streams {first} and {second} to the decoder: {layout}", requested[0], requested[1], layout);

			var result = client.SetViewConfiguration(visible);
			RecordResult(result);

			if (!result.Success)
			{
				this.LogWarning("Select streams {first} and {second} failed: {code} {message}", requested[0], requested[1], result.ErrorCode, result.ErrorMessage);
				return;
			}

			// a stream that could not be built is reported, unless the decoder reported an error of its own
			if (buildError != null && lastErrorCode == 0) SetError(-3, buildError);

			this.LogInformation("Streams {first} ({firstName}) and {second} ({secondName}) applied, {layout}. Decoder configurationId={id}. Decoder reported error {code} {message}",
				requested[0], StreamName(requested[0]), requested[1], StreamName(requested[1]), layout, result.ConfigurationId, result.ErrorCode, result.ErrorMessage);

			selectionMade = true;
			for (var pane = 0; pane < PaneCount; pane++)
			{
				selectedStreams[pane] = requested[pane];
				paneBlankedByPrivacy[pane] = panes[pane].Status == AxisD1110PaneStatus.HiddenByPrivacy;
			}

			configurationId = result.ConfigurationId ?? string.Empty;
			SetOnline(true);
			SelectedStreamFeedback.FireUpdate();
			SelectedStream2Feedback.FireUpdate();
			SelectedStreamNameFeedback.FireUpdate();
			ConfigurationIdFeedback.FireUpdate();
		}

		private bool BuildStream(int number, out AxisD1110StreamConfig stream, out string error)
		{
			AxisD1110StreamConfig configured;
			streams.TryGetValue(number, out configured);

			string bridgeUrl;
			lock (bridgeUrls)
			{
				bridgeUrls.TryGetValue(number, out bridgeUrl);
			}

			return AxisD1110StreamBuilder.TryBuild(config, configured, bridgeUrl, CameraOf(number), out stream, out error);
		}

		private static string LayoutName(int visibleStreams)
		{
			switch (visibleStreams)
			{
				case 0: return "blank";
				case 1: return "full screen";
				default: return "side by side";
			}
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

			if (configurationId.Length > 0 && newId != configurationId && (selectedStreams[0] != 0 || selectedStreams[1] != 0))
			{
				this.LogWarning("Decoder configuration changed outside this plugin ({old} to {new}). Selected streams are now unknown", configurationId, newId);
				for (var pane = 0; pane < PaneCount; pane++)
				{
					selectedStreams[pane] = 0;
					paneBlankedByPrivacy[pane] = false;
				}

				SelectedStreamFeedback.FireUpdate();
				SelectedStream2Feedback.FireUpdate();
				SelectedStreamNameFeedback.FireUpdate();
			}

			configurationId = newId;
			ConfigurationIdFeedback.FireUpdate();

			// the default stream goes in the first pane
			if (!wasOnline && !selectionMade && config.DefaultStream > 0)
			{
				ProcessSelect(0, config.DefaultStream);
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

			trilist.SetUShortSigAction(joinMap.SelectStream.JoinNumber, OnSelectJoin);
			trilist.SetUShortSigAction(joinMap.SelectStream2.JoinNumber, OnSelect2Join);
			trilist.SetSigTrueAction(joinMap.Clear.JoinNumber, Clear);
			trilist.SetSigTrueAction(joinMap.Refresh.JoinNumber, Poll);

			this.LogInformation("Linked ({layout} layout): select stream is analog join {select}, second stream is analog join {select2}, online is digital join {online}",
				indexed ? "indexed" : "contiguous", joinMap.SelectStream.JoinNumber, joinMap.SelectStream2.JoinNumber, joinMap.IsOnline.JoinNumber);

			if (config.IsBridgeSource && !indexed)
			{
				// only streams 1 to 16 have URL joins
				this.LogInformation("Stream URLs come from the bridge, serial joins {start} to {end}", joinMap.StreamUrls.JoinNumber, joinMap.StreamUrls.JoinNumber + MaxBridgeStreams - 1);

				for (var i = 0; i < MaxBridgeStreams; i++)
				{
					var number = i + 1;
					trilist.SetStringSigAction(joinMap.StreamUrls.JoinNumber + (uint)i, url => SetBridgeStreamUrl(number, url));
				}
			}

			OnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
			SelectedStreamFeedback.LinkInputSig(trilist.UShortInput[joinMap.SelectStream.JoinNumber]);
			SelectedStream2Feedback.LinkInputSig(trilist.UShortInput[joinMap.SelectStream2.JoinNumber]);
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

			// the stream names have no joins in the indexed layout, and only streams 1 to 16 have one in the contiguous layout
			for (var number = 1; number <= MaxBridgeStreams && !indexed; number++)
			{
				AxisD1110StreamConfig configured;
				streams.TryGetValue(number, out configured);
				trilist.SetString(joinMap.StreamNames.JoinNumber + (uint)(number - 1), configured == null ? string.Empty : StreamName(number));
			}

			OnlineFeedback.FireUpdate();
			SelectedStreamFeedback.FireUpdate();
			SelectedStream2Feedback.FireUpdate();
			SelectedStreamNameFeedback.FireUpdate();
			LastErrorCodeFeedback.FireUpdate();
			LastErrorFeedback.FireUpdate();
			ConfigurationIdFeedback.FireUpdate();
		}

		#endregion
	}
}
