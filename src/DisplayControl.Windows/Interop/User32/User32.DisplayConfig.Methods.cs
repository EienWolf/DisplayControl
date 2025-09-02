using System;
using System.Runtime.InteropServices;

namespace DisplayControl.Windows.Interop.User32
{
    /// <summary>
    /// P/Invoke bindings for Display Configuration (DisplayConfig) APIs.
    /// </summary>
    /// <remarks>
    /// Docs: GetDisplayConfigBufferSizes / QueryDisplayConfig / SetDisplayConfig / DisplayConfigGetDeviceInfo
    /// - https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getdisplayconfigbuffersizes
    /// - https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-querydisplayconfig
    /// - https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setdisplayconfig
    /// - https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-displayconfiggetdeviceinfo
    /// </remarks>
    internal static class User32DisplayConfig
    {
        /// <summary>Retrieves the sizes of the buffers that the caller must allocate for the path and mode information.</summary>
        /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getdisplayconfigbuffersizes</remarks>
        [DllImport("user32.dll")]
        internal static extern int GetDisplayConfigBufferSizes(QDC flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

        /// <summary>Retrieves information about all possible display paths for all display devices in the current setting.</summary>
        /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-querydisplayconfig</remarks>
        [DllImport("user32.dll")]
        internal static extern int QueryDisplayConfig(QDC flags,
            ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] pathInfoArray,
            ref uint modeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
            IntPtr currentTopologyId);

        /// <summary>Modifies the current display topology, source modes, and target modes.</summary>
        /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setdisplayconfig</remarks>
        [DllImport("user32.dll")]
        internal static extern int SetDisplayConfig(uint numPathArrayElements, [In] DISPLAYCONFIG_PATH_INFO[] pathArray,
            uint numModeInfoArrayElements, [In] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, SDC flags);

        /// <summary>Retrieves information about a display target including its friendly name and EDID data.</summary>
        /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-displayconfiggetdeviceinfo</remarks>
        [DllImport("user32.dll")]
        internal static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME requestPacket);

        /// <summary>Retrieves the GDI device name for a display source.</summary>
        /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-displayconfiggetdeviceinfo</remarks>
        [DllImport("user32.dll")]
        internal static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME requestPacket);

        /// <summary>Retrieves advanced color information for a display target.</summary>
        /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-displayconfiggetdeviceinfo</remarks>
        [DllImport("user32.dll")]
        internal static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO requestPacket);
    }
}
