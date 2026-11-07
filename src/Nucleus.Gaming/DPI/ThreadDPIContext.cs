using System;
using System.Runtime.InteropServices;

namespace Nucleus.Gaming.DPI
{
    public static class ThreadDPIContext
    {
        public enum DpiAwarenessContext
        {
            DPI_AWARENESS_CONTEXT_UNAWARE = -1,
            DPI_AWARENESS_CONTEXT_SYSTEM_AWARE = -2,
            DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE = -3,
            DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4,
            DPI_AWARENESS_CONTEXT_UNAWARE_GDISCALED = -5
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetThreadDpiAwarenessContext();

        public static IntPtr GetDpiAwarenessContext(DpiAwarenessContext awareness)
        {
            return new IntPtr((int)awareness);
        }
    }
}
