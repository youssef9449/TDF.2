using Microsoft.Maui.Controls;
using TDFMAUI.Helpers;
using TDFMAUI.Extensions;

namespace TDFMAUI.Controls
{
    public partial class ThemedCard : ContentView
    {
        // Bindable properties
        public static readonly BindableProperty TitleProperty = 
            BindableProperty.Create(nameof(Title), typeof(string), typeof(ThemedCard), string.Empty);
            
        public static readonly BindableProperty ContentProperty = 
            BindableProperty.Create(nameof(Content), typeof(View), typeof(ThemedCard), null);
            
        public static readonly BindableProperty ActionTextProperty = 
            BindableProperty.Create(nameof(ActionText), typeof(string), typeof(ThemedCard), string.Empty, 
                propertyChanged: OnActionTextChanged);
                
        public static readonly BindableProperty ActionCommandProperty = 
            BindableProperty.Create(nameof(ActionCommand), typeof(System.Windows.Input.ICommand), typeof(ThemedCard), null);
            
        public static readonly BindableProperty UsePlatformStylesProperty = 
            BindableProperty.Create(nameof(UsePlatformStyles), typeof(bool), typeof(ThemedCard), true, 
                propertyChanged: OnUsePlatformStylesChanged);
        
        // Properties
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }
        
        public View Content
        {
            get => (View)GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }
        
        public string ActionText
        {
            get => (string)GetValue(ActionTextProperty);
            set => SetValue(ActionTextProperty, value);
        }
        
        public System.Windows.Input.ICommand ActionCommand
        {
            get => (System.Windows.Input.ICommand)GetValue(ActionCommandProperty);
            set => SetValue(ActionCommandProperty, value);
        }
        
        public bool UsePlatformStyles
        {
            get => (bool)GetValue(UsePlatformStylesProperty);
            set => SetValue(UsePlatformStylesProperty, value);
        }
        
        public ThemedCard()
        {
            InitializeComponent();
            
            // Set the binding context to this control
            BindingContext = this;
            
            // Apply platform-specific styles if enabled
            if (UsePlatformStyles)
            {
                ApplyPlatformStyles();
            }
            
            // Subscribe to theme changes
            ThemeHelper.ThemeChanged += OnThemeChanged;
            
            // Set up action button
            ActionButton.Clicked += OnActionButtonClicked;
        }
        
        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();
            
            // Apply platform-specific styles when the handler changes
            if (UsePlatformStyles)
            {
                ApplyPlatformStyles();
            }
        }
        
        private void OnThemeChanged(object sender, AppTheme e)
        {
            // Re-apply platform-specific styles when the theme changes
            if (UsePlatformStyles)
            {
                ApplyPlatformStyles();
            }
        }
        
        private void ApplyPlatformStyles()
        {
            // Apply platform-specific styles to the card
            if (DeviceHelper.IsWindows)
            {
                CardBorder.Style = (Style)Resources["WindowsCardStyle"];
            }
            else if (DeviceHelper.IsMacOS)
            {
                CardBorder.Style = (Style)Resources["MacOSCardStyle"];
            }
            else if (DeviceHelper.IsIOS)
            {
                CardBorder.Style = (Style)Resources["iOSCardStyle"];
            }
            else if (DeviceHelper.IsAndroid)
            {
                CardBorder.Style = (Style)Resources["AndroidCardStyle"];
            }
            
            // Apply platform-specific styles to the action button
            // Note: Removed recursive call to ApplyPlatformStyles() that was causing stack overflow
            ApplyPlatformButtonStyles();
        }
        
        private void ApplyPlatformButtonStyles()
        {
            // Apply platform-specific styles to the action button if needed
            // This can be implemented later if specific button styling is required
        }
        
        private static void OnActionTextChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var card = (ThemedCard)bindable;
            var actionText = (string)newValue;
            
            // Update the action button
            card.ActionButton.Text = actionText;
            card.ActionButton.IsVisible = !string.IsNullOrEmpty(actionText);
        }
        
        private static void OnUsePlatformStylesChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var card = (ThemedCard)bindable;
            var usePlatformStyles = (bool)newValue;
            
            if (usePlatformStyles)
            {
                card.ApplyPlatformStyles();
            }
            else
            {
                // Reset to default style
                card.CardBorder.Style = (Style)card.Resources["CardBorderStyle"];
            }
        }
        
        private void OnActionButtonClicked(object sender, System.EventArgs e)
        {
            if (ActionCommand?.CanExecute(null) == true)
            {
                ActionCommand.Execute(null);
            }
        }
    }
}