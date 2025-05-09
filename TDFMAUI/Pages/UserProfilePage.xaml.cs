using System;
using Microsoft.Maui.Controls;
using TDFMAUI.Helpers;
using TDFMAUI.ViewModels;
using TDFMAUI.Services;

namespace TDFMAUI.Pages
{
    public partial class UserProfilePage : ContentPage
    {
        private readonly UserProfileViewModel _viewModel;
        private readonly ILocalStorageService _localStorageService;

        public UserProfilePage(UserProfileViewModel viewModel, ILocalStorageService localStorageService)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _localStorageService = localStorageService;
            BindingContext = _viewModel;

            // Apply platform-specific UI customizations
            ApplyPlatformSpecificUI();

            Loaded += OnPageLoaded;
        }

        private async void OnPageLoaded(object sender, EventArgs e)
        {
            if (!_viewModel.IsDataLoaded)
            {
                await _viewModel.LoadUserProfileCommand.ExecuteAsync(null);
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadUserProfileCommand.ExecuteAsync(null);
        }

        private void ApplyPlatformSpecificUI()
        {
            if (DeviceHelper.IsDesktop)
            {
                // Apply desktop-specific UI adjustments
                ApplyDesktopLayout();
            }
            else if (DeviceHelper.IsMobile)
            {
                // Apply mobile-specific UI adjustments
                ApplyMobileLayout();
            }

            // Platform-specific styling
            if (DeviceHelper.IsWindows)
            {
                // Windows-specific styling
                Resources["PrimaryBackgroundColor"] = new Color(0xF3, 0xF3, 0xF3);
                Resources["CardBackgroundColor"] = Colors.White;
            }
            else if (DeviceHelper.IsMacOS)
            {
                // macOS-specific styling
                Resources["PrimaryBackgroundColor"] = new Color(0xF5, 0xF5, 0xF5);
                Resources["CardBackgroundColor"] = Colors.White;
            }
            else if (DeviceHelper.IsAndroid)
            {
                // Android-specific styling
                Resources["PrimaryBackgroundColor"] = new Color(0xF2, 0xF2, 0xF2);
                Resources["CardBackgroundColor"] = Colors.White;
            }
            else if (DeviceHelper.IsIOS)
            {
                // iOS-specific styling
                Resources["PrimaryBackgroundColor"] = new Color(0xF8, 0xF8, 0xF8);
                Resources["CardBackgroundColor"] = Colors.White;
            }
        }

        private void ApplyDesktopLayout()
        {
            // For desktop platforms, use a two-column layout
            MainGrid.ColumnDefinitions.Clear();
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.4, GridUnitType.Star) });
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.6, GridUnitType.Star) });

            // Move profile image to left column
            if (ProfileImageContainer != null)
            {
                Grid.SetColumn(ProfileImageContainer, 0);
                Grid.SetRow(ProfileImageContainer, 0);
                Grid.SetRowSpan(ProfileImageContainer, 2);
            }

            // Move profile details to right column
            if (ProfileDetailsContainer != null)
            {
                Grid.SetColumn(ProfileDetailsContainer, 1);
                Grid.SetRow(ProfileDetailsContainer, 0);
            }

            // Move buttons to right column
            if (ActionButtonsContainer != null)
            {
                Grid.SetColumn(ActionButtonsContainer, 1);
                Grid.SetRow(ActionButtonsContainer, 1);
            }

            // Adjust spacing and margins for desktop view
            MainGrid.Margin = new Thickness(20);
            MainGrid.RowSpacing = 15;
            MainGrid.ColumnSpacing = 20;
        }

        private void ApplyMobileLayout()
        {
            // For mobile platforms, use a single-column layout with rows
            MainGrid.ColumnDefinitions.Clear();
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

            // Stack vertically
            Grid.SetColumn(ProfileImageContainer, 0);
            Grid.SetRow(ProfileImageContainer, 0);
            Grid.SetRowSpan(ProfileImageContainer, 1);

            Grid.SetColumn(ProfileDetailsContainer, 0);
            Grid.SetRow(ProfileDetailsContainer, 1);

            Grid.SetColumn(ActionButtonsContainer, 0);
            Grid.SetRow(ActionButtonsContainer, 2);

            // Adjust spacing and margins for mobile view
            MainGrid.Margin = new Thickness(10);
            MainGrid.RowSpacing = 10;
        }

        private VerticalStackLayout CreateUserDetailsForm(bool isMobile = false)
        {
            var form = new VerticalStackLayout
            {
                Spacing = isMobile ? 15 : 20
            };

            // Full Name
            var nameLabel = new Label { Text = "Full Name:", FontAttributes = FontAttributes.Bold };
            var nameEntry = new Entry { Placeholder = "Enter full name" };
            nameEntry.SetBinding(Entry.TextProperty, "CurrentUser.FullName");
            nameEntry.SetBinding(IsEnabledProperty, nameof(_viewModel.IsEditing));


            // Department
            var deptLabel = new Label { Text = "Department:", FontAttributes = FontAttributes.Bold };
            var deptEntry = new Entry { Placeholder = "Enter department" };
            deptEntry.SetBinding(Entry.TextProperty, "CurrentUser.Department");
            deptEntry.SetBinding(IsEnabledProperty, nameof(_viewModel.IsEditing));

            // Title
            var titleLabel = new Label { Text = "Title:", FontAttributes = FontAttributes.Bold };
            var titleEntry = new Entry { Placeholder = "Enter job title" };
            titleEntry.SetBinding(Entry.TextProperty, "CurrentUser.Title");
            titleEntry.SetBinding(IsEnabledProperty, nameof(_viewModel.IsEditing));

            // Add all fields to the form
            form.Add(nameLabel);
            form.Add(nameEntry);

            form.Add(deptLabel);
            form.Add(deptEntry);
            form.Add(titleLabel);
            form.Add(titleEntry);

            return form;
        }

        private HorizontalStackLayout CreateActionButtons()
        {
            var buttonsLayout = new HorizontalStackLayout
            {
                HorizontalOptions = LayoutOptions.Center,
                Spacing = 20,
                Margin = new Thickness(0, 20, 0, 0)
            };

            // Edit Button
            var editButton = new Button
            {
                Text = "Edit Profile",
                WidthRequest = 120
            };
            editButton.SetBinding(Button.CommandProperty, nameof(_viewModel.EditProfileCommand));
            editButton.SetBinding(IsVisibleProperty, nameof(_viewModel.IsEditing), 
                converter: new Converters.BooleanInverter());

            // Save Button
            var saveButton = new Button
            {
                Text = "Save",
                WidthRequest = 120,
                BackgroundColor = Colors.Green
            };
            saveButton.SetBinding(Button.CommandProperty, nameof(_viewModel.SaveProfileCommand));
            saveButton.SetBinding(IsVisibleProperty, nameof(_viewModel.IsEditing));
            saveButton.SetBinding(Button.IsEnabledProperty, nameof(_viewModel.IsSaving), 
                converter: new Converters.BooleanInverter());

            // Cancel Button
            var cancelButton = new Button
            {
                Text = "Cancel",
                WidthRequest = 120,
                BackgroundColor = Colors.Red
            };
            cancelButton.SetBinding(Button.CommandProperty, nameof(_viewModel.CancelEditCommand));
            cancelButton.SetBinding(IsVisibleProperty, nameof(_viewModel.IsEditing));
            cancelButton.SetBinding(Button.IsEnabledProperty, nameof(_viewModel.IsSaving), 
                converter: new Converters.BooleanInverter());

            buttonsLayout.Add(editButton);
            buttonsLayout.Add(saveButton);
            buttonsLayout.Add(cancelButton);

            return buttonsLayout;
        }
    }
} 