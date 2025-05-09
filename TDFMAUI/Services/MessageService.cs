using System.Net.Http.Json;
//  // Removed incorrect using
using TDFShared.Models.Message; // Added for MessageModel

namespace TDFMAUI.Services
{
    public class MessageService
    {
        private readonly HttpClient _httpClient;

        public MessageService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<MessageModel>> GetAllMessagesAsync()
        {
            return await _httpClient.GetFromJsonAsync<List<MessageModel>>("api/Message");
        }

        public async Task<MessageModel> GetMessageByIdAsync(int id)
        {
            return await _httpClient.GetFromJsonAsync<MessageModel>($"api/Message/{id}");
        }

        public async Task CreateMessageAsync(MessageModel message)
        {
            await _httpClient.PostAsJsonAsync("api/Message", message);
        }
    }
}