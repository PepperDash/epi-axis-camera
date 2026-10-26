using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

namespace PepperDash.Essentials.Plugins.Camera.Axis
{
	/// <summary>
	/// Builds Axis D1110 decoder devices
	/// </summary>
	public class AxisD1110DeviceFactory : EssentialsPluginDeviceFactory<AxisD1110Device>
	{
		/// <summary>
		/// Constructor
		/// </summary>
		public AxisD1110DeviceFactory()
		{
			MinimumEssentialsFrameworkVersion = "3.0.0";

			TypeNames = new List<string>() { "axisD1110" };
		}

		/// <summary>
		/// Builds and returns an instance of AxisD1110Device
		/// </summary>
		/// <param name="dc">device configuration</param>
		/// <returns>device or null</returns>
		public override EssentialsDevice BuildDevice(DeviceConfig dc)
		{
			Debug.LogVerbose("[{key}] Factory Attempting to create new device from type: {type}", dc.Key, dc.Type);

			var propertiesConfig = dc.Properties.ToObject<AxisD1110Config>();
			if (propertiesConfig == null)
			{
				Debug.LogError("[{key}] Factory: failed to read properties config for {name}", dc.Key, dc.Name);
				return null;
			}

			if (!string.IsNullOrEmpty(propertiesConfig.StreamSource) &&
				!string.Equals(propertiesConfig.StreamSource, "config", System.StringComparison.OrdinalIgnoreCase) &&
				!propertiesConfig.IsBridgeSource)
			{
				Debug.LogWarning("[{key}] Factory: streamSource {source} is not config or bridge, using config", dc.Key, propertiesConfig.StreamSource);
			}

			if (string.IsNullOrEmpty(propertiesConfig.Host))
			{
				Debug.LogError("[{key}] Factory: properties.host is required for {name}", dc.Key, dc.Name);
				return null;
			}

			return new AxisD1110Device(dc.Key, dc.Name, propertiesConfig);
		}
	}
}
