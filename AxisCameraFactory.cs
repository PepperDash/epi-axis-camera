using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

namespace AxisCameraEpi
{
    public class AxisCameraFactory : EssentialsPluginDeviceFactory<AxisCamera>
    {
        public AxisCameraFactory()
        {
            MinimumEssentialsFrameworkVersion = "1.6.4";
            TypeNames = new List<string>() {"axisCamera"};
        }
        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            var props = AxisCameraPropsConfig.FromDeviceConfig(dc);

            return AxisCameraBuilder
                .CreateBuilder(dc.Key, dc.Name)
                .BuildClient(props.Hostname)
                .BuildMonitor(props.CommunicationMonitor)
                .BuildPresets(props.Presets)
                .Build();
        }
    }
}