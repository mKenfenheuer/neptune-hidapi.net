using hidapi;
using neptune_hidapi.net.Hid;
using neptune_hidapi.net.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace neptune_hidapi.net
{
    public class NeptuneController
    {
        private HidDevice _hidDevice;
        private ushort _vid = 0x28de, _pid = 0x1205;
        private Task _configureTask;
        private bool _active = false;

        public bool LizardMouseEnabled { get; set; }
        public bool LizardButtonsEnabled { get; set; }
        public string SerialNumber { get; private set; }
        public Func<NeptuneControllerInputEventArgs, Task> OnControllerInputReceived;

        public NeptuneController()
        {
            _hidDevice = new HidDevice(_vid, _pid, 64);
            _hidDevice.OnInputReceived = input => Task.Run(() => OnInputReceived(input));
        }

        private void OnInputReceived(HidDeviceInputReceivedEventArgs e)
        {
            if (e.Buffer[0] == 1)
            {
                SDCInput input = e.Buffer.ToStructure<SDCInput>();
                NeptuneControllerInputState state = new NeptuneControllerInputState(input);
                if (OnControllerInputReceived != null)
                    OnControllerInputReceived(new NeptuneControllerInputEventArgs(state));
            }
            else
            {

            }
        }

        private async Task<bool> SetLizardMode(bool mouse, bool buttons)
        {
            try
            {
                if (!mouse)
                {
                    //Disable mouse emulation
                    byte[] data = new byte[] { 0x87, 0x03, 0x08, 0x07 };
                    await _hidDevice.RequestFeatureReportAsync(data);
                }
                else
                {
                    //Enable mouse emulation
                    byte[] data = new byte[] { 0x8e, 0x00 };
                    await _hidDevice.RequestFeatureReportAsync(data);
                }

                if (!buttons)
                {
                    //Disable keyboard/mouse button emulation
                    byte[] data = new byte[] { 0x81, 0x00 };
                    await _hidDevice.RequestFeatureReportAsync(data);

                }
                else
                {
                    //Enable keyboard/mouse button emulation
                    byte[] data = new byte[] { 0x85, 0x00 };
                    await _hidDevice.RequestFeatureReportAsync(data);
                }
            }
            catch (Exception ex)
            {
                return false;
            }
            return true;
        }

        private async Task ConfigureLoop()
        {
            while (_active)
            {
                await SetLizardMode(LizardMouseEnabled, LizardButtonsEnabled);
                await Task.Delay(250);
            }
        }

        public async Task<string> ReadSerialNumberAsync()
        {
            byte[] request = new byte[] { 0xAE, 0x15, 0x01 };
            byte[] response = await _hidDevice.RequestFeatureReportAsync(request);
            byte[] serial = new byte[response.Length - 5];
            Array.Copy(response, 4, serial, 0, serial.Length);

            return Encoding.ASCII.GetString(serial).TrimEnd((Char)0);
        }
        public string ReadSerialNumber()
        {
            byte[] request = new byte[] { 0xAE, 0x15, 0x01 };
            byte[] response = _hidDevice.RequestFeatureReport(request);
            byte[] serial = new byte[response.Length - 5];
            Array.Copy(response, 4, serial, 0, serial.Length);

            return Encoding.ASCII.GetString(serial).TrimEnd((Char)0);
        }

        public async Task OpenAsync()
        {
            if (!await _hidDevice.OpenDeviceAsync())
                throw new Exception("Could not open device!");
            SerialNumber = await ReadSerialNumberAsync();
            _hidDevice.BeginRead();
            _active = true;
            _configureTask = ConfigureLoop();
        }
        public void Open()
        {
            if (!_hidDevice.OpenDevice())
                throw new Exception("Could not open device!");
            SerialNumber = ReadSerialNumber();
            _hidDevice.BeginRead();
            _active = true;
            _configureTask = ConfigureLoop();
        }

        public Task CloseAsync() => Task.Run(() => Close());
        public void Close()
        {
            if (_hidDevice.IsDeviceValid)
                _hidDevice.EndRead();
            _hidDevice.Dispose();
            _active = false;
        }
    }
}
