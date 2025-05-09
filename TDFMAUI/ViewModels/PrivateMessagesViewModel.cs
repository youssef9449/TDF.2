using System.Collections.ObjectModel;
using System.Windows.Input;
using TDFMAUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using System;
using TDFShared.Models.Message;
using System.Linq;
using TDFShared.DTOs.Messages;

namespace TDFMAUI.ViewModels
{
    public partial class PrivateMessagesViewModel : ObservableObject
    {
        private readonly ApiService _apiService;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
        private string _newMessageText;

        [ObservableProperty]
        ObservableCollection<MessageModel> messages = new ObservableCollection<MessageModel>();

        [ObservableProperty]
        MessageModel selectedMessage;

        [ObservableProperty]
        private int _partnerId;

        public PrivateMessagesViewModel(ApiService apiService)
        {
            _apiService = apiService;
            LoadMessagesAsync();
        }

        public async Task LoadMessagesAsync()
        {
            if (PartnerId <= 0) return;
            
            try
            {
                Messages.Clear();
                
                var pagination = new MessagePaginationDto
                {
                    PageNumber = 1,
                    PageSize = 50,
                    SortDescending = true,
                    FromUserId = PartnerId
                };
                
                var messagesResult = await _apiService.GetPrivateMessagesAsync(App.CurrentUser?.UserID ?? 0, pagination);
                
                if (messagesResult?.Items != null)
                {
                    foreach (var dto in messagesResult.Items)
                    {
                        var messageModel = new MessageModel
                        {
                            MessageId = dto.MessageId,
                            SenderId = dto.SenderId,
                            SenderName = dto.SenderName,
                            MessageContent = dto.MessageText,
                            Timestamp = dto.SentAt,
                            MessageType = dto.MessageType
                        };
                        Messages.Add(messageModel);
                    }
                    
                    // Mark incoming messages as read
                    await MarkMessagesAsRead(PartnerId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading private messages: {ex.Message}");
            }
        }

        [RelayCommand(CanExecute = nameof(CanSendMessage))]
        private async Task SendMessageAsync()
        {
            var messageContent = NewMessageText?.Trim();
            if (string.IsNullOrEmpty(messageContent) || PartnerId <= 0) return;
            
            NewMessageText = string.Empty;
            
            var newMessage = new MessageCreateDto
            {
                MessageText = messageContent,
                SentAt = DateTime.Now,
                SenderID = App.CurrentUser?.UserID ?? 0,
                ReceiverID = PartnerId,
                MessageType = TDFShared.Enums.MessageType.Private
            };

            try
            {
                var sentMessageDto = await _apiService.CreatePrivateMessageAsync(newMessage);
                
                var messageModel = new MessageModel
                {
                    MessageId = sentMessageDto.MessageId,
                    SenderId = sentMessageDto.SenderId,
                    SenderName = sentMessageDto.SenderName,
                    MessageContent = sentMessageDto.MessageText,
                    Timestamp = sentMessageDto.SentAt,
                    MessageType = sentMessageDto.MessageType
                };
                
                Messages.Add(messageModel);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending private message: {ex.Message}");
            }
        }

        private bool CanSendMessage()
        {
            return !string.IsNullOrWhiteSpace(NewMessageText);
        }

        private async Task MarkMessagesAsRead(int partnerId)
        {
            try
            {
                if (App.CurrentUser == null) return;
                
                // Get all unread message IDs from this partner
                var unreadMessageIds = Messages
                    .Where(m => m.SenderId == partnerId && !m.IsRead)
                    .Select(m => m.MessageId)
                    .ToList();
                    
                if (unreadMessageIds.Any())
                {
                    await _apiService.MarkMessagesAsReadAsync(unreadMessageIds);
                    
                    // Update the local messages as read
                    foreach (var message in Messages.Where(m => unreadMessageIds.Contains(m.MessageId)))
                    {
                        // Safely handle read-only IsRead property
                        if (message is IReadStatusAware readStatusAware)
                        {
                            readStatusAware.SetAsRead();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error marking messages as read: {ex.Message}");
            }
        }
    }
    
    // Interface to safely update read status if available
    public interface IReadStatusAware
    {
        void SetAsRead();
    }
}