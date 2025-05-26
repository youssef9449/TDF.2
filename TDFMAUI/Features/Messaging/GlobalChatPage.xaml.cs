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
    private readonly ApiService _apiService;
    private readonly WebSocketService _webSocketService;
    private readonly ObservableCollection<MessageModel> chatMessages = new ObservableCollection<MessageModel>();
    
    // Remove these duplicate field declarations as they're already defined by x:Name in XAML
    // private ActivityIndicator loadingIndicator;
    // private Label noMessagesLabel;
    // private ScrollView chatScrollView;
    // private VerticalStackLayout chatDisplay;
    // private Button refreshButton;
    // private Entry messageInput;
    // private Button sendButton;
    private CollectionView MessagesListView;

    public GlobalChatPage(ApiService apiService, WebSocketService webSocketService)
    {
        InitializeComponent();
        _apiService = apiService;
        _webSocketService = webSocketService;
        
        // We don't need to use FindByName for elements with x:Name already defined in XAML
        // They're already accessible as properties after InitializeComponent()
        MessagesListView = this.FindByName<CollectionView>("MessagesListView");
        
        if (MessagesListView != null)
            MessagesListView.ItemsSource = chatMessages;

        // Subscribe to WebSocket events
        if (_webSocketService != null)
        {
            _webSocketService.ChatMessageReceived += OnWebSocketMessageReceived;
        }
        
        // Load initial messages
        LoadInitialMessages();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Unregister from WebSocket events
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
            // Get chat messages from API
            var messages = await _apiService.GetRecentChatMessagesAsync(50);
            
            foreach (var message in messages)
            {
                if (!displayedMessageIds.Contains(message.MessageId.ToString()))
                {
                    AppendChatMessage(
                        message.SenderId,
                        message.MessageText,
                        message.MessageId.ToString(),
                        message.SenderName,
                        message.SentAt
                    );
                }
            }
            
            noMessagesLabel.IsVisible = displayedMessageIds.Count == 0;
        }
        catch (ApiException ex)
        {
            if (ex.IsNetworkError)
            {
                await DisplayAlert("Connection Error", "Cannot connect to the server. Please check your internet connection.", "OK");
            }
            else
            {
                await DisplayAlert("Error", $"Failed to load chat history: {ex.Message}", "OK");
            }
            noMessagesLabel.IsVisible = true;
            noMessagesLabel.Text = "Unable to load messages. Please try again later.";
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
            // Get recent chat messages from API
            var messages = await _apiService.GetRecentChatMessagesAsync(20);
            
            await MainThread.InvokeOnMainThreadAsync(() => {
                bool newMessagesAdded = false;
                
                foreach (var message in messages)
                {
                    if (!displayedMessageIds.Contains(message.MessageId.ToString()))
                    {
                        AppendChatMessage(
                            message.SenderId,
                            message.MessageText,
                            message.MessageId.ToString(),
                            message.SenderName,
                            message.SentAt
                        );
                        newMessagesAdded = true;
                    }
                }
                
                if (newMessagesAdded)
                {
                    // Scroll to bottom if new messages were added
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
                TextColor = Colors.Gray,
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

        // Disable send button during send
        sendButton.IsEnabled = false;
        messageInput.IsEnabled = false;
        
        try
        {
            // Construct payload for WebSocket message
            // Assuming the server expects a specific format for global chat messages
            var webSocketPayload = new 
            {
                type = "global_chat_message", // Example type, adjust based on server implementation
                senderId = App.CurrentUser.UserID,
                senderName = App.CurrentUser.UserName,
                content = messageContent,
                timestamp = DateTime.UtcNow // Use UtcNow for consistency
            };

            // Send message via WebSocketService
            await _webSocketService.SendMessageAsync(webSocketPayload);
            
            // Clear the input field immediately after sending via WebSocket
            messageInput.Text = string.Empty;

            // OPTIONAL: Append the sent message locally immediately for better UX
            // (instead of waiting for it to come back from the server via OnWebSocketMessageReceived)
            // If doing this, ensure OnWebSocketMessageReceived handles potential duplicates.
            // AppendChatMessage(App.CurrentUser.Id, messageContent, Guid.NewGuid().ToString(), App.CurrentUser.Username, DateTime.Now);
            // chatScrollView.ScrollToAsync(0, chatDisplay.Height, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error sending chat message via WebSocket: {ex.Message}");
            await DisplayAlert("Error", "Could not send message. Please try again.", "OK");
            // Consider logging the error more formally
        }
        finally
        {
            // Re-enable send button and input field
            sendButton.IsEnabled = true;
            messageInput.IsEnabled = true;
            messageInput.Focus(); // Set focus back to input
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
                // Display the message details directly
                await DisplayAlert("Message Details", 
                    $"From: {selectedMessage.SenderName}\n" +
                    $"Sent: {selectedMessage.SentAt}\n" +
                    $"Content: {selectedMessage.MessageText}", 
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
        // Navigate to the new message composition page
        await Navigation.PushAsync(new NewMessagePage(_apiService, _webSocketService));
    }

    private void OnWebSocketMessageReceived(object sender, ChatMessageEventArgs args)
    {
        // We received a message from the WebSocket
        if (args != null)
        {
            // Only process on UI thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                string messageId = args.MessageId.ToString();
                
                // Only add if not already displayed
                if (!displayedMessageIds.Contains(messageId))
                {
                    AppendChatMessage(
                        args.SenderId,
                        args.Message,
                        messageId,
                        args.SenderName,
                        args.Timestamp
                    );
                    
                    // Scroll to the new message
                    chatScrollView.ScrollToAsync(0, chatDisplay.Height, true);
                    
                    // Hide "no messages" label if it was visible
                    noMessagesLabel.IsVisible = false;
                }
            });
        }
    }

    private async void LoadInitialMessages()
    {
        try
        {
            // Call the existing method to load chat history
            await LoadChatHistory();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading initial messages: {ex.Message}");
            // Show a user-friendly error if needed
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                noMessagesLabel.IsVisible = true;
                noMessagesLabel.Text = "Unable to load messages. Please try again later.";
            });
        }
    }
}