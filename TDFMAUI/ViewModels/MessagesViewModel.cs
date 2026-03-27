using System.Collections.ObjectModel;
using TDFMAUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using System;
using TDFShared.DTOs.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using TDFShared.Enums;

namespace TDFMAUI.ViewModels
{
    public partial class MessagesViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        private readonly ILogger<MessagesViewModel> _logger;
        private readonly WebSocketService _webSocketService;
        private readonly IUserPresenceService _userPresenceService;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
        private string _newMessageText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<MessageItemViewModel> _messages = new();

        public MessagesViewModel(
            ApiService apiService,
            ILogger<MessagesViewModel> logger,
            WebSocketService webSocketService,
            IUserPresenceService userPresenceService)
        {
            _apiService = apiService;
            _logger = logger;
            _webSocketService = webSocketService;
            _userPresenceService = userPresenceService;
            Title = "Messages";
        }

        [RelayCommand]
        public async Task LoadMessagesAsync()
        {
            if (App.CurrentUser == null) return;
            IsBusy = true;
            try
            {
                var pagination = new MessagePaginationDto { PageNumber = 1, PageSize = 50, SortDescending = true };
                var result = await _apiService.GetUserMessagesAsync(App.CurrentUser.UserID, pagination);

                Messages.Clear();
                if (result?.Items != null)
                {
                    var deliveredIds = new List<int>();
                    foreach (var m in result.Items)
                    {
                        var vm = new MessageItemViewModel
                        {
                            Id = m.MessageId,
                            Content = m.MessageText,
                            Timestamp = m.Timestamp,
                            SenderId = m.SenderId,
                            SenderName = m.SenderName,
                            IsRead = m.IsRead,
                            IsDelivered = m.IsDelivered
                        };
                        Messages.Add(vm);
                        if (!m.IsRead && !m.IsDelivered && m.SenderId != App.CurrentUser.UserID) deliveredIds.Add(m.MessageId);
                    }
                    if (deliveredIds.Any()) await _webSocketService.MarkMessagesAsDeliveredAsync(deliveredIds);
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
            var content = NewMessageText;
            NewMessageText = string.Empty;
            try
            {
                var dto = new MessageCreateDto { MessageText = content, ReceiverID = 0, MessageType = MessageType.Chat };
                var created = await _apiService.CreateMessageAsync(dto);
                if (created != null) await LoadMessagesAsync();
            }
            catch (Exception ex) { ErrorMessage = "Failed to send message."; }
        }

        public void HandleMessageReceived(ChatMessageEventArgs e)
        {
            if (Messages.Any(m => m.Id == e.MessageId)) return;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Messages.Insert(0, new MessageItemViewModel
                {
                    Id = e.MessageId,
                    Content = e.Message,
                    Timestamp = e.Timestamp,
                    SenderId = e.SenderId,
                    SenderName = e.SenderName,
                    IsRead = false
                });
            });
        }
    }

    public partial class MessageItemViewModel : ObservableObject
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BackgroundColor))]
        private bool _isRead;

        public bool IsDelivered { get; set; }
        public Color BackgroundColor => IsRead
            ? TDFMAUI.Helpers.ThemeHelper.GetThemeResource<Color>("SurfaceColor")
            : TDFMAUI.Helpers.ThemeHelper.GetThemeResource<Color>("BlueCardColor");
    }
}
