using TDFMAUI.ViewModels;

namespace TDFMAUI.Pages
{
    public partial class RequestApprovalPage : ContentPage
    {
        private readonly RequestApprovalViewModel _viewModel;

        public RequestApprovalPage(RequestApprovalViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadRequestsCommand.ExecuteAsync(null);
        }

        private void OnToggleFiltersClicked(object sender, EventArgs e)
        {
            FiltersPanel.IsVisible = !FiltersPanel.IsVisible;
        }
    }
}
