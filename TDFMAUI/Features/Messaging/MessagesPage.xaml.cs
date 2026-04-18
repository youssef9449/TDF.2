using TDFMAUI.ViewModels;
using TDFMAUI.Services;
using TDFMAUI.Helpers;

namespace TDFMAUI.Pages;

public partial class MessagesPage : ContentPage
{
    private readonly MessagesViewModel _viewModel;
    private readonly WebSocketService _webSocketService;
    
    public MessagesPage(MessagesViewModel viewModel, WebSocketService webSocketService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _webSocketService = webSocketService;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _webSocketService.ChatMessageReceived += OnChatMessageReceived;
        await _viewModel.LoadMessagesAsync();
    }
    
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _webSocketService.ChatMessageReceived -= OnChatMessageReceived;
    }
    
    private void OnChatMessageReceived(object sender, ChatMessageEventArgs e)
    {
        _viewModel.HandleMessageReceived(e);
    }

    private async void OnNewMessageClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(App.Services.GetRequiredService<NewMessagePage>());
    }
}
