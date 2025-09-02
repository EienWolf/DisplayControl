using System;
using System.Runtime.InteropServices;

namespace DisplayControl.Windows.Interop.User32
{
    /// <summary>
    /// DisplayConfig types and structures for Win32 interop.
    /// See Display Configuration APIs overview: https://learn.microsoft.com/windows/win32/api/_displaydev/
    /// </summary>
    // Enums and flags
    internal enum DISPLAYCONFIG_DEVICE_INFO_TYPE : int
    {
        GET_SOURCE_NAME = 1,
        GET_TARGET_NAME = 2,
        GET_TARGET_PREFERRED_MODE = 3,
        GET_ADAPTER_NAME = 4,
        GET_TARGET_ADVANCED_COLOR_INFO = 11,
    }

    internal enum DISPLAYCONFIG_MODE_INFO_TYPE : uint
    {
        SOURCE = 1,
        TARGET = 2,
        DESKTOP_IMAGE = 3
    }

    internal enum DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY : uint
    {
        Other = 0xFFFFFFFF
    }

    internal enum DISPLAYCONFIG_ROTATION : uint
    {
        Identity = 1,
        Rotate90 = 2,
        Rotate180 = 3,
        Rotate270 = 4
    }
    internal enum DISPLAYCONFIG_SCALING : uint
    {
        Identity = 1,
        Centered = 2,
        Stretched = 3,
        AspectRatioCenteredMax = 4,
        Custom = 5,
        Preferred = 128
    }
    internal enum DISPLAYCONFIG_SCANLINE_ORDERING : uint { Unspecified = 0 }
    internal enum DISPLAYCONFIG_PIXELFORMAT : uint { PIXELFORMAT_32BPP = 5 }

    [Flags]
    internal enum DISPLAYCONFIG_PATH_INFO_FLAGS : uint
    {
        NONE = 0,
        ACTIVE = 0x00000001
    }

    // Structs
    [StructLayout(LayoutKind.Sequential)]
    internal struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_RATIONAL
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_2DREGION
    {
        public uint cx;
        public uint cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINTL { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;             // sourceId
        public uint modeInfoIdx;    // index a MODE_INFO
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID adapterId;
        public uint id;             // targetId
        public uint modeInfoIdx;
        public DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY outputTechnology;
        public DISPLAYCONFIG_ROTATION rotation;
        public DISPLAYCONFIG_SCALING scaling;
        public DISPLAYCONFIG_RATIONAL refreshRate;
        public DISPLAYCONFIG_SCANLINE_ORDERING scanLineOrdering;
        [MarshalAs(UnmanagedType.Bool)] public bool targetAvailable;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public DISPLAYCONFIG_PATH_INFO_FLAGS flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_SOURCE_MODE
    {
        public uint width;
        public uint height;
        public DISPLAYCONFIG_PIXELFORMAT pixelFormat;
        public POINTL position;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO
    {
        public ulong pixelRate;
        public DISPLAYCONFIG_RATIONAL hSyncFreq;
        public DISPLAYCONFIG_RATIONAL vSyncFreq;
        public DISPLAYCONFIG_2DREGION activeSize;
        public DISPLAYCONFIG_2DREGION totalSize;
        public uint videoStandard;
        public DISPLAYCONFIG_SCANLINE_ORDERING scanLineOrdering;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_TARGET_MODE
    {
        public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetVideoSignalInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_DESKTOP_IMAGE_INFO
    {
        public POINTL PathSourceSize;
        public RECT DesktopImageRegion;
        public RECT DesktopImageClip;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct DISPLAYCONFIG_MODE_INFO
    {
        [FieldOffset(0)] public DISPLAYCONFIG_MODE_INFO_TYPE infoType;
        [FieldOffset(4)] public uint id;
        [FieldOffset(8)] public LUID adapterId;

        // union
        [FieldOffset(16)] public DISPLAYCONFIG_TARGET_MODE targetMode;
        [FieldOffset(16)] public DISPLAYCONFIG_SOURCE_MODE sourceMode;
        [FieldOffset(16)] public DISPLAYCONFIG_DESKTOP_IMAGE_INFO desktopImageInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public DISPLAYCONFIG_DEVICE_INFO_TYPE type;
        public uint size;
        public LUID adapterId;
        public uint id; // targetId o sourceId según "type"
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DISPLAYCONFIG_TARGET_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint flags;
        public DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY outputTechnology;
        public ushort edidManufactureId;
        public ushort edidProductCodeId;
        public uint connectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string monitorFriendlyDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string monitorDevicePath;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string viewGdiDeviceName;
    }

    internal enum DISPLAYCONFIG_COLOR_ENCODING : uint
    {
        RGB = 0,
        YCbCr444 = 1,
        YCbCr422 = 2,
        YCbCr420 = 3,
        Intensity = 4
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint value;
        public DISPLAYCONFIG_COLOR_ENCODING colorEncoding;
        public uint bitsPerColorChannel;

        public bool advancedColorSupported => (value & 0x1) != 0;
        public bool advancedColorEnabled => (value & 0x2) != 0;
        public bool wideColorEnforced => (value & 0x4) != 0;
        public bool advancedColorForceDisabled => (value & 0x8) != 0;
    }
}
