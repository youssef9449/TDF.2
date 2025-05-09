using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using TDFShared.Models.User;
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Common;
using TDFMAUI.Services;
using TDFMAUI.ViewModels;
using TDFShared.DTOs.Users;
using TDFMAUI.Helpers;

namespace TDFMAUI.Pages;

public partial class AddUserPage : ContentPage
{
    private readonly ApiService _apiService;
    private readonly LookupService _lookupService;

    public ObservableCollection<LookupItem> Departments { get; set; }
    public ObservableCollection<string> Titles { get; set; }
    public string SelectedDepartment { get; set; }

    public AddUserPage()
    {
        InitializeComponent();

        // Get services from DI
        _apiService = App.Services.GetService<ApiService>();
        _lookupService = App.Services.GetService<LookupService>();

        // Initialize collections
        Departments = new ObservableCollection<LookupItem>();
        Titles = new ObservableCollection<string>();

        BindingContext = this;

        // Load departments from lookup service
        LoadDepartmentsAsync();
    }

    public AddUserPage(ApiService apiService)
    {
        InitializeComponent();
        _apiService = apiService;
        _lookupService = App.Services.GetService<LookupService>();

        // Initialize collections
        Departments = new ObservableCollection<LookupItem>();
        Titles = new ObservableCollection<string>();

        BindingContext = this;

        // Load departments from lookup service
        LoadDepartmentsAsync();
    }

    private async void LoadDepartmentsAsync()
    {
        var departments = await _lookupService.GetDepartmentsAsync();
        if (departments != null && departments.Any())
        {
            Departments.Clear();
            foreach (var department in departments)
            {
                Departments.Add(department);
            }
        }
    }

    protected async void OnDepartmentSelectedIndexChanged(object sender, EventArgs e)
    {
        if (departmentPicker.SelectedItem == null)
            return;

        var selectedDepartment = (LookupItem)departmentPicker.SelectedItem;
        string departmentValue = selectedDepartment.Id;

        // Load titles for the selected department
        var titles = await _lookupService.GetTitlesForDepartmentAsync(departmentValue);

        Titles.Clear();
        if (titles != null && titles.Any())
        {
            foreach (var title in titles)
            {
                Titles.Add(title);
            }

            // Select first title
            if (Titles.Count > 0 && titlePicker != null)
            {
                titlePicker.SelectedIndex = 0;
            }
        }
    }

    private bool IsPasswordStrong(string password, out string validationMessage)
    {
        validationMessage = string.Empty;
        if (string.IsNullOrEmpty(password) || password.Length < 8)
        {
            validationMessage = "Password must be at least 8 characters.";
            return false;
        }
        if (!password.Any(char.IsUpper)) { validationMessage = "Password needs an uppercase letter."; return false; }
        if (!password.Any(char.IsLower)) { validationMessage = "Password needs a lowercase letter."; return false; }
        if (!password.Any(char.IsDigit)) { validationMessage = "Password needs a digit."; return false; }
        if (!password.Any(c => !char.IsLetterOrDigit(c))) { validationMessage = "Password needs a special character."; return false; }
        return true;
    }

    private async void OnAddUserClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(usernameEntry.Text))
        {
            await DisplayAlert("Error", "Please enter a username", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(passwordEntry.Text))
        {
            await DisplayAlert("Error", "Please enter a password", "OK");
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

        if (!IsPasswordStrong(passwordEntry.Text, out string passwordError))
        {
            await DisplayAlert("Weak Password", passwordError, "OK");
            return;
        }

        loadingIndicator.IsVisible = true;
        loadingIndicator.IsRunning = true;

        try
        {
            var selectedDept = (LookupItem)departmentPicker.SelectedItem;
            
            var createUserRequest = new CreateUserRequest
            {
                Username = usernameEntry.Text,
                Password = passwordEntry.Text,
                FullName = fullNameEntry.Text,
                Department = selectedDept.Id,
                Title = titlePicker.SelectedItem.ToString(),
                IsAdmin = adminCheckBox.IsChecked
            };

            await _apiService.CreateUserAsync(createUserRequest);
            await DisplayAlert("Success", "User added successfully", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to add user: {ex.Message}", "OK");
        }
        finally
        {
            loadingIndicator.IsVisible = false;
            loadingIndicator.IsRunning = false;
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}