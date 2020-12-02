using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using Newtonsoft.Json;

namespace AxisCameraEpi
{
    public class AxisCameraPropsConfig
    {
        public static AxisCameraPropsConfig FromDeviceConfig(DeviceConfig config)
        {
            return JsonConvert.DeserializeObject<AxisCameraPropsConfig>(config.Properties.ToString());
        }

        public AxisCameraPropsConfig()
        {
            Presets = new List<AxisCameraPreset>();
        }

        [JsonProperty("hostname")]
        public string Hostname { get; set; }

        [JsonProperty("communicationMonitor")]
        public CommunicationMonitorConfig CommunicationMonitor { get; set; }

        [JsonProperty("presets")]
        public List<AxisCameraPreset> Presets { get; set; }
    }
}