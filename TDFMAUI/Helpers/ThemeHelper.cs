using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TDFMAUI.Helpers
{
    /// <summary>
    /// Helper class for managing application themes with platform-aware adaptations
    /// </summary>
    public static class ThemeHelper
    {
        // Event for theme changes
        private static event EventHandler<AppTheme>? _themeChanged;
        private static readonly object _eventLock = new object();
        
        // Theme resource dictionaries
        private static readonly ResourceDictionary _lightThemeResources = new ResourceDictionary();
        private static readonly ResourceDictionary _darkThemeResources = new ResourceDictionary();
        
        // Platform-specific theme resources
        private static readonly ResourceDictionary _platformLightThemeResources = new ResourceDictionary();
        private static readonly ResourceDictionary _platformDarkThemeResources = new ResourceDictionary();
        
        // Last detected system theme
        private static AppTheme _lastSystemTheme = AppTheme.Light;
        
        public static event EventHandler<AppTheme> ThemeChanged
        {
            add { lock (_eventLock) { _themeChanged += value; } }
            remove { lock (_eventLock) { _themeChanged -= value; } }
        }

        /// <summary>
        /// Gets the current app theme
        /// </summary>
        public static AppTheme CurrentTheme => Application.Current?.UserAppTheme ?? AppTheme.Light;

        /// <summary>
        /// Gets the current system theme
        /// </summary>
        public static AppTheme SystemTheme => Application.Current?.PlatformAppTheme ?? AppTheme.Light;

        /// <summary>
        /// Gets whether the app is currently using dark theme
        /// </summary>
        public static bool IsDarkTheme => CurrentTheme == AppTheme.Dark;

        /// <summary>
        /// Gets whether the app is currently using light theme
        /// </summary>
        public static bool IsLightTheme => CurrentTheme == AppTheme.Light;
        
        /// <summary>
        /// Gets whether the system is currently using dark theme
        /// </summary>
        public static bool IsSystemDarkTheme => SystemTheme == AppTheme.Dark;
        
        /// <summary>
        /// Gets whether the system is currently using light theme
        /// </summary>
        public static bool IsSystemLightTheme => SystemTheme == AppTheme.Light;

        /// <summary>
        /// Gets or sets whether the app should follow the system theme
        /// </summary>
        public static bool FollowSystemTheme
        {
            get
            {
                try
                {
                    return Preferences.Get(nameof(FollowSystemTheme), true);
                }
                catch (Exception ex)
                {
                    // Log error and return default value
                    System.Diagnostics.Debug.WriteLine($"Error reading FollowSystemTheme preference: {ex.Message}");
                    return true;
                }
            }
            set
            {
                Preferences.Set(nameof(FollowSystemTheme), value);
                ApplyTheme();
            }
        }

        /// <summary>
        /// Gets or sets the user's preferred theme when not following system theme
        /// </summary>
        public static AppTheme UserTheme
        {
            get => (AppTheme)Preferences.Get(nameof(UserTheme), (int)AppTheme.Light);
            set
            {
                Preferences.Set(nameof(UserTheme), (int)value);
                if (!FollowSystemTheme)
                {
                    ApplyTheme();
                }
            }
        }


        
        /// <summary>
        /// Gets whether the app is using platform-specific theme adaptations
        /// </summary>
        public static bool UsePlatformSpecificThemes
        {
            get => Preferences.Get(nameof(UsePlatformSpecificThemes), true);
            set
            {
                Preferences.Set(nameof(UsePlatformSpecificThemes), value);
                ApplyTheme();
            }
        }

        /// <summary>
        /// Initialize theme settings and apply the appropriate theme
        /// </summary>
        private static EventHandler<AppThemeChangedEventArgs>? _systemThemeHandler;
        
        public static void Initialize()
        {
            try
            {
                if (Application.Current == null)
                {
                    System.Diagnostics.Debug.WriteLine("Application.Current is null, cannot initialize theme helper");
                    return;
                }
                
                // Initialize theme resources
                InitializeThemeResources();
                
                // Unsubscribe existing handler if any
                if (_systemThemeHandler != null)
                    Application.Current.RequestedThemeChanged -= _systemThemeHandler;
                
                // Register for system theme changes
                _systemThemeHandler = (s, e) =>
                {
                    // Store the last detected system theme
                    _lastSystemTheme = e.RequestedTheme;
                    
                    if (FollowSystemTheme)
                    {
                        // If following system theme, update when system changes
                        ApplyTheme();
                    }
                };
                
                // Register for system theme changes
                Application.Current.RequestedThemeChanged += _systemThemeHandler;
                
                // Store initial system theme
                _lastSystemTheme = Application.Current.PlatformAppTheme;
                
                // Apply the initial theme
                ApplyTheme();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing theme helper: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Initialize theme resources for light and dark themes
        /// </summary>
        private static void InitializeThemeResources()
        {
            try
            {
                // Clear existing resources
                _lightThemeResources.Clear();
                _darkThemeResources.Clear();
                _platformLightThemeResources.Clear();
                _platformDarkThemeResources.Clear();
                
                // Load resources from the existing theme dictionaries
                LoadResourcesFromExistingThemes();
                
                // Add platform-specific resources
                UpdatePlatformSpecificResources();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing theme resources: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Load resources from the existing theme dictionaries in the app
        /// </summary>
        private static void LoadResourcesFromExistingThemes()
        {
            try
            {
                if (Application.Current?.Resources?.MergedDictionaries == null)
                    return;
                
                var mergedDictionaries = Application.Current.Resources.MergedDictionaries;
                
                // Find the theme dictionaries
                var lightDict = mergedDictionaries.FirstOrDefault(d => d.Source?.OriginalString?.Contains("Colors.Light.xaml") == true);
                var darkDict = mergedDictionaries.FirstOrDefault(d => d.Source?.OriginalString?.Contains("Colors.Dark.xaml") == true);
                var platformDict = mergedDictionaries.FirstOrDefault(d => d.Source?.OriginalString?.Contains("PlatformColors.xaml") == true);
                var stylesDict = mergedDictionaries.FirstOrDefault(d => d.Source?.OriginalString?.Contains("Styles.xaml") == true);
                
                // Load light theme resources
                if (lightDict != null)
                {
                    foreach (var key in lightDict.Keys)
                    {
                        if (key is string stringKey && lightDict[key] != null)
                        {
                            _lightThemeResources[stringKey] = lightDict[key];
                        }
                    }
                }
                else
                {
                    // Fallback light theme resources if dictionary not found
                    _lightThemeResources["BackgroundColor"] = Colors.White;
                    _lightThemeResources["ForegroundColor"] = Colors.Black;
                    _lightThemeResources["PrimaryColor"] = Color.FromArgb("#FF0078D7");
                    _lightThemeResources["SecondaryColor"] = Color.FromArgb("#FF6C757D");
                    _lightThemeResources["AccentColor"] = Color.FromArgb("#FF0078D7");
                    _lightThemeResources["SurfaceColor"] = Color.FromArgb("#FFF8F9FA");
                    _lightThemeResources["CardColor"] = Colors.White;
                    _lightThemeResources["BorderColor"] = Color.FromArgb("#FFCED4DA");
                    _lightThemeResources["TextColor"] = Colors.Black;
                    _lightThemeResources["TextSecondaryColor"] = Color.FromArgb("#FF6C757D");
                    _lightThemeResources["TextTertiaryColor"] = Color.FromArgb("#FF8A8A8A");
                    _lightThemeResources["DividerColor"] = Color.FromArgb("#FFE0E0E0");
                }
                
                // Load dark theme resources
                if (darkDict != null)
                {
                    foreach (var key in darkDict.Keys)
                    {
                        if (key is string stringKey && darkDict[key] != null)
                        {
                            _darkThemeResources[stringKey] = darkDict[key];
                        }
                    }
                }
                else
                {
                    // Fallback dark theme resources if dictionary not found
                    _darkThemeResources["BackgroundColor"] = Color.FromArgb("#FF121212");
                    _darkThemeResources["ForegroundColor"] = Colors.White;
                    _darkThemeResources["PrimaryColor"] = Color.FromArgb("#FF0078D7");
                    _darkThemeResources["SecondaryColor"] = Color.FromArgb("#FF6C757D");
                    _darkThemeResources["AccentColor"] = Color.FromArgb("#FF0078D7");
                    _darkThemeResources["SurfaceColor"] = Color.FromArgb("#FF1E1E1E");
                    _darkThemeResources["CardColor"] = Color.FromArgb("#FF2D2D2D");
                    _darkThemeResources["BorderColor"] = Color.FromArgb("#FF444444");
                    _darkThemeResources["TextColor"] = Colors.White;
                    _darkThemeResources["TextSecondaryColor"] = Color.FromArgb("#FFB0B0B0");
                    _darkThemeResources["TextTertiaryColor"] = Color.FromArgb("#FF8A8A8A");
                    _darkThemeResources["DividerColor"] = Color.FromArgb("#FF333333");
                }
                
                // Load platform-specific resources
                if (platformDict != null)
                {
                    foreach (var key in platformDict.Keys)
                    {
                        if (key is string stringKey && platformDict[key] != null)
                        {
                            // AppThemeBinding is not accessible in C#; just add all resources
                            _lightThemeResources[stringKey] = platformDict[key];
                            _darkThemeResources[stringKey] = platformDict[key];
                        }
                    }
                }
                
                // Load common styles
                if (stylesDict != null)
                {
                    // Store style resources that should be applied to both themes
                    var styleKeys = new List<string>();
                    
                    foreach (var key in stylesDict.Keys)
                    {
                        if (key is string stringKey && !stringKey.Contains("Color") && stylesDict[key] != null)
                        {
                            styleKeys.Add(stringKey);
                        }
                    }
                    
                    // Apply styles to both themes
                    foreach (var key in styleKeys)
                    {
                        _lightThemeResources[key] = stylesDict[key];
                        _darkThemeResources[key] = stylesDict[key];
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading resources from existing themes: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Update platform-specific theme resources
        /// </summary>
        private static void UpdatePlatformSpecificResources()
        {
            try
            {
                // Clear existing platform resources
                _platformLightThemeResources.Clear();
                _platformDarkThemeResources.Clear();
                
                // Get platform-specific resources
                var platformLightResources = DeviceHelper.GetPlatformThemeResources(AppTheme.Light);
                var platformDarkResources = DeviceHelper.GetPlatformThemeResources(AppTheme.Dark);
                
                // Add platform-specific resources
                foreach (var key in platformLightResources.Keys)
                {
                    _platformLightThemeResources[key] = platformLightResources[key];
                }
                
                foreach (var key in platformDarkResources.Keys)
                {
                    _platformDarkThemeResources[key] = platformDarkResources[key];
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating platform-specific resources: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Check for system theme changes
        /// </summary>
        public static void CheckForSystemThemeChanges()
        {
            try
            {
                if (Application.Current == null) return;
                
                var currentSystemTheme = Application.Current.PlatformAppTheme;
                
                // If system theme has changed
                if (currentSystemTheme != _lastSystemTheme)
                {
                    _lastSystemTheme = currentSystemTheme;
                    
                    if (FollowSystemTheme)
                    {
                        // Apply the new theme
                        ApplyTheme();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking for system theme changes: {ex.Message}");
            }
        }

        /// <summary>
        /// Apply the appropriate theme based on settings
        /// </summary>
        public static void ApplyTheme()
        {
            try
            {
                if (Application.Current == null) return;
                
                // Determine which theme to use
                var newTheme = FollowSystemTheme 
                    ? Application.Current.PlatformAppTheme 
                    : UserTheme;

                // Apply the theme
                Application.Current.UserAppTheme = newTheme;
                
                // Apply theme resources
                ApplyThemeResources(newTheme);
                
                // Notify listeners of theme change
                lock (_eventLock)
                {
                    _themeChanged?.Invoke(null, newTheme);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying theme: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Apply theme resources to the application
        /// </summary>
        private static void ApplyThemeResources(AppTheme theme)
        {
            try
            {
                if (Application.Current?.Resources == null) return;
                
                // Get the merged dictionaries
                var mergedDictionaries = Application.Current.Resources.MergedDictionaries;
                if (mergedDictionaries == null) return;
                
                // Find the theme dictionaries
                var lightDict = mergedDictionaries.FirstOrDefault(d => d.Source?.OriginalString?.Contains("Colors.Light.xaml") == true);
                var darkDict = mergedDictionaries.FirstOrDefault(d => d.Source?.OriginalString?.Contains("Colors.Dark.xaml") == true);
                
                // Get the appropriate theme resources
                var themeResources = theme == AppTheme.Dark ? _darkThemeResources : _lightThemeResources;
                
                // Apply theme resources from the appropriate dictionary first
                var sourceDict = theme == AppTheme.Dark ? darkDict : lightDict;
                if (sourceDict != null)
                {
                    foreach (var key in sourceDict.Keys)
                    {
                        if (key is string stringKey)
                        {
                            Application.Current.Resources[stringKey] = sourceDict[key];
                        }
                    }
                }
                
                // Apply our enhanced theme resources (these will override any conflicts)
                foreach (var key in themeResources.Keys)
                {
                    Application.Current.Resources[key] = themeResources[key];
                }
                
                // Apply platform-specific theme resources if enabled
                if (UsePlatformSpecificThemes)
                {
                    var platformResources = theme == AppTheme.Dark 
                        ? _platformDarkThemeResources 
                        : _platformLightThemeResources;
                    
                    foreach (var key in platformResources.Keys)
                    {
                        Application.Current.Resources[key] = platformResources[key];
                    }
                }
                
                // Update dynamic resources that use AppThemeBinding
                UpdateDynamicResources();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying theme resources: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Update dynamic resources that use AppThemeBinding
        /// </summary>
        private static void UpdateDynamicResources()
        {
            try
            {
                if (Application.Current?.Resources == null) return;
                
                // Force update of any dynamic resources
                var temp = new ResourceDictionary();
                var keys = Application.Current.Resources.Keys.ToList();
                
                foreach (var key in keys)
                {
                    if (key is string stringKey && Application.Current.Resources[key] != null)
                    {
                        // Store the resource temporarily
                        temp[stringKey] = Application.Current.Resources[key];
                        
                        // Remove and re-add to force update of AppThemeBinding
                        Application.Current.Resources.Remove(stringKey);
                        Application.Current.Resources[stringKey] = temp[stringKey];
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating dynamic resources: {ex.Message}");
            }
        }

        /// <summary>
        /// Toggle between light and dark themes
        /// </summary>
        public static void ToggleTheme()
        {
            // If following system, switch to manual mode
            if (FollowSystemTheme)
            {
                FollowSystemTheme = false;
                UserTheme = Application.Current?.PlatformAppTheme == AppTheme.Dark 
                    ? AppTheme.Light 
                    : AppTheme.Dark;
            }
            else
            {
                // Toggle between light and dark
                UserTheme = UserTheme == AppTheme.Dark 
                    ? AppTheme.Light 
                    : AppTheme.Dark;
            }
        }

        /// <summary>
        /// Set the app to use the system theme
        /// </summary>
        public static void UseSystemTheme()
        {
            FollowSystemTheme = true;
        }

        /// <summary>
        /// Set the app to use light theme
        /// </summary>
        public static void UseLightTheme()
        {
            FollowSystemTheme = false;
            UserTheme = AppTheme.Light;
        }

        /// <summary>
        /// Set the app to use dark theme
        /// </summary>
        public static void UseDarkTheme()
        {
            FollowSystemTheme = false;
            UserTheme = AppTheme.Dark;
        }
        
        /// <summary>
        /// Get a color that adapts to the current theme
        /// </summary>
        public static Color GetAdaptiveColor(Color lightColor, Color darkColor)
        {
            return IsDarkTheme ? darkColor : lightColor;
        }
        
        /// <summary>
        /// Get a resource value that adapts to the current theme
        /// </summary>
        public static T GetAdaptiveResource<T>(string lightResourceKey, string darkResourceKey)
        {
            if (Application.Current?.Resources == null)
                return default!;
                
            string key = IsDarkTheme ? darkResourceKey : lightResourceKey;
            
            if (Application.Current.Resources.TryGetValue(key, out var value) && value is T typedValue)
                return typedValue;
                
            return default!;
        }
        
        /// <summary>
        /// Get a theme-specific resource value
        /// </summary>
        public static T GetThemeResource<T>(string resourceKey, AppTheme theme)
        {
            if (Application.Current?.Resources == null)
                return default!;
                
            // Try to get the theme-specific resource
            string themeKey = $"{resourceKey}{(theme == AppTheme.Dark ? "Dark" : "Light")}";
            
            if (Application.Current.Resources.TryGetValue(themeKey, out var themeValue) && themeValue is T themeTypedValue)
                return themeTypedValue;
                
            // Fall back to the base resource
            if (Application.Current.Resources.TryGetValue(resourceKey, out var value) && value is T typedValue)
                return typedValue;
                
            return default!;
        }
        
        /// <summary>
        /// Apply platform-specific theme adaptations
        /// </summary>
        public static void ApplyPlatformSpecificAdaptations()
        {
            try
            {
                if (Application.Current == null) return;
                
                // Update platform-specific resources
                UpdatePlatformSpecificResources();
                
                // Apply the current theme with updated resources
                ApplyThemeResources(CurrentTheme);
                
                // Apply platform-specific UI adaptations
                if (DeviceHelper.IsWindows)
                {
                    // Windows-specific adaptations
                    ApplyWindowsThemeAdaptations();
                }
                else if (DeviceHelper.IsMacOS)
                {
                    // macOS-specific adaptations
                    ApplyMacOSThemeAdaptations();
                }
                else if (DeviceHelper.IsIOS)
                {
                    // iOS-specific adaptations
                    ApplyIOSThemeAdaptations();
                }
                else if (DeviceHelper.IsAndroid)
                {
                    // Android-specific adaptations
                    ApplyAndroidThemeAdaptations();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying platform-specific adaptations: {ex.Message}");
            }
        }
        
        // Platform-specific theme adaptations
        private static void ApplyWindowsThemeAdaptations()
        {
            // Windows-specific theme adaptations would go here
            // This might involve setting window chrome colors, etc.
        }
        
        private static void ApplyMacOSThemeAdaptations()
        {
            // macOS-specific theme adaptations would go here
        }
        
        private static void ApplyIOSThemeAdaptations()
        {
            // iOS-specific theme adaptations would go here
            // This might involve setting status bar style, etc.
        }
        
        private static void ApplyAndroidThemeAdaptations()
        {
            // Android-specific theme adaptations would go here
            // This might involve setting status bar and navigation bar colors
        }
    }
}