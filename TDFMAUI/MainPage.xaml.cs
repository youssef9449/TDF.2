using TDFMAUI.Services;
using System.Text.Json;
using TDFShared.DTOs.Messages;
using TDFShared.DTOs.Users;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Extensions.Logging;
using TDFMAUI.Helpers;

namespace TDFMAUI
{
    public partial class MainPage : ContentPage
    {
        private readonly WebSocketService _webSocketService;
        int count = 0;

        public MainPage(WebSocketService webSocketService)
        {
            InitializeComponent();
            _webSocketService = webSocketService;

            // Register for WebSocket messages
            _webSocketService.ChatMessageReceived += OnWebSocketMessageReceived;
        }

        private void OnCounterClicked(object sender, EventArgs e)
        {
            count++;

            if (count == 1)
                CounterBtn.Text = $"Clicked {count} time";
            else
                CounterBtn.Text = $"Clicked {count} times";

            SemanticScreenReader.Announce(CounterBtn.Text);
        }

        private void OnWebSocketMessageReceived(object sender, ChatMessageEventArgs args)
        {
            // Display notification or update UI
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DisplayAlert("New Message", args.Message, "OK");
            });
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            // Unregister from WebSocket events
            _webSocketService.ChatMessageReceived -= OnWebSocketMessageReceived;
        }
    }
}
