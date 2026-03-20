using TDFMAUI.ViewModels;

namespace TDFMAUI.Pages
{
    public partial class EditUserPage : ContentPage
    {
        private readonly EditUserViewModel _viewModel;

        public EditUserPage(EditUserViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}
