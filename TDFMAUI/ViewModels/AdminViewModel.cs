using CommunityToolkit.Mvvm.Input;
using TDFMAUI.Pages;

namespace TDFMAUI.ViewModels
{
    public partial class AdminViewModel : BaseViewModel
    {
        [RelayCommand]
        private async Task AddUserAsync() => await Shell.Current.Navigation.PushAsync(App.Services.GetRequiredService<AddUserPage>());

        [RelayCommand]
        private async Task ManageRequestsAsync() => await Shell.Current.DisplayAlert("Not Implemented", "Manage Requests is not fully implemented yet.", "OK");

        [RelayCommand]
        private async Task BackAsync() => await Shell.Current.GoToAsync("..");
    }
}
