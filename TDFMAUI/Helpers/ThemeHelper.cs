using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public static AppTheme CurrentTheme => NormalizeTheme(Application.Current?.UserAppTheme ?? _lastSystemTheme);

        /// <summary>
        /// Gets the current system theme
        /// </summary>
        public static AppTheme SystemTheme => NormalizeTheme(Application.Current?.PlatformAppTheme ?? _lastSystemTheme);

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

                _lastSystemTheme = NormalizeTheme(Application.Current.PlatformAppTheme);

                // Initialize theme resources
                InitializeThemeResources();

                // Unsubscribe existing handler if any
                if (_systemThemeHandler != null)
                    Application.Current.RequestedThemeChanged -= _systemThemeHandler;

                // Register for system theme changes
                _systemThemeHandler = (s, e) =>
                {
                    _lastSystemTheme = NormalizeTheme(e.RequestedTheme);

                    if (FollowSystemTheme)
                    {
                        ApplyTheme();
                    }
                };

                Application.Current.RequestedThemeChanged += _systemThemeHandler;

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

                var colorsDict = FindMergedDictionary(mergedDictionaries, "Colors.xaml", "Primary");
                var platformDict = FindMergedDictionary(mergedDictionaries, "PlatformColors.xaml", "WindowsControlHighlightColor");
                var stylesDict = FindMergedDictionary(mergedDictionaries, "Styles.xaml", "Headline");

                if (colorsDict != null)
                {
                    foreach (var key in colorsDict.Keys)
                    {
                        if (key is string stringKey && colorsDict[key] != null)
                        {
                            _lightThemeResources[stringKey] = colorsDict[key];
                            _darkThemeResources[stringKey] = colorsDict[key];
                        }
                    }
                }
                else
                {
                    _lightThemeResources["BackgroundColor"] = Colors.White;
                    _lightThemeResources["TextColor"] = Colors.Black;
                    _lightThemeResources["Primary"] = Color.FromArgb("#FF0078D7");
                    _lightThemeResources["SurfaceColor"] = Color.FromArgb("#FFF8F9FA");
                    _lightThemeResources["TextSecondaryColor"] = Color.FromArgb("#FF6C757D");

                    _darkThemeResources["BackgroundColor"] = Color.FromArgb("#FF121212");
                    _darkThemeResources["TextColor"] = Colors.White;
                    _darkThemeResources["Primary"] = Color.FromArgb("#FF0078D7");
                    _darkThemeResources["SurfaceColor"] = Color.FromArgb("#FF1E1E1E");
                    _darkThemeResources["TextSecondaryColor"] = Color.FromArgb("#FFB0B0B0");
                }

                if (platformDict != null)
                {
                    foreach (var key in platformDict.Keys)
                    {
                        if (key is string stringKey && platformDict[key] != null)
                        {
                            _lightThemeResources[stringKey] = platformDict[key];
                            _darkThemeResources[stringKey] = platformDict[key];
                        }
                    }
                }

                if (stylesDict != null)
                {
                    var styleKeys = new List<string>();

                    foreach (var key in stylesDict.Keys)
                    {
                        if (key is string stringKey && !stringKey.Contains("Color") && stylesDict[key] != null)
                        {
                            styleKeys.Add(stringKey);
                        }
                    }

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
        

        private static ResourceDictionary? FindMergedDictionary(IList<ResourceDictionary> mergedDictionaries, string expectedFileName, string requiredKey)
        {
            var normalizedFileName = expectedFileName.ToLowerInvariant();

            var bySource = mergedDictionaries.FirstOrDefault(d =>
            {
                var source = NormalizeSource(d.Source?.OriginalString);
                return source.EndsWith(normalizedFileName, StringComparison.OrdinalIgnoreCase);
            });

            if (bySource != null)
            {
                return bySource;
            }

            return mergedDictionaries.FirstOrDefault(d => d.ContainsKey(requiredKey));
        }

        private static string NormalizeSource(string? source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return string.Empty;
            }

            return source.Replace('\\', '/').Trim().TrimStart('/').ToLowerInvariant();
        }

        private static AppTheme NormalizeTheme(AppTheme theme)
        {
            return theme == AppTheme.Unspecified ? _lastSystemTheme : theme;
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
                
                // Add only missing platform-specific resources. This keeps XAML-defined
                // resources authoritative and uses generated values strictly as fallbacks.
                foreach (var key in platformLightResources.Keys)
                {
                    if (!_lightThemeResources.ContainsKey(key))
                    {
                        _platformLightThemeResources[key] = platformLightResources[key];
                    }
                }
                
                foreach (var key in platformDarkResources.Keys)
                {
                    if (!_darkThemeResources.ContainsKey(key))
                    {
                        _platformDarkThemeResources[key] = platformDarkResources[key];
                    }
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
                
                var currentSystemTheme = NormalizeTheme(Application.Current.PlatformAppTheme);
                
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
                    ? NormalizeTheme(Application.Current.PlatformAppTheme)
                    : NormalizeTheme(UserTheme);

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
                
                // Since we're using AppThemeBinding in Colors.xaml, the theme switching is automatic
                // We just need to ensure the UserAppTheme is set correctly
                // The AppThemeBinding will handle the color switching automatically
                
                // Apply enhanced theme resources, but do not replace existing XAML-defined
                // resources. This prevents C#-generated values from fighting with
                // AppThemeBinding and merged dictionaries.
                var themeResources = theme == AppTheme.Dark ? _darkThemeResources : _lightThemeResources;
                foreach (var key in themeResources.Keys)
                {
                    if (!Application.Current.Resources.ContainsKey(key))
                    {
                        Application.Current.Resources[key] = themeResources[key];
                    }
                }
                
                // Apply platform-specific theme resources if enabled
                if (UsePlatformSpecificThemes)
                {
                    var platformResources = theme == AppTheme.Dark 
                        ? _platformDarkThemeResources 
                        : _platformLightThemeResources;
                    
                    foreach (var key in platformResources.Keys)
                    {
                        if (!Application.Current.Resources.ContainsKey(key))
                        {
                            Application.Current.Resources[key] = platformResources[key];
                        }
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
