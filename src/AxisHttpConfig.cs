using Newtonsoft.Json;

namespace PepperDash.Essentials.Plugins.Camera.Axis
{
	/// <summary>
	/// Connection properties shared by every Axis device type in this plugin
	/// </summary>
	public class AxisHttpConfig
	{
		/// <summary>
		/// IP address or hostname of the device
		/// </summary>
		[JsonProperty("host")]
		public string Host { get; set; }

		/// <summary>
		/// HTTP(S) port. Defaults to 80, or 443 when useHttps is true
		/// </summary>
		[JsonProperty("port")]
		public int Port { get; set; }

		/// <summary>
		/// Use HTTPS to reach the device
		/// </summary>
		[JsonProperty("useHttps")]
		public bool UseHttps { get; set; }

		/// <summary>
		/// Accept a self signed certificate from the device when using HTTPS
		/// </summary>
		[JsonProperty("ignoreCertificateErrors")]
		public bool IgnoreCertificateErrors { get; set; }

		/// <summary>
		/// Device login (Operator role or higher)
		/// </summary>
		[JsonProperty("username")]
		public string Username { get; set; }

		/// <summary>
		/// Device password
		/// </summary>
		[JsonProperty("password")]
		public string Password { get; set; }

		/// <summary>
		/// "digest" (default) answers whatever the device challenges with (Digest or Basic).
		/// "basic" sends a Basic Authorization header on every request.
		/// </summary>
		[JsonProperty("authMode")]
		public string AuthMode { get; set; }

		/// <summary>
		/// Request timeout in milliseconds. Defaults to 5000
		/// </summary>
		[JsonProperty("requestTimeoutMs")]
		public int RequestTimeoutMs { get; set; }

		/// <summary>
		/// The connection described by these properties
		/// </summary>
		public AxisConnection ToConnection()
		{
			return new AxisConnection
			{
				Host = Host,
				Port = Port,
				UseHttps = UseHttps,
				IgnoreCertificateErrors = IgnoreCertificateErrors,
				Username = Username,
				Password = Password,
				AuthMode = AuthMode,
				TimeoutMs = RequestTimeoutMs
			};
		}
	}
}
