using System;
using System.Text;
using Crestron.SimplSharp.Net.Http;
using PepperDash.Core;

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
        public const string Cmd = "axis-cgi/com/ptz.cgi?";
        public const string PanTiltCmd = "continuouspantiltmove=";
        public const string ZoomCmd = "continuouszoommove=";
        public const string PollCmd = "info=1";
        public const string RecallPresetCmd = "gotoserverpresetno=";
        public const string SavePresetCmd = "";

        private readonly AxisCamera _camera;
        private readonly StringBuilder _command = new StringBuilder(Cmd);

        public static IAxisCommandBuilder SetDevice(AxisCamera camera)
        {
            return new AxisCameraCommandBuilder(camera);
        }

        private AxisCameraCommandBuilder(AxisCamera camera)
        {
            _camera = camera;
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
            _command.Append("0");
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
            try
            {
                _camera.Client.SendText(_command.ToString());
            }
            catch (Exception ex)
            {
                Debug.Console(1, _camera, "Error dispatching command : {0}", ex.Message);
            }
        }

        #endregion
    }
}