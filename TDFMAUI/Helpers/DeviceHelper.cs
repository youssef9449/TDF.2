using Microsoft.Maui.Devices;

namespace TDFMAUI.Helpers
{
    /// <summary>
    /// Helper class for detecting platform information
    /// </summary>
    public static class DeviceHelper
    {
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
        /// Determines if the device is in portrait orientation
        /// </summary>
        public static bool IsPortrait => 
            DeviceDisplay.MainDisplayInfo.Orientation == DisplayOrientation.Portrait;
            
        /// <summary>
        /// Determines if the device is in landscape orientation
        /// </summary>
        public static bool IsLandscape => 
            DeviceDisplay.MainDisplayInfo.Orientation == DisplayOrientation.Landscape;
            
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
    }
} 