using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using System;

namespace TDFMAUI.Helpers
{
    /// <summary>
    /// Helper class for managing application themes
    /// </summary>
    public static class ThemeHelper
    {
        // Event for theme changes
        private static event EventHandler<AppTheme> _themeChanged;
 private static readonly object _eventLock = new object();
 
 public static event EventHandler<AppTheme> ThemeChanged
 {
     add { lock (_eventLock) { _themeChanged += value; } }
     remove { lock (_eventLock) { _themeChanged -= value; } }
 }

        /// <summary>
        /// Gets the current app theme
        /// </summary>
        public static AppTheme CurrentTheme => Application.Current.RequestedTheme;

        /// <summary>
        /// Gets whether the app is currently using dark theme
        /// </summary>
        public static bool IsDarkTheme => CurrentTheme == AppTheme.Dark;

        /// <summary>
        /// Gets whether the app is currently using light theme
        /// </summary>
        public static bool IsLightTheme => CurrentTheme == AppTheme.Light;

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
        /// Initialize theme settings and apply the appropriate theme
        /// </summary>
 private static EventHandler<AppThemeChangedEventArgs> _systemThemeHandler;
 
  public static void Initialize()
  {
     // Unsubscribe existing handler if any
     if (_systemThemeHandler != null)
         Application.Current.RequestedThemeChanged -= _systemThemeHandler;
     
     // Register for system theme changes
     _systemThemeHandler = (s, e) =>
      {
          if (FollowSystemTheme)
          {
              // If following system theme, update when system changes
              ApplyTheme();
          }
     };
 if (Application.Current == null)
 {
     System.Diagnostics.Debug.WriteLine("Application.Current is null, cannot initialize theme helper");
     return;
 }
 
  // Register for system theme changes
  Application.Current.RequestedThemeChanged += (s, e) =>
            // Apply the initial theme
            ApplyTheme();
        }

        /// <summary>
        /// Apply the appropriate theme based on settings
        /// </summary>
public static void ApplyTheme()
  {
     try
     {
         if (Application.Current == null) return;
         
          var newTheme = FollowSystemTheme 
              ? Application.Current.PlatformAppTheme 
              : UserTheme;

          Application.Current.UserAppTheme = newTheme;
          
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
        /// Toggle between light and dark themes
        /// </summary>
        public static void ToggleTheme()
        {
            // If following system, switch to manual mode
            if (FollowSystemTheme)
            {
                FollowSystemTheme = false;
                UserTheme = Application.Current.PlatformAppTheme == AppTheme.Dark 
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
    }
}