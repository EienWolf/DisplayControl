using System;
using System.Runtime.InteropServices;

namespace DisplayControl.Windows.Interop.Shcore
{
    /// <summary>
    /// Monitor DPI type for SHCore.GetDpiForMonitor.
    /// https://learn.microsoft.com/windows/win32/api/shellscalingapi/ne-shellscalingapi-monitor_dpitype
    /// </summary>
    internal enum MonitorDpiType
    {
        MDT_EFFECTIVE_DPI = 0,
        MDT_ANGULAR_DPI = 1,
        MDT_RAW_DPI = 2,
        MDT_DEFAULT = MDT_EFFECTIVE_DPI
    }

    /// <summary>
    /// P/Invoke for SHCore DPI APIs.
    /// </summary>
    /// <remarks>
    /// GetDpiForMonitor: https://learn.microsoft.com/windows/win32/api/shellscalingapi/nf-shellscalingapi-getdpiformonitor
    /// </remarks>
    internal static class ShcoreMethods
    {
        [DllImport("Shcore.dll")]
        internal static extern int GetDpiForMonitor(IntPtr hmonitor, MonitorDpiType dpiType, out uint dpiX, out uint dpiY);
    }
}
