using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace AxisCameraEpi
{
    public interface IAxisCommandBuilder
    {
        IAxisCommandDispatcher RecallPreset(int preset);
        IAxisCommandDispatcher SavePreset(int preset);
        IAxisCommandDispatcher Poll();
        IAxisCommandDispatcher PanTiltStop();
        IAxisCommandDispatcher ZoomStop();
        IAxisCommandDispatcher TiltUp();
        IAxisCommandDispatcher TiltDown();
        IAxisCommandDispatcher PanLeft();
        IAxisCommandDispatcher PanRight();
        IAxisCommandDispatcher ZoomIn();
        IAxisCommandDispatcher ZoomOut();
        IAxisCommandDispatcher SetCustomCommand(string command);
    }

    public interface IAxisCommandDispatcher
    {
        void Dispatch();
    }

    public class AxisCameraCommandBuilder : IAxisCommandBuilder, IAxisCommandDispatcher
    {
        private static readonly string Cmd = "axis-cgi/com/ptz.cgi?";
        private static readonly string PanTiltCmd = "continuouspantiltmove=";
        private static readonly string ZoomCmd = "continuouszoommove=";
        private static readonly string PresetCmd = "gotoserverpresetno=";
        private static readonly string PollCmd = "info=1";
        private static readonly string RecallPresetCmd = "gotoserverpresetno=";
        private static readonly string SavePresetCmd = String.Empty;

        private readonly AxisCamera _camera;
        private StringBuilder _command;

        public static IAxisCommandBuilder SetDevice(AxisCamera camera)
        {
            return new AxisCameraCommandBuilder(camera);
        }

        private AxisCameraCommandBuilder(AxisCamera camera)
        {
            _camera = camera;
            _command = new StringBuilder(Cmd);
        }

        #region IAxisCommandBuilder Members

        public IAxisCommandDispatcher TiltUp()
        {
            _command.Append(PanTiltCmd);
            _command.Append("0,");
            _command.Append(_camera.TiltSpeed);
            return this;
        }

        public IAxisCommandDispatcher TiltDown()
        {
            _command.Append(PanTiltCmd);
            _command.Append("0,");
            _command.Append(0 - _camera.TiltSpeed);
            return this;
        }

        public IAxisCommandDispatcher PanLeft()
        {
            _command.Append(PanTiltCmd);
            _command.Append(0 - _camera.PanSpeed);
            _command.Append(",0");
            return this;
        }

        public IAxisCommandDispatcher PanRight()
        {
            _command.Append(PanTiltCmd);
            _command.Append(_camera.PanSpeed);
            _command.Append(",0");
            return this;
        }

        public IAxisCommandDispatcher ZoomIn()
        {
            _command.Append(ZoomCmd);
            _command.Append(_camera.ZoomSpeed);
            return this;
        }

        public IAxisCommandDispatcher ZoomOut()
        {
            _command.Append(ZoomCmd);
            _command.Append(0 - _camera.ZoomSpeed);
            return this;
        }

        public IAxisCommandDispatcher PanTiltStop()
        {
            _command.Append(PanTiltCmd);
            _command.Append("0,0");
            return this;
        }

        public IAxisCommandDispatcher ZoomStop()
        {
            _command.Append(ZoomCmd);
            _command.Append("0,0");
            return this;
        }

        public IAxisCommandDispatcher RecallPreset(int preset)
        {
            _command.Append(RecallPresetCmd);
            _command.Append(preset);
            return this;
        }

        public IAxisCommandDispatcher SavePreset(int preset)
        {
            _command.Append(SavePresetCmd);
            _command.Append(preset);
            return this;
        }

        public IAxisCommandDispatcher Poll()
        {
            _command.Append(PollCmd);
            return this;
        }

        public IAxisCommandDispatcher SetCustomCommand(string command)
        {
            _command.Append(command);
            return this;
        }

        #endregion

        #region IAxisCommandDispatcher Members

        public void Dispatch()
        {
            _camera.Client.SendText(_command.ToString());
        }

        #endregion
    }
}