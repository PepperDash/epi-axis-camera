using System.Collections.Generic;
using Newtonsoft.Json;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.Plugins.Camera.Axis
{
	/// <summary>
	/// Axis PTZ camera configuration (written for the M5526-E)
	/// </summary>
	/// <example>
	/// <code>
	/// "properties": {
	///		"host": "192.168.10.241",
	///		"username": "admin",
	///		"password": "password",
	///		"presetMode": "server",
	///		"presetCount": 8,
	///		"panSpeed": 50, "tiltSpeed": 50, "zoomSpeed": 75
	/// }
	/// </code>
	/// </example>
	/// <example>
	/// Address and login come from SIMPL over the bridge, camera 5 of a multi camera EISC:
	/// <code>
	/// "properties": {
	///		"connectionSource": "bridge",
	///		"joinLayout": "indexed",
	///		"cameraIndex": 5
	/// }
	/// </code>
	/// </example>
	[ConfigSnippet("\"properties\":{\"host\":\"\",\"username\":\"\",\"password\":\"\"")]
	public class AxisCameraConfig : AxisHttpConfig
	{
		/// <summary>
		/// Where the host, port and login come from. "config" (default) uses the properties of this device.
		/// "bridge" uses the values SIMPL sends on the host, port, username and password serial joins,
		/// and falls back to the properties for any that are empty
		/// </summary>
		[JsonProperty("connectionSource")]
		public string ConnectionSource { get; set; }

		/// <summary>
		/// True when the connection comes from the bridge
		/// </summary>
		[JsonIgnore]
		public bool IsBridgeSource
		{
			get { return string.Equals(ConnectionSource, "bridge", System.StringComparison.OrdinalIgnoreCase); }
		}

		/// <summary>
		/// "contiguous" (default): the joins follow each other from the joinStart of the bridge entry.
		/// "indexed": the joins of many cameras share one EISC, laid out by cameraIndex (see the README).
		/// Use joinStart 1 with indexed
		/// </summary>
		[JsonProperty("joinLayout")]
		public string JoinLayout { get; set; }

		/// <summary>
		/// True when the indexed join layout is used
		/// </summary>
		[JsonIgnore]
		public bool IsIndexedLayout
		{
			get { return string.Equals(JoinLayout, "indexed", System.StringComparison.OrdinalIgnoreCase); }
		}

		/// <summary>
		/// Which camera this is (1 or higher) in the indexed join layout
		/// </summary>
		[JsonProperty("cameraIndex")]
		public int CameraIndex { get; set; }

		/// <summary>
		/// Pan speed 1 to 100. Default 50
		/// </summary>
		[JsonProperty("panSpeed")]
		public int PanSpeed { get; set; }

		/// <summary>
		/// Tilt speed 1 to 100. Default 50
		/// </summary>
		[JsonProperty("tiltSpeed")]
		public int TiltSpeed { get; set; }

		/// <summary>
		/// Zoom speed 1 to 100. Default 75
		/// </summary>
		[JsonProperty("zoomSpeed")]
		public int ZoomSpeed { get; set; }

		/// <summary>
		/// Focus speed 1 to 100. Default 50
		/// </summary>
		[JsonProperty("focusSpeed")]
		public int FocusSpeed { get; set; }

		/// <summary>
		/// Iris speed 1 to 100. Default 50
		/// </summary>
		[JsonProperty("irisSpeed")]
		public int IrisSpeed { get; set; }

		/// <summary>
		/// "server" (default) saves and recalls presets by name (Preset01, Preset02, ...). "device" uses the
		/// camera's numbered device presets
		/// </summary>
		[JsonProperty("presetMode")]
		public string PresetMode { get; set; }

		/// <summary>
		/// True when presets are server presets
		/// </summary>
		[JsonIgnore]
		public bool UseServerPresets
		{
			get { return !string.Equals(PresetMode, "device", System.StringComparison.OrdinalIgnoreCase); }
		}

		/// <summary>
		/// How many presets are offered (1 to 100). Default 8. The M5526-E stores up to 100
		/// </summary>
		[JsonProperty("presetCount")]
		public int PresetCount { get; set; }

		/// <summary>
		/// Optional names for the presets, keyed by preset number. Others are called "Preset N"
		/// </summary>
		[JsonProperty("presets")]
		public Dictionary<string, string> Presets { get; set; }

		/// <summary>
		/// Path of the camera's main stream. Default /axis-media/media.amp. Used for the preview URL sent to SIMPL
		/// and for decoder streams that name this camera
		/// </summary>
		[JsonProperty("streamPath")]
		public string StreamPath { get; set; }

		/// <summary>
		/// Resolution in the preview URL sent to SIMPL, for example 800x450 (the default). Decoders ask for their own
		/// resolution, so this only sets what touch panel previews pull
		/// </summary>
		[JsonProperty("previewResolution")]
		public string PreviewResolution { get; set; }

		/// <summary>
		/// When true (default), turning privacy on for this camera hides its video everywhere: the preview URL sent to SIMPL
		/// is cleared and every decoder that is showing this camera is blanked, and both come back when privacy ends.
		/// The camera moving to its privacy preset is done by whatever sends the preset recall, as before.
		/// False makes privacy only a state that is reported
		/// </summary>
		[JsonProperty("privacyClearsStream")]
		public bool? PrivacyClearsStream { get; set; }

		/// <summary>
		/// Poll interval in milliseconds. Defaults to 30000. A negative value polls once at startup only
		/// </summary>
		[JsonProperty("pollTimeMs")]
		public long PollTimeMs { get; set; }

		/// <summary>
		/// Constructor
		/// </summary>
		public AxisCameraConfig()
		{
			Presets = new Dictionary<string, string>();
		}
	}
}
