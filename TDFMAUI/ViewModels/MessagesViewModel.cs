using System.Collections.ObjectModel;
using TDFMAUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using System;
using TDFShared.DTOs.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace TDFMAUI.ViewModels
{
    public partial class MessagesViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        private readonly ILogger<MessagesViewModel> _logger;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
        private string _newMessageText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<ChatMessageDto> _messages = new();

        public MessagesViewModel(ApiService apiService, ILogger<MessagesViewModel> logger)
        {
            _apiService = apiService;
            _logger = logger;
            Title = "Messages";
            _ = LoadMessagesAsync();
        }

        [RelayCommand]
        public async Task LoadMessagesAsync()
        {
            IsBusy = true;
            try
            {
                var result = await _apiService.GetRecentChatMessagesAsync(50);
                Messages.Clear();
                if (result != null)
                {
                    foreach (var message in result) Messages.Add(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading messages");
                ErrorMessage = "Failed to load messages.";
            }
            finally { IsBusy = false; }
        }

        private bool CanSendMessage() => !string.IsNullOrWhiteSpace(NewMessageText) && !IsBusy;

        [RelayCommand(CanExecute = nameof(CanSendMessage))]
        private async Task SendMessageAsync()
        {
            var messageContent = NewMessageText;
            NewMessageText = string.Empty;

            var newMessage = new MessageCreateDto
            {
                MessageText = messageContent,
                ReceiverID = 0,
                MessageType = TDFShared.Enums.MessageType.Chat
            };

            try
            {
                var createdMessage = await _apiService.CreateMessageAsync(newMessage);
                if (createdMessage != null) Messages.Add(createdMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                ErrorMessage = "Failed to send message.";
            }
        }
    }
}
