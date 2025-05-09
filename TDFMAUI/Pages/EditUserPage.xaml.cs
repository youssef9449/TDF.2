using System;
using TDFMAUI.Services;
using TDFShared.DTOs.Users;

namespace TDFMAUI.Pages
{
    public partial class EditUserPage : ContentPage
    {
        private readonly ApiService _apiService;
        private UpdateUserRequest _user;

        public List<string> Departments { get; set; } = new List<string>
        {
            "IT",
            "HR",
            "Finance",
            "Marketing",
            "Operations",
            "Sales"
        };

        public List<string> Titles { get; set; } = new List<string>
        {
            "Manager",
            "Supervisor",
            "Team Lead",
            "Senior",
            "Junior",
            "Intern"
        };

        public EditUserPage(UpdateUserRequest user, ApiService apiService)
        {
            InitializeComponent();
            _apiService = apiService;
            _user = user;
            BindingContext = this;
            LoadUserData();
        }

        private void LoadUserData()
        {
            usernameEntry.Text = _user.Username;
            fullNameEntry.Text = _user.FullName;
            departmentPicker.SelectedItem = _user.Department;
            titlePicker.SelectedItem = _user.Title;
            adminCheckBox.IsChecked = _user.IsAdmin;
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(usernameEntry.Text))
            {
                await DisplayAlert("Error", "Please enter a username", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(fullNameEntry.Text))
            {
                await DisplayAlert("Error", "Please enter a full name", "OK");
                return;
            }

            if (departmentPicker.SelectedItem == null)
            {
                await DisplayAlert("Error", "Please select a department", "OK");
                return;
            }

            if (titlePicker.SelectedItem == null)
            {
                await DisplayAlert("Error", "Please select a title", "OK");
                return;
            }

            loadingIndicator.IsVisible = true;
            loadingIndicator.IsRunning = true;

            try
            {
                _user.Username = usernameEntry.Text;
                _user.FullName = fullNameEntry.Text;
                _user.Department = departmentPicker.SelectedItem.ToString();
                _user.Title = titlePicker.SelectedItem.ToString();
                _user.IsAdmin = adminCheckBox.IsChecked;

                await _apiService.UpdateUserAsync(_user.UserID, _user);
                await DisplayAlert("Success", "User updated successfully", "OK");
                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to update user: {ex.Message}", "OK");
            }
            finally
            {
                loadingIndicator.IsVisible = false;
                loadingIndicator.IsRunning = false;
            }
        }

        private async void OnDeleteClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Confirm Delete", 
                "Are you sure you want to delete this user? This action cannot be undone.", 
                "Delete", "Cancel");

            if (confirm)
            {
                loadingIndicator.IsVisible = true;
                loadingIndicator.IsRunning = true;

                try
                {
                    await _apiService.DeleteUserAsync(_user.UserID);
                    await DisplayAlert("Success", "User deleted successfully", "OK");
                    await Navigation.PopAsync();
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Failed to delete user: {ex.Message}", "OK");
                }
                finally
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
} 