using System.Collections.Generic;
using Newtonsoft.Json;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.Plugins.Camera.Axis
{
	/// <summary>
	/// Axis D1110 device configuration object
	/// </summary>
	/// <example>
	/// <code>
	/// "properties": {
	///		"host": "192.168.10.60",
	///		"username": "root",
	///		"password": "password",
	///		"authMode": "digest",
	///		"pollTimeMs": 30000,
	///		"defaultStream": 0,
	///		"streams": {
	///			"1": { "name": "Left PTZ", "url": "rtsp://192.168.10.211/axis-media/media.amp", "videoCodec": "H264" },
	///			"2": { "name": "Right PTZ", "url": "rtsp://192.168.10.212/axis-media/media.amp", "videoCodec": "H264" },
	///			"3": { "name": "Shared Monopod", "url": "rtsp://192.168.10.240/axis-media/media.amp", "videoCodec": "H264" }
	///		}
	/// }
	/// </code>
	/// </example>
	[ConfigSnippet("\"properties\":{\"host\":\"\",\"username\":\"\",\"password\":\"\",\"streams\":{}")]
	public class AxisD1110Config : AxisHttpConfig
	{
		/// <summary>
		/// "contiguous" (default): the joins follow each other from the joinStart of the bridge entry.
		/// "indexed": up to 8 decoders share one small EISC, laid out by decoderIndex (see the README). Use joinStart 1.
		/// The stream name and stream URL joins are not available in the indexed layout
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
		/// Which decoder this is (1 to 8) in the indexed join layout
		/// </summary>
		[JsonProperty("decoderIndex")]
		public int DecoderIndex { get; set; }

		/// <summary>
		/// When true, the select stream joins (both panes) only act on numbers that are configured streams. Any other value, including 0, is
		/// ignored (use the clear join to blank the output). Use it when the join is driven by something that also carries other
		/// numbers, such as a display's selected source, where the camera buttons are sources 1 to 3 and everything else is not a stream
		/// </summary>
		[JsonProperty("selectOnlyConfiguredStreams")]
		public bool SelectOnlyConfiguredStreams { get; set; }

		/// <summary>
		/// Poll interval in milliseconds. Defaults to 30000. A negative value disables polling
		/// </summary>
		[JsonProperty("pollTimeMs")]
		public long PollTimeMs { get; set; }

		/// <summary>
		/// Stream selected (first pane) when the decoder first comes online. 0 (default) leaves the decoder as it is
		/// </summary>
		[JsonProperty("defaultStream")]
		public int DefaultStream { get; set; }

		/// <summary>
		/// Where the stream URLs come from. "config" (default) uses the url of each entry in streams.
		/// "bridge" uses the URLs SIMPL sends on the stream URL serial joins, so the camera addresses and
		/// logins stay in the SIMPL program and are not repeated here.
		/// </summary>
		[JsonProperty("streamSource")]
		public string StreamSource { get; set; }

		/// <summary>
		/// True when the stream URLs come from the bridge
		/// </summary>
		[JsonIgnore]
		public bool IsBridgeSource
		{
			get { return string.Equals(StreamSource, "bridge", System.StringComparison.OrdinalIgnoreCase); }
		}

		/// <summary>
		/// Resolution to request from the cameras, for example 1920x1080. Replaces the resolution in every stream URL
		/// (the preview URLs SIMPL holds ask for a small one). Empty leaves the URL as it is
		/// </summary>
		[JsonProperty("resolution")]
		public string Resolution { get; set; }

		/// <summary>
		/// Path of the camera stream when a stream names a camera with cameraKey. Default /axis-media/media.amp
		/// </summary>
		[JsonProperty("cameraStreamPath")]
		public string CameraStreamPath { get; set; }

		/// <summary>
		/// Login for the camera streams when the URL does not carry one
		/// </summary>
		[JsonProperty("streamUsername")]
		public string StreamUsername { get; set; }

		/// <summary>
		/// Password for the camera streams when the URL does not carry a login
		/// </summary>
		[JsonProperty("streamPassword")]
		public string StreamPassword { get; set; }

		/// <summary>
		/// Selectable streams, keyed by the stream number SIMPL sends (1 to 64). With streamSource bridge
		/// the entries are optional and only supply names and per stream settings, the URL comes from the bridge
		/// </summary>
		[JsonProperty("streams")]
		public Dictionary<string, AxisD1110StreamConfig> Streams { get; set; }

		/// <summary>
		/// Constructor
		/// </summary>
		public AxisD1110Config()
		{
			Streams = new Dictionary<string, AxisD1110StreamConfig>();
		}
	}

	/// <summary>
	/// One stream the decoder can be told to show
	/// </summary>
	public class AxisD1110StreamConfig
	{
		/// <summary>
		/// Display name reported on the bridge
		/// </summary>
		[JsonProperty("name")]
		public string Name { get; set; }

		/// <summary>
		/// Key of an axisCamera device. The stream is that camera: its address and login are used, so they are
		/// not repeated here. Takes the place of url and of the bridge URL
		/// </summary>
		[JsonProperty("cameraKey")]
		public string CameraKey { get; set; }

		/// <summary>
		/// RTSP URL of the source camera stream. May carry the login as rtsp://user:password@host/path.
		/// Not used when the stream has a cameraKey or streamSource is bridge
		/// </summary>
		[JsonProperty("url")]
		public string Url { get; set; }

		/// <summary>
		/// Resolution for this stream only. Overrides the plugin level resolution
		/// </summary>
		[JsonProperty("resolution")]
		public string Resolution { get; set; }

		/// <summary>
		/// H264 (default), MJPEG or MPEG4
		/// </summary>
		[JsonProperty("videoCodec")]
		public string VideoCodec { get; set; }

		/// <summary>
		/// AAC, G711 or G726. Empty (default) for no audio
		/// </summary>
		[JsonProperty("audioCodec")]
		public string AudioCodec { get; set; }

		/// <summary>
		/// Login for the camera stream. Overrides a login inside the URL
		/// </summary>
		[JsonProperty("username")]
		public string Username { get; set; }

		/// <summary>
		/// Password for the camera stream, if it requires one
		/// </summary>
		[JsonProperty("password")]
		public string Password { get; set; }
	}
}
