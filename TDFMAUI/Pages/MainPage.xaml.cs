using System;
using TDFMAUI.Services;
using TDFMAUI.Features.Admin;
using TDFMAUI.Features.Auth;
using TDFMAUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using TDFMAUI.Services.Presence;

namespace TDFMAUI.Pages;

public partial class MainPage : ContentPage
{
    private readonly IServiceProvider _serviceProvider; // Add IServiceProvider

    public MainPage(IServiceProvider serviceProvider) // Inject IServiceProvider
    {
        InitializeComponent();
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider)); // Assign IServiceProvider
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
                adminFrame.IsVisible = App.CurrentUser.IsAdmin ?? false;
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
        var requestsViewModel = App.Services.GetService<RequestsViewModel>();
        await Navigation.PushAsync(new RequestsPage(requestsViewModel));
    }

    private async void OnMessagesClicked(object sender, EventArgs e)
    {
        var webSocketService = App.Services.GetService<WebSocketService>();
        var viewModel = App.Services.GetRequiredService<MessagesViewModel>();
        await Navigation.PushAsync(new MessagesPage(viewModel, webSocketService));
    }

    private async void OnProfileClicked(object sender, EventArgs e)
    {
        var viewModel = App.Services.GetService<UserProfileViewModel>();
        await Navigation.PushAsync(new ProfilePage(viewModel));
    }

    private async void OnAdminClicked(object sender, EventArgs e)
    {
        if (App.CurrentUser != null && (App.CurrentUser.IsAdmin ?? false))
        {
            await Navigation.PushAsync(_serviceProvider.GetRequiredService<AdminPage>());
        }
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        App.UserSessionService?.SetCurrentUser(null);
        var loginViewModel = App.Services.GetService<LoginPageViewModel>();
        await Navigation.PushAsync(new LoginPage(loginViewModel));
    }
}