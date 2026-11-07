using System;
using System.Runtime.InteropServices;
using static Nucleus.Gaming.Coop.InputManagement.RawInputManager;

namespace Nucleus.Gaming.Coop.InputManagement
{
    internal sealed class RawInputDeviceName : IDisposable
    {
        private static IntPtr dataDispose;

        public void Dispose()
        {
            Marshal.FreeHGlobal(dataDispose);
        }

        public string GetDeviceName(IntPtr data, IntPtr hDevice)
        {
            uint pcbSize = 0;

            uint result = GetRawInputDeviceInfo(hDevice, RawInputDeviceInformationCommand.RIDI_DEVICENAME, data, ref pcbSize);

            IntPtr extraData = Marshal.AllocHGlobal(((int)pcbSize) * 2);

            result = GetRawInputDeviceInfo(hDevice, RawInputDeviceInformationCommand.RIDI_DEVICENAME, extraData, ref pcbSize);

            dataDispose = extraData;

            return Marshal.PtrToStringAuto(extraData);
        }
    }
}
