using System.Collections.ObjectModel;
using TDFMAUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using System;
using System.Linq;
using TDFShared.Models.Message;

namespace TDFMAUI.ViewModels
{
    public partial class GlobalMessagesViewModel : BaseViewModel
    {
        private readonly IMessageApiService _messageApiService;
        private readonly IWebSocketService _webSocketService;

        [ObservableProperty]
        private ObservableCollection<ChatMessageModel> _messages = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
        private string _newMessageText = string.Empty;

        public GlobalMessagesViewModel(IMessageApiService messageApiService, IWebSocketService webSocketService)
        {
            _messageApiService = messageApiService;
            _webSocketService = webSocketService;
            Title = "Global Messages";
            _ = LoadMessagesAsync();
        }

        [RelayCommand]
        public async Task LoadMessagesAsync()
        {
            IsBusy = true;
            try
            {
                var messages = await _messageApiService.GetRecentChatMessagesAsync(100);
                Messages.Clear();
                foreach (var dto in messages.OrderBy(m => m.SentAt))
                {
                    Messages.Add(new ChatMessageModel
                    {
                        MessageId = dto.MessageId,
                        FromUserName = dto.SenderName,
                        SenderId = dto.SenderId,
                        Content = dto.MessageText,
                        SentAt = dto.SentAt
                    });
                }
            }
            catch (Exception ex) { ErrorMessage = "Failed to load messages."; }
            finally { IsBusy = false; }
        }

        private bool CanSendMessage() => !string.IsNullOrWhiteSpace(NewMessageText) && !IsBusy;

        [RelayCommand(CanExecute = nameof(CanSendMessage))]
        private async Task SendMessageAsync()
        {
            var content = NewMessageText;
            NewMessageText = string.Empty;

            try
            {
                var chatMessage = new ChatMessageModel
                {
                    FromUserName = "Me",
                    Content = content,
                    SentAt = DateTime.Now
                };
                await _webSocketService.SendMessageAsync(chatMessage);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to send message.";
            }
        }
    }
}
