using TDFMAUI.ViewModels;

namespace TDFMAUI.Pages;

public partial class AddUserPage : ContentPage
{
    private readonly AddUserViewModel _viewModel;

    public AddUserPage(AddUserViewModel viewModel)
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
