using System;
using TDFMAUI.Services;
using TDFMAUI.Features.Admin;
using TDFMAUI.Features.Auth;
using TDFMAUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace TDFMAUI.Pages;

public partial class MainPage : ContentPage
{
    private readonly ApiService _apiService;

    public MainPage()
    {
        InitializeComponent();
        _apiService = App.Services.GetService<ApiService>();
        LoadUserInfo();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadUserInfo();
    }

    private void LoadUserInfo()
    {
        if (App.CurrentUser != null)
        {
            var userNameLabel = this.FindByName("userNameLabel") as Label;
            var adminFrame = this.FindByName("adminFrame") as Frame;

            if (userNameLabel != null)
            {
                userNameLabel.Text = App.CurrentUser.FullName;
            }

            if (adminFrame != null)
            {
                adminFrame.IsVisible = App.CurrentUser.IsAdmin;
            }
        }
        else
        {
            var loginViewModel = App.Services.GetService<LoginPageViewModel>();
            Navigation.PushAsync(new LoginPage(loginViewModel));
        }
    }

    private async void OnLeaveRequestsClicked(object sender, EventArgs e)
    {
        var requestsViewModel = App.Services.GetService<ViewModels.RequestsViewModel>();
        await Navigation.PushAsync(new RequestsPage(requestsViewModel));
    }

    // Added for XAML event handler fix
    private void OnLeaveRequestClicked(object sender, EventArgs e)
    {
        // Call the plural version for compatibility
        OnLeaveRequestsClicked(sender, e);
    }

    private async void OnMessagesClicked(object sender, EventArgs e)
    {
        var webSocketService = App.Services.GetService<WebSocketService>();
        var userPresenceService = App.Services.GetService<IUserPresenceService>();
        await Navigation.PushAsync(new MessagesPage(_apiService, webSocketService, userPresenceService));
    }

    private async void OnProfileClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new ProfilePage(_apiService));
    }

    private async void OnAdminClicked(object sender, EventArgs e)
    {
        if (App.CurrentUser != null && App.CurrentUser.IsAdmin == true)
        {
            var requestService = App.Services.GetRequiredService<IRequestService>();
            var apiService = App.Services.GetRequiredService<ApiService>();
            var errorHandlingService = App.Services.GetRequiredService<TDFShared.Services.IErrorHandlingService>();
            await Navigation.PushAsync(new AdminPage(requestService, apiService, errorHandlingService));
        }
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        App.CurrentUser = null;
        var loginViewModel = App.Services.GetService<LoginPageViewModel>();
        await Navigation.PushAsync(new LoginPage(loginViewModel));
    }
}