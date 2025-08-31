namespace DisplayControl.Windows.Helpers
{
    internal static class EdidHelper
    {
        // EDID vendor, p.ej. "GSM" para LG
        public static string DecodePnP(ushort id)
        {
            char c1 = (char)(((id >> 10) & 0x1F) + 0x40);
            char c2 = (char)(((id >> 5) & 0x1F) + 0x40);
            char c3 = (char)((id & 0x1F) + 0x40);
            return new string(new[] { c1, c2, c3 });
        }
    }
}

