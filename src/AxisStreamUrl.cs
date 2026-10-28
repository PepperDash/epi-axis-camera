using System;
using System.Collections.Generic;

namespace PepperDash.Essentials.Plugins.Camera.Axis
{
	/// <summary>
	/// An RTSP stream URL taken apart so the credentials can be moved out of it and the resolution changed.
	/// </summary>
	/// <remarks>
	/// Parsed by hand instead of with System.Uri because the URLs are built in SIMPL and the password may contain
	/// characters (such as @) that Uri rejects. The credentials are everything before the last @ in the authority.
	/// </remarks>
	public sealed class AxisStreamUrl
	{
		private const string ResolutionKey = "resolution";

		/// <summary>
		/// For example rtsp
		/// </summary>
		public string Scheme { get; private set; }

		/// <summary>
		/// Login taken from user:password@host. Empty when the URL has none
		/// </summary>
		public string Username { get; private set; }

		/// <summary>
		/// Password taken from user:password@host. Empty when the URL has none
		/// </summary>
		public string Password { get; private set; }

		/// <summary>
		/// Host and optional port, for example 192.168.10.240 or 192.168.10.240:554
		/// </summary>
		public string HostPort { get; private set; }

		/// <summary>
		/// Path, starting with a slash, or empty
		/// </summary>
		public string Path { get; private set; }

		/// <summary>
		/// Query without the leading question mark, or empty
		/// </summary>
		public string Query { get; private set; }

		private AxisStreamUrl()
		{
		}

		/// <summary>
		/// Splits a URL of the form scheme://[user[:password]@]host[:port][/path][?query]
		/// </summary>
		/// <param name="url">URL as sent by SIMPL or written in the configuration</param>
		/// <param name="result">The parsed URL when this returns true</param>
		/// <returns>False when the text is not a URL with a scheme and a host</returns>
		public static bool TryParse(string url, out AxisStreamUrl result)
		{
			result = null;

			if (string.IsNullOrWhiteSpace(url)) return false;

			url = url.Trim();

			var schemeEnd = url.IndexOf("://", StringComparison.Ordinal);
			if (schemeEnd <= 0) return false;

			var scheme = url.Substring(0, schemeEnd);
			var rest = url.Substring(schemeEnd + 3);

			var fragmentStart = rest.IndexOf('#');
			if (fragmentStart >= 0) rest = rest.Substring(0, fragmentStart);

			var authorityEnd = rest.IndexOfAny(new[] { '/', '?' });
			var authority = authorityEnd < 0 ? rest : rest.Substring(0, authorityEnd);
			var remainder = authorityEnd < 0 ? string.Empty : rest.Substring(authorityEnd);

			var username = string.Empty;
			var password = string.Empty;

			var at = authority.LastIndexOf('@');
			if (at >= 0)
			{
				var userInfo = authority.Substring(0, at);
				authority = authority.Substring(at + 1);

				var colon = userInfo.IndexOf(':');
				username = Unescape(colon < 0 ? userInfo : userInfo.Substring(0, colon));
				password = colon < 0 ? string.Empty : Unescape(userInfo.Substring(colon + 1));
			}

			if (authority.Length == 0) return false;

			var queryStart = remainder.IndexOf('?');
			var path = queryStart < 0 ? remainder : remainder.Substring(0, queryStart);
			var query = queryStart < 0 ? string.Empty : remainder.Substring(queryStart + 1);

			result = new AxisStreamUrl
			{
				Scheme = scheme,
				Username = username,
				Password = password,
				HostPort = authority,
				Path = path,
				Query = query
			};

			return true;
		}

		/// <summary>
		/// The default path of an Axis camera's main stream
		/// </summary>
		public const string DefaultCameraPath = "/axis-media/media.amp";

