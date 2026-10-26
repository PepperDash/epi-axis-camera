using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Queues;
using PepperDash.Essentials.Devices.Common.Cameras;

namespace PepperDash.Essentials.Plugins.Camera.Axis
{
	/// <summary>
	/// Where a video decoder can find an Axis camera: its address and login
	/// </summary>
	public interface IAxisCameraSource : IKeyed
	{
		/// <summary>
		/// Camera address, or empty until it is known (it may arrive from the bridge)
		/// </summary>
		string Host { get; }

		/// <summary>
		/// Camera login, or empty
		/// </summary>
		string Username { get; }

		/// <summary>
		/// Path of the camera's main stream, for example /axis-media/media.amp
		/// </summary>
		string StreamPath { get; }

		/// <summary>
		/// Camera password, or empty
		/// </summary>
		string Password { get; }

		/// <summary>
		/// True while the camera is in privacy and its video must be hidden everywhere (privacyClearsStream)
		/// </summary>
		bool PrivacyHidesVideo { get; }

		/// <summary>
		/// Raised when PrivacyHidesVideo changes
		/// </summary>
		event EventHandler PrivacyChanged;
	}

	/// <summary>
	/// Axis PTZ camera controlled through VAPIX (written for the M5526-E): pan, tilt, zoom, focus, iris, home and presets
	/// </summary>
	/// <remarks>
	/// Continuous moves start when the join goes high and stop when it goes low. Requests are sent one at a time,
	/// in the order they were made, so a stop never overtakes the move it belongs to.
	/// </remarks>
	public class AxisCameraDevice : EssentialsBridgeableDevice, IAxisCameraSource,
		IHasCameraPtzControl, IHasCameraFocusControl, IHasAutoFocusMode, IHasCameraPresets
	{
		private const long DefaultPollTimeMs = 30000;
		private const int DefaultPresetCount = 8;
		private const int MaxPresetCount = 100;
		private const int PresetStoredPulseMs = 1000;

		private readonly AxisCameraConfig config;
		private readonly GenericQueue commandQueue;
		private readonly object clientLock = new object();

		private AxisHttpClient client;
		private CTimer pollTimer;
		private CTimer presetStoredTimer;

		private string bridgeHost = string.Empty;
		private string bridgePort = string.Empty;
		private string bridgeUsername = string.Empty;
		private string bridgePassword = string.Empty;

		private bool online;
		private bool? autoFocus;
		private bool? autoIris;
		private bool presetStored;
		private bool privacyOn;
		private string lastError = string.Empty;
		private bool errorLogged;

		private readonly int panSpeed;
		private readonly int tiltSpeed;
		private readonly int zoomSpeed;
		private readonly int focusSpeed;
		private readonly int irisSpeed;
		private readonly int presetCount;

		/// <summary>
		/// Camera answered the last request
		/// </summary>
		public BoolFeedback OnlineFeedback { get; private set; }

		/// <summary>
		/// Autofocus is on
		/// </summary>
		public BoolFeedback AutoFocusFeedback { get; private set; }

		/// <summary>
		/// Auto iris is on
		/// </summary>
		public BoolFeedback AutoIrisFeedback { get; private set; }

		/// <summary>
		/// High for a moment after a preset was saved
		/// </summary>
		public BoolFeedback PresetStoredFeedback { get; private set; }

		/// <summary>
		/// Last error from the camera, empty when the last request worked
		/// </summary>
		public StringFeedback LastErrorFeedback { get; private set; }

		/// <summary>
		/// RTSP URL of the camera for touch panel previews, empty until the camera has an address
		/// </summary>
		public StringFeedback PreviewUrlFeedback { get; private set; }

		/// <summary>
		/// Privacy is on for this camera
		/// </summary>
		public BoolFeedback PrivacyOnFeedback { get; private set; }

		/// <summary>
		/// True while privacy is on and privacyClearsStream (the default) says its video is hidden everywhere
		/// </summary>
		public bool PrivacyHidesVideo
		{
			get { return privacyOn && (config.PrivacyClearsStream ?? true); }
		}

		/// <summary>
		/// Raised when PrivacyHidesVideo changes
		/// </summary>
		public event EventHandler PrivacyChanged;

		/// <summary>
		/// The preview URL: rtsp://host + streamPath at previewResolution. Empty while privacy hides the camera's video
		/// </summary>
		public string PreviewUrl
		{
			get
			{
				return AxisPrivacy.PreviewUrl(Host, StreamPath,
					string.IsNullOrWhiteSpace(config.PreviewResolution) ? "800x450" : config.PreviewResolution, PrivacyHidesVideo);
			}
		}

		/// <summary>
		/// Path of the camera's main stream
		/// </summary>
		public string StreamPath
		{
			get { return string.IsNullOrWhiteSpace(config.StreamPath) ? AxisStreamUrl.DefaultCameraPath : config.StreamPath.Trim(); }
		}

		/// <summary>
		/// The presets offered. Names come from the presets property
		/// </summary>
		public List<CameraPreset> Presets { get; private set; }

		/// <summary>
		/// Raised when the list of presets changes
		/// </summary>
		public event EventHandler<EventArgs> PresetsListHasChanged;

		/// <summary>
		/// Camera address in use
		/// </summary>
		public string Host
		{
			get { return CurrentConnection().Host ?? string.Empty; }
		}

		/// <summary>
		/// Camera login in use
		/// </summary>
		public string Username
		{
			get { return CurrentConnection().Username ?? string.Empty; }
		}

		/// <summary>
		/// Camera password in use
		/// </summary>
		public string Password
		{
			get { return CurrentConnection().Password ?? string.Empty; }
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="key">Device key</param>
		/// <param name="name">Device name</param>
		/// <param name="config">Device properties</param>
		public AxisCameraDevice(string key, string name, AxisCameraConfig config)
			: base(key, name)
		{
			this.config = config;

			panSpeed = Speed(config.PanSpeed, 50);
			tiltSpeed = Speed(config.TiltSpeed, 50);
			zoomSpeed = Speed(config.ZoomSpeed, 75);
			focusSpeed = Speed(config.FocusSpeed, 50);
			irisSpeed = Speed(config.IrisSpeed, 50);
			presetCount = config.PresetCount < 1 ? DefaultPresetCount : Math.Min(config.PresetCount, MaxPresetCount);

			this.LogInformation(
				"Configured for {host} connectionSource={source} presets={mode} x{count} speeds pan={pan} tilt={tilt} zoom={zoom} focus={focus} iris={iris} joinLayout={layout} cameraIndex={index} pollTimeMs={poll}",
				string.IsNullOrEmpty(config.Host) ? "(host from bridge)" : config.Host,
				config.IsBridgeSource ? "bridge" : "config",
				config.UseServerPresets ? "server" : "device",
				presetCount, panSpeed, tiltSpeed, zoomSpeed, focusSpeed, irisSpeed,
				config.IsIndexedLayout ? "indexed" : "contiguous",
				config.CameraIndex,
				config.PollTimeMs == 0 ? DefaultPollTimeMs : config.PollTimeMs);

			if (config.IsIndexedLayout && config.CameraIndex < 1)
			{
				this.LogWarning("joinLayout is indexed but cameraIndex is {index}. It must be 1 or higher, using the contiguous layout", config.CameraIndex);
			}

			commandQueue = new GenericQueue(key + "-queue");

			Presets = new List<CameraPreset>();
			for (var number = 1; number <= presetCount; number++)
			{
				Presets.Add(new CameraPreset(number, PresetDescription(number), true, true));
			}

			OnlineFeedback = new BoolFeedback("online", () => online);
			AutoFocusFeedback = new BoolFeedback("autoFocus", () => autoFocus == true);
			AutoIrisFeedback = new BoolFeedback("autoIris", () => autoIris == true);
			PresetStoredFeedback = new BoolFeedback("presetStored", () => presetStored);
			LastErrorFeedback = new StringFeedback("lastError", () => lastError);
			PreviewUrlFeedback = new StringFeedback("previewUrl", () => PreviewUrl);
			PrivacyOnFeedback = new BoolFeedback("privacyOn", () => privacyOn);

			RebuildClient();

			AddPostActivationAction(StartPolling);
		}

		private static int Speed(int configured, int fallback)
		{
			return AxisPtzCommands.ClampSpeed(configured < 1 ? fallback : configured);
		}

		private string PresetDescription(int number)
		{
			string name;
			return config.Presets != null && config.Presets.TryGetValue(number.ToString(), out name) && !string.IsNullOrEmpty(name)
				? name
				: "Preset " + number;
		}

		#region Connection

		private AxisConnection CurrentConnection()
		{
			var connection = config.ToConnection();

			if (!config.IsBridgeSource) return connection;

			if (bridgeHost.Length > 0) connection.Host = bridgeHost;
			if (bridgeUsername.Length > 0) connection.Username = bridgeUsername;
			if (bridgePassword.Length > 0) connection.Password = bridgePassword;

			int port;
			if (int.TryParse(bridgePort, out port) && port > 0) connection.Port = port;

			return connection;
		}

		private void RebuildClient()
		{
			var connection = CurrentConnection();

			lock (clientLock)
			{
				if (!connection.IsUsable)
				{
					if (client != null)
					{
						client.Dispose();
						client = null;
					}

					return;
				}

				if (client != null && client.Connection.SameAs(connection)) return;

				this.LogInformation("Connecting to {host}:{port} as {user}", connection.Host, connection.EffectivePort, string.IsNullOrEmpty(connection.Username) ? "(no login)" : connection.Username);

				var previous = client;
				client = new AxisHttpClient(connection, message => this.LogDebug("{message}", message));

				if (previous != null) previous.Dispose();
			}
		}

		private AxisHttpClient CurrentClient()
		{
			lock (clientLock)
			{
				return client;
			}
		}

		/// <summary>
		/// Sets the camera address from the bridge. Used when connectionSource is bridge
		/// </summary>
		public void SetBridgeHost(string host)
		{
			SetBridgeValue(ref bridgeHost, host, "address");
		}

		/// <summary>
		/// Sets the camera HTTP port from the bridge. Used when connectionSource is bridge
		/// </summary>
		public void SetBridgePort(string port)
		{
			SetBridgeValue(ref bridgePort, port, "port");
		}

		/// <summary>
		/// Sets the camera login from the bridge. Used when connectionSource is bridge
		/// </summary>
		public void SetBridgeUsername(string username)
		{
			SetBridgeValue(ref bridgeUsername, username, "login");
		}

		/// <summary>
		/// Sets the camera password from the bridge. Used when connectionSource is bridge
		/// </summary>
		public void SetBridgePassword(string password)
		{
			SetBridgeValue(ref bridgePassword, password, "password");
		}

		private void SetBridgeValue(ref string field, string value, string what)
		{
			value = value == null ? string.Empty : value.Trim();

			if (field == value) return;

			field = value;

			// the password is never logged
			this.LogInformation("Bridge sent the camera {what} {value}", what, what == "password" ? "(hidden)" : (value.Length == 0 ? "(empty)" : value));

			RebuildClient();
			PreviewUrlFeedback.FireUpdate();

			if (CurrentClient() != null) Poll();
		}

		#endregion

		#region Polling

		private void StartPolling()
		{
			var pollTimeMs = config.PollTimeMs == 0 ? DefaultPollTimeMs : config.PollTimeMs;

			this.LogInformation("Starting polling. First poll in 1000 ms, then every {interval} ms", pollTimeMs > 0 ? pollTimeMs : 0);

			pollTimer = new CTimer(o => Poll(), null, 1000, pollTimeMs > 0 ? pollTimeMs : System.Threading.Timeout.Infinite);
		}

		/// <summary>
		/// Asks the camera for its state now
		/// </summary>
		public void Poll()
		{
			Enqueue("poll", AxisPtzCommands.QueryPosition(), true);
		}

		#endregion

		#region Commands

		private void Enqueue(string label, string request, bool refreshState)
		{
			commandQueue.Enqueue(new ProcessStringMessage(request, path => Execute(label, path, refreshState)));
		}

		private void Execute(string label, string request, bool readState)
		{
			try
			{
				var http = CurrentClient();
				if (http == null)
				{
					this.LogWarning("Cannot send {label}: there is no camera address yet", label);
					return;
				}

				var result = http.Get(label, request);

				string error;
				if (AxisPtzCommands.IsError(result, out error))
				{
					HandleError(label, error);
					return;
				}

				SetError(string.Empty);
				SetOnline(true);

				if (readState) ReadState(result.Body);
			}
			catch (Exception ex)
			{
				this.LogError(ex, "Error sending {label}", label);
			}
		}

		// poll failures warn once, then stay quiet until the camera answers again
		private void HandleError(string label, string error)
		{
			SetError(error);

			var offline = label == "poll";
			if (offline) SetOnline(false);

			if (!errorLogged)
			{
				this.LogWarning("{label} failed: {error}", label, error);
				errorLogged = true;
			}
			else
			{
				this.LogDebug("{label} failed: {error}", label, error);
			}
		}

		private void ReadState(string body)
		{
			var position = AxisPtzPosition.Parse(body);

			if (position.AutoFocus.HasValue && position.AutoFocus != autoFocus)
			{
				autoFocus = position.AutoFocus;
				AutoFocusFeedback.FireUpdate();
			}

			if (position.AutoIris.HasValue && position.AutoIris != autoIris)
			{
				autoIris = position.AutoIris;
				AutoIrisFeedback.FireUpdate();
			}
		}

		private void SetOnline(bool value)
		{
			if (value) errorLogged = false;

			if (online == value) return;

			this.LogInformation("Camera is now {state}", value ? "online" : "offline");

			online = value;
			OnlineFeedback.FireUpdate();
		}

		private void SetError(string message)
		{
			if (lastError == message) return;

			lastError = message;
			LastErrorFeedback.FireUpdate();
		}

		/// <summary>
		/// Starts a pan and tilt. Speeds are signed: positive pan is right, positive tilt is up
		/// </summary>
		private void Move(string name, int pan, int tilt)
		{
			this.LogDebug("Move {name}", name);
			Enqueue(name, AxisPtzCommands.PanTilt(pan, tilt), false);
		}

		/// <summary>
		/// Tilts up
		/// </summary>
		public void TiltUp() { Move("tilt up", 0, tiltSpeed); }

		/// <summary>
		/// Tilts down
		/// </summary>
		public void TiltDown() { Move("tilt down", 0, -tiltSpeed); }

		/// <summary>
		/// Pans left
		/// </summary>
		public void PanLeft() { Move("pan left", -panSpeed, 0); }

		/// <summary>
		/// Pans right
		/// </summary>
		public void PanRight() { Move("pan right", panSpeed, 0); }

		/// <summary>
		/// Tilts up and pans right
		/// </summary>
		public void MoveUpRight() { Move("up right", panSpeed, tiltSpeed); }

		/// <summary>
		/// Tilts down and pans right
		/// </summary>
		public void MoveDownRight() { Move("down right", panSpeed, -tiltSpeed); }

		/// <summary>
		/// Tilts up and pans left
		/// </summary>
		public void MoveUpLeft() { Move("up left", -panSpeed, tiltSpeed); }

		/// <summary>
		/// Tilts down and pans left
		/// </summary>
		public void MoveDownLeft() { Move("down left", -panSpeed, -tiltSpeed); }

		/// <summary>
		/// Stops panning
		/// </summary>
		public void PanStop() { StopPanTilt(); }

		/// <summary>
		/// Stops tilting
		/// </summary>
		public void TiltStop() { StopPanTilt(); }

		/// <summary>
		/// Stops panning and tilting
		/// </summary>
		public void StopPanTilt()
		{
			this.LogDebug("Stop pan and tilt");
			Enqueue("stop pan tilt", AxisPtzCommands.StopPanTilt(), false);
		}

		/// <summary>
		/// Zooms in
		/// </summary>
		public void ZoomIn()
		{
			this.LogDebug("Zoom in");
			Enqueue("zoom in", AxisPtzCommands.Zoom(zoomSpeed), false);
		}

		/// <summary>
		/// Zooms out
		/// </summary>
		public void ZoomOut()
		{
			this.LogDebug("Zoom out");
			Enqueue("zoom out", AxisPtzCommands.Zoom(-zoomSpeed), false);
		}

		/// <summary>
		/// Stops zooming
		/// </summary>
		public void ZoomStop()
		{
			this.LogDebug("Zoom stop");
			Enqueue("zoom stop", AxisPtzCommands.Zoom(0), false);
		}

		/// <summary>
		/// Focuses near
		/// </summary>
		public void FocusNear()
		{
			this.LogDebug("Focus near");
			Enqueue("focus near", AxisPtzCommands.Focus(-focusSpeed), false);
		}

		/// <summary>
		/// Focuses far
		/// </summary>
		public void FocusFar()
		{
			this.LogDebug("Focus far");
			Enqueue("focus far", AxisPtzCommands.Focus(focusSpeed), false);
		}

		/// <summary>
		/// Stops focusing
		/// </summary>
		public void FocusStop()
		{
			this.LogDebug("Focus stop");
			Enqueue("focus stop", AxisPtzCommands.Focus(0), false);
		}

		/// <summary>
		/// Turns autofocus on, then reads the camera state back
		/// </summary>
		public void TriggerAutoFocus()
		{
			SetFocusModeAuto();
		}

		/// <summary>
		/// Turns autofocus on
		/// </summary>
		public void SetFocusModeAuto()
		{
			this.LogInformation("Autofocus on");
			Enqueue("autofocus on", AxisPtzCommands.AutoFocus(true), false);
			Poll();
		}

		/// <summary>
		/// Turns autofocus off
		/// </summary>
		public void SetFocusModeManual()
		{
			this.LogInformation("Autofocus off");
			Enqueue("autofocus off", AxisPtzCommands.AutoFocus(false), false);
			Poll();
		}

		/// <summary>
		/// Toggles autofocus
		/// </summary>
		public void ToggleFocusMode()
		{
			if (autoFocus == true) SetFocusModeManual(); else SetFocusModeAuto();
		}

		/// <summary>
		/// Opens the iris
		/// </summary>
		public void IrisOpen()
		{
			this.LogDebug("Iris open");
			Enqueue("iris open", AxisPtzCommands.Iris(irisSpeed), false);
		}

		/// <summary>
		/// Closes the iris
		/// </summary>
		public void IrisClose()
		{
			this.LogDebug("Iris close");
			Enqueue("iris close", AxisPtzCommands.Iris(-irisSpeed), false);
		}

		/// <summary>
		/// Stops moving the iris
		/// </summary>
		public void IrisStop()
		{
			this.LogDebug("Iris stop");
			Enqueue("iris stop", AxisPtzCommands.Iris(0), false);
		}

		/// <summary>
		/// Toggles auto iris
		/// </summary>
		public void ToggleAutoIris()
		{
			var turnOn = autoIris != true;
			this.LogInformation("Auto iris {state}", turnOn ? "on" : "off");
			Enqueue("autoiris", AxisPtzCommands.AutoIris(turnOn), false);
			Poll();
		}

		/// <summary>
		/// Turns privacy on or off for this camera. With privacyClearsStream (the default) the preview URL sent to SIMPL is
		/// cleared and every decoder showing this camera is blanked while privacy is on. The camera's privacy preset is not
		/// recalled here: that is done by whatever sends the preset recall. Also for DEVJSON testing
		/// </summary>
		/// <param name="on">True for privacy on</param>
		public void SetPrivacy(bool on)
		{
			if (privacyOn == on) return;

			var wasHiding = PrivacyHidesVideo;
			privacyOn = on;

			this.LogInformation("Privacy is now {state}. Video is {video}", on ? "on" : "off", PrivacyHidesVideo ? "hidden" : "shown");

			PrivacyOnFeedback.FireUpdate();

			if (wasHiding == PrivacyHidesVideo) return;

			PreviewUrlFeedback.FireUpdate();

			var changed = PrivacyChanged;
			if (changed != null) changed(this, EventArgs.Empty);
		}

		/// <summary>
		/// Moves to the home position
		/// </summary>
		public void PositionHome()
		{
			this.LogInformation("Moving to the home position");
			Enqueue("home", AxisPtzCommands.Home(), false);
		}

		/// <summary>
		/// Restarts the camera
		/// </summary>
		public void Restart()
		{
			this.LogInformation("Restarting the camera");
			Enqueue("restart", AxisPtzCommands.RestartPath, false);
		}

		#endregion

		#region Presets

		/// <summary>
		/// Moves to a saved preset
		/// </summary>
		/// <param name="preset">Preset number</param>
		public void PresetSelect(int preset)
		{
			if (preset < 1 || preset > presetCount)
			{
				this.LogWarning("Preset {preset} is not available. Presets are 1 to {count}", preset, presetCount);
				return;
			}

			this.LogInformation("Recalling preset {preset}", preset);
			Enqueue("recall preset " + preset, AxisPtzCommands.GotoPreset(preset, config.UseServerPresets), false);
		}

		/// <summary>
		/// Saves the current position as a preset
		/// </summary>
		/// <param name="preset">Preset number</param>
		/// <param name="description">New name for the preset in the list. May be empty</param>
		public void PresetStore(int preset, string description)
		{
			if (preset < 1 || preset > presetCount)
			{
				this.LogWarning("Preset {preset} is not available. Presets are 1 to {count}", preset, presetCount);
				return;
			}

			if (!string.IsNullOrEmpty(description))
			{
				Presets[preset - 1] = new CameraPreset(preset, description, true, true);

				var changed = PresetsListHasChanged;
				if (changed != null) changed(this, EventArgs.Empty);
			}

			this.LogInformation("Saving preset {preset}", preset);

			commandQueue.Enqueue(new ProcessStringMessage("save preset " + preset, label => SavePreset(preset)));
		}

		/// <summary>
		/// Saves the current position as a preset
		/// </summary>
		/// <param name="preset">Preset number</param>
		public void PresetStore(int preset)
		{
			PresetStore(preset, null);
		}

		// Axis documents saving presets on ptzconfig.cgi. The old control program used ptz.cgi, which older
		// firmware accepts. Try the documented one first and fall back to the other
		private void SavePreset(int preset)
		{
			try
			{
				var http = CurrentClient();
				if (http == null)
				{
					this.LogWarning("Cannot save preset {preset}: there is no camera address yet", preset);
					return;
				}

				var label = "save preset " + preset;
				string error;

				var request = AxisPtzCommands.SetPreset(preset, config.UseServerPresets, AxisPtzCommands.PtzConfigPath, true);
				if (!AxisPtzCommands.IsError(http.Get(label, request), out error))
				{
					PresetSaved(preset);
					return;
				}

				this.LogWarning("Saving preset {preset} with ptzconfig.cgi failed: {error}. Trying ptz.cgi", preset, error);

				request = AxisPtzCommands.SetPreset(preset, config.UseServerPresets, AxisPtzCommands.PtzPath, true);
				if (!AxisPtzCommands.IsError(http.Get(label + " (ptz.cgi)", request), out error))
				{
					PresetSaved(preset);
					return;
				}

				HandleError(label, error);
			}
			catch (Exception ex)
			{
				this.LogError(ex, "Error saving preset {preset}", preset);
			}
		}

		private void PresetSaved(int preset)
		{
			this.LogInformation("Preset {preset} saved", preset);

			SetError(string.Empty);
			SetOnline(true);

			presetStored = true;
			PresetStoredFeedback.FireUpdate();

			if (presetStoredTimer != null) presetStoredTimer.Dispose();
			presetStoredTimer = new CTimer(o =>
			{
				presetStored = false;
				PresetStoredFeedback.FireUpdate();
			}, null, PresetStoredPulseMs);
		}

		#endregion

		#region DEVJSON test commands

		/// <summary>
		/// Logs the current state of the camera. For DEVJSON testing
		/// </summary>
		public void PrintStatus()
		{
			var connection = CurrentConnection();

			this.LogInformation(
				"Status: host={host}:{port} login={login} online={online} autoFocus={af} autoIris={ai} privacy={privacy} lastError={error} previewUrl={url}",
				connection.IsUsable ? connection.Host : "(none)", connection.EffectivePort,
				string.IsNullOrEmpty(connection.Username) ? "(none)" : connection.Username,
				online, autoFocus, autoIris, privacyOn, lastError, PreviewUrl);
		}

		/// <summary>
		/// Sends a raw VAPIX request and logs the answer. For DEVJSON testing, for example
		/// {"deviceKey":"cam-1","methodName":"SendRequest","params":["/axis-cgi/com/ptz.cgi?info=1"]}
		/// </summary>
		/// <param name="pathAndQuery">Path and query, for example /axis-cgi/com/ptz.cgi?info=1</param>
		public void SendRequest(string pathAndQuery)
		{
			this.LogInformation("Raw request {request}", pathAndQuery);

			commandQueue.Enqueue(new ProcessStringMessage(pathAndQuery, request =>
			{
				try
				{
					var http = CurrentClient();
					if (http == null)
					{
						this.LogWarning("Cannot send: there is no camera address yet");
						return;
					}

					var result = http.Get("raw", request);

					string error;
					if (AxisPtzCommands.IsError(result, out error))
					{
						this.LogInformation("Raw request failed: {error}", error);
						return;
					}

					this.LogInformation("Raw request answered HTTP {code}: {body}", result.StatusCode, string.IsNullOrEmpty(result.Body) ? "(no body)" : result.Body);
				}
				catch (Exception ex)
				{
					this.LogError(ex, "Raw request failed");
				}
			}));
		}

		#endregion

		#region Overrides of EssentialsBridgeableDevice

		/// <summary>
		/// Links the camera to the EISC bridge
		/// </summary>
		public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
		{
			var joinMap = new AxisCameraBridgeJoinMap(joinStart, config.IsIndexedLayout ? config.CameraIndex : 0);

			// This adds the join map to the collection on the bridge
			bridge?.AddJoinMap(Key, joinMap);

			var customJoins = JoinMapHelper.TryGetJoinMapAdvancedForDevice(joinMapKey);

			if (customJoins != null)
			{
				joinMap.SetCustomJoinData(customJoins);
			}

			this.LogDebug("Linking to Trilist {id} at join start {start}", trilist.ID.ToString("X"), joinStart);
			this.LogInformation("Linked: tilt up is digital join {up}, preset save is analog join {save}", joinMap.Up.JoinNumber, joinMap.PresetSave.JoinNumber);

			// movement joins start on press and stop on release
			Hold(trilist, joinMap.Up.JoinNumber, TiltUp, StopPanTilt);
			Hold(trilist, joinMap.Down.JoinNumber, TiltDown, StopPanTilt);
			Hold(trilist, joinMap.Left.JoinNumber, PanLeft, StopPanTilt);
			Hold(trilist, joinMap.Right.JoinNumber, PanRight, StopPanTilt);
			Hold(trilist, joinMap.UpRight.JoinNumber, MoveUpRight, StopPanTilt);
			Hold(trilist, joinMap.DownRight.JoinNumber, MoveDownRight, StopPanTilt);
			Hold(trilist, joinMap.UpLeft.JoinNumber, MoveUpLeft, StopPanTilt);
			Hold(trilist, joinMap.DownLeft.JoinNumber, MoveDownLeft, StopPanTilt);
			Hold(trilist, joinMap.ZoomIn.JoinNumber, ZoomIn, ZoomStop);
			Hold(trilist, joinMap.ZoomOut.JoinNumber, ZoomOut, ZoomStop);
			Hold(trilist, joinMap.FocusFar.JoinNumber, FocusFar, FocusStop);
			Hold(trilist, joinMap.FocusNear.JoinNumber, FocusNear, FocusStop);
			Hold(trilist, joinMap.IrisOpen.JoinNumber, IrisOpen, IrisStop);
			Hold(trilist, joinMap.IrisClose.JoinNumber, IrisClose, IrisStop);

			// these act on the press only
			trilist.SetSigTrueAction(joinMap.AutoFocus.JoinNumber, ToggleFocusMode);
			trilist.SetSigTrueAction(joinMap.AutoIris.JoinNumber, ToggleAutoIris);
			trilist.SetSigTrueAction(joinMap.Restart.JoinNumber, Restart);
			trilist.SetSigTrueAction(joinMap.Home.JoinNumber, PositionHome);
			trilist.SetBoolSigAction(joinMap.Privacy.JoinNumber, SetPrivacy);

			for (var i = 0; i < 8 && i < presetCount; i++)
			{
				var preset = i + 1;
				trilist.SetSigTrueAction(joinMap.PresetRecall.JoinNumber + (uint)i, () => PresetSelect(preset));
			}

			trilist.SetUShortSigAction(joinMap.PresetSave.JoinNumber, value =>
			{
				if (value == 0) return;

				PresetStore(value);
			});

			if (config.IsBridgeSource)
			{
				this.LogInformation("Camera address and login come from the bridge: serial joins {host}, {port}, {user}, {password}",
					joinMap.Host.JoinNumber, joinMap.Port.JoinNumber, joinMap.Username.JoinNumber, joinMap.Password.JoinNumber);

				trilist.SetStringSigAction(joinMap.Host.JoinNumber, SetBridgeHost);
				trilist.SetStringSigAction(joinMap.Port.JoinNumber, SetBridgePort);
				trilist.SetStringSigAction(joinMap.Username.JoinNumber, SetBridgeUsername);
				trilist.SetStringSigAction(joinMap.Password.JoinNumber, SetBridgePassword);
			}

			OnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
			AutoFocusFeedback.LinkInputSig(trilist.BooleanInput[joinMap.AutoFocus.JoinNumber]);
			AutoIrisFeedback.LinkInputSig(trilist.BooleanInput[joinMap.AutoIris.JoinNumber]);
			PresetStoredFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PresetStored.JoinNumber]);
			LastErrorFeedback.LinkInputSig(trilist.StringInput[joinMap.LastError.JoinNumber]);
			PreviewUrlFeedback.LinkInputSig(trilist.StringInput[joinMap.PreviewUrl.JoinNumber]);
			PrivacyOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Privacy.JoinNumber]);

			this.LogInformation("Sending SIMPL the camera name on serial join {name} and the preview URL on serial join {url}",
				joinMap.DeviceName.JoinNumber, joinMap.PreviewUrl.JoinNumber);

			UpdateFeedbacks(trilist, joinMap);

			trilist.OnlineStatusChange += (o, a) =>
			{
				if (!a.DeviceOnLine) return;

				UpdateFeedbacks(trilist, joinMap);
			};
		}

		private static void Hold(BasicTriList trilist, uint join, Action start, Action stop)
		{
			trilist.SetBoolSigAction(join, pressed =>
			{
				if (pressed) start(); else stop();
			});
		}

		private void UpdateFeedbacks(BasicTriList trilist, AxisCameraBridgeJoinMap joinMap)
		{
			trilist.SetString(joinMap.DeviceName.JoinNumber, Name);

			OnlineFeedback.FireUpdate();
			AutoFocusFeedback.FireUpdate();
			AutoIrisFeedback.FireUpdate();
			PresetStoredFeedback.FireUpdate();
			LastErrorFeedback.FireUpdate();
			PreviewUrlFeedback.FireUpdate();
			PrivacyOnFeedback.FireUpdate();
		}

		#endregion
	}
}
