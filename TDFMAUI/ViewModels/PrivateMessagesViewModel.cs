using System.Collections.ObjectModel;
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
    public partial class PrivateMessagesViewModel : BaseViewModel
    {
        private readonly IMessageService _messageApiService;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
        private string _newMessageText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<MessageModel> _messages = new();

        [ObservableProperty]
        private MessageModel? _selectedMessage;

        [ObservableProperty]
        private int _partnerId;

        public PrivateMessagesViewModel(IMessageService messageApiService)
        {
            _messageApiService = messageApiService;
            Title = "Private Messages";
        }

        [RelayCommand]
        public async Task LoadMessagesAsync()
        {
            if (PartnerId <= 0) return;
            IsBusy = true;
            try
            {
                var pagination = new MessagePaginationDto
                {
                    PageNumber = 1,
                    PageSize = 50,
                    SortDescending = true,
                    FromUserId = PartnerId
                };

                var result = await _messageApiService.GetPrivateMessagesAsync(App.CurrentUser?.UserID ?? 0, pagination);
                Messages.Clear();
                if (result?.Items != null)
                {
                    foreach (var dto in result.Items)
                    {
                        Messages.Add(new MessageModel
                        {
                            Id = dto.Id,
                            FromUserId = dto.SenderId,
                            FromUserName = dto.SenderFullName,
                            Content = dto.Content,
                            SentAt = dto.Timestamp,
                            MessageType = dto.MessageType,
                            ReadAt = dto.IsRead ? dto.Timestamp : null,
                            DeliveredAt = dto.IsDelivered ? dto.Timestamp : null
                        });
                    }
                    await MarkMessagesAsReadAsync(PartnerId);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load private messages.";
            }
            finally { IsBusy = false; }
        }

        private bool CanSendMessage() => !string.IsNullOrWhiteSpace(NewMessageText) && PartnerId > 0 && !IsBusy;

        [RelayCommand(CanExecute = nameof(CanSendMessage))]
        private async Task SendMessageAsync()
        {
            var content = NewMessageText.Trim();
            NewMessageText = string.Empty;

            var dto = new MessageCreateDto
            {
                Content = content,
                SenderId = App.CurrentUser?.UserID ?? 0,
                ReceiverId = PartnerId,
                MessageType = TDFShared.Enums.MessageType.Private
            };

            try
            {
                var sent = await _messageApiService.CreateMessageAsync(dto);
                Messages.Add(new MessageModel
                {
                    Id = sent.Id,
                    FromUserId = sent.SenderId,
                    FromUserName = sent.SenderFullName,
                    Content = sent.Content,
                    SentAt = sent.Timestamp,
                    MessageType = sent.MessageType
                });
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to send private message.";
            }
        }

        private async Task MarkMessagesAsReadAsync(int partnerId)
        {
            try
            {
                var unreadIds = Messages.Where(m => m.FromUserId == partnerId && !m.IsRead).Select(m => m.Id).ToList();
                if (unreadIds.Any()) await _messageApiService.MarkMessagesAsReadAsync(unreadIds);
            }
            catch (Exception ex) { }
        }
    }
}
