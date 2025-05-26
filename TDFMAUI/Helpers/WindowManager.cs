using Microsoft.Maui.Platform;
using System.Runtime.InteropServices;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Platform;

#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Windowing;
using static Microsoft.UI.Win32Interop;
using WinRT;
#endif

namespace TDFMAUI.Helpers
{
    /// <summary>
    /// Helper class for managing window properties like size, position, and resizability
    /// </summary>
    public static class WindowManager
    {
        // Default window size
        public const int DefaultWidth = 1280;
        public const int DefaultHeight = 720;

#if WINDOWS
        // Constants for window styles (Windows-specific)
        private const int GWL_STYLE = -16;
        private const int WS_THICKFRAME = 0x00040000;
        private const int WS_MAXIMIZEBOX = 0x00010000;
        private const int WS_MINIMIZEBOX = 0x00020000;

        // Windows API imports
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int width, int height, uint flags);
#endif

        /// <summary>
        /// Configures the main window with the specified settings
        /// </summary>
        public static void ConfigureMainWindow(IWindow window)
        {
            if (window == null)
                return;

#if WINDOWS
            // On Windows, we can use the Microsoft.Maui.Handlers.WindowHandler to set window properties
            var mauiWindow = window as Microsoft.Maui.Controls.Window;
            if (mauiWindow != null)
            {
                // Set initial window size using CreateWindow event
                mauiWindow.Created += (s, e) =>
                {
                    // Get the native window handle
                    var nativeWindow = GetNativeWindow(mauiWindow);
                    if (nativeWindow != null)
                    {
                        // Set size and position
                        var displayInfo = DeviceDisplay.Current.MainDisplayInfo;
                        var screenWidth = displayInfo.Width / displayInfo.Density;
                        var screenHeight = displayInfo.Height / displayInfo.Density;
                        
                        // Calculate center position
                        var x = (int)((screenWidth - DefaultWidth) / 2);
                        var y = (int)((screenHeight - DefaultHeight) / 2);
                        
                        // Set window bounds using RectInt32
                        var rect = new Windows.Graphics.RectInt32(x, y, DefaultWidth, DefaultHeight);
                        nativeWindow.MoveAndResize(rect);
                    }
                };
                
                // Monitor window size changes
                mauiWindow.SizeChanged += (s, e) =>
                {
                    // Check if window is maximized by comparing to screen size
                    var currentDisplayInfo = DeviceDisplay.Current.MainDisplayInfo;
                    var currentScreenWidth = currentDisplayInfo.Width / currentDisplayInfo.Density;
                    var currentScreenHeight = currentDisplayInfo.Height / currentDisplayInfo.Density;

                    // Consider a window maximized if it's close to the screen size or significantly larger than default
                    bool isMaximized = (Math.Abs(mauiWindow.Width - currentScreenWidth) < 20 && 
                                       Math.Abs(mauiWindow.Height - currentScreenHeight) < 20) ||
                                       (mauiWindow.Width > DefaultWidth + 100 && mauiWindow.Height > DefaultHeight + 100);
                    
                    // Update the maximized state
                    DeviceHelper.SetWindowMaximized(isMaximized);
                };
            }
#elif MACCATALYST
            // On macOS, we can use the Microsoft.Maui.Controls.Window properties
            var mauiWindow = window as Microsoft.Maui.Controls.Window;
            if (mauiWindow != null)
            {
                // Set min/max size to prevent resizing
                mauiWindow.MinimumWidth = DefaultWidth;
                mauiWindow.MinimumHeight = DefaultHeight;
                mauiWindow.MaximumWidth = DefaultWidth;
                mauiWindow.MaximumHeight = DefaultHeight;

                // Set initial size and position
                mauiWindow.Width = DefaultWidth;
                mauiWindow.Height = DefaultHeight;

                // Center the window
                var displayInfo = DeviceDisplay.Current.MainDisplayInfo;
                var screenWidth = displayInfo.Width / displayInfo.Density;
                var screenHeight = displayInfo.Height / displayInfo.Density;
                mauiWindow.X = (screenWidth - DefaultWidth) / 2;
                mauiWindow.Y = (screenHeight - DefaultHeight) / 2;

                // Monitor window size changes
                mauiWindow.SizeChanged += (s, e) =>
                {
                    // Check if window is maximized
                    var currentDisplayInfo = DeviceDisplay.Current.MainDisplayInfo;
                    var currentScreenWidth = currentDisplayInfo.Width / currentDisplayInfo.Density;
                    var currentScreenHeight = currentDisplayInfo.Height / currentDisplayInfo.Density;

                    bool isMaximized = (Math.Abs(mauiWindow.Width - currentScreenWidth) < 20 &&
                                       Math.Abs(mauiWindow.Height - currentScreenHeight) < 20) ||
                                       (mauiWindow.Width > DefaultWidth + 100 && mauiWindow.Height > DefaultHeight + 100);

                    DeviceHelper.SetWindowMaximized(isMaximized);
                };
            }
#endif

            // Platform-specific window configuration
            ConfigurePlatformWindow(window);
        }

        /// <summary>
        /// Configure platform-specific window properties
        /// </summary>
        private static void ConfigurePlatformWindow(IWindow window)
        {
#if WINDOWS
            // On Windows, disable resizing by removing the WS_THICKFRAME style
            var platformWindow = window.Handler?.PlatformView;
            if (platformWindow != null)
            {
                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
                
                // Get current style
                int style = GetWindowLong(hwnd, GWL_STYLE);
                
                // Remove resizing border but keep maximize button
                style &= ~WS_THICKFRAME;
                
                // Apply the new style
                SetWindowLong(hwnd, GWL_STYLE, style);
                
                // Force window to update
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, 
                    0x0001 | // SWP_NOSIZE
                    0x0002 | // SWP_NOMOVE
                    0x0020 | // SWP_FRAMECHANGED
                    0x0004); // SWP_NOZORDER
            }
#endif
        }

#if WINDOWS
        /// <summary>
        /// Gets the native window object from a MAUI window
        /// </summary>
        private static Microsoft.UI.Windowing.AppWindow GetNativeWindow(Microsoft.Maui.Controls.Window window)
        {
            try
            {
                var handler = window.Handler;
                if (handler == null)
                    return null;
                
                var platformWindow = handler.PlatformView;
                if (platformWindow == null)
                    return null;
                
                var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
                var windowId = GetWindowIdFromWindow(windowHandle);
                return Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            }
            catch
            {
                return null;
            }
        }
#endif
    }
}