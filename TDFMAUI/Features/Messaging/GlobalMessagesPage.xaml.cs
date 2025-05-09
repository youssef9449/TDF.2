using System.Collections.ObjectModel;
using TDFMAUI.Services;
using TDFMAUI.ViewModels;
using Microsoft.Maui.Controls;
using System;
using TDFShared.DTOs.Messages;

namespace TDFMAUI.Pages
{
    public partial class GlobalMessagesPage : ContentPage
    {
        public GlobalMessagesPage(ApiService apiService, WebSocketService webSocketService)
        {
            InitializeComponent();
            BindingContext = new GlobalMessagesViewModel(apiService, webSocketService);
        }

        // Added for XAML event handler fix
        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        // Added for XAML event handler fix
        private void OnMessageSelected(object sender, SelectionChangedEventArgs e)
        {
            // e.CurrentSelection gives the selected item(s)
            // Clear selection to prevent item staying highlighted
            if (e.CurrentSelection.Any() && sender is CollectionView collectionView)
            {
                collectionView.SelectedItem = null;
            }
        }

        // Added for XAML event handler fix
        private void OnNewMessageClicked(object sender, EventArgs e)
        {
            // Assuming the primary send logic is in the ViewModel.
            // This handler might be used to focus the input field.
            var messageInput = this.FindByName<InputView>("messageInput"); // Try finding Entry or Editor
            messageInput?.Focus();
        }
    }
}