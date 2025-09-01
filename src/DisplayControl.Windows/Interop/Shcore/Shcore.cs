using System;
using System.Runtime.InteropServices;

namespace DisplayControl.Windows.Interop.Shcore
{
    internal enum MonitorDpiType
    {
        MDT_EFFECTIVE_DPI = 0,
        MDT_ANGULAR_DPI = 1,
        MDT_RAW_DPI = 2,
        MDT_DEFAULT = MDT_EFFECTIVE_DPI
    }

    internal static class ShcoreMethods
    {
        [DllImport("Shcore.dll")]
        internal static extern int GetDpiForMonitor(IntPtr hmonitor, MonitorDpiType dpiType, out uint dpiX, out uint dpiY);
    }
}

