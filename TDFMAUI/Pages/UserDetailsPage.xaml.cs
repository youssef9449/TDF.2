using System;
using TDFMAUI.Services;
using Microsoft.Extensions.DependencyInjection;
using TDFShared.Models.User;

namespace TDFMAUI.Pages;

public partial class UserDetailsPage : ContentPage
{
    private readonly IUserApiService _userApiService;
    private readonly int _userId;

    public UserDetailsPage(int userId)
    {
        InitializeComponent();
        _userApiService = App.Services.GetService<IUserApiService>();
        _userId = userId;
        LoadUserDetails();
    }

    private async void LoadUserDetails()
    {
        try
        {
            var user = await _userApiService.GetUserByIdAsync(_userId);
            if (user != null)
            {
                userNameLabel.Text = user.UserName;
                fullNameLabel.Text = user.FullName;
                departmentLabel.Text = user.Department;
                titleLabel.Text = user.Title;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load user details: {ex.Message}", "OK");
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
} 