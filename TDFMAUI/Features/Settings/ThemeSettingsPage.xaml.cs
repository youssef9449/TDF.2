using System;
using TDFMAUI.Helpers;
using Microsoft.Maui.Controls;

namespace TDFMAUI.Features.Settings
{
    public partial class ThemeSettingsPage : ContentPage
    {
        // Properties for binding
        public bool FollowSystemTheme => ThemeHelper.FollowSystemTheme;
        public bool IsLightTheme => !ThemeHelper.FollowSystemTheme && ThemeHelper.UserTheme == AppTheme.Light;
        public bool IsDarkTheme => !ThemeHelper.FollowSystemTheme && ThemeHelper.UserTheme == AppTheme.Dark;
        
        public string CurrentThemeText => ThemeHelper.CurrentTheme.ToString();
        public string SystemThemeText => Application.Current.PlatformAppTheme.ToString();
        public string FollowingSystemText => ThemeHelper.FollowSystemTheme ? "Yes" : "No";
        
        public Command ToggleThemeCommand { get; }

        public ThemeSettingsPage()
        {
            InitializeComponent();
            
            // Set the binding context to this page
            BindingContext = this;
            
            // Initialize the toggle theme command
            ToggleThemeCommand = new Command(OnToggleTheme);
            
            // Subscribe to theme changes to update the UI
            ThemeHelper.ThemeChanged += OnThemeChanged;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            // Unsubscribe from theme changes when the page disappears
            ThemeHelper.ThemeChanged -= OnThemeChanged;
        }
        
        private void OnThemeChanged(object sender, AppTheme e)
        {
            // Refresh the bindings when the theme changes
            OnPropertyChanged(nameof(FollowSystemTheme));
            OnPropertyChanged(nameof(IsLightTheme));
            OnPropertyChanged(nameof(IsDarkTheme));
            OnPropertyChanged(nameof(CurrentThemeText));
            OnPropertyChanged(nameof(FollowingSystemText));
        }
        
        private void OnToggleTheme()
        {
            ThemeHelper.ToggleTheme();
        }
        
        private void OnThemeRadioButtonCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (sender is RadioButton radioButton && e.Value)
            {
                if (radioButton.GroupName == "ThemeGroup")
                {
                    if (radioButton.IsChecked && radioButton == systemRadioButton)
                    {
                        ThemeHelper.UseSystemTheme();
                    }
                    else if (radioButton.IsChecked && radioButton == lightRadioButton)
                    {
                        ThemeHelper.UseLightTheme();
                    }
                    else if (radioButton.IsChecked && radioButton == darkRadioButton)
                    {
                        ThemeHelper.UseDarkTheme();
                    }
                    RefreshBindings();
                }
            }
        }
        
        private void OnSystemThemeClicked(object sender, EventArgs e)
        {
            ThemeHelper.UseSystemTheme();
            RefreshBindings();
        }
        
        private void OnLightThemeClicked(object sender, EventArgs e)
        {
            ThemeHelper.UseLightTheme();
            RefreshBindings();
        }
        
        private void OnDarkThemeClicked(object sender, EventArgs e)
        {
            ThemeHelper.UseDarkTheme();
            RefreshBindings();
        }
        
        private void RefreshBindings()
        {
            OnPropertyChanged(nameof(FollowSystemTheme));
            OnPropertyChanged(nameof(IsLightTheme));
            OnPropertyChanged(nameof(IsDarkTheme));
            OnPropertyChanged(nameof(CurrentThemeText));
            OnPropertyChanged(nameof(FollowingSystemText));
        }
    }
}