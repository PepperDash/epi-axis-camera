using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.Plugins.Camera.Axis
{
	/// <summary>
	/// Axis D1110 bridge join map
	/// </summary>
	/// <remarks>
	/// Contiguous layout (default): every join is an offset from the joinStart of the bridge entry.
	///
	/// Indexed layout, for one small EISC shared by up to 8 decoders (decoder n, n starting at 1, use joinStart 1):
	/// digital online n, clear 8+n, refresh 16+n. Analog select stream n, last error code 8+n, select stream 2 16+n.
	/// Serial device name n, selected stream name 8+n, last error 16+n, configuration id 24+n.
	/// The stream names and stream URLs joins are not used. Everything fits in 32 joins of each type.
	/// </remarks>
	public class AxisD1110BridgeJoinMap : JoinMapBaseAdvanced
	{
		/// <summary>
		/// Most decoders the indexed layout can hold
		/// </summary>
		public const int MaxIndexedDecoders = 8;

		/// <summary>
		/// Streams that have a stream name join and a stream URL join in the contiguous layout (streams 1 to 16).
		/// Higher stream numbers can still be selected, they just have no name or URL join
		/// </summary>
		public const int MaxBridgeStreams = 16;

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
				Description = "Stream to show (1-64, 0 = blank) and selected stream feedback",
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

		[JoinName("SelectStream2")]
		public JoinDataComplete SelectStream2 = new JoinDataComplete(
			new JoinData { JoinNumber = 3, JoinSpan = 1 },
			new JoinMetadata
			{
				Description = "Second stream, shown side by side with the first (0 = none)",
				JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
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
			new JoinData { JoinNumber = 31, JoinSpan = MaxBridgeStreams },
			new JoinMetadata
			{
				Description = "Stream RTSP URLs for streams 1-16, in stream number order. Used when streamSource is bridge",
				JoinCapabilities = eJoinCapabilities.FromSIMPL,
				JoinType = eJoinType.Serial
			});

		[JoinName("StreamNames")]
		public JoinDataComplete StreamNames = new JoinDataComplete(
			new JoinData { JoinNumber = 11, JoinSpan = MaxBridgeStreams },
			new JoinMetadata
			{
				Description = "Configured stream names for streams 1-16, in stream number order",
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

		/// <summary>
		/// Constructor that applies the indexed layout when decoderIndex is 1 to 8
		/// </summary>
		/// <param name="joinStart">The first join on the EISC bridge. Use 1 with the indexed layout</param>
		/// <param name="decoderIndex">Which decoder this is in the indexed layout, or 0 for the contiguous layout</param>
		public AxisD1110BridgeJoinMap(uint joinStart, int decoderIndex)
			: this(joinStart)
		{
			if (decoderIndex >= 1 && decoderIndex <= MaxIndexedDecoders)
			{
				ApplyIndexedLayout((uint)decoderIndex);
			}
		}

		private void ApplyIndexedLayout(uint index)
		{
			// raw numbers, so the joinStart offset is added once on top
			IsOnline.JoinNumber = index;
			Clear.JoinNumber = 8 + index;
			Refresh.JoinNumber = 16 + index;

			SelectStream.JoinNumber = index;
			LastErrorCode.JoinNumber = 8 + index;
			SelectStream2.JoinNumber = 16 + index;

			DeviceName.JoinNumber = index;
			SelectedStreamName.JoinNumber = 8 + index;
			LastError.JoinNumber = 16 + index;
			ConfigurationId.JoinNumber = 24 + index;
		}
	}
}
