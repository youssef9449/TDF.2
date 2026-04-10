using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Graphics;
using System;
using System.Threading.Tasks;
using TDFMAUI.Helpers;

namespace TDFMAUI.Services
{
    /// <summary>
    /// Service for managing application themes across all platforms
    /// </summary>
    public class ThemeService
    {
        private bool _isInitialized = false;
        
        /// <summary>
        /// Initialize the theme service
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;
            
            try
            {
                // ThemeHelper initialization is deferred until app resources and window are ready.
                // Subscribe to theme changes
                ThemeHelper.ThemeChanged += OnThemeChanged;
                
                // Subscribe to display changes
                DeviceHelper.DisplayInfoChanged += OnDisplayInfoChanged;
                
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing theme service: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Handle theme changes
        /// </summary>
        private void OnThemeChanged(object sender, AppTheme newTheme)
        {
            try
            {
                // Apply platform-specific adaptations
                ThemeHelper.ApplyPlatformSpecificAdaptations();
                
                // Update status bar colors based on theme
                UpdateStatusBarColors(newTheme);
                
                // Log theme change
                System.Diagnostics.Debug.WriteLine($"Theme changed to: {newTheme}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling theme change: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Handle display info changes
        /// </summary>
        private void OnDisplayInfoChanged(object sender, DisplayInfoChangedEventArgs e)
        {
            try
            {
                // Update UI for new display metrics
                // This might involve adjusting layouts, font sizes, etc.
                
                // Log display change
                System.Diagnostics.Debug.WriteLine($"Display changed: {e.DisplayInfo.Width}x{e.DisplayInfo.Height}, Density: {e.DisplayInfo.Density}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling display change: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Update status bar colors based on theme
        /// </summary>
        private void UpdateStatusBarColors(AppTheme theme)
        {
            try
            {
                // Use CommunityToolkit.Maui to handle status bar changes cross-platform
                // This requires adding StatusBarBehavior to pages, or we can use the
                // CommunityToolkit.Maui.Core platform methods if available.

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        var statusBarColor = ThemeHelper.GetThemeResource<Color>("AndroidStatusBarColor", theme);

                        if (statusBarColor == null)
                        {
                            statusBarColor = theme == AppTheme.Dark ? Color.FromArgb("#121212") : Color.FromArgb("#F5F5F5");
                        }

                        var statusBarStyle = theme == AppTheme.Dark
                            ? CommunityToolkit.Maui.Core.StatusBarStyle.LightContent
                            : CommunityToolkit.Maui.Core.StatusBarStyle.DarkContent;

#if ANDROID
                        CommunityToolkit.Maui.Core.Platform.StatusBar.SetColor(statusBarColor);
                        CommunityToolkit.Maui.Core.Platform.StatusBar.SetStyle(statusBarStyle);
#elif IOS
                        CommunityToolkit.Maui.Core.Platform.StatusBar.SetStyle(statusBarStyle);
#endif
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error applying status bar color: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating status bar colors: {ex.Message}");
            }
        }
        
        public void UseDarkTheme()
        {
            ThemeHelper.FollowSystemTheme = false;
            ThemeHelper.UserTheme = AppTheme.Dark;
            ThemeHelper.ApplyTheme();
        }

        public void UseSystemTheme()
        {
            ThemeHelper.FollowSystemTheme = true;
            ThemeHelper.ApplyTheme();
        }

        public void UseLightTheme()
        {
            ThemeHelper.FollowSystemTheme = false;
            ThemeHelper.UserTheme = AppTheme.Light;
            ThemeHelper.ApplyTheme();
        }

        public void ToggleTheme()
        {
            if (ThemeHelper.FollowSystemTheme)
            {
                ThemeHelper.FollowSystemTheme = false;
                ThemeHelper.UserTheme = ThemeHelper.SystemTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
            }
            else
            {
                ThemeHelper.UserTheme = ThemeHelper.UserTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
            }
            ThemeHelper.ApplyTheme();
        }
    }
}