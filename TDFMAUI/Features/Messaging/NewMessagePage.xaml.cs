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
    private readonly IMessageService _messageApiService;
    private readonly IUserApiService _userApiService;
    private readonly WebSocketService _webSocketService;
    
    public int PreSelectedUserId { get; set; }
    public string PreSelectedUserName { get; set; }

    public NewMessagePage(IMessageService messageApiService, WebSocketService webSocketService = null)
    {
        InitializeComponent();
        _messageApiService = messageApiService;
        _userApiService = App.Services.GetService<IUserApiService>();
        _webSocketService = webSocketService;
        BindingContext = this;
        LoadUsers();
    }

    private async void LoadUsers()
    {
        if (App.CurrentUser == null)
        {
            var loginViewModel = App.Services.GetService<LoginPageViewModel>();
            await Navigation.PushAsync(new LoginPage(loginViewModel));
            return;
        }

        loadingIndicator.IsVisible = true;
        loadingIndicator.IsRunning = true;

        try
        {
            Users.Clear();
            var usersResult = await _userApiService.GetAllUsersAsync();
            if (usersResult != null && usersResult.Items != null)
            {
                var others = usersResult.Items.Where(u => u.UserID != App.CurrentUser.UserID).ToList();
                foreach (var user in others)
                {
                    Users.Add(user);
                }
                recipientPicker.ItemsSource = others;
            }

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
                SenderId = App.CurrentUser.UserID,
                ReceiverId = recipient.UserID,
                Content = contentEditor.Text,
                MessageType = TDFShared.Enums.MessageType.Chat
            };

            await _messageApiService.CreateMessageAsync(message);
            
            if (_webSocketService != null)
            {
                await _webSocketService.SendChatMessageAsync(recipient.UserID, contentEditor.Text);
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
