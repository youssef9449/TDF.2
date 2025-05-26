using System.Collections.ObjectModel;
using TDFMAUI.Services;
using System.Linq;
using TDFMAUI.ViewModels;
using TDFShared.DTOs.Users;
using TDFShared.Models.Message;
using TDFShared.DTOs.Messages;
using TDFMAUI.Features.Auth;
using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;

namespace TDFMAUI.Pages;

public partial class NewMessagePage : ContentPage
{
    public ObservableCollection<UserDto> Users { get; set; } = new ObservableCollection<UserDto>();
    private readonly ApiService _apiService;
    private readonly WebSocketService _webSocketService;
    
    // Add properties for pre-selected user
    public int PreSelectedUserId { get; set; }
    public string PreSelectedUserName { get; set; }

    public NewMessagePage(ApiService apiService, WebSocketService webSocketService = null)
    {
        InitializeComponent();
        _apiService = apiService;
        _webSocketService = webSocketService;
        BindingContext = this;
        LoadUsers();
    }

    private async void LoadUsers()
    {
        if (App.CurrentUser == null)
        {
            // Get LoginPageViewModel from the service provider and pass it to LoginPage
            var loginViewModel = App.Services.GetService<LoginPageViewModel>();
            await Navigation.PushAsync(new LoginPage(loginViewModel));
            return;
        }

        loadingIndicator.IsVisible = true;
        loadingIndicator.IsRunning = true;

        try
        {
            Users.Clear();
            var usersResult = await _apiService.GetAllUsersAsync();
            if (usersResult != null && usersResult.Items != null)
            {
                foreach (var user in usersResult.Items.Where(u => u.UserID != App.CurrentUser.UserID))
                {
                    Users.Add(user);
                }
                recipientPicker.ItemsSource = usersResult.Items.Where(u => u.UserID != App.CurrentUser.UserID).ToList();
            }

            // If we have a pre-selected user, try to select them
            if (PreSelectedUserId > 0 && !string.IsNullOrEmpty(PreSelectedUserName))
            {
                var preSelectedUser = Users.FirstOrDefault(u => u.UserID == PreSelectedUserId);
                if (preSelectedUser != null)
                {
                    recipientPicker.SelectedItem = preSelectedUser;
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load users: {ex.Message}", "OK");
        }
        finally
        {
            loadingIndicator.IsVisible = false;
            loadingIndicator.IsRunning = false;
        }
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        if (recipientPicker.SelectedItem == null)
        {
            await DisplayAlert("Error", "Please select a recipient", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(subjectEntry.Text))
        {
            await DisplayAlert("Error", "Please enter a subject", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(contentEditor.Text))
        {
            await DisplayAlert("Error", "Please enter a message", "OK");
            return;
        }

        loadingIndicator.IsVisible = true;
        loadingIndicator.IsRunning = true;

        try
        {
            UserDto recipient = (UserDto)recipientPicker.SelectedItem;
            var message = new MessageCreateDto
            {
                SenderID = App.CurrentUser.UserID,
                FromUserName = App.CurrentUser.UserName,
                ReceiverID = recipient.UserID,
                MessageText = contentEditor.Text,
                SentAt = DateTime.UtcNow
            };

            await _apiService.CreateMessageAsync(message);
            
            // Send a WebSocket notification if available
            if (_webSocketService != null)
            {
                await _webSocketService.SendMessageAsync($"{{\"type\":\"newMessage\",\"recipientId\":{recipient.UserID}}}");
            }
            
            await DisplayAlert("Success", "Message sent successfully", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to send message: {ex.Message}", "OK");
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