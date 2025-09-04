using System;
using System.Runtime.InteropServices;

namespace DisplayControl.Windows.Interop.User32
{
    /// <summary>
    /// User32 dialog helpers for MessageBox and basic window operations.
    /// </summary>
    /// <remarks>
    /// MessageBox: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-messageboxw
    /// FindWindow: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-findwindoww
    /// PostMessage: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-postmessagew
    /// SetForegroundWindow: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setforegroundwindow
    /// </remarks>
    internal static class User32Dialogs
    {
        public const int IDYES = 6;
        public const int IDNO = 7;

        public const uint MB_OK = 0x00000000;
        public const uint MB_YESNO = 0x00000004;
        public const uint MB_ICONQUESTION = 0x00000020;
        public const uint MB_SETFOREGROUND = 0x00010000;
        public const uint MB_TOPMOST = 0x00040000;

        private const uint WM_CLOSE = 0x0010;

        /// <summary>
        /// Shows a top-most MessageBox with the specified flags enforced.
        /// </summary>
        public static int MessageBoxTopMost(IntPtr hWnd, string text, string caption, uint type)
        {
            // Ensure top-most flag is present.
            type |= MB_TOPMOST;
            return MessageBoxW(hWnd, text, caption, type);
        }

        /// <summary>
        /// Attempts to find a top-level window by its caption (title).
        /// </summary>
        public static IntPtr FindWindowByCaption(string caption)
        {
            return FindWindowW(null, caption);
        }

        /// <summary>
        /// Posts WM_CLOSE to a window handle.
        /// </summary>
        public static void PostClose(IntPtr hWnd)
        {
            PostMessageW(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindWindowW(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessageW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}

