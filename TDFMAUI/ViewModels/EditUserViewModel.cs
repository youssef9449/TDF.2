using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TDFMAUI.Services;
using TDFShared.DTOs.Users;

namespace TDFMAUI.ViewModels
{
    public partial class EditUserViewModel : BaseViewModel
    {
        private readonly IUserApiService _userApiService;
        private readonly UpdateUserRequest _user;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _fullName = string.Empty;

        [ObservableProperty]
        private string _department = string.Empty;

        [ObservableProperty]
        private string _titleName = string.Empty;

        [ObservableProperty]
        private bool _isAdmin;

        public List<string> DepartmentOptions { get; } = new() { "IT", "HR", "Finance", "Marketing", "Operations", "Sales" };
        public List<string> TitleOptions { get; } = new() { "Manager", "Supervisor", "Team Lead", "Senior", "Junior", "Intern" };

        public EditUserViewModel(UpdateUserRequest user, IUserApiService userApiService)
        {
            _userApiService = userApiService;
            _user = user;
            Title = "Edit User";

            Username = _user.Username;
            FullName = _user.FullName;
            Department = _user.Department;
            TitleName = _user.Title;
            IsAdmin = _user.IsAdmin;
        }

        [RelayCommand(CanExecute = nameof(IsNotBusy))]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(FullName))
            {
                ErrorMessage = "All fields are required.";
                return;
            }

            IsBusy = true;
            try
            {
                _user.Username = Username;
                _user.FullName = FullName;
                _user.Department = Department;
                _user.Title = TitleName;
                _user.IsAdmin = IsAdmin;

                await _userApiService.UpdateUserAsync(_user.UserID, _user);
                await Shell.Current.DisplayAlert("Success", "User updated successfully", "OK");
                await Shell.Current.Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to update user: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(IsNotBusy))]
        private async Task DeleteAsync()
        {
            bool confirm = await Shell.Current.DisplayAlert("Confirm Delete",
                "Are you sure you want to delete this user?", "Delete", "Cancel");

            if (!confirm) return;

            IsBusy = true;
            try
            {
                await _userApiService.DeleteUserAsync(_user.UserID);
                await Shell.Current.DisplayAlert("Success", "User deleted successfully", "OK");
                await Shell.Current.Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to delete user: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
