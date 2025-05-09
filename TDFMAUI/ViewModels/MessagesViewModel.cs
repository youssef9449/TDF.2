using System.Collections.ObjectModel;
using System.Windows.Input;
using TDFMAUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using System.Net.Http;
using System;
using TDFShared.Models.Message;
using TDFMAUI.Helpers;
using System.Drawing;
using Color = System.Drawing.Color;

namespace TDFMAUI.ViewModels
{
    public partial class MessagesViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isLoading;
    
        private readonly MessageService _messageService;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
        private string _newMessageText;

        [ObservableProperty]
        private Color senderStatusColor = Color.Gray; // Default gray

        [ObservableProperty]
        private bool showSenderStatus;

        [ObservableProperty]
        private string senderName;

        [ObservableProperty]
        ObservableCollection<MessageModel> messages;

        [ObservableProperty]
        MessageModel selectedMessage;

        public MessagesViewModel()
        {
            _messageService = new MessageService(new HttpClient());
            LoadMessagesAsync();
            LoadMessagesCommand = new RelayCommand(async () => await LoadMessagesAsync());
        }

        public ICommand LoadMessagesCommand { get; }

        private async Task LoadMessagesAsync()
        {
            Messages.Clear();
            var loadedMessages = await _messageService.GetAllMessagesAsync();
            foreach (var message in loadedMessages)
            {
                Messages.Add(message);
            }
        }

        [RelayCommand(CanExecute = nameof(CanSendMessage))]
        private async Task SendMessageAsync()
        {
            if (!CanSendMessage()) return;

            var messageContent = NewMessageText;
            NewMessageText = string.Empty;

            var newMessage = new MessageModel
            {
                FromUserName = "Me",
                Content = messageContent,
                SentAt = DateTime.Now
            };

            try
            {
                await _messageService.CreateMessageAsync(newMessage);
                Messages.Add(newMessage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending message: {ex.Message}");
            }
        }

        private bool CanSendMessage()
        {
            return !string.IsNullOrWhiteSpace(NewMessageText);
        }
    }
}