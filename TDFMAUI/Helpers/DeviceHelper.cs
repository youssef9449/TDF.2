using Microsoft.Maui.Devices;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using TDFMAUI.Services;
using TDFShared.Enums;
using TDFShared.DTOs.Users;
using TDFShared.Constants;
using Microsoft.Maui.Graphics;

namespace TDFMAUI.Helpers
{
    /// <summary>
    /// Helper class for detecting platform information and device capabilities
    /// </summary>
    public static class DeviceHelper
    {
        // Event for display info changes
        private static readonly object _displayEventLock = new object();
        private static readonly object _initializationLock = new object();
        private static event EventHandler<DisplayInfoChangedEventArgs>? _displayInfoChanged;
        private static bool _isInitialized;
        
        public static event EventHandler<DisplayInfoChangedEventArgs> DisplayInfoChanged
        {
            add { lock (_displayEventLock) { _displayInfoChanged += value; } }
            remove { lock (_displayEventLock) { _displayInfoChanged -= value; } }
        }
        
        // Window state tracking
        public static bool IsWindowMaximized { get; set; } = false;
        
        // Event for window maximization state changes
        private static readonly object _windowEventLock = new object();
        private static event EventHandler<bool>? _windowMaximizationChanged;
        
        public static event EventHandler<bool> WindowMaximizationChanged
        {
            add { lock (_windowEventLock) { _windowMaximizationChanged += value; } }
            remove { lock (_windowEventLock) { _windowMaximizationChanged -= value; } }
        }
        
        // Update window maximization state and trigger event
        public static void SetWindowMaximized(bool isMaximized)
        {
            if (IsWindowMaximized != isMaximized)
            {
                IsWindowMaximized = isMaximized;
                
                // Invoke the event on the main thread to avoid cross-thread issues
                if (_windowMaximizationChanged is not null)
                {
                    if (Application.Current?.Dispatcher?.IsDispatchRequired ?? false)
                    {
                        Application.Current.Dispatcher.Dispatch(() => 
                            _windowMaximizationChanged?.Invoke(null, isMaximized));
                    }
                    else
                    {
                        _windowMaximizationChanged?.Invoke(null, isMaximized);
                    }
                }
            }
        }
        
        /// <summary>
        /// Initialize device helper and register for system events
        /// </summary>
        public static bool Initialize(bool requireWindowForDesktop = false)
        {
            try
            {
                lock (_initializationLock)
                {
                    if (_isInitialized)
                    {
                        return true;
                    }

                    if (requireWindowForDesktop && IsWindows && (Application.Current?.Windows?.Count ?? 0) == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("DeviceHelper initialization deferred until a window exists on Windows.");
                        return false;
                    }

                // Register for display info changes
                    DeviceDisplay.MainDisplayInfoChanged += OnDisplayInfoChanged;
                    _isInitialized = true;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing DeviceHelper: {ex.Message}");
                return false;
            }
        }
        
        private static void OnDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e)
        {
            // Notify listeners of display changes
            if (_displayInfoChanged is not null)
            {
                if (Application.Current?.Dispatcher?.IsDispatchRequired ?? false)
                {
                    Application.Current.Dispatcher.Dispatch(() => 
                        _displayInfoChanged?.Invoke(sender, e));
                }
                else
                {
                    _displayInfoChanged?.Invoke(sender, e);
                }
            }
            
            // Notify ThemeHelper of potential system theme changes
            // Some platforms change theme based on display brightness/ambient light
            ThemeHelper.CheckForSystemThemeChanges();
        }
        /// <summary>
        /// Returns true if the current device is running Windows
        /// </summary>
        public static bool IsWindows => DeviceInfo.Platform == DevicePlatform.WinUI;
                                     
        /// <summary>
        /// Returns true if the current device is running macOS
        /// </summary>
        public static bool IsMacOS => DeviceInfo.Platform == DevicePlatform.MacCatalyst;
        
        /// <summary>
        /// Returns true if the current device is running iOS
        /// </summary>
        public static bool IsIOS => DeviceInfo.Platform == DevicePlatform.iOS;
        
        /// <summary>
        /// Returns true if the current device is running Android
        /// </summary>
        public static bool IsAndroid => DeviceInfo.Platform == DevicePlatform.Android;
        
        /// <summary>
        /// Returns true if the current device is a desktop platform
        /// </summary>
        public static bool IsDesktop => IsWindows || IsMacOS;
            
        /// <summary>
        /// Determines if the current device is a mobile platform (iOS/Android)
        /// </summary>
        public static bool IsMobile => 
            DeviceInfo.Platform == DevicePlatform.iOS || 
            DeviceInfo.Platform == DevicePlatform.Android;
        
        /// <summary>
        /// Gets the current platform
        /// </summary>
        public static DevicePlatform CurrentPlatform => DeviceInfo.Platform;
        
        /// <summary>
        /// Gets the current platform version
        /// </summary>
        public static string PlatformVersion => DeviceInfo.VersionString;
        