		/// <summary>
		/// Builds the RTSP URL of a camera stream: rtsp://host + path, with the resolution set when one is given.
		/// Returns an empty string when there is no host yet
		/// </summary>
		/// <param name="host">Camera address</param>
		/// <param name="path">Stream path. Empty uses the Axis default</param>
		/// <param name="resolution">For example 800x450. Empty leaves the URL without one</param>
		public static string ForCamera(string host, string path, string resolution)
		{
			if (string.IsNullOrWhiteSpace(host)) return string.Empty;

			path = string.IsNullOrWhiteSpace(path) ? DefaultCameraPath : path.Trim();
			if (!path.StartsWith("/", StringComparison.Ordinal)) path = "/" + path;

			AxisStreamUrl url;
			if (!TryParse("rtsp://" + host.Trim() + path, out url)) return string.Empty;

			return url.WithResolution(resolution).ToUrl();
		}

		/// <summary>
		/// Returns a copy with the resolution query parameter set. Replaces the value if the URL already has
		/// one, otherwise adds it. An empty resolution returns this URL unchanged.
		/// </summary>
		/// <param name="resolution">For example 1920x1080</param>
		public AxisStreamUrl WithResolution(string resolution)
		{
			if (string.IsNullOrWhiteSpace(resolution)) return this;

			var parts = new List<string>();
			var replaced = false;

			foreach (var part in Query.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
			{
				var equals = part.IndexOf('=');
				var key = equals < 0 ? part : part.Substring(0, equals);

				if (string.Equals(key, ResolutionKey, StringComparison.OrdinalIgnoreCase))
				{
					if (!replaced) parts.Add(ResolutionKey + "=" + resolution.Trim());
					replaced = true;
					continue;
				}

				parts.Add(part);
			}

			if (!replaced) parts.Add(ResolutionKey + "=" + resolution.Trim());

			return new AxisStreamUrl
			{
				Scheme = Scheme,
				Username = Username,
				Password = Password,
				HostPort = HostPort,
				Path = Path,
				Query = string.Join("&", parts)
			};
		}

		/// <summary>
		/// The URL without credentials, as sent to the decoder
		/// </summary>
		public string ToUrl()
		{
			return Scheme + "://" + HostPort + Path + (Query.Length > 0 ? "?" + Query : string.Empty);
		}

		/// <summary>
		/// The URL with the login shown and the password hidden, for logging
		/// </summary>
		public string ToMaskedUrl()
		{
			var login = Username.Length > 0 ? Username + ":***@" : string.Empty;

			return Scheme + "://" + login + HostPort + Path + (Query.Length > 0 ? "?" + Query : string.Empty);
		}

		private static string Unescape(string text)
		{
			try
			{
				return Uri.UnescapeDataString(text);
			}
			catch (UriFormatException)
			{
				return text;
			}
		}
	}

	/// <summary>
	/// What a decoder does when the camera it is showing enters or leaves privacy
	/// </summary>
	public enum AxisPrivacyAction
	{
		/// <summary>
		/// Nothing changes
		/// </summary>
		None,

		/// <summary>
		/// Blank the decoder output
		/// </summary>
		Blank,

		/// <summary>
		/// Show the selected stream again
		/// </summary>
		Restore
	}

	/// <summary>
	/// Rules for camera privacy: privacy hides the camera's video everywhere, in addition to the camera moving to its privacy preset
	/// </summary>
	public static class AxisPrivacy
	{
		/// <summary>
		/// The preview URL SIMPL is sent: empty while the camera's video is hidden by privacy
		/// </summary>
		/// <param name="host">Camera address</param>
		/// <param name="path">Stream path</param>
		/// <param name="resolution">Preview resolution</param>
		/// <param name="hidden">True while privacy hides the camera's video</param>
		public static string PreviewUrl(string host, string path, string resolution, bool hidden)
		{
			return hidden ? string.Empty : AxisStreamUrl.ForCamera(host, path, resolution);
		}

