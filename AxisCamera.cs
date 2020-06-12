using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;

namespace AxisCameraEpi
{
    public class AxisCamera : EssentialsDevice, IBridgeAdvanced, ICommunicationMonitor
    {
        private StatusMonitorBase _monitor;
        private GenericHttpClient _client;
        private IEnumerable<AxisCameraPreset> _presets;

        public Dictionary<uint, StringFeedback> PresetNamesFeedbacks { get; private set; }
        public IntFeedback NumberOfPresetsFeedback { get; private set; }
        public StringFeedback NameFeedback { get; private set; }
        public IntFeedback PanSpeedFeedback { get; private set; }
        public IntFeedback ZoomSpeedFeedback { get; private set; }
        public IntFeedback TiltSpeedFeedback { get; private set; }

        public GenericHttpClient Client { get { return _client; } }
        public StatusMonitorBase CommunicationMonitor { get { return _monitor; } }
        public IEnumerable<AxisCameraPreset> Presets { get { return _presets; } }

        private int _panSpeed;
        private int _tiltSpeed;
        private int _zoomSpeed;

        public AxisCamera(IAxisCameraBuilderWithClient builder)
            : base(builder.Key, builder.Name)
        {
            _client = builder.Client;
            _presets = builder.Presets.OrderBy(x => x.Id);

            CheckComsAndSubscribe();
            BuildCommunicationMonitor(builder);
            SetupFeedbacks();
            SetDefaultValues();

            AddPostActivationAction(() => CommunicationMonitor.Start());
        }

        private void CheckComsAndSubscribe()
        {
            _client.ResponseRecived += HandleResponseReceived;
        }

        private void BuildCommunicationMonitor(IAxisCameraBuilderWithClient builder)
        {
            _monitor = new AxisCameraMonitor(this, builder.Client, builder.MonitorConfig);
        }

        private void SetupFeedbacks()
        {
            NameFeedback = new StringFeedback(() => Name);
            NameFeedback.FireUpdate();

            NumberOfPresetsFeedback = new IntFeedback(() => Presets.Count());
            NumberOfPresetsFeedback.FireUpdate();

            PanSpeedFeedback = new IntFeedback(() => PanSpeed);
            TiltSpeedFeedback = new IntFeedback(() => TiltSpeed);
            ZoomSpeedFeedback = new IntFeedback(() => ZoomSpeed);

            PanSpeedFeedback.FireUpdate();
            TiltSpeedFeedback.FireUpdate();
            ZoomSpeedFeedback.FireUpdate();

            PresetNamesFeedbacks = Presets.ToDictionary(x => (uint)x.Id, x => new StringFeedback(() => x.Name));

            foreach (var feedback in PresetNamesFeedbacks)
            {
                feedback.Value.FireUpdate();
            }
        }
        
        private void SetDefaultValues()
        {
            _panSpeed = 50;
            _tiltSpeed = 50;
            _zoomSpeed = 50;
        }

        private void HandleResponseReceived(object sender, GenericHttpClientEventArgs e)
        {
            throw new NotImplementedException();
        }

        public int PanSpeed
        {
            get { return _panSpeed; }
            set
            {
                if (value > 100 || value < 0)
                    return;

                _panSpeed = value;
                PanSpeedFeedback.FireUpdate();
            }
        }

        public int ZoomSpeed
        {
            get { return _zoomSpeed; }
            set
            {
                if (value > 100 || value < 0)
                    return;

                _zoomSpeed = value;
                ZoomSpeedFeedback.FireUpdate();
            }
        }

        public int TiltSpeed
        {
            get { return _tiltSpeed; }
            set
            {
                if (value > 100 || value < 0)
                    return;

                _tiltSpeed = value;
                TiltSpeedFeedback.FireUpdate();
            }
        }

        #region IBridgeAdvanced Members

        public void LinkToApi(Crestron.SimplSharpPro.DeviceSupport.BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new AxisCameraJoinMap(joinStart);

            NameFeedback.LinkInputSig(trilist.StringInput[joinMap.DeviceName]);
            NumberOfPresetsFeedback.LinkInputSig(trilist.UShortInput[joinMap.NumberOfPresets]);
            CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline]);
            PanSpeedFeedback.LinkInputSig(trilist.UShortInput[joinMap.PanSpeed]);
            TiltSpeedFeedback.LinkInputSig(trilist.UShortInput[joinMap.TiltSpeed]);
            ZoomSpeedFeedback.LinkInputSig(trilist.UShortInput[joinMap.ZoomSpeed]);