        /// <summary>
        /// Gets the manufacturer of the device
        /// </summary>
        public static string Manufacturer => DeviceInfo.Manufacturer;
        
        /// <summary>
        /// Gets the model of the device
        /// </summary>
        public static string Model => DeviceInfo.Model;
        
        /// <summary>
        /// Gets the screen width in device-independent pixels
        /// </summary>
        public static double ScreenWidth => 
            DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
            
        /// <summary>
        /// Gets the screen height in device-independent pixels
        /// </summary>
        public static double ScreenHeight => 
            DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density;
        
        /// <summary>
        /// Gets the screen density (DPI scaling factor)
        /// </summary>
        public static double ScreenDensity => DeviceDisplay.MainDisplayInfo.Density;
        
        /// <summary>
        /// Gets the screen orientation
        /// </summary>
        public static DisplayOrientation ScreenOrientation => DeviceDisplay.MainDisplayInfo.Orientation;
            
        /// <summary>
        /// Determines if the device is in portrait orientation
        /// </summary>
        public static bool IsPortrait => ScreenOrientation == DisplayOrientation.Portrait;
            
        /// <summary>
        /// Determines if the device is in landscape orientation
        /// </summary>
        public static bool IsLandscape => ScreenOrientation == DisplayOrientation.Landscape;
        
        /// <summary>
        /// Gets whether the device supports system theme changes
        /// </summary>
        public static bool SupportsThemeChanges
        {
            get
            {
                // All modern platforms support theme changes
                return true;
            }
        }
        
        /// <summary>
        /// Gets whether the device has a dynamic system theme (e.g., auto dark mode based on time)
        /// </summary>
        public static bool HasDynamicSystemTheme
        {
            get
            {
                // Windows 10+, iOS 13+, Android 10+, and macOS 10.14+ support dynamic themes
                return true;
            }
        }
            
        /// <summary>
        /// Gets a value indicating whether the current platform is a larger-screen device
        /// </summary>
        public static bool IsLargeScreen
        {
            get
            {
                // Check if we're on a desktop platform or a tablet
                if (IsDesktop)
                    return true;

                // For mobile devices, check if screen is large enough to be considered a tablet
                double width = DeviceDisplay.Current.MainDisplayInfo.Width / DeviceDisplay.Current.MainDisplayInfo.Density;
                double height = DeviceDisplay.Current.MainDisplayInfo.Height / DeviceDisplay.Current.MainDisplayInfo.Density;
                
                // Use the smaller dimension to account for orientation
                double smallerDimension = Math.Min(width, height);
                
                // Consider 600dp or larger as a tablet-sized screen
                return smallerDimension >= 600;
            }
        }

        /// <summary>
        /// Determines if the current platform should handle automatic reconnection and login redirection
        /// This is true for mobile platforms (iOS/Android) and false for desktop platforms
        /// </summary>
        public static bool ShouldHandleAutoReconnect => IsMobile;

        /// <summary>
        /// Gets the current device idiom (phone, tablet, desktop, tv, watch)
        /// </summary>
        public static DeviceIdiom DeviceIdiom => DeviceInfo.Current.Idiom;

        // Screen size thresholds
        private const double SMALL_WIDTH_THRESHOLD = 540;
        private const double MEDIUM_WIDTH_THRESHOLD = 840;
        private const double LARGE_WIDTH_THRESHOLD = 1200;
        
        // Screen size detection based on width
        public static bool IsSmallScreen => DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density < SMALL_WIDTH_THRESHOLD;
        public static bool IsMediumScreen => DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density >= SMALL_WIDTH_THRESHOLD 
                                            && DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density < MEDIUM_WIDTH_THRESHOLD;
        public static bool IsExtraLargeScreen => DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density >= LARGE_WIDTH_THRESHOLD;
        
        // UI mode detection 
        public static bool UseCompactUI => DeviceIdiom == DeviceIdiom.Phone || IsSmallScreen;
        public static bool UseMediumUI => DeviceIdiom == DeviceIdiom.Tablet || IsMediumScreen;
        public static bool UseExpandedUI => IsDesktop || IsLargeScreen || IsExtraLargeScreen;
        
        /// <summary>
        /// Gets whether the device is in dark mode based on system settings
        /// </summary>
        public static bool IsSystemInDarkMode
        {
            get
            {
                try
                {
                    // Use the platform's app theme to determine if system is in dark mode
                    return Application.Current?.PlatformAppTheme == AppTheme.Dark;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error detecting system dark mode: {ex.Message}");
                    return false;
                }
            }
        }
        
        /// <summary>
        /// Gets the secondary text color based on current theme
        /// </summary>
        public static Color GetSecondaryTextColor()
        {
            try
            {
                return Application.Current?.Resources["TextSecondaryColor"] as Color ?? Colors.Gray;
            }
            catch
            {
                return Colors.Gray; // Default fallback
            }
        }
        
        /// <summary>
        /// Gets whether the device supports dark mode
        /// </summary>
        public static bool SupportsDarkMode
        {
            get
            {
                // All supported platforms have dark mode
                return true;
            }
        }
        