		/// <summary>
		/// What a decoder showing a camera should do now
		/// </summary>
		/// <param name="cameraHidesVideo">The camera's video is hidden by privacy</param>
		/// <param name="outputBlankedByPrivacy">The decoder output is currently blanked because of privacy</param>
		public static AxisPrivacyAction Plan(bool cameraHidesVideo, bool outputBlankedByPrivacy)
		{
			if (cameraHidesVideo && !outputBlankedByPrivacy) return AxisPrivacyAction.Blank;
			if (!cameraHidesVideo && outputBlankedByPrivacy) return AxisPrivacyAction.Restore;

			return AxisPrivacyAction.None;
		}

		/// <summary>
		/// True when a decoder showing several panes has to send its layout again, because privacy changed
		/// on the camera of at least one pane. Each pane is planned on its own with <see cref="Plan"/>
		/// </summary>
		/// <param name="paneCameraHidesVideo">Per pane: the camera of the pane's stream has its video hidden by privacy</param>
		/// <param name="paneBlankedByPrivacy">Per pane: the pane is currently left out because of privacy</param>
		public static bool PanesNeedApply(IList<bool> paneCameraHidesVideo, IList<bool> paneBlankedByPrivacy)
		{
			for (var pane = 0; pane < paneCameraHidesVideo.Count && pane < paneBlankedByPrivacy.Count; pane++)
			{
				if (Plan(paneCameraHidesVideo[pane], paneBlankedByPrivacy[pane]) != AxisPrivacyAction.None) return true;
			}

			return false;
		}
	}

	/// <summary>
	/// Decides whether a value sent on the select stream join is acted on
	/// </summary>
	public static class AxisD1110Select
	{
		/// <summary>
		/// Highest stream number a decoder can be configured with or told to show
		/// </summary>
		public const int MaxStreams = 64;

		/// <summary>
		/// True when the number is a stream number (1 to <see cref="MaxStreams"/>)
		/// </summary>
		/// <param name="number">Stream number</param>
		public static bool IsStreamNumber(int number)
		{
			return number >= 1 && number <= MaxStreams;
		}

		/// <summary>
		/// Reads a key of the streams configuration
		/// </summary>
		/// <param name="key">Key from the config, for example "33"</param>
		/// <param name="number">The stream number when this returns true</param>
		/// <returns>False when the key is not a number from 1 to <see cref="MaxStreams"/></returns>
		public static bool TryParseStreamKey(string key, out int number)
		{
			return int.TryParse(key, out number) && IsStreamNumber(number);
		}

		/// <summary>
		/// True when the value should select a stream
		/// </summary>
		/// <param name="value">What SIMPL sent on the select stream join</param>
		/// <param name="onlyConfiguredStreams">The selectOnlyConfiguredStreams property</param>
		/// <param name="isConfigured">Whether a stream number is a configured stream</param>
		public static bool ShouldSelect(int value, bool onlyConfiguredStreams, System.Func<int, bool> isConfigured)
		{
			if (!onlyConfiguredStreams) return true;

			return value >= 1 && isConfigured(value);
		}
	}

	/// <summary>
	/// What one decoder pane ends up showing
	/// </summary>
	public enum AxisD1110PaneStatus
	{
		/// <summary>
		/// No stream asked for (0)
		/// </summary>
		Empty,

		/// <summary>
		/// The stream's camera is in privacy, so the pane is left out
		/// </summary>
		HiddenByPrivacy,

		/// <summary>
		/// The stream cannot be built (not configured, no URL, camera missing), so the pane is left out
		/// </summary>
		Unavailable,

		/// <summary>
		/// The stream is shown
		/// </summary>
		Shown
	}

	/// <summary>
	/// One decoder pane: the stream asked for and what came of it
	/// </summary>
	public sealed class AxisD1110Pane
	{
		/// <summary>
		/// Stream number asked for. 0 means none
		/// </summary>
		public int Number { get; set; }

