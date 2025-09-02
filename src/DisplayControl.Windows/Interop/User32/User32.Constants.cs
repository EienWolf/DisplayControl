using System;

namespace DisplayControl.Windows.Interop.User32
{
    [Flags]
    public enum QDC : uint
    {
        ALL_PATHS = 0x00000001,
        ONLY_ACTIVE_PATHS = 0x00000002,
        DATABASE_CURRENT = 0x00000004
    }

    [Flags]
    public enum SDC : uint
    {
        TOPOLOGY_INTERNAL = 0x00000001,
        TOPOLOGY_CLONE = 0x00000002,
        TOPOLOGY_EXTEND = 0x00000004,
        TOPOLOGY_EXTERNAL = 0x00000008,
        USE_SUPPLIED_DISPLAY_CONFIG = 0x00000020,
        APPLY = 0x00000080,
        SAVE_TO_DATABASE = 0x00000200,
        ALLOW_CHANGES = 0x00000400,
        PATH_PERSIST_IF_REQUIRED = 0x00000800,
        VIRTUAL_MODE_AWARE = 0x00001000
    }
}

