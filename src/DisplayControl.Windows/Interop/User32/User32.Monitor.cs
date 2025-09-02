using System;
using System.Runtime.InteropServices;

namespace DisplayControl.Windows.Interop.User32
{
    /// <summary>
    /// P/Invoke bindings for monitor enumeration and information.
    /// </summary>
    /// <remarks>
    /// - EnumDisplayMonitors: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-enumdisplaymonitors
    /// - GetMonitorInfo: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getmonitorinfow
    /// - MONITORINFOEX: https://learn.microsoft.com/windows/win32/api/winuser/ns-winuser-monitorinfoexw
    /// </remarks>
    internal static class User32Monitor
    {
        internal delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        internal static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);
    }

    /// <summary>
    /// Extended monitor information.
    /// https://learn.microsoft.com/windows/win32/api/winuser/ns-winuser-monitorinfoexw
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MONITORINFOEX
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }
}
