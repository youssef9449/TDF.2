using System.Collections.ObjectModel;
using TDFMAUI.Services;
using Microsoft.Maui.Controls;
using System.Text.Json;
using System;
using TDFShared.Models.User;
using TDFMAUI.Helpers;
using TDFShared.Enums;
using System.Linq;
using TDFMAUI.ViewModels;
using TDFShared.DTOs.Users;
using TDFShared.DTOs.Messages;
using TDFMAUI.Features.Auth;
using Microsoft.Maui.Graphics;

namespace TDFMAUI.Pages;

public partial class MessagesPage : ContentPage
{
    // Add fields for UI elements referenced in the code
    private ActivityIndicator loadingIndicator;
    private RefreshView refreshView;
    private CollectionView messagesCollection;
    
    private readonly ApiService _apiService;
    private readonly WebSocketService _webSocketService;
    private readonly IUserPresenceService _userPresenceService;
    
    private ObservableCollection<MessageViewModel> _messages = new ObservableCollection<MessageViewModel>();
    private Dictionary<int, UserPresenceStatus> _userStatuses = new Dictionary<int, UserPresenceStatus>();
    
    public MessagesPage(
        ApiService apiService, 
        WebSocketService webSocketService,
        IUserPresenceService userPresenceService)
    {
        InitializeComponent();
        
        _apiService = apiService;
        _webSocketService = webSocketService;
        _userPresenceService = userPresenceService;
        
        // Find the UI elements after InitializeComponent is called
        loadingIndicator = this.FindByName<ActivityIndicator>("LoadingIndicator");
        refreshView = this.FindByName<RefreshView>("MessagesRefreshView");
        messagesCollection = this.FindByName<CollectionView>("MessagesCollection");
        
        // Register for real-time updates
        _webSocketService.ChatMessageReceived += OnChatMessageReceived;
        _webSocketService.MessageStatusChanged += OnMessageStatusChanged;
        _userPresenceService.UserStatusChanged += OnUserStatusChanged;
        
        // Set collection source
        if (messagesCollection != null)
            messagesCollection.ItemsSource = _messages;
            
        // Set refresh callback
        if (refreshView != null)
            refreshView.Command = new Command(async () => await LoadMessages());
    }
    
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Unregister from events when page is not visible
        _webSocketService.ChatMessageReceived -= OnChatMessageReceived;
        _webSocketService.MessageStatusChanged -= OnMessageStatusChanged;
        _userPresenceService.UserStatusChanged -= OnUserStatusChanged;
    }
    
    private async void OnChatMessageReceived(object sender, ChatMessageEventArgs e)
    {
        var existingMessage = _messages.FirstOrDefault(m => m.Id == e.MessageId);
        if (existingMessage == null)
        {
            var newMessage = new MessageViewModel
            {
                Id = e.MessageId,
                Content = e.Message,
                SentAt = e.Timestamp,
                SenderId = e.SenderId,
                SenderName = e.SenderName,
                IsRead = false,
                IsUnread = true,
                BackgroundColor = MessagesPage.ColorToHex((Color)Application.Current.Resources["BlueCardColor"]),
                ShowSenderStatus = true
            };
            
            // Update sender status if known
            if (_userStatuses.TryGetValue(e.SenderId, out var status))
            {
                newMessage.SenderStatus = status;
                newMessage.SenderStatusColor = GetStatusColor(status);
            }
            else
            {
                // Get status from service
                try
                {
                    var userStatus = await _userPresenceService.GetUserStatusAsync(e.SenderId);
                    newMessage.SenderStatus = userStatus;
                    newMessage.SenderStatusColor = GetStatusColor(userStatus);
                    _userStatuses[e.SenderId] = userStatus;
                }
                catch
                {
                    newMessage.SenderStatus = UserPresenceStatus.Offline;
                    newMessage.SenderStatusColor = MessagesPage.GetAppResourceColor("TextSecondaryColor");
                }
            }
            
            // Add to collection on UI thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _messages.Insert(0, newMessage);
            });
            
            // Mark as delivered
            await _webSocketService.MarkMessagesAsDeliveredAsync(e.MessageId);
        }
    }
    
    private void OnMessageStatusChanged(object sender, MessageStatusEventArgs e)
    {
        foreach (var messageId in e.MessageIds)
        {
            var message = _messages.FirstOrDefault(m => m.Id == messageId);
            if (message != null)
            {
                if (e.Status == MessageStatus.Read)
                {
                    message.IsRead = true;
                    message.IsUnread = false;
                }
                else if (e.Status == MessageStatus.Delivered)
                {
                    message.IsDelivered = true;
                }
            }
        }
    }
    
    private void OnUserStatusChanged(object sender, UserStatusChangedEventArgs e)
    {
        // Update our cache
        _userStatuses[e.UserId] = e.Status;
        
        // Update any messages from this sender
        foreach (var message in _messages.Where(m => m.SenderId == e.UserId))
        {
            message.SenderStatus = e.Status;
            message.SenderStatusColor = GetStatusColor(e.Status);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadMessages();
        await LoadUserStatuses();
    }
    
    private async Task LoadUserStatuses()
    {
        try
        {
            var onlineUsers = await _userPresenceService.GetOnlineUsersAsync();
            
            foreach (var user in onlineUsers.Values)
            {
                _userStatuses[user.UserId] = user.Status;
            }
            
            // Update message UI with status info
            foreach (var message in _messages)
            {
                if (_userStatuses.TryGetValue(message.SenderId, out var status))
                {
                    message.SenderStatus = status;
                    message.SenderStatusColor = GetStatusColor(status);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load user statuses: {ex.Message}");
        }
    }
    
    private Color GetStatusColor(UserPresenceStatus status)
    {
        return status switch
        {
            UserPresenceStatus.Online => Color.FromRgb(76, 175, 80),    // Light Green
            UserPresenceStatus.Away => Color.FromRgb(255, 193, 7),      // Yellow
            UserPresenceStatus.Busy => Color.FromRgb(255, 152, 0),      // Orange
            UserPresenceStatus.DoNotDisturb => Color.FromRgb(244, 67, 54), // Red
            UserPresenceStatus.Offline => MessagesPage.GetAppResourceColor("TextSecondaryColor"),
                _ => MessagesPage.GetAppResourceColor("TextSecondaryColor")
        };
    }

    public static Microsoft.Maui.Graphics.Color GetAppResourceColor(string key)
    {
        if (Application.Current.Resources.TryGetValue(key, out var value) && value is Microsoft.Maui.Graphics.Color color)
        {
            return color;
        }
        // Fallback to a default color if resource not found or not a Color
        return Microsoft.Maui.Graphics.Colors.Gray;
    }
    
    // Helper method to convert Microsoft.Maui.Graphics.Color to XAML-compatible string
    public static string ColorToHex(Microsoft.Maui.Graphics.Color color)
    {
        return $"#{(byte)(color.Red * 255):X2}{(byte)(color.Green * 255):X2}{(byte)(color.Blue * 255):X2}";
    }
    
    private async Task LoadMessages()
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
            // Clear existing messages
            _messages.Clear();
            
            // Get messages using the dedicated ApiService method
            var pagination = new MessagePaginationDto
            {
                PageNumber = 1,
                PageSize = 50,
                SortDescending = true
            };
            var messagesResult = await _apiService.GetUserMessagesAsync(App.CurrentUser.UserID, pagination);
            
            if (messagesResult?.Items != null)
            {
                var messageIdsToMarkDelivered = new List<int>();
                foreach (var message in messagesResult.Items)
                {
                    var viewModel = new MessageViewModel
                    {
                        Id = message.MessageId,
                        Content = message.MessageText,
                        Subject = message.Type,
                        SentAt = message.Timestamp,
                        SenderId = message.SenderId,
                        SenderName = message.SenderName, 
                        IsRead = message.IsRead,
                        IsUnread = !message.IsRead,
                        IsDelivered = message.IsDelivered,
                        BackgroundColor = MessagesPage.ColorToHex(message.IsRead ?
                (Color)Application.Current.Resources["SurfaceColor"] :
                (Color)Application.Current.Resources["BlueCardColor"]),
                        ShowSenderStatus = true
                    };
                    
                    // Set status color if we know the sender status
                    if (_userStatuses.TryGetValue(message.SenderId, out var status))
                    {
                        viewModel.SenderStatus = status;
                        viewModel.SenderStatusColor = GetStatusColor(status);
                    }
                    
                    _messages.Add(viewModel);
                    
                    // Track unread messages to mark as delivered
                    if (!message.IsRead && !message.IsDelivered && message.SenderId != App.CurrentUser.UserID)
                    {
                        messageIdsToMarkDelivered.Add(message.MessageId);
                    }
                }
                
                // Mark messages as delivered (if any)
                if (messageIdsToMarkDelivered.Any())
                {
                    await _webSocketService.MarkMessagesAsDeliveredAsync(messageIdsToMarkDelivered);
                }
            }
        }
        catch (Exception ex)
        {
            // Log error
            System.Diagnostics.Debug.WriteLine($"Failed to load messages: {ex.Message}");
            
            // Perhaps show error on UI
            await Application.Current.MainPage.DisplayAlert("Error", "Failed to load messages, please try again.", "OK");
        }
        finally
        {
            // Hide loading indicator
            loadingIndicator.IsVisible = false;
            loadingIndicator.IsRunning = false;
            
            // If using refresh view, end refreshing
            if (refreshView != null)
                refreshView.IsRefreshing = false;
        }
    }

    private async void OnMessageSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is MessageViewModel selectedMessage)
        {
            messagesCollection.SelectedItem = null;
            
            // Mark as read if needed
            if (selectedMessage.IsUnread)
            {
                var success = await _apiService.PostAsync<object, bool>($"messages/{selectedMessage.Id}/read", 
                    new { userId = App.CurrentUser.UserID });
                    
                if (success)
                {
                    selectedMessage.IsRead = true;
                    selectedMessage.IsUnread = false;
                    selectedMessage.BackgroundColor = MessagesPage.ColorToHex((Color)Application.Current.Resources["SurfaceColor"]);
                }
            }

            // Display message details
            await DisplayAlert(selectedMessage.Subject ?? "Message",
                $"From: {selectedMessage.SenderName}\n" +
                $"Date: {selectedMessage.SentAt:g}\n\n" +
                $"{selectedMessage.Content}",
                "OK");
        }
    }

    private async void OnNewMessageClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new NewMessagePage(_apiService));
    }
    
    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        await LoadMessages();
        await LoadUserStatuses();
    }
}

