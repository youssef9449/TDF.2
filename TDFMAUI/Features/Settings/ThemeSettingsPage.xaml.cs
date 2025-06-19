using System;
using TDFMAUI.Helpers;
using TDFMAUI.Services;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Devices;
using Microsoft.Maui.ApplicationModel;

namespace TDFMAUI.Features.Settings
{
    public partial class ThemeSettingsPage : ContentPage
    {
        private readonly ThemeService _themeService;
        
        // Properties for binding
        public bool FollowSystemTheme => ThemeHelper.FollowSystemTheme;
        public bool IsLightTheme => !ThemeHelper.FollowSystemTheme && ThemeHelper.UserTheme == AppTheme.Light;
        public bool IsDarkTheme => !ThemeHelper.FollowSystemTheme && ThemeHelper.UserTheme == AppTheme.Dark;
        public bool UsePlatformSpecificThemes => ThemeHelper.UsePlatformSpecificThemes;
        
        public string CurrentThemeText => ThemeHelper.CurrentTheme.ToString();
        public string SystemThemeText => ThemeHelper.SystemTheme.ToString();
        public string FollowingSystemText => ThemeHelper.FollowSystemTheme ? "Yes" : "No";
        
        // Device information for display
        public string PlatformText => DeviceHelper.CurrentPlatform.ToString();
        public string VersionText => DeviceHelper.PlatformVersion;
        public string DeviceTypeText => DeviceHelper.DeviceIdiom.ToString();
        public string ScreenSizeText => $"{DeviceHelper.ScreenWidth:F0} x {DeviceHelper.ScreenHeight:F0} ({DeviceHelper.ScreenDensity:F1}x)";
        public string OrientationText => DeviceHelper.ScreenOrientation.ToString();
        
        public Command ToggleThemeCommand { get; }
        public Command ApplyPlatformAdaptationsCommand { get; }

        public ThemeSettingsPage()
        {
            InitializeComponent();
            
            // Get the theme service
            _themeService = Application.Current?.Handler?.MauiContext?.Services.GetService<ThemeService>();
            
            // Set the binding context to this page
            BindingContext = this;
            
            // Initialize commands
            ToggleThemeCommand = new Command(OnToggleTheme);
            ApplyPlatformAdaptationsCommand = new Command(OnApplyPlatformAdaptations);
            
            // Subscribe to theme changes to update the UI
            ThemeHelper.ThemeChanged += OnThemeChanged;
            
            // Subscribe to display changes
            DeviceHelper.DisplayInfoChanged += OnDisplayInfoChanged;
            
            // Update platform information
            UpdatePlatformInfo();
            
            // Set initial checkbox state
            PlatformAdaptationsCheckbox.IsChecked = UsePlatformSpecificThemes;
            
            // Set initial radio button states
            SystemThemeRadio.IsChecked = FollowSystemTheme;
            LightThemeRadio.IsChecked = IsLightTheme;
            DarkThemeRadio.IsChecked = IsDarkTheme;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            
            // Refresh bindings when the page appears
            RefreshBindings();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            // Unsubscribe from events when the page disappears
            ThemeHelper.ThemeChanged -= OnThemeChanged;
            DeviceHelper.DisplayInfoChanged -= OnDisplayInfoChanged;
        }
        
        private void OnThemeChanged(object sender, AppTheme e)
        {
            // Refresh the bindings when the theme changes
            RefreshBindings();
        }
        
        private void OnDisplayInfoChanged(object sender, DisplayInfoChangedEventArgs e)
        {
            // Update orientation and screen size information
            OnPropertyChanged(nameof(ScreenSizeText));
            OnPropertyChanged(nameof(OrientationText));
        }
        
        private void OnToggleTheme()
        {
            _themeService?.ToggleTheme();
        }
        

        
        private void OnThemeRadioCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (!e.Value) return; // Only handle when a radio button is checked
            
            if (sender == SystemThemeRadio)
            {
                _themeService?.UseSystemTheme();
            }
            else if (sender == LightThemeRadio)
            {
                _themeService?.UseLightTheme();
            }
            else if (sender == DarkThemeRadio)
            {
                _themeService?.UseDarkTheme();
            }
            
            RefreshBindings();
        }
        
        private void OnPlatformAdaptationsCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            ThemeHelper.UsePlatformSpecificThemes = e.Value;
            OnApplyPlatformAdaptations();
        }
        
        private void OnApplyPlatformAdaptations()
        {
            ThemeHelper.ApplyPlatformSpecificAdaptations();
            RefreshBindings();
        }
        
        private void UpdatePlatformInfo()
        {
            // Update platform information labels
            PlatformLabel.Text = PlatformText;
            VersionLabel.Text = VersionText;
            DeviceTypeLabel.Text = DeviceTypeText;
            SystemThemeLabel.Text = SystemThemeText;
            CurrentThemeLabel.Text = CurrentThemeText;
        }
        
        private void RefreshBindings()
        {
            // Update all bindable properties
            OnPropertyChanged(nameof(FollowSystemTheme));
            OnPropertyChanged(nameof(IsLightTheme));
            OnPropertyChanged(nameof(IsDarkTheme));
            OnPropertyChanged(nameof(UsePlatformSpecificThemes));
            OnPropertyChanged(nameof(CurrentThemeText));
            OnPropertyChanged(nameof(SystemThemeText));
            OnPropertyChanged(nameof(FollowingSystemText));
            
            // Update platform information
            UpdatePlatformInfo();
        }
    }
}