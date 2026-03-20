using CommunityToolkit.Mvvm.Input;
using TDFMAUI.Pages;

namespace TDFMAUI.ViewModels
{
    public partial class AdminViewModel : BaseViewModel
    {
        [RelayCommand]
        private async Task AddUserAsync() => await Shell.Current.Navigation.PushAsync(App.Services.GetRequiredService<AddUserPage>());

        [RelayCommand]
        private async Task ManageRequestsAsync() => await Shell.Current.Navigation.PushAsync(App.Services.GetRequiredService<TDFMAUI.Pages.RequestApprovalPage>());

        [RelayCommand]
        private async Task BackAsync() => await Shell.Current.GoToAsync("..");
    }
}
