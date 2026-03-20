using TDFMAUI.ViewModels;

namespace TDFMAUI.Pages
{
    public partial class UserProfilePage : ContentPage
    {
        private readonly UserProfileViewModel _viewModel;

        public UserProfilePage(UserProfileViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (!_viewModel.IsDataLoaded)
            {
                await _viewModel.LoadUserProfileAsync();
            }
        }
    }
}