        /// <summary>
        /// Gets whether the device has a notch or display cutout
        /// </summary>
        public static bool HasDisplayCutout
        {
            get
            {
                // This is a simplified implementation
                // In a real app, you might want to use platform-specific code to detect this
                return IsMobile && DeviceInfo.Current.Idiom == DeviceIdiom.Phone && 
                       DeviceInfo.Version.Major >= (IsIOS ? 11 : 9);
            }
        }
        
        // Get optimal list item width for responsive grids
        public static double GetOptimalListItemWidth()
        {
            if (UseCompactUI)
                return DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density - 40; // Full width minus padding
            else if (UseMediumUI)
                return 320; // Medium card width
            else
                return 360; // Larger card width
        }
        
        // Get optimal column count for collections
        public static int GetOptimalColumnCount()
        {
            double screenWidth = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
            
            if (screenWidth < SMALL_WIDTH_THRESHOLD)
                return 1;
            else if (screenWidth < MEDIUM_WIDTH_THRESHOLD)
                return 2;
            else if (screenWidth < LARGE_WIDTH_THRESHOLD)
                return 3;
            else
                return 4;
        }
        
        // Get optimal app shell layout
        public static string GetOptimalShellLayout()
        {
            if (IsDesktop)
                return "FlyoutWithTabs"; // Left flyout menu plus tabs on desktop
            else if (DeviceIdiom == DeviceIdiom.Tablet || IsLargeScreen)
                return "TabsWithFlyout"; // Bottom tabs with hamburger menu on tablet
            else
                return "Tabs"; // Just bottom tabs on phone
        }
        
        /// <summary>
        /// Updates the user's presence status to offline when the app is closing
        /// This is specifically for desktop platforms
        /// </summary>
        public static async Task UpdateUserStatusToOfflineOnExit()
        {
            if (!IsDesktop)
            {
                return; // Only apply to desktop platforms
            }
            
            try
            {
                if (Application.Current?.Handler?.MauiContext == null || App.CurrentUser == null)
                {
                    System.Diagnostics.Debug.WriteLine("Cannot update status: MauiContext or CurrentUser is null");
                    return;
                }

                // Validate user ID before proceeding
                if (App.CurrentUser.UserID <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"DeviceHelper: Invalid user ID ({App.CurrentUser.UserID}), skipping status update");
                    return;
                }
                
                var userPresenceService = Application.Current.Handler.MauiContext.Services.GetService<IUserPresenceService>();
                if (userPresenceService != null)
                {
                    System.Diagnostics.Debug.WriteLine($"DeviceHelper: Setting user {App.CurrentUser.UserName} (ID: {App.CurrentUser.UserID}) status to Offline on app exit");
                    
                    // Update status through the presence service only
                    // The presence service will handle the API call internally
                    await userPresenceService.UpdateStatusAsync(TDFShared.Enums.UserPresenceStatus.Offline, "");
                    System.Diagnostics.Debug.WriteLine("DeviceHelper: Successfully updated user status to Offline");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("DeviceHelper: UserPresenceService could not be resolved");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeviceHelper: Error updating user status to Offline: {ex.Message}");
            }
        }
        
        // Get optimal page navigation style
        public static string GetOptimalNavigationStyle()
        {
            if (IsWindows || IsMacOS)
                return "Hierarchical"; // Windows/Mac style navigation
            else if (IsIOS)
                return "Stack"; // iOS stack navigation
            else
                return "Material"; // Material design navigation for Android
        }
        
        /// <summary>
        /// Gets the optimal theme for the current platform and time of day
        /// </summary>
        public static AppTheme GetOptimalTheme()
        {
            // Get the system theme
            var systemTheme = Application.Current?.PlatformAppTheme ?? AppTheme.Light;
            
            // Return the system theme as the optimal choice
            return systemTheme;
        }
        
        /// <summary>
        /// Gets the current system theme
        /// </summary>
        public static AppTheme GetSystemTheme()
        {
            return Application.Current?.PlatformAppTheme ?? AppTheme.Light;
        }
        
       /* /// <summary>
        /// Checks if the device is in power saving mode
        /// </summary>
        public static bool IsInPowerSavingMode()
        {
            try
            {
                // Check if the device has a battery saver mode enabled
                // This is platform-specific and may not be available on all platforms
#if ANDROID
                // For Android, we can check the power manager
                var context = Android.App.Application.Context;
                var powerManager = context.GetSystemService(Android.Content.Context.PowerService) as Android.OS.PowerManager;
                return powerManager?.IsPowerSaveMode ?? false;
#elif IOS || MACCATALYST
                // For iOS/macOS, check low power mode
                return Foundation.NSProcessInfo.ProcessInfo.LowPowerModeEnabled;
#else
                // For other platforms, we don't have a direct way to check
                // Could implement platform-specific checks in the future
                return false;
#endif
            }
            catch
            {
                return false;
            }
        }*/
    }
}
