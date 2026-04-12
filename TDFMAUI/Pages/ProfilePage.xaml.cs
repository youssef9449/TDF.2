using TDFMAUI.ViewModels;

namespace TDFMAUI.Pages;

public partial class ProfilePage : ContentPage
{
    private readonly UserProfileViewModel _viewModel;

    public ProfilePage(UserProfileViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadUserProfileAsync();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
