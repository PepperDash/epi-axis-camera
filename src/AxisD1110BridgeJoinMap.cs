using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.Plugins.Camera.Axis
{
	/// <summary>
	/// Axis D1110 bridge join map
	/// </summary>
	public class AxisD1110BridgeJoinMap : JoinMapBaseAdvanced
	{
		#region Digital

		[JoinName("IsOnline")]
		public JoinDataComplete IsOnline = new JoinDataComplete(
			new JoinData { JoinNumber = 1, JoinSpan = 1 },
			new JoinMetadata
			{
				Description = "Decoder is online",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Digital
			});

		[JoinName("Clear")]
		public JoinDataComplete Clear = new JoinDataComplete(
			new JoinData { JoinNumber = 2, JoinSpan = 1 },
			new JoinMetadata
			{
				Description = "Pulse to blank the decoder output (stream 0)",
				JoinCapabilities = eJoinCapabilities.FromSIMPL,
				JoinType = eJoinType.Digital
			});

		[JoinName("Refresh")]
		public JoinDataComplete Refresh = new JoinDataComplete(
			new JoinData { JoinNumber = 3, JoinSpan = 1 },
			new JoinMetadata
			{
				Description = "Pulse to poll the decoder now",
				JoinCapabilities = eJoinCapabilities.FromSIMPL,
				JoinType = eJoinType.Digital
			});

		#endregion

		#region Analog

		[JoinName("SelectStream")]
		public JoinDataComplete SelectStream = new JoinDataComplete(
			new JoinData { JoinNumber = 1, JoinSpan = 1 },
			new JoinMetadata
			{
				Description = "Stream to show (1-16, 0 = blank) and selected stream feedback",
				JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
				JoinType = eJoinType.Analog
			});

		[JoinName("LastErrorCode")]
		public JoinDataComplete LastErrorCode = new JoinDataComplete(
			new JoinData { JoinNumber = 2, JoinSpan = 1 },
			new JoinMetadata
			{
				Description = "Last Decoder API error code (0 = none)",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Analog
			});

		#endregion

		#region Serial

		[JoinName("DeviceName")]
		public JoinDataComplete DeviceName = new JoinDataComplete(
			new JoinData { JoinNumber = 1, JoinSpan = 1 },
			new JoinMetadata
			{
				Description = "Device name",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Serial
			});

		[JoinName("SelectedStreamName")]
		public JoinDataComplete SelectedStreamName = new JoinDataComplete(
			new JoinData { JoinNumber = 2, JoinSpan = 1 },
			new JoinMetadata
			{
				Description = "Name of the selected stream",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Serial
			});

		[JoinName("LastError")]
		public JoinDataComplete LastError = new JoinDataComplete(
			new JoinData { JoinNumber = 3, JoinSpan = 1 },
			new JoinMetadata
			{
				Description = "Last Decoder API error message",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Serial
			});

		[JoinName("ConfigurationId")]
		public JoinDataComplete ConfigurationId = new JoinDataComplete(
			new JoinData { JoinNumber = 4, JoinSpan = 1 },
			new JoinMetadata
			{
				Description = "Decoder view configuration id",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Serial
			});

		[JoinName("StreamUrls")]
		public JoinDataComplete StreamUrls = new JoinDataComplete(
			new JoinData { JoinNumber = 31, JoinSpan = 16 },
			new JoinMetadata
			{
				Description = "Stream RTSP URLs, in stream number order. Used when streamSource is bridge",
				JoinCapabilities = eJoinCapabilities.FromSIMPL,
				JoinType = eJoinType.Serial
			});

		[JoinName("StreamNames")]
		public JoinDataComplete StreamNames = new JoinDataComplete(
			new JoinData { JoinNumber = 11, JoinSpan = 16 },
			new JoinMetadata
			{
				Description = "Configured stream names, in stream number order",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Serial
			});

		#endregion

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="joinStart">The first join on the EISC bridge</param>
		public AxisD1110BridgeJoinMap(uint joinStart)
			: base(joinStart, typeof(AxisD1110BridgeJoinMap))
		{
		}
	}
}
