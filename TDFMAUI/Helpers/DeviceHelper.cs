using Microsoft.Maui.Devices;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using TDFMAUI.Services;
using TDFShared.Enums;
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
        private static event EventHandler<DisplayInfoChangedEventArgs>? _displayInfoChanged;
        
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
        public static void Initialize()
        {
            try
            {
                // Register for display info changes
                DeviceDisplay.MainDisplayInfoChanged += OnDisplayInfoChanged;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing DeviceHelper: {ex.Message}");
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
        /// Gets the platform-specific theme resource dictionary
        /// </summary>
        public static ResourceDictionary GetPlatformThemeResources(AppTheme theme)
        {
            var resources = new ResourceDictionary();
            
            // Common resources for all platforms
            if (theme == AppTheme.Dark)
            {
                // Common dark theme resources
                resources["BackgroundColor"] = Color.FromArgb("#FF121212");
                resources["ForegroundColor"] = Colors.White;
                resources["SurfaceColor"] = Color.FromArgb("#FF1E1E1E");
                resources["CardColor"] = Color.FromArgb("#FF2D2D2D");
                resources["BorderColor"] = Color.FromArgb("#FF444444");
                resources["TextColor"] = Colors.White;
                resources["TextSecondaryColor"] = Color.FromArgb("#FFB0B0B0");
                resources["TextTertiaryColor"] = Color.FromArgb("#FF8A8A8A");
                resources["DividerColor"] = Color.FromArgb("#FF333333");
                resources["ShadowColor"] = Color.FromArgb("#40000000");
                resources["ElevationColor"] = Color.FromArgb("#FF2D2D2D");
            }
            else
            {
                // Common light theme resources
                resources["BackgroundColor"] = Colors.White;
                resources["ForegroundColor"] = Colors.Black;
                resources["SurfaceColor"] = Color.FromArgb("#FFF8F9FA");
                resources["CardColor"] = Colors.White;
                resources["BorderColor"] = Color.FromArgb("#FFCED4DA");
                resources["TextColor"] = Colors.Black;
                resources["TextSecondaryColor"] = Color.FromArgb("#FF6C757D");
                resources["TextTertiaryColor"] = Color.FromArgb("#FF8A8A8A");
                resources["DividerColor"] = Color.FromArgb("#FFE0E0E0");
                resources["ShadowColor"] = Color.FromArgb("#20000000");
                resources["ElevationColor"] = Colors.White;
            }
            
            // Add platform-specific theme resources
            if (IsWindows)
            {
                // Windows-specific theme resources
                if (theme == AppTheme.Dark)
                {
                    // Windows dark theme
                    resources["WindowBackgroundColor"] = Color.FromArgb("#FF202020");
                    resources["WindowTitleBarColor"] = Color.FromArgb("#FF202020");
                    resources["WindowTitleBarButtonBackgroundColor"] = Color.FromArgb("#FF202020");
                    resources["WindowTitleBarButtonForegroundColor"] = Colors.White;
                    resources["WindowTitleBarButtonHoverBackgroundColor"] = Color.FromArgb("#FF404040");
                    resources["WindowTitleBarButtonHoverForegroundColor"] = Colors.White;
                    resources["WindowTitleBarButtonPressedBackgroundColor"] = Color.FromArgb("#FF606060");
                    resources["WindowTitleBarButtonPressedForegroundColor"] = Colors.White;
                    resources["WindowTitleBarButtonInactiveBackgroundColor"] = Color.FromArgb("#FF202020");
                    resources["WindowTitleBarButtonInactiveForegroundColor"] = Color.FromArgb("#FF808080");
                    resources["WindowBorderColor"] = Color.FromArgb("#FF404040");
                    resources["WindowShadowColor"] = Color.FromArgb("#40000000");
                    
                    // Windows-specific control colors
                    resources["WindowsAccentColor"] = Color.FromArgb("#FF0078D7");
                    resources["WindowsControlBackgroundColor"] = Color.FromArgb("#FF2D2D2D");
                    resources["WindowsControlBorderColor"] = Color.FromArgb("#FF444444");
                    resources["WindowsControlHighlightColor"] = Color.FromArgb("#FF0078D7");
                    resources["WindowsControlForegroundColor"] = Colors.White;
                    resources["WindowsControlDisabledColor"] = Color.FromArgb("#FF666666");
                }
                else
                {
                    // Windows light theme
                    resources["WindowBackgroundColor"] = Colors.White;
                    resources["WindowTitleBarColor"] = Color.FromArgb("#FFF0F0F0");
                    resources["WindowTitleBarButtonBackgroundColor"] = Color.FromArgb("#FFF0F0F0");
                    resources["WindowTitleBarButtonForegroundColor"] = Colors.Black;
                    resources["WindowTitleBarButtonHoverBackgroundColor"] = Color.FromArgb("#FFE0E0E0");
                    resources["WindowTitleBarButtonHoverForegroundColor"] = Colors.Black;
                    resources["WindowTitleBarButtonPressedBackgroundColor"] = Color.FromArgb("#FFD0D0D0");
                    resources["WindowTitleBarButtonPressedForegroundColor"] = Colors.Black;
                    resources["WindowTitleBarButtonInactiveBackgroundColor"] = Color.FromArgb("#FFF0F0F0");
                    resources["WindowTitleBarButtonInactiveForegroundColor"] = Color.FromArgb("#FF808080");
                    resources["WindowBorderColor"] = Color.FromArgb("#FFE0E0E0");
                    resources["WindowShadowColor"] = Color.FromArgb("#20000000");
                    
                    // Windows-specific control colors
                    resources["WindowsAccentColor"] = Color.FromArgb("#FF0078D7");
                    resources["WindowsControlBackgroundColor"] = Colors.White;
                    resources["WindowsControlBorderColor"] = Color.FromArgb("#FFCED4DA");
                    resources["WindowsControlHighlightColor"] = Color.FromArgb("#FF0078D7");
                    resources["WindowsControlForegroundColor"] = Colors.Black;
                    resources["WindowsControlDisabledColor"] = Color.FromArgb("#FFCCCCCC");
                }
            }
            else if (IsMacOS)
            {
                // macOS-specific theme resources
                if (theme == AppTheme.Dark)
                {
                    // macOS dark theme
                    resources["MacOSWindowBackgroundColor"] = Color.FromArgb("#FF2D2D2D");
                    resources["MacOSTitleBarColor"] = Color.FromArgb("#FF3D3D3D");
                    resources["MacOSToolbarColor"] = Color.FromArgb("#FF3D3D3D");
                    resources["MacOSSidebarColor"] = Color.FromArgb("#FF252525");
                    resources["MacOSAccentColor"] = Color.FromArgb("#FF0078D7");
                    resources["MacOSControlBackgroundColor"] = Color.FromArgb("#FF3D3D3D");
                    resources["MacOSControlBorderColor"] = Color.FromArgb("#FF555555");
                    resources["MacOSControlHighlightColor"] = Color.FromArgb("#FF0078D7");
                    resources["MacOSControlForegroundColor"] = Colors.White;
                    resources["MacOSControlDisabledColor"] = Color.FromArgb("#FF666666");
                }
                else
                {
                    // macOS light theme
                    resources["MacOSWindowBackgroundColor"] = Colors.White;
                    resources["MacOSTitleBarColor"] = Color.FromArgb("#FFF5F5F5");
                    resources["MacOSToolbarColor"] = Color.FromArgb("#FFF5F5F5");
                    resources["MacOSSidebarColor"] = Color.FromArgb("#FFF0F0F0");
                    resources["MacOSAccentColor"] = Color.FromArgb("#FF0078D7");
                    resources["MacOSControlBackgroundColor"] = Colors.White;
                    resources["MacOSControlBorderColor"] = Color.FromArgb("#FFCED4DA");
                    resources["MacOSControlHighlightColor"] = Color.FromArgb("#FF0078D7");
                    resources["MacOSControlForegroundColor"] = Colors.Black;
                    resources["MacOSControlDisabledColor"] = Color.FromArgb("#FFCCCCCC");
                }
            }
            else if (IsIOS)
            {
                // iOS-specific theme resources
                if (theme == AppTheme.Dark)
                {
                    // iOS dark theme
                    resources["iOSBackgroundColor"] = Color.FromArgb("#FF000000");
                    resources["iOSGroupedBackgroundColor"] = Color.FromArgb("#FF1C1C1E");
                    resources["iOSNavigationBarColor"] = Color.FromArgb("#FF1C1C1E");
                    resources["iOSTabBarColor"] = Color.FromArgb("#FF2C2C2E");
                    resources["iOSToolbarColor"] = Color.FromArgb("#FF1C1C1E");
                    resources["iOSSearchBarColor"] = Color.FromArgb("#FF1C1C1E");
                    resources["iOSCellBackgroundColor"] = Color.FromArgb("#FF1C1C1E");
                    resources["iOSCellSelectedBackgroundColor"] = Color.FromArgb("#FF2C2C2E");
                    resources["iOSSeparatorColor"] = Color.FromArgb("#FF38383A");
                    resources["iOSLabelColor"] = Colors.White;
                    resources["iOSSecondaryLabelColor"] = Color.FromArgb("#99FFFFFF");
                    resources["iOSTertiaryLabelColor"] = Color.FromArgb("#66FFFFFF");
                    resources["iOSSystemFillColor"] = Color.FromArgb("#33FFFFFF");
                    resources["iOSSystemBackgroundColor"] = Color.FromArgb("#FF1C1C1E");
                    resources["iOSSystemGroupedBackgroundColor"] = Color.FromArgb("#FF000000");
                    resources["iOSSystemGray"] = Color.FromArgb("#FF8E8E93");
                    resources["iOSSystemGray2"] = Color.FromArgb("#FF636366");
                    resources["iOSSystemGray3"] = Color.FromArgb("#FF48484A");
                    resources["iOSSystemGray4"] = Color.FromArgb("#FF3A3A3C");
                    resources["iOSSystemGray5"] = Color.FromArgb("#FF2C2C2E");
                    resources["iOSSystemGray6"] = Color.FromArgb("#FF1C1C1E");
                }
                else
                {
                    // iOS light theme
                    resources["iOSBackgroundColor"] = Color.FromArgb("#FFF2F2F7");
                    resources["iOSGroupedBackgroundColor"] = Color.FromArgb("#FFF2F2F7");
                    resources["iOSNavigationBarColor"] = Color.FromArgb("#FFF8F8F8");
                    resources["iOSTabBarColor"] = Colors.White;
                    resources["iOSToolbarColor"] = Color.FromArgb("#FFF8F8F8");
                    resources["iOSSearchBarColor"] = Color.FromArgb("#FFF8F8F8");
                    resources["iOSCellBackgroundColor"] = Colors.White;
                    resources["iOSCellSelectedBackgroundColor"] = Color.FromArgb("#FFD9D9D9");
                    resources["iOSSeparatorColor"] = Color.FromArgb("#FFC6C6C8");
                    resources["iOSLabelColor"] = Colors.Black;
                    resources["iOSSecondaryLabelColor"] = Color.FromArgb("#993C3C43");
                    resources["iOSTertiaryLabelColor"] = Color.FromArgb("#663C3C43");
                    resources["iOSSystemFillColor"] = Color.FromArgb("#33787880");
                    resources["iOSSystemBackgroundColor"] = Colors.White;
                    resources["iOSSystemGroupedBackgroundColor"] = Color.FromArgb("#FFF2F2F7");
                    resources["iOSSystemGray"] = Color.FromArgb("#FF8E8E93");
                    resources["iOSSystemGray2"] = Color.FromArgb("#FFAEAEB2");
                    resources["iOSSystemGray3"] = Color.FromArgb("#FFC7C7CC");
                    resources["iOSSystemGray4"] = Color.FromArgb("#FFD1D1D6");
                    resources["iOSSystemGray5"] = Color.FromArgb("#FFE5E5EA");
                    resources["iOSSystemGray6"] = Color.FromArgb("#FFF2F2F7");
                }
            }
            else if (IsAndroid)
            {
                // Android-specific theme resources
                if (theme == AppTheme.Dark)
                {
                    // Android dark theme (Material Design)
                    resources["AndroidStatusBarColor"] = Color.FromArgb("#FF121212");
                    resources["AndroidNavigationBarColor"] = Color.FromArgb("#FF1E1E1E");
                    resources["AndroidBackgroundColor"] = Color.FromArgb("#FF121212");
                    resources["AndroidSurfaceColor"] = Color.FromArgb("#FF1E1E1E");
                    resources["AndroidPrimaryColor"] = Color.FromArgb("#FF1F1F1F");
                    resources["AndroidPrimaryDarkColor"] = Color.FromArgb("#FF121212");
                    resources["AndroidAccentColor"] = Color.FromArgb("#FF0078D7");
                    resources["AndroidPrimaryTextColor"] = Colors.White;
                    resources["AndroidSecondaryTextColor"] = Color.FromArgb("#B3FFFFFF");
                    resources["AndroidDisabledTextColor"] = Color.FromArgb("#61FFFFFF");
                    resources["AndroidButtonColor"] = Color.FromArgb("#FF0078D7");
                    resources["AndroidButtonTextColor"] = Colors.White;
                    resources["AndroidSwitchThumbColor"] = Color.FromArgb("#FF0078D7");
                    resources["AndroidSwitchTrackColor"] = Color.FromArgb("#520078D7");
                    resources["AndroidDividerColor"] = Color.FromArgb("#1FFFFFFF");
                    resources["AndroidRippleColor"] = Color.FromArgb("#33FFFFFF");
                    resources["AndroidToolbarColor"] = Color.FromArgb("#FF1F1F1F");
                    resources["AndroidTabLayoutColor"] = Color.FromArgb("#FF1F1F1F");
                    resources["AndroidCardViewColor"] = Color.FromArgb("#FF2D2D2D");
                    resources["AndroidDialogBackgroundColor"] = Color.FromArgb("#FF2D2D2D");
                    resources["AndroidProgressIndicatorColor"] = Color.FromArgb("#FF0078D7");
                    resources["AndroidSelectionHandleColor"] = Color.FromArgb("#FF0078D7");
                    resources["AndroidNavigationViewColor"] = Color.FromArgb("#FF1F1F1F");
                    resources["AndroidBottomNavigationColor"] = Color.FromArgb("#FF1F1F1F");
                }
                else
                {
                    // Android light theme (Material Design)
                    resources["AndroidStatusBarColor"] = Color.FromArgb("#FFF5F5F5");
                    resources["AndroidNavigationBarColor"] = Colors.White;
                    resources["AndroidBackgroundColor"] = Colors.White;
                    resources["AndroidSurfaceColor"] = Colors.White;
                    resources["AndroidPrimaryColor"] = Color.FromArgb("#FF0078D7");
                    resources["AndroidPrimaryDarkColor"] = Color.FromArgb("#FF005A9E");
                    resources["AndroidAccentColor"] = Color.FromArgb("#FF0078D7");
                    resources["AndroidPrimaryTextColor"] = Color.FromArgb("#DE000000");
                    resources["AndroidSecondaryTextColor"] = Color.FromArgb("#8A000000");
                    resources["AndroidDisabledTextColor"] = Color.FromArgb("#61000000");
                    resources["AndroidButtonColor"] = Color.FromArgb("#FF0078D7");
                    resources["AndroidButtonTextColor"] = Colors.White;
                    resources["AndroidSwitchThumbColor"] = Color.FromArgb("#FF0078D7");
                    resources["AndroidSwitchTrackColor"] = Color.FromArgb("#520078D7");
                    resources["AndroidDividerColor"] = Color.FromArgb("#1F000000");
                    resources["AndroidRippleColor"] = Color.FromArgb("#33000000");
                    resources["AndroidToolbarColor"] = Colors.White;
                    resources["AndroidTabLayoutColor"] = Colors.White;
                    resources["AndroidCardViewColor"] = Colors.White;
                    resources["AndroidDialogBackgroundColor"] = Colors.White;
                    resources["AndroidProgressIndicatorColor"] = Color.FromArgb("#FF0078D7");
                    resources["AndroidSelectionHandleColor"] = Color.FromArgb("#FF0078D7");
                    resources["AndroidNavigationViewColor"] = Colors.White;
                    resources["AndroidBottomNavigationColor"] = Colors.White;
                }
            }
            
            return resources;
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