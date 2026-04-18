using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

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
        /// Gets or sets whether the app should use platform-specific themes
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

                // On Windows/desktop, defer initialization until a MAUI window exists.
                if (DeviceHelper.IsDesktop && Application.Current.Windows.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("ThemeHelper.Initialize deferred: window is not ready yet.");
                    return;
                }

                _lastSystemTheme = NormalizeTheme(Application.Current.PlatformAppTheme);

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

                if (DeviceHelper.IsDesktop && Application.Current.Windows.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("ThemeHelper.ApplyTheme skipped: window is not ready yet.");
                    return;
                }
                
                // Determine which theme to use
                var newTheme = FollowSystemTheme
                    ? NormalizeTheme(Application.Current.PlatformAppTheme)
                    : NormalizeTheme(UserTheme);

                // Apply the theme - This triggers AppThemeBinding updates automatically
                Application.Current.UserAppTheme = newTheme;

                // Swap the base color resources (e.g. BackgroundColor) to the
                // matching "Light" or "Dark" sibling key so that consumers
                // using {DynamicResource BackgroundColor} react to the theme
                // change. The XAML dictionaries define BackgroundColorLight
                // and BackgroundColorDark alongside every theme-aware key.
                ApplyThemeColors(newTheme);

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
        
        private static AppTheme NormalizeTheme(AppTheme theme)
        {
            return theme == AppTheme.Unspecified ? _lastSystemTheme : theme;
        }

        /// <summary>
        /// Walks the application's merged resource dictionaries looking for
        /// theme-aware color pairs (keys ending in "Light" / "Dark") and
        /// copies the appropriate sibling into the base key. For example,
        /// when the new theme is Dark and both <c>BackgroundColorLight</c>
        /// and <c>BackgroundColorDark</c> exist, the value of
        /// <c>BackgroundColorDark</c> is copied into <c>BackgroundColor</c>.
        ///
        /// Consumers using <c>{DynamicResource BackgroundColor}</c> then
        /// receive the new value automatically.
        /// </summary>
        private static void ApplyThemeColors(AppTheme theme)
        {
            try
            {
                var appResources = Application.Current?.Resources;
                if (appResources == null) return;

                const string lightSuffix = "Light";
                const string darkSuffix = "Dark";
                var suffix = theme == AppTheme.Dark ? darkSuffix : lightSuffix;

                // Collect all string keys visible from the application scope
                // (which includes merged dictionaries). Project them to any
                // key that has a "Light" or "Dark" variant defined.
                var baseKeys = new HashSet<string>(StringComparer.Ordinal);
                CollectThemeBaseKeys(appResources, baseKeys);

                foreach (var baseKey in baseKeys)
                {
                    var sourceKey = baseKey + suffix;
                    if (TryFindResource(appResources, sourceKey, out var value) && value != null)
                    {
                        // Setting on Application.Current.Resources overlays the
                        // merged dictionary entry and propagates to any
                        // DynamicResource listeners.
                        appResources[baseKey] = value;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying theme colors: {ex.Message}");
            }
        }

        private static void CollectThemeBaseKeys(ResourceDictionary dictionary, HashSet<string> baseKeys)
        {
            if (dictionary == null) return;

            foreach (var key in dictionary.Keys)
            {
                if (key is string stringKey)
                {
                    if (stringKey.EndsWith("Light", StringComparison.Ordinal) && stringKey.Length > 5)
                    {
                        baseKeys.Add(stringKey.Substring(0, stringKey.Length - 5));
                    }
                    else if (stringKey.EndsWith("Dark", StringComparison.Ordinal) && stringKey.Length > 4)
                    {
                        baseKeys.Add(stringKey.Substring(0, stringKey.Length - 4));
                    }
                }
            }

            if (dictionary.MergedDictionaries != null)
            {
                foreach (var merged in dictionary.MergedDictionaries)
                {
                    CollectThemeBaseKeys(merged, baseKeys);
                }
            }
        }

        private static bool TryFindResource(ResourceDictionary dictionary, string key, out object? value)
        {
            if (dictionary != null)
            {
                if (dictionary.TryGetValue(key, out value)) return true;

                if (dictionary.MergedDictionaries != null)
                {
                    foreach (var merged in dictionary.MergedDictionaries)
                    {
                        if (TryFindResource(merged, key, out value)) return true;
                    }
                }
            }

            value = null;
            return false;
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
        public static T GetThemeResource<T>(string resourceKey, AppTheme? theme = null)
        {
            if (Application.Current?.Resources == null)
                return default!;
                
            // Try to get the resource
            if (Application.Current.Resources.TryGetValue(resourceKey, out var value) && value != null)
            {
                // If the resource is already of the requested type, return it
                if (value is T typedValue)
                    return typedValue;
            }
                
            return default!;
        }
        
        /// <summary>
        /// Apply platform-specific theme adaptations
        /// </summary>
        public static void ApplyPlatformSpecificAdaptations()
        {
            // Trigger a theme refresh to ensure all platform-specific styles are applied
            ApplyTheme();
        }
    }
}
