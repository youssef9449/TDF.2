using Microsoft.Maui.Controls;
using TDFMAUI.Services;
using TDFMAUI.ViewModels;
using TDFShared.Services;
using System;
using System.Threading.Tasks;

namespace TDFMAUI.Pages
{
    public partial class ReportsPage : ContentPage
    {
        private readonly ReportsViewModel _viewModel;

        // Inject ViewModel and IRequestService via constructor
        public ReportsPage(IRequestService requestService, IErrorHandlingService errorHandlingService, IAuthService authService)
        {
            InitializeComponent();

            // Create the ViewModel, passing dependencies
            _viewModel = new ReportsViewModel(requestService, Navigation, errorHandlingService, authService);

            // Set the binding context for the page
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            // Initialize the ViewModel and load data when the page appears
            // Use Task.Run to avoid blocking the UI thread if InitializeAsync is long
            await Task.Run(() => _viewModel.InitializeAsync());
        }

        // Remove all other methods like LoadLeaveRequests, OnAddClicked,
        // OnRefreshing, OnFilterChanged, OnItemTapped as this logic
        // is now handled by the ViewModel through commands and bindings.
    }
}