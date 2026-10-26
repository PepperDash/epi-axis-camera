using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

namespace PepperDash.Essentials.Plugins.Camera.Axis
{
	/// <summary>
	/// Builds Axis PTZ camera devices
	/// </summary>
	public class AxisCameraFactory : EssentialsPluginDeviceFactory<AxisCameraDevice>
	{
		/// <summary>
		/// Constructor
		/// </summary>
		public AxisCameraFactory()
		{
			MinimumEssentialsFrameworkVersion = "3.0.0";

			TypeNames = new List<string>() { "axisCamera" };
		}

		/// <summary>
		/// Builds and returns an instance of AxisCameraDevice
		/// </summary>
		/// <param name="dc">device configuration</param>
		/// <returns>device or null</returns>
		public override EssentialsDevice BuildDevice(DeviceConfig dc)
		{
			Debug.LogVerbose("[{key}] Factory Attempting to create new device from type: {type}", dc.Key, dc.Type);

			var propertiesConfig = dc.Properties.ToObject<AxisCameraConfig>();
			if (propertiesConfig == null)
			{
				Debug.LogError("[{key}] Factory: failed to read properties config for {name}", dc.Key, dc.Name);
				return null;
			}

			if (!propertiesConfig.IsBridgeSource && string.IsNullOrEmpty(propertiesConfig.Host))
			{
				Debug.LogError("[{key}] Factory: properties.host is required for {name} unless connectionSource is bridge", dc.Key, dc.Name);
				return null;
			}

			return new AxisCameraDevice(dc.Key, dc.Name, propertiesConfig);
		}
	}
}