public class MessageViewModel : BindableObject
{
    public int Id { get; set; }
    public string Subject { get; set; }
    public string Content { get; set; }
    public DateTime SentAt { get; set; }
    public int SenderId { get; set; }
    public string SenderName { get; set; }
    
    private bool _isRead;
    public bool IsRead 
    {
        get => _isRead;
        set
        {
            if (_isRead != value)
            {
                _isRead = value;
                OnPropertyChanged();
                
                // Update IsUnread and BackgroundColor
                if (value)
                {
                    IsUnread = false;
                    BackgroundColor = MessagesPage.ColorToHex((Color)Application.Current.Resources["SurfaceColor"]);
                }
            }
        }
    }
    
    private bool _isUnread;
    public bool IsUnread
    {
        get => _isUnread;
        set
        {
            if (_isUnread != value)
            {
                _isUnread = value;
                OnPropertyChanged();
                
                // Update BackgroundColor
                if (value)
                {
                    BackgroundColor = MessagesPage.ColorToHex((Color)Application.Current.Resources["BlueCardColor"]);
                }
                else
                {
                    BackgroundColor = MessagesPage.ColorToHex((Color)Application.Current.Resources["SurfaceColor"]);
                }
            }
        }
    }
    
    public bool IsDelivered { get; set; }
    