		/// <summary>
		/// What the pane shows
		/// </summary>
		public AxisD1110PaneStatus Status { get; set; }

		/// <summary>
		/// The stream to send when Status is Shown
		/// </summary>
		public AxisD1110StreamConfig Stream { get; set; }

		/// <summary>
		/// Why the stream cannot be built when Status is Unavailable
		/// </summary>
		public string Error { get; set; }
	}

	/// <summary>
	/// Builds the stream for a stream number
	/// </summary>
	/// <param name="number">Stream number</param>
	/// <param name="stream">The stream to send when this returns true</param>
	/// <param name="error">Why no stream could be built when this returns false</param>
	public delegate bool AxisD1110PaneBuilder(int number, out AxisD1110StreamConfig stream, out string error);

	/// <summary>
	/// Rules for the decoder layout: up to two panes, like a quad multiviewer following two touch panel video windows.
	/// A pane that is 0, hidden by privacy or cannot be built is left out and the other pane is still shown.
	/// No pane left blanks the output, one is shown full screen, two are shown side by side
	/// </summary>
	public static class AxisD1110Layout
	{
		/// <summary>
		/// Number of panes the decoder is driven with
		/// </summary>
		public const int PaneCount = 2;

		/// <summary>
		/// Decides what one pane shows
		/// </summary>
		/// <param name="number">Stream number asked for, 0 for none</param>
		/// <param name="hiddenByPrivacy">The stream's camera is in privacy</param>
		/// <param name="build">Builds the stream. Not called for 0 or a stream hidden by privacy</param>
		public static AxisD1110Pane Resolve(int number, bool hiddenByPrivacy, AxisD1110PaneBuilder build)
		{
			var pane = new AxisD1110Pane { Number = number };

			if (number == 0)
			{
				pane.Status = AxisD1110PaneStatus.Empty;
				return pane;
			}

			if (hiddenByPrivacy)
			{
				pane.Status = AxisD1110PaneStatus.HiddenByPrivacy;
				return pane;
			}

			AxisD1110StreamConfig stream;
			string error;
			if (build(number, out stream, out error) && stream != null)
			{
				pane.Status = AxisD1110PaneStatus.Shown;
				pane.Stream = stream;
				return pane;
			}

			pane.Status = AxisD1110PaneStatus.Unavailable;
			pane.Error = string.IsNullOrEmpty(error) ? "the stream cannot be built" : error;
			return pane;
		}

		/// <summary>
		/// The streams to send to the decoder, in pane order, leaving out every pane that shows nothing
		/// </summary>
		/// <param name="panes">The resolved panes</param>
		public static List<AxisD1110StreamConfig> Visible(IEnumerable<AxisD1110Pane> panes)
		{
			var visible = new List<AxisD1110StreamConfig>();

			foreach (var pane in panes)
			{
				if (pane != null && pane.Status == AxisD1110PaneStatus.Shown) visible.Add(pane.Stream);
			}

			return visible;
		}
	}

	/// <summary>
	/// Turns a stream's configuration, and the URL SIMPL may have sent, into what is sent to the decoder
	/// </summary>
	public static class AxisD1110StreamBuilder
	{
		/// <summary>
		/// Builds the stream to send to the decoder
		/// </summary>
		/// <remarks>
		/// A stream with a cameraKey is that camera: the URL is rtsp://camera address + cameraStreamPath.
		/// Otherwise the URL comes from the bridge when streamSource is bridge, or from the stream's configuration.
		/// The login is the stream's own username and password if set, otherwise the one inside the URL,
		/// otherwise the camera's, otherwise the plugin level streamUsername and streamPassword. The resolution is the stream's own
		/// if set, otherwise the plugin level one. The URL sent never contains the login.
		/// </remarks>
		/// <param name="config">Device configuration</param>
		/// <param name="stream">The stream's configuration. Null when only the bridge knows the stream</param>
		/// <param name="bridgeUrl">URL last sent by SIMPL for this stream, or null</param>
		/// <param name="result">The stream to send when this returns true</param>
		/// <param name="error">Why no stream could be built when this returns false</param>
		public static bool TryBuild(AxisD1110Config config, AxisD1110StreamConfig stream, string bridgeUrl, out AxisD1110StreamConfig result, out string error)
		{
			return TryBuild(config, stream, bridgeUrl, null, out result, out error);
		}

