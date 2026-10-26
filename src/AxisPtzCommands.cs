using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PepperDash.Essentials.Plugins.Camera.Axis
{
	/// <summary>
	/// Builds the VAPIX PTZ requests (/axis-cgi/com/ptz.cgi and ptzconfig.cgi) and reads their answers.
	/// Speeds are -100 to 100, as the VAPIX continuous move parameters.
	/// </summary>
	public static class AxisPtzCommands
	{
		/// <summary>
		/// PTZ control
		/// </summary>
		public const string PtzPath = "/axis-cgi/com/ptz.cgi";

		/// <summary>
		/// PTZ configuration (saving presets), administrator access
		/// </summary>
		public const string PtzConfigPath = "/axis-cgi/com/ptzconfig.cgi";

		/// <summary>
		/// Restarts the camera
		/// </summary>
		public const string RestartPath = "/axis-cgi/restart.cgi";

		/// <summary>
		/// Keeps a speed in the 1 to 100 range the camera accepts
		/// </summary>
		public static int ClampSpeed(int speed)
		{
			return Math.Max(1, Math.Min(100, speed));
		}

		/// <summary>
		/// Starts a continuous pan and tilt. Positive pan is right, positive tilt is up. 0,0 stops
		/// </summary>
		public static string PanTilt(int pan, int tilt)
		{
			return PtzPath + "?continuouspantiltmove=" + pan + "," + tilt;
		}

		/// <summary>
		/// Stops panning and tilting
		/// </summary>
		public static string StopPanTilt()
		{
			return PanTilt(0, 0);
		}

		/// <summary>
		/// Starts a continuous zoom. Positive zooms in. 0 stops
		/// </summary>
		public static string Zoom(int speed)
		{
			return PtzPath + "?continuouszoommove=" + speed;
		}

		/// <summary>
		/// Starts a continuous focus move. Positive focuses far, negative near. 0 stops
		/// </summary>
		public static string Focus(int speed)
		{
			return PtzPath + "?continuousfocusmove=" + speed;
		}

		/// <summary>
		/// Starts a continuous iris move. Positive opens. 0 stops
		/// </summary>
		public static string Iris(int speed)
		{
			return PtzPath + "?continuousirismove=" + speed;
		}

		/// <summary>
		/// Moves to the home position
		/// </summary>
		public static string Home()
		{
			return PtzPath + "?move=home";
		}

		/// <summary>
		/// Turns autofocus on or off
		/// </summary>
		public static string AutoFocus(bool on)
		{
			return PtzPath + "?autofocus=" + (on ? "on" : "off");
		}

		/// <summary>
		/// Turns auto iris on or off
		/// </summary>
		public static string AutoIris(bool on)
		{
			return PtzPath + "?autoiris=" + (on ? "on" : "off");
		}

		/// <summary>
		/// Asks for the current position and the autofocus and auto iris state
		/// </summary>
		public static string QueryPosition()
		{
			return PtzPath + "?query=position";
		}

		/// <summary>
		/// The name a server preset is saved under, for example Preset01
		/// </summary>
		public static string PresetName(int number)
		{
			return "Preset" + number.ToString("00", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Moves to a saved preset
		/// </summary>
		/// <param name="number">Preset number, 1 or higher</param>
		/// <param name="server">True for server presets (by name), false for device presets (by number)</param>
		public static string GotoPreset(int number, bool server)
		{
			return server
				? PtzPath + "?gotoserverpresetname=" + PresetName(number)
				: PtzPath + "?gotodevicepreset=" + number;
		}

		/// <summary>
		/// Saves the current position as a preset
		/// </summary>
		/// <param name="number">Preset number, 1 or higher</param>
		/// <param name="server">True for server presets (by name), false for device presets (by number)</param>
		/// <param name="path">PtzConfigPath (documented by Axis) or PtzPath (older firmware, used by the old control program)</param>
		/// <param name="homeIfFirst">Also makes preset 1 the home position</param>
		public static string SetPreset(int number, bool server, string path, bool homeIfFirst)
		{
			if (!server)
			{
				return path + "?setdevicepreset=" + number;
			}

			var request = path + "?setserverpresetname=" + PresetName(number);

			if (homeIfFirst && number == 1)
			{
				request += "&home=yes";
			}

			return request;
		}

		/// <summary>
		/// Decides whether an answer to a PTZ request was an error. VAPIX answers success with 204 and no body.
		/// An error is HTTP 200 with a body that starts with Error:
		/// </summary>
		/// <param name="result">What came back</param>
		/// <param name="error">The reason when this returns true</param>
		public static bool IsError(AxisHttpResult result, out string error)
		{
			error = null;

			if (!result.GotResponse)
			{
				error = result.Error;
				return true;
			}

			if (!result.IsSuccessStatusCode)
			{
				error = result.Describe();
				return true;
			}

			var body = (result.Body ?? string.Empty).TrimStart();
			if (body.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
			{
				error = body.Substring("Error:".Length).Trim();
				if (error.Length == 0) error = "the camera reported an error";
				return true;
			}

			return false;
		}
	}

	/// <summary>
	/// What query=position reports
	/// </summary>
	public sealed class AxisPtzPosition
	{
		private static readonly Regex AutoFocusPattern = new Regex(@"autofocus\s*=\s*(on|off)", RegexOptions.IgnoreCase);
		private static readonly Regex AutoIrisPattern = new Regex(@"autoiris\s*=\s*(on|off)", RegexOptions.IgnoreCase);
		private static readonly Regex PanPattern = new Regex(@"(?<![a-z])pan\s*=\s*(-?\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
		private static readonly Regex TiltPattern = new Regex(@"(?<![a-z])tilt\s*=\s*(-?\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
		private static readonly Regex ZoomPattern = new Regex(@"(?<![a-z])zoom\s*=\s*(-?\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);

		/// <summary>
		/// Autofocus state, or null when the camera did not report it
		/// </summary>
		public bool? AutoFocus { get; private set; }

		/// <summary>
		/// Auto iris state, or null when the camera did not report it
		/// </summary>
		public bool? AutoIris { get; private set; }

		/// <summary>
		/// Pan in degrees, or null
		/// </summary>
		public double? Pan { get; private set; }

		/// <summary>
		/// Tilt in degrees, or null
		/// </summary>
		public double? Tilt { get; private set; }

		/// <summary>
		/// Zoom step, or null
		/// </summary>
		public double? Zoom { get; private set; }

		/// <summary>
		/// Reads a query=position answer. Works whether the values are on separate lines or run together
		/// </summary>
		public static AxisPtzPosition Parse(string body)
		{
			var position = new AxisPtzPosition();

			if (string.IsNullOrEmpty(body)) return position;

			position.AutoFocus = OnOff(AutoFocusPattern, body);
			position.AutoIris = OnOff(AutoIrisPattern, body);
			position.Pan = Number(PanPattern, body);
			position.Tilt = Number(TiltPattern, body);
			position.Zoom = Number(ZoomPattern, body);

			return position;
		}

		private static bool? OnOff(Regex pattern, string text)
		{
			var match = pattern.Match(text);
			if (!match.Success) return null;

			return string.Equals(match.Groups[1].Value, "on", StringComparison.OrdinalIgnoreCase);
		}

		private static double? Number(Regex pattern, string text)
		{
			var match = pattern.Match(text);
			if (!match.Success) return null;

			double value;
			return double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : (double?)null;
		}
	}
}
