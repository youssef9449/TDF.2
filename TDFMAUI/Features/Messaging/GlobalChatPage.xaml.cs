using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using TDFMAUI.Helpers;
using TDFMAUI.Services;
using TDFShared.DTOs.Messages;
using TDFShared.Exceptions;
using TDFShared.Models.Message;

namespace TDFMAUI.Pages;

public partial class GlobalChatPage : ContentPage
{
    private HashSet<string> displayedMessageIds = new HashSet<string>();
    private readonly IMessageService _messageApiService;
    private readonly WebSocketService _webSocketService;
    private readonly ObservableCollection<MessageModel> chatMessages = new ObservableCollection<MessageModel>();
    private CollectionView MessagesListView;

    public GlobalChatPage(IMessageService messageApiService, WebSocketService webSocketService)
    {
        InitializeComponent();
        _messageApiService = messageApiService;
        _webSocketService = webSocketService;
        
        MessagesListView = this.FindByName<CollectionView>("MessagesListView");
        
        if (MessagesListView != null)
            MessagesListView.ItemsSource = chatMessages;

        if (_webSocketService != null)
        {
            _webSocketService.ChatMessageReceived += OnWebSocketMessageReceived;
        }
        
        LoadInitialMessages();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_webSocketService != null)
        {
            _webSocketService.ChatMessageReceived -= OnWebSocketMessageReceived;
        }
    }

    private async Task LoadChatHistory()
    {
        loadingIndicator.IsVisible = true;
        loadingIndicator.IsRunning = true;

        try
        {
            var messages = await _messageApiService.GetRecentChatMessagesAsync(50);
            
            foreach (var message in messages)
            {
                if (!displayedMessageIds.Contains(message.MessageId.ToString()))
                {
                    AppendChatMessage(
                        message.SenderId,
                        message.Content,
                        message.MessageId.ToString(),
                        message.SenderName,
                        message.Timestamp
                    );
                }
            }
            
            noMessagesLabel.IsVisible = displayedMessageIds.Count == 0;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load chat history: {ex.Message}", "OK");
            noMessagesLabel.IsVisible = true;
            noMessagesLabel.Text = "Unable to load messages. Please try again later.";
        }
        finally
        {
            loadingIndicator.IsVisible = false;
            loadingIndicator.IsRunning = false;
        }
    }

    private async Task RefreshChatAsync()
    {
        try
        {
            var messages = await _messageApiService.GetRecentChatMessagesAsync(20);
            
            await MainThread.InvokeOnMainThreadAsync(() => {
                bool newMessagesAdded = false;
                
                foreach (var message in messages)
                {
                    if (!displayedMessageIds.Contains(message.MessageId.ToString()))
                    {
                        AppendChatMessage(
                            message.SenderId,
                            message.Content,
                            message.MessageId.ToString(),
                            message.SenderName,
                            message.Timestamp
                        );
                        newMessagesAdded = true;
                    }
                }
                
                if (newMessagesAdded)
                {
                    chatScrollView.ScrollToAsync(0, chatDisplay.Height, true);
                }
                
                noMessagesLabel.IsVisible = displayedMessageIds.Count == 0;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error refreshing chat: {ex.Message}");
        }
    }

    private void AppendChatMessage(int senderId, string message, string messageId, string senderName, DateTime timestamp)
    {
        if (!displayedMessageIds.Contains(messageId))
        {
            Frame messageFrame = new Frame
            {
                BackgroundColor = senderId == App.CurrentUser.UserID ? 
                    Color.FromArgb("#E3F2FD") : Color.FromArgb("#FFFFFF"),
                Padding = new Thickness(10),
                CornerRadius = 10,
                Margin = new Thickness(0, 5)
            };

            VerticalStackLayout messageLayout = new VerticalStackLayout
            {
                Spacing = 5
            };

            Label senderLabel = new Label
            {
                Text = senderName,
                FontAttributes = FontAttributes.Bold,
                FontSize = 14
            };

            Label messageLabel = new Label
            {
                Text = message,
                FontSize = 16
            };

            Label timeLabel = new Label
            {
                Text = timestamp.ToString("HH:mm"),
                FontSize = 12,
                TextColor = ThemeHelper.GetThemeResource<Color>("TextSecondaryColor"),
                HorizontalOptions = LayoutOptions.End
            };

            messageLayout.Children.Add(senderLabel);
            messageLayout.Children.Add(messageLabel);
            messageLayout.Children.Add(timeLabel);
            messageFrame.Content = messageLayout;

            chatDisplay.Children.Add(messageFrame);
            displayedMessageIds.Add(messageId);
        }
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        await SendMessage();
    }

    private async void OnMessageInputCompleted(object sender, EventArgs e)
    {
        await SendMessage();
    }

    private async Task SendMessage()
    {
        string messageContent = messageInput.Text?.Trim();
        if (string.IsNullOrEmpty(messageContent))
            return;

        sendButton.IsEnabled = false;
        messageInput.IsEnabled = false;
        
        try
        {
            var webSocketPayload = new 
            {
                type = "chat_message",
                senderId = App.CurrentUser.UserID,
                senderName = App.CurrentUser.UserName,
                content = messageContent,
                timestamp = DateTime.UtcNow
            };

            await _webSocketService.SendMessageAsync(webSocketPayload);
            messageInput.Text = string.Empty;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error sending chat message via WebSocket: {ex.Message}");
            await DisplayAlert("Error", "Could not send message. Please try again.", "OK");
        }
        finally
        {
            sendButton.IsEnabled = true;
            messageInput.IsEnabled = true;
            messageInput.Focus();
        }
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        refreshButton.IsEnabled = false;
        try
        {
            await RefreshChatAsync();
        }
        finally
        {
            refreshButton.IsEnabled = true;
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnMessageSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is ChatMessageDto selectedMessage)
        {
            try
            {
                await DisplayAlert("Message Details", 
                    $"From: {selectedMessage.SenderName}\n" +
                    $"Sent: {selectedMessage.Timestamp}\n" +
                    $"Content: {selectedMessage.Content}",
                    "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to display message details: {ex.Message}", "OK");
            }
        }
    }

    private async void OnNewMessageClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new NewMessagePage(_messageApiService, _webSocketService));
    }

    private void OnWebSocketMessageReceived(object sender, ChatMessageEventArgs args)
    {
        if (args != null)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                string messageId = args.MessageId.ToString();
                
                if (!displayedMessageIds.Contains(messageId))
                {
                    AppendChatMessage(
                        args.SenderId,
                        args.Message,
                        messageId,
                        args.SenderName,
                        args.Timestamp
                    );
                    
                    chatScrollView.ScrollToAsync(0, chatDisplay.Height, true);
                    noMessagesLabel.IsVisible = false;
                }
            });
        }
    }

    private async void LoadInitialMessages()
    {
        try
        {
            await LoadChatHistory();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading initial messages: {ex.Message}");
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                noMessagesLabel.IsVisible = true;
                noMessagesLabel.Text = "Unable to load messages. Please try again later.";
            });
        }
    }
}
