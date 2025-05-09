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
        private readonly ApiService _apiService;

        public PrivateMessagesPage(PrivateMessagesViewModel viewModel, ApiService apiService)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _apiService = apiService;
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
            await Navigation.PushAsync(new NewMessagePage(_apiService));
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}