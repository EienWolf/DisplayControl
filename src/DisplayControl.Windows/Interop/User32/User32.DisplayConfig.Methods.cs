using System;
using System.Runtime.InteropServices;

namespace DisplayControl.Windows.Interop.User32
{
    internal static class User32DisplayConfig
    {
        [DllImport("user32.dll")]
        internal static extern int GetDisplayConfigBufferSizes(QDC flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

        [DllImport("user32.dll")]
        internal static extern int QueryDisplayConfig(QDC flags,
            ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] pathInfoArray,
            ref uint modeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
            IntPtr currentTopologyId);

        [DllImport("user32.dll")]
        internal static extern int SetDisplayConfig(uint numPathArrayElements, [In] DISPLAYCONFIG_PATH_INFO[] pathArray,
            uint numModeInfoArrayElements, [In] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, SDC flags);

        [DllImport("user32.dll")]
        internal static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME requestPacket);

        [DllImport("user32.dll")]
        internal static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME requestPacket);
    }
}

