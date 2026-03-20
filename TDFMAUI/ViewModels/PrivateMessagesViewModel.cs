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
        private readonly ApiService _apiService;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
        private string _newMessageText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<MessageModel> _messages = new();

        [ObservableProperty]
        private MessageModel? _selectedMessage;

        [ObservableProperty]
        private int _partnerId;

        public PrivateMessagesViewModel(ApiService apiService)
        {
            _apiService = apiService;
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

                var result = await _apiService.GetPrivateMessagesAsync(App.CurrentUser?.UserID ?? 0, pagination);
                Messages.Clear();
                if (result?.Items != null)
                {
                    foreach (var dto in result.Items)
                    {
                        Messages.Add(new MessageModel
                        {
                            Id = dto.MessageId,
                            FromUserId = dto.SenderId,
                            FromUserName = dto.SenderName,
                            Content = dto.MessageText,
                            SentAt = dto.SentAt,
                            MessageType = dto.MessageType
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
                MessageText = content,
                SentAt = DateTime.Now,
                SenderID = App.CurrentUser?.UserID ?? 0,
                ReceiverID = PartnerId,
                MessageType = TDFShared.Enums.MessageType.Private
            };

            try
            {
                var sent = await _apiService.CreatePrivateMessageAsync(dto);
                Messages.Add(new MessageModel
                {
                    Id = sent.MessageId,
                    FromUserId = sent.SenderId,
                    FromUserName = sent.SenderName,
                    Content = sent.MessageText,
                    SentAt = sent.SentAt,
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
                if (unreadIds.Any()) await _apiService.MarkMessagesAsReadAsync(unreadIds);
            }
            catch (Exception ex) { }
        }
    }
}
