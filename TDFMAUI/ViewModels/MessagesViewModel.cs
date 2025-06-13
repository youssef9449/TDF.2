using System.Collections.ObjectModel;
using System.Windows.Input;
using TDFMAUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using System.Net.Http;
using System;
using TDFShared.DTOs.Messages;
using TDFMAUI.Helpers;
using Color = Microsoft.Maui.Graphics.Color;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace TDFMAUI.ViewModels
{
    public partial class MessagesViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isLoading;

        private readonly ApiService _apiService;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
        private string _newMessageText;

        [ObservableProperty]
        private Color senderStatusColor = Application.Current.Resources.TryGetValue("TextSecondaryColor", out var resourceValue) && resourceValue is Color colorValue ? colorValue : Colors.Gray;

        [ObservableProperty]
        private bool showSenderStatus;

        [ObservableProperty]
        private string senderName = string.Empty;

        [ObservableProperty]
        ObservableCollection<ChatMessageDto> messages = new ObservableCollection<ChatMessageDto>();

        [ObservableProperty]
        ChatMessageDto? selectedMessage;

        public MessagesViewModel(ApiService? apiService = null, ILogger<MessagesViewModel>? logger = null)
        {
            _newMessageText = string.Empty;
            if (apiService == null)
            {
                // Use App.Current.Services to get the ApiService
                _apiService = App.Current?.Handler?.MauiContext?.Services?.GetService<ApiService>();
                if (_apiService == null)
                {
                    throw new InvalidOperationException("ApiService could not be resolved from dependency injection.");
                }
            }
            else
            {
                _apiService = apiService;
            }

            Messages = new ObservableCollection<ChatMessageDto>();
            Task.Run(async () => await LoadMessagesAsync());
            LoadMessagesCommand = new RelayCommand(async () => await LoadMessagesAsync());
        }

        public ICommand LoadMessagesCommand { get; }

        private async Task LoadMessagesAsync()
        {
            try
            {
                IsLoading = true;
                Messages.Clear();
                var result = await _apiService.GetRecentChatMessagesAsync(50);
                if (result != null)
                {
                    foreach (var message in result)
                    {
                        Messages.Add(message);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading messages: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanSendMessage))]
        private async Task SendMessageAsync()
        {
            if (!CanSendMessage()) return;

            var messageContent = NewMessageText;
            NewMessageText = string.Empty;

            var newMessage = new MessageCreateDto
            {
                MessageText = messageContent,
                ReceiverID = 0, // For global chat
                MessageType = TDFShared.Enums.MessageType.Chat
            };

            try
            {
                var createdMessage = await _apiService.CreateMessageAsync(newMessage);
                if (createdMessage != null)
                {
                    Messages.Add(createdMessage);
                }
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