		/// <summary>
		/// Builds the stream to send to the decoder, taking the address and login from a camera when the stream names one
		/// </summary>
		/// <param name="config">Device configuration</param>
		/// <param name="stream">The stream's configuration. Null when only the bridge knows the stream</param>
		/// <param name="bridgeUrl">URL last sent by SIMPL for this stream, or null</param>
		/// <param name="camera">The camera the stream's cameraKey names, or null when it has none or it was not found</param>
		/// <param name="result">The stream to send when this returns true</param>
		/// <param name="error">Why no stream could be built when this returns false</param>
		public static bool TryBuild(AxisD1110Config config, AxisD1110StreamConfig stream, string bridgeUrl, IAxisCameraSource camera, out AxisD1110StreamConfig result, out string error)
		{
			result = null;
			error = null;

			var cameraKey = stream == null ? null : stream.CameraKey;
			string source;

			if (!string.IsNullOrWhiteSpace(cameraKey))
			{
				if (camera == null)
				{
					error = "camera " + cameraKey + " was not found";
					return false;
				}

				if (string.IsNullOrWhiteSpace(camera.Host))
				{
					error = "camera " + cameraKey + " has no address yet";
					return false;
				}

				// the decoder's own cameraStreamPath wins, then the camera's streamPath, then the Axis default
				var path = !string.IsNullOrWhiteSpace(config.CameraStreamPath) ? config.CameraStreamPath : camera.StreamPath;
				source = AxisStreamUrl.ForCamera(camera.Host, path, null);
			}
			else
			{
				var bridge = config.IsBridgeSource;
				source = bridge ? bridgeUrl : (stream == null ? null : stream.Url);

				if (string.IsNullOrWhiteSpace(source))
				{
					error = bridge ? "no URL has been received from the bridge" : "no url is configured";
					return false;
				}
			}

			AxisStreamUrl parsed;
			if (!AxisStreamUrl.TryParse(source, out parsed))
			{
				error = "not a valid stream URL: " + source;
				return false;
			}

			var resolution = stream != null && !string.IsNullOrWhiteSpace(stream.Resolution) ? stream.Resolution : config.Resolution;
			parsed = parsed.WithResolution(resolution);

			var cameraUsername = camera == null ? null : camera.Username;
			var cameraPassword = camera == null ? null : camera.Password;

			var username = FirstNonEmpty(stream == null ? null : stream.Username, parsed.Username, cameraUsername, config.StreamUsername);

			// the password goes with the login that supplied it
			string password;
			if (stream != null && !string.IsNullOrEmpty(stream.Username))
			{
				password = stream.Password ?? string.Empty;
			}
			else if (!string.IsNullOrEmpty(parsed.Username))
			{
				password = parsed.Password;
			}
			else if (!string.IsNullOrEmpty(cameraUsername))
			{
				password = cameraPassword ?? string.Empty;
			}
			else
			{
				password = config.StreamPassword ?? string.Empty;
			}

			result = new AxisD1110StreamConfig
			{
				Name = stream == null ? null : stream.Name,
				Url = parsed.ToUrl(),
				VideoCodec = stream == null ? null : stream.VideoCodec,
				AudioCodec = stream == null ? null : stream.AudioCodec,
				Username = username,
				Password = password
			};

			return true;
		}

		private static string FirstNonEmpty(params string[] values)
		{
			foreach (var value in values)
			{
				if (!string.IsNullOrEmpty(value)) return value;
			}

			return string.Empty;
		}
	}
}
