using System;
using Microsoft.Maui.Controls;
using TDFMAUI.ViewModels;

namespace TDFMAUI.Features.Requests
{
    [QueryProperty(nameof(RequestId), "RequestId")]
    public partial class RequestDetailsPage : ContentPage
    {
        private readonly RequestDetailsViewModel _viewModel;
        public int RequestId { get; set; }

        public RequestDetailsPage(RequestDetailsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            if (_viewModel.RequestId <= 0 && RequestId > 0)
            {
                _viewModel.RequestId = RequestId;
            }
            
            await _viewModel.Initialize();
        }
        private void OnBackClicked(object sender, EventArgs e)
        {
            // Navigate back or close the page
            Shell.Current?.GoToAsync("..", true);
        }
    }
}