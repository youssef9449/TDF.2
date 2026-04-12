using System.Collections.ObjectModel;
using TDFMAUI.Services;
using TDFMAUI.ViewModels;
using Microsoft.Maui.Controls;
using System;
using TDFShared.Models.User;
using TDFShared.Models.Message;
using Microsoft.Extensions.Logging;

namespace TDFMAUI.Pages
{
    public partial class PrivateMessagesPage : ContentPage
    {
        private readonly PrivateMessagesViewModel _viewModel;
        private readonly IMessageApiService _messageApiService;

        public PrivateMessagesPage(PrivateMessagesViewModel viewModel, IMessageApiService messageApiService)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _messageApiService = messageApiService;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
        }

        private async void OnMessageSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is MessageModel selectedMessage)
            {
                await DisplayAlert("Message Details", selectedMessage.MessageContent, "OK");
                messagesCollection.SelectedItem = null;
            }
        }

        private async void OnNewMessageClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new NewMessagePage(_messageApiService));
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}