            trilist.SetStringSigAction(joinMap.DeviceComs, command => 
                {
                    AxisCameraCommandBuilder
                        .SetDevice(this)
                        .SetCustomCommand(command)
                        .Dispatch();
                });

            trilist.SetBoolSigAction(joinMap.PanLeft, sig =>
            {
                if (sig) 
                {
                    AxisCameraCommandBuilder
                        .SetDevice(this)
                        .PanLeft()
                        .Dispatch();
                }
                else 
                {
                    AxisCameraCommandBuilder
                        .SetDevice(this)
                        .PanTiltStop()
                        .Dispatch();
                }
            });

            trilist.SetBoolSigAction(joinMap.PanRight, sig =>
            {
                if (sig) 
                {
                    AxisCameraCommandBuilder
                        .SetDevice(this)
                        .PanRight()
                        .Dispatch();
                }
                else 
                {
                    AxisCameraCommandBuilder
                        .SetDevice(this)
                        .PanTiltStop()
                        .Dispatch();
                }
            });


            trilist.SetBoolSigAction(joinMap.TiltUp, sig =>
            {
                if (sig) 
                {
                    AxisCameraCommandBuilder
                        .SetDevice(this)
                        .TiltUp()
                        .Dispatch();
                }
                else 
                {
                    AxisCameraCommandBuilder
                        .SetDevice(this)
                        .PanTiltStop()
                        .Dispatch();
                }
            });

            trilist.SetBoolSigAction(joinMap.TiltDown, sig =>
            {
                if (sig)
                {
                    AxisCameraCommandBuilder
                        .SetDevice(this)
                        .TiltDown()
                        .Dispatch();
                }
                else
                {
                    AxisCameraCommandBuilder
                        .SetDevice(this)
                        .PanTiltStop()
                        .Dispatch();
                }
            });

            trilist.SetBoolSigAction(joinMap.ZoomIn, sig =>
            {
                if (sig)
                {
                    AxisCameraCommandBuilder
                        .SetDevice(this)
                        .ZoomIn()
                        .Dispatch();
                }
                else
                {
                    AxisCameraCommandBuilder
                        .SetDevice(this)
                        .ZoomStop()
                        .Dispatch();
                }
            });

            trilist.SetBoolSigAction(joinMap.ZoomOut, sig =>
            {
                if (sig)
                {
                    AxisCameraCommandBuilder
                        .SetDevice(this)
                        .ZoomOut()
                        .Dispatch();
                }
                else
                {
                    AxisCameraCommandBuilder
                        .SetDevice(this)
                        .ZoomStop()
                        .Dispatch();
                }
            });

            trilist.SetUShortSigAction(joinMap.PanSpeed, panSpeed => PanSpeed = panSpeed);
            trilist.SetUShortSigAction(joinMap.ZoomSpeed, zoomSpeed => ZoomSpeed = zoomSpeed);
            trilist.SetUShortSigAction(joinMap.TiltSpeed, tiltSpeed => TiltSpeed = tiltSpeed);

            foreach (var preset in PresetNamesFeedbacks)
            {
                var presetNumber = preset.Key;
                var nameJoin = joinMap.PresetNameStart + presetNumber - 1;

                preset.Value.LinkInputSig(trilist.StringInput[nameJoin]);
                preset.Value.FireUpdate();

                var recallJoin = joinMap.PresetRecallStart + presetNumber - 1;
                var saveJoin = joinMap.PresetSaveStart + presetNumber - 1;

                trilist.SetSigTrueAction(recallJoin, () =>
                    {
                        AxisCameraCommandBuilder
                            .SetDevice(this)
                            .RecallPreset((int)recallJoin)
                            .Dispatch();
                    });

                trilist.SetSigHeldAction(recallJoin, 5000, () =>
                    {
                        AxisCameraCommandBuilder
                            .SetDevice(this)
                            .SavePreset((int)recallJoin)
                            .Dispatch();
                    });

                trilist.SetSigTrueAction(saveJoin, () => {
                        AxisCameraCommandBuilder
                            .SetDevice(this)
                            .SavePreset((int)recallJoin)
                            .Dispatch();
                    });
            }	
        }

        #endregion
    }
}