using System.Collections.ObjectModel;
using System.Windows.Input;
using TDFMAUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using System;
using System.Linq;
using TDFShared.Models.Message;

namespace TDFMAUI.ViewModels
{
    public partial class GlobalMessagesViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _senderName;
    
        private readonly ApiService _apiService;
        private readonly WebSocketService _webSocketService;

        [ObservableProperty]
        private ObservableCollection<ChatMessageModel> _messages = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
        private string _newMessageText;

        [ObservableProperty]
        private DateTime _timestamp = DateTime.Now;

        public GlobalMessagesViewModel(ApiService apiService, WebSocketService webSocketService)
        {
            _apiService = apiService;
            _webSocketService = webSocketService;
            
            LoadMessagesAsync();
            RefreshCommand = new RelayCommand(async () => await LoadMessagesAsync());
        }

        public ICommand RefreshCommand { get; }

        private async Task LoadMessagesAsync()
        {
            var messages = await _apiService.GetRecentChatMessagesAsync(100);
            Messages.Clear();
            foreach (var messageDto in messages.OrderBy(m => m.SentAt))
            {
                var chatMessage = new ChatMessageModel
                {
                    MessageId = messageDto.MessageId,
                    FromUserName = messageDto.SenderName,
                    SenderId = messageDto.SenderId,
                    Content = messageDto.MessageText,
                    SentAt = messageDto.SentAt
                };
                Messages.Add(chatMessage);
            }
        }

        [RelayCommand(CanExecute = nameof(CanSendMessage))]
        private async Task SendMessageAsync()
        {
            if (!CanSendMessage()) return;

            var messageContent = NewMessageText;
            NewMessageText = string.Empty;
            
            try
            {
                var newMessage = new ChatMessageModel
                {
                    FromUserName = "Me",
                    Content = messageContent,
                    SentAt = DateTime.Now
                };
                await _webSocketService.SendMessageAsync(newMessage);
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending global message via WebSocket: {ex.Message}");
            }
        }

        private bool CanSendMessage()
        {
            return !string.IsNullOrWhiteSpace(NewMessageText);
        }
    }
}