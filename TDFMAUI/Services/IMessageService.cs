using System.Collections.Generic;
using System.Threading.Tasks;
using TDFShared.Models.Message;

namespace TDFMAUI.Services
{
    public interface IMessageService
    {
        Task<List<MessageModel>> GetUserMessagesAsync(int userId);
        Task<MessageModel> CreateMessageAsync(MessageModel message);
        Task<bool> MarkMessageAsReadAsync(int messageId, int userId);
    }
} 