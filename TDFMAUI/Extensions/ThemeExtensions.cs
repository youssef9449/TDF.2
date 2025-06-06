using Microsoft.Maui.Controls;
using System;
using TDFMAUI.Helpers;

namespace TDFMAUI.Extensions
{
    /// <summary>
    /// Extension methods for theme-related functionality
    /// </summary>
    public static class ThemeExtensions
    {
        /// <summary>
        /// Get a theme-specific resource value
        /// </summary>
        public static T GetThemeResource<T>(this VisualElement element, string resourceKey)
        {
            return ThemeHelper.GetThemeResource<T>(resourceKey, ThemeHelper.CurrentTheme);
        }
        
        /// <summary>
        /// Get a resource that adapts to the current theme
        /// </summary>
        public static T GetAdaptiveResource<T>(this VisualElement element, string lightResourceKey, string darkResourceKey)
        {
            return ThemeHelper.GetAdaptiveResource<T>(lightResourceKey, darkResourceKey);
        }
        
        /// <summary>
        /// Apply platform-specific styles to an element
        /// </summary>
        public static void ApplyPlatformStyles(this VisualElement element)
        {
            // Apply platform-specific styles based on the current platform
            if (DeviceHelper.IsWindows)
            {
                ApplyWindowsStyles(element);
            }
            else if (DeviceHelper.IsMacOS)
            {
                ApplyMacOSStyles(element);
            }
            else if (DeviceHelper.IsIOS)
            {
                ApplyIOSStyles(element);
            }
            else if (DeviceHelper.IsAndroid)
            {
                ApplyAndroidStyles(element);
            }
        }
        
        // Platform-specific style application
        private static void ApplyWindowsStyles(VisualElement element)
        {
            if (element is Button button)
            {
                button.CornerRadius = 4;
                button.Padding = new Thickness(12, 8);
            }
            else if (element is Entry entry)
            {
                entry.HeightRequest = 32;
            }
            else if (element is Frame frame)
            {
                frame.CornerRadius = 4;
                frame.BorderColor = ThemeHelper.GetThemeResource<Color>("WindowsControlBorderColor", ThemeHelper.CurrentTheme);
            }
        }
        
        private static void ApplyMacOSStyles(VisualElement element)
        {
            if (element is Button button)
            {
                button.CornerRadius = 6;
                button.Padding = new Thickness(12, 6);
            }
            else if (element is Entry entry)
            {
                entry.HeightRequest = 30;
            }
            else if (element is Frame frame)
            {
                frame.CornerRadius = 6;
                frame.BorderColor = ThemeHelper.GetThemeResource<Color>("MacOSControlBorderColor", ThemeHelper.CurrentTheme);
            }
        }
        
        private static void ApplyIOSStyles(VisualElement element)
        {
            if (element is Button button)
            {
                button.CornerRadius = 10;
                button.Padding = new Thickness(16, 12);
            }
            else if (element is Entry entry)
            {
                entry.HeightRequest = 36;
            }
            else if (element is Frame frame)
            {
                frame.CornerRadius = 10;
                frame.HasShadow = true;
                frame.BorderColor = Colors.Transparent;
            }
        }
        
        private static void ApplyAndroidStyles(VisualElement element)
        {
            if (element is Button button)
            {
                button.CornerRadius = 8;
                button.Padding = new Thickness(16, 12);
            }
            else if (element is Entry entry)
            {
                entry.HeightRequest = 48;
            }
            else if (element is Frame frame)
            {
                frame.CornerRadius = 8;
                frame.HasShadow = true;
                frame.BorderColor = Colors.Transparent;
            }
        }
    }
}