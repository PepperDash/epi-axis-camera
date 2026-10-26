using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.Plugins.Camera.Axis
{
	/// <summary>
	/// Axis camera bridge join map.
	/// </summary>
	/// <remarks>
	/// Contiguous layout (default): every join is an offset from the joinStart of the bridge entry.
	///
	/// Indexed layout, for one EISC shared by many cameras (camera n of the table below, n starting at 1):
	/// digital joins are the contiguous ones plus 30 for each camera before this one, the preset save analog join is n,
	/// and the serial joins are n (host), 50+n (port), 100+n (username), 150+n (password), 200+n (name), 250+n (last error).
	/// </remarks>
	public class AxisCameraBridgeJoinMap : JoinMapBaseAdvanced
	{
		/// <summary>
		/// Digital joins used by each camera in the indexed layout
		/// </summary>
		public const uint IndexedDigitalStride = 30;

		#region Digital

		[JoinName("Up")]
		public JoinDataComplete Up = Digital(1, 1, "Tilt up while held", eJoinCapabilities.FromSIMPL);

		[JoinName("Down")]
		public JoinDataComplete Down = Digital(2, 1, "Tilt down while held", eJoinCapabilities.FromSIMPL);

		[JoinName("Left")]
		public JoinDataComplete Left = Digital(3, 1, "Pan left while held", eJoinCapabilities.FromSIMPL);

		[JoinName("Right")]
		public JoinDataComplete Right = Digital(4, 1, "Pan right while held", eJoinCapabilities.FromSIMPL);

		[JoinName("UpRight")]
		public JoinDataComplete UpRight = Digital(5, 1, "Tilt up and pan right while held", eJoinCapabilities.FromSIMPL);

		[JoinName("DownRight")]
		public JoinDataComplete DownRight = Digital(6, 1, "Tilt down and pan right while held", eJoinCapabilities.FromSIMPL);

		[JoinName("UpLeft")]
		public JoinDataComplete UpLeft = Digital(7, 1, "Tilt up and pan left while held", eJoinCapabilities.FromSIMPL);

		[JoinName("DownLeft")]
		public JoinDataComplete DownLeft = Digital(8, 1, "Tilt down and pan left while held", eJoinCapabilities.FromSIMPL);

		[JoinName("ZoomIn")]
		public JoinDataComplete ZoomIn = Digital(9, 1, "Zoom in while held", eJoinCapabilities.FromSIMPL);

		[JoinName("ZoomOut")]
		public JoinDataComplete ZoomOut = Digital(10, 1, "Zoom out while held", eJoinCapabilities.FromSIMPL);

		[JoinName("FocusFar")]
		public JoinDataComplete FocusFar = Digital(11, 1, "Focus far (positive speed) while held", eJoinCapabilities.FromSIMPL);

		[JoinName("FocusNear")]
		public JoinDataComplete FocusNear = Digital(12, 1, "Focus near (negative speed) while held", eJoinCapabilities.FromSIMPL);

		[JoinName("AutoFocus")]
		public JoinDataComplete AutoFocus = Digital(13, 1, "Press to toggle autofocus. Feedback is the autofocus state", eJoinCapabilities.ToFromSIMPL);

		[JoinName("IrisOpen")]
		public JoinDataComplete IrisOpen = Digital(14, 1, "Open the iris while held", eJoinCapabilities.FromSIMPL);

		[JoinName("IrisClose")]
		public JoinDataComplete IrisClose = Digital(15, 1, "Close the iris while held", eJoinCapabilities.FromSIMPL);

		[JoinName("AutoIris")]
		public JoinDataComplete AutoIris = Digital(16, 1, "Press to toggle auto iris. Feedback is the auto iris state", eJoinCapabilities.ToFromSIMPL);

		[JoinName("PresetRecall")]
		public JoinDataComplete PresetRecall = Digital(17, 8, "Press to recall preset 1 to 8", eJoinCapabilities.FromSIMPL);

		[JoinName("PresetStored")]
		public JoinDataComplete PresetStored = Digital(25, 1, "High for a moment after a preset is saved", eJoinCapabilities.ToSIMPL);

		[JoinName("Restart")]
		public JoinDataComplete Restart = Digital(26, 1, "Press to restart the camera", eJoinCapabilities.FromSIMPL);

		[JoinName("Home")]
		public JoinDataComplete Home = Digital(27, 1, "Press to move to the home position", eJoinCapabilities.FromSIMPL);

		[JoinName("IsOnline")]
		public JoinDataComplete IsOnline = Digital(28, 1, "Camera answered the last request", eJoinCapabilities.ToSIMPL);

		#endregion

		#region Analog

		[JoinName("PresetSave")]
		public JoinDataComplete PresetSave = new JoinDataComplete(
			new JoinData { JoinNumber = 1, JoinSpan = 1 },
			new JoinMetadata
			{
				Description = "Set to a preset number to save the current position as that preset",
				JoinCapabilities = eJoinCapabilities.FromSIMPL,
				JoinType = eJoinType.Analog
			});

		#endregion

		#region Serial

		[JoinName("Host")]
		public JoinDataComplete Host = Serial(1, "Camera address. Used when connectionSource is bridge", eJoinCapabilities.FromSIMPL);

		[JoinName("Port")]
		public JoinDataComplete Port = Serial(2, "Camera HTTP port. Used when connectionSource is bridge", eJoinCapabilities.FromSIMPL);

		[JoinName("Username")]
		public JoinDataComplete Username = Serial(3, "Camera login. Used when connectionSource is bridge", eJoinCapabilities.FromSIMPL);

		[JoinName("Password")]
		public JoinDataComplete Password = Serial(4, "Camera password. Used when connectionSource is bridge", eJoinCapabilities.FromSIMPL);

		[JoinName("DeviceName")]
		public JoinDataComplete DeviceName = Serial(5, "Device name", eJoinCapabilities.ToSIMPL);

		[JoinName("LastError")]
		public JoinDataComplete LastError = Serial(6, "Last error from the camera", eJoinCapabilities.ToSIMPL);

		#endregion

		/// <summary>
		/// Constructor for the contiguous layout
		/// </summary>
		/// <param name="joinStart">The first join on the EISC bridge</param>
		public AxisCameraBridgeJoinMap(uint joinStart)
			: base(joinStart, typeof(AxisCameraBridgeJoinMap))
		{
		}

		/// <summary>
		/// Constructor that applies the indexed layout when cameraIndex is 1 or higher
		/// </summary>
		/// <param name="joinStart">The first join on the EISC bridge. Use 1 with the indexed layout</param>
		/// <param name="cameraIndex">Which camera this is in the indexed layout, or 0 for the contiguous layout</param>
		public AxisCameraBridgeJoinMap(uint joinStart, int cameraIndex)
			: this(joinStart)
		{
			if (cameraIndex >= 1)
			{
				ApplyIndexedLayout((uint)cameraIndex);
			}
		}

		private void ApplyIndexedLayout(uint index)
		{
			var shift = (index - 1) * IndexedDigitalStride;

			foreach (var join in new[]
			{
				Up, Down, Left, Right, UpRight, DownRight, UpLeft, DownLeft, ZoomIn, ZoomOut, FocusFar, FocusNear,
				AutoFocus, IrisOpen, IrisClose, AutoIris, PresetRecall, PresetStored, Restart, Home, IsOnline
			})
			{
				join.JoinNumber = join.JoinNumber - JoinOffset + shift;
			}

			PresetSave.JoinNumber = index;
			Host.JoinNumber = index;
			Port.JoinNumber = 50 + index;
			Username.JoinNumber = 100 + index;
			Password.JoinNumber = 150 + index;
			DeviceName.JoinNumber = 200 + index;
			LastError.JoinNumber = 250 + index;
		}

		private static JoinDataComplete Digital(uint number, uint span, string description, eJoinCapabilities capabilities)
		{
			return new JoinDataComplete(
				new JoinData { JoinNumber = number, JoinSpan = span },
				new JoinMetadata { Description = description, JoinCapabilities = capabilities, JoinType = eJoinType.Digital });
		}

		private static JoinDataComplete Serial(uint number, string description, eJoinCapabilities capabilities)
		{
			return new JoinDataComplete(
				new JoinData { JoinNumber = number, JoinSpan = 1 },
				new JoinMetadata { Description = description, JoinCapabilities = capabilities, JoinType = eJoinType.Serial });
		}
	}
}