    private string _backgroundColor = MessagesPage.ColorToHex((Color)Application.Current.Resources["SurfaceColor"]);
    public string BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            if (_backgroundColor != value)
            {
                _backgroundColor = value;
                OnPropertyChanged();
            }
        }
    }
    
    public bool ShowSenderStatus { get; set; }
    
    private UserPresenceStatus _senderStatus;
    public UserPresenceStatus SenderStatus
    {
        get => _senderStatus;
        set
        {
            if (_senderStatus != value)
            {
                _senderStatus = value;
                OnPropertyChanged();
            }
        }
    }
    
    private Color _senderStatusColor = MessagesPage.GetAppResourceColor("TextSecondaryColor");
    public Color SenderStatusColor
    {
        get => _senderStatusColor;
        set
        {
            if (!_senderStatusColor.Equals(value))
            {
                _senderStatusColor = value;
                OnPropertyChanged();
                // Update color hex for binding
                SenderStatusColorHex = MessagesPage.ColorToHex(value);
            }
        }
    }
    
    // This property is used for XAML binding
    private string _senderStatusColorHex = MessagesPage.ColorToHex((Color)Application.Current.Resources["TextSecondaryColor"]);
    public string SenderStatusColorHex
    {
        get => _senderStatusColorHex;
        set
        {
            if (_senderStatusColorHex != value)
            {
                _senderStatusColorHex = value;
                OnPropertyChanged();
            }
        }
    }
}