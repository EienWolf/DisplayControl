using System;
using System.Runtime.InteropServices;

namespace DisplayControl.Windows.Interop.User32
{
    /// <summary>
    /// P/Invoke bindings for DEVMODE-based display settings APIs.
    /// </summary>
    /// <remarks>
    /// - EnumDisplaySettingsEx: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-enumdisplaysettingsexw
    /// - ChangeDisplaySettingsEx: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-changedisplaysettingsexw
    /// - DEVMODE: https://learn.microsoft.com/windows/win32/api/wingdi/ns-wingdi-devmodew
    /// </remarks>
    internal static class User32DisplaySettings
    {
        public const int ENUM_CURRENT_SETTINGS = -1;
        public const int EDS_ROTATEDMODE = 0x00000004;
        public const int DISP_CHANGE_SUCCESSFUL = 0;

        // dmFields bit flags
        public const uint DM_POSITION = 0x00000020;
        public const uint DM_PELSWIDTH = 0x00080000;
        public const uint DM_PELSHEIGHT = 0x00100000;
        public const uint DM_DISPLAYFREQUENCY = 0x00400000;
        public const uint DM_DISPLAYORIENTATION = 0x00000080;

        // ChangeDisplaySettingsEx flags
        public const uint CDS_UPDATEREGISTRY = 0x00000001;
        public const uint CDS_NORESET = 0x10000000;
        public const uint CDS_RESET = 0x40000000;

        /// <summary>Enumerates display settings for a given device.</summary>
        /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-enumdisplaysettingsexw</remarks>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern bool EnumDisplaySettingsEx(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode, int dwFlags);

        /// <summary>Changes the settings of the default or specified display device.</summary>
        /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-changedisplaysettingsexw</remarks>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwFlags, IntPtr lParam);

        /// <summary>Applies any changes previously registered with CDS_NORESET.</summary>
        /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-changedisplaysettingsexw</remarks>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int ChangeDisplaySettingsEx(string? lpszDeviceName, IntPtr lpDevMode, IntPtr hwnd, uint dwFlags, IntPtr lParam);
    }

    /// <summary>
    /// DEVMODE structure for display settings.
    /// https://learn.microsoft.com/windows/win32/api/wingdi/ns-wingdi-devmodew
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint dmFields;

        public int dmPositionX;
        public int dmPositionY;
        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;

        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;
        // When DM_DISPLAYORIENTATION is used, these values apply
        // 0: DMDO_DEFAULT, 1: DMDO_90, 2: DMDO_180, 3: DMDO_270
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }
}
