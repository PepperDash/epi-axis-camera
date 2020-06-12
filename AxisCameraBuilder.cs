using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

namespace AxisCameraEpi
{
    public interface IAxisCameraBuilder
    {
        string Key { get; }
        string Name { get; }
        IAxisCameraBuilderWithClient BuildClient(string hostname);
    }

    public interface IAxisCameraBuilderWithClient : IAxisCameraBuilder
    {
        GenericHttpClient Client { get; }
        CommunicationMonitorConfig MonitorConfig { get; }
        IEnumerable<AxisCameraPreset> Presets { get; }
        IAxisCameraBuilderWithClient BuildMonitor(CommunicationMonitorConfig config);
        IAxisCameraBuilderWithClient BuildPresets(IEnumerable<AxisCameraPreset> presets);
        EssentialsDevice Build();
    }

    public class AxisCameraBuilder : IAxisCameraBuilder, IAxisCameraBuilderWithClient
    {
        public static IAxisCameraBuilder CreateBuilder(string deviceKey, string deviceName)
        {
            return new AxisCameraBuilder(deviceKey, deviceName);  
        }

        private AxisCameraBuilder(string key, string name)
        {
            Key = key;
            Name = name;

            MonitorConfig = new CommunicationMonitorConfig
            {
                PollInterval = 60000,
                TimeToWarning = 180000,
                TimeToError = 300000,
                PollString = String.Empty
            };

            Presets = new List<AxisCameraPreset>();
        }

        #region IAxisCameraBuilder Members

        public string Key { get; private set; }

        public string Name { get; private set; }

        public IAxisCameraBuilderWithClient BuildClient(string hostname)
        {
            Client = new GenericHttpClient(Key + "-httpClient", Name, hostname);
            return this;
        }

        #endregion

        #region IAxisCameraBuilderWithClient Members

        public GenericHttpClient Client { get; private set; }

        public CommunicationMonitorConfig MonitorConfig { get; private set; }

        public IEnumerable<AxisCameraPreset> Presets { get; private set; }

        public IAxisCameraBuilderWithClient BuildMonitor(CommunicationMonitorConfig config)
        {
            if (config != null)
                MonitorConfig = config;

            return this;
        }

        public IAxisCameraBuilderWithClient BuildPresets(IEnumerable<AxisCameraPreset> presets)
        {
            if (presets != null)
                Presets = presets;

            return this;
        }

        public EssentialsDevice Build()
        {
            return new AxisCamera(this);
        }

        #endregion
    }
}