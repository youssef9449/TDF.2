using TDFMAUI.Services;
using TDFShared.Models.User;
using TDFShared.DTOs.Users;
using System.ComponentModel;

namespace TDFMAUI.Pages;

public partial class ProfilePage : ContentPage, INotifyPropertyChanged
{
    private readonly ApiService _apiService;

    public ProfilePage(ApiService apiService)
    {
        InitializeComponent();
        _apiService = apiService;
        BindingContext = this;
        LoadUserInfo();
    }

    private void LoadUserInfo()
    {
        if (App.CurrentUser != null)
        {
            var fullNameEntry = this.FindByName("fullNameEntry") as Entry;
            var departmentEntry = this.FindByName("departmentEntry") as Entry;
            var titleEntry = this.FindByName("titleEntry") as Entry;

            if (fullNameEntry != null) fullNameEntry.Text = App.CurrentUser.FullName;
            if (departmentEntry != null) departmentEntry.Text = App.CurrentUser.Department;
            if (titleEntry != null) titleEntry.Text = App.CurrentUser.Title;
        }
    }

    // Added for XAML event handler fix
    private async void OnSaveClicked(object sender, EventArgs e)
    {
        // Get editable UI elements (assuming they exist and are Entry controls)
        var fullNameEntry = this.FindByName("fullNameEntry") as Entry;
        var departmentEntry = this.FindByName("departmentEntry") as Entry;
        var titleEntry = this.FindByName("titleEntry") as Entry;

        if (App.CurrentUser == null)
        {
            await DisplayAlert("Error", "No user is currently logged in.", "OK");
            return;
        }

        // Validate input
        if (fullNameEntry == null || departmentEntry == null || titleEntry == null)
        {
            await DisplayAlert("Error", "Profile fields are missing on the page.", "OK");
            return;
        }
        if (string.IsNullOrWhiteSpace(fullNameEntry.Text) || string.IsNullOrWhiteSpace(departmentEntry.Text) || string.IsNullOrWhiteSpace(titleEntry.Text))
        {
            await DisplayAlert("Error", "All profile fields must be filled.", "OK");
            return;
        }

        // Update user model
        App.CurrentUser.FullName = fullNameEntry.Text.Trim();
        App.CurrentUser.Department = departmentEntry.Text.Trim();
        App.CurrentUser.Title = titleEntry.Text.Trim();

        try
        {
            // Map UserDto (App.CurrentUser) to UserDto for the API call
            var UserDto = new UpdateUserRequest
            {
                UserID = App.CurrentUser.UserID,
                Username = App.CurrentUser.UserName,
                FullName = App.CurrentUser.FullName,

                Department = App.CurrentUser.Department,
                Title = App.CurrentUser.Title,
                IsAdmin = App.CurrentUser.IsAdmin,
                IsActive = App.CurrentUser.IsActive,
                // Map other relevant properties if needed
            };

            var updatedUserDto = await _apiService.UpdateUserAsync(UserDto.UserID, UserDto);
            
            // Update App.CurrentUser with potentially changed data (map back from UserDto)
            // Assuming API returns the updated user as UserDto
            if (updatedUserDto != null)
            {
                App.CurrentUser.FullName = updatedUserDto.FullName;
                App.CurrentUser.Department = updatedUserDto.Department;
                App.CurrentUser.Title = updatedUserDto.Title;

                App.CurrentUser.IsAdmin = updatedUserDto.IsAdmin;
                App.CurrentUser.IsActive = updatedUserDto.IsActive;
                // Update other fields if necessary
            }
            
            await DisplayAlert("Success", "Profile changes saved successfully.", "OK");
            LoadUserInfo(); // Reload UI with potentially updated App.CurrentUser
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save profile changes: {ex.Message}", "OK");
        }
    }

    private async void OnChangePasswordClicked(object sender, EventArgs e)
    {
        // Get UI elements and check if they exist
        var currentPasswordEntry = this.FindByName("currentPasswordEntry") as Entry;
        var newPasswordEntry = this.FindByName("newPasswordEntry") as Entry;
        var confirmPasswordEntry = this.FindByName("confirmPasswordEntry") as Entry;
        var loadingIndicator = this.FindByName("loadingIndicator") as ActivityIndicator;

        // Validate that required elements exist
        if (currentPasswordEntry == null || newPasswordEntry == null || confirmPasswordEntry == null)
        {
            await DisplayAlert("Error", "Password entry fields are not available", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(currentPasswordEntry.Text) ||
            string.IsNullOrWhiteSpace(newPasswordEntry.Text) ||
            string.IsNullOrWhiteSpace(confirmPasswordEntry.Text))
        {
            await DisplayAlert("Error", "Please fill in all password fields", "OK");
            return;
        }

        if (newPasswordEntry.Text != confirmPasswordEntry.Text)
        {
            await DisplayAlert("Error", "New passwords do not match", "OK");
            return;
        }

        // Show loading indicator if it exists
        if (loadingIndicator != null)
        {
            loadingIndicator.IsVisible = true;
            loadingIndicator.IsRunning = true;
        }

        try
        {
            var changePasswordDto = new ChangePasswordDto 
            {
                CurrentPassword = currentPasswordEntry.Text,
                NewPassword = newPasswordEntry.Text
            };
            
            var success = await _apiService.ChangePasswordAsync(changePasswordDto);

            if (success)
            {
                await DisplayAlert("Success", "Password changed successfully", "OK");
                currentPasswordEntry.Text = string.Empty;
                newPasswordEntry.Text = string.Empty;
                confirmPasswordEntry.Text = string.Empty;
            }
            else
            {
                await DisplayAlert("Error", "Current password is incorrect", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to change password: {ex.Message}", "OK");
        }
        finally
        {
            // Hide loading indicator if it exists
            if (loadingIndicator != null)
            {
                loadingIndicator.IsVisible = false;
                loadingIndicator.IsRunning = false;
            }
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
} 