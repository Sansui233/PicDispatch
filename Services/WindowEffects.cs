using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace PicDispatch.Services
{
    public static class WindowEffects
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        public static void ApplyWindows11Chrome(Window window)
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var darkMode = 1;
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            var cornerPreference = DWMWCP_ROUND;
            DwmSetWindowAttribute(handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));
        }

        public static void CenterOnCurrentScreen(Window window)
        {
            var helper = new WindowInteropHelper(window);
            Forms.Screen screen;
            if (helper.Owner != IntPtr.Zero)
            {
                screen = Forms.Screen.FromHandle(helper.Owner);
            }
            else if (helper.Handle != IntPtr.Zero)
            {
                screen = Forms.Screen.FromHandle(helper.Handle);
            }
            else
            {
                screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
            }

            var source = PresentationSource.FromVisual(window);
            var transform = source?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;
            var topLeft = transform.Transform(new Point(screen.WorkingArea.Left, screen.WorkingArea.Top));
            var bottomRight = transform.Transform(new Point(screen.WorkingArea.Right, screen.WorkingArea.Bottom));
            var width = bottomRight.X - topLeft.X;
            var height = bottomRight.Y - topLeft.Y;
            var fullTopLeft = transform.Transform(new Point(screen.Bounds.Left, screen.Bounds.Top));
            var fullBottomRight = transform.Transform(new Point(screen.Bounds.Right, screen.Bounds.Bottom));
            var fullHeight = fullBottomRight.Y - fullTopLeft.Y;
            var taskbarOffset = Math.Max(0, fullHeight - height);

            window.Left = topLeft.X + Math.Max(0, (width - window.ActualWidth) / 2);
            window.Top = topLeft.Y + Math.Max(0, (height - window.ActualHeight) / 2) - taskbarOffset;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);
    }
}
