using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TDFMAUI.Services;
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Users;
using TDFShared.Services;

namespace TDFMAUI.ViewModels
{
    public partial class AddUserViewModel : BaseViewModel
    {
        private readonly IUserApiService _userApiService;
        private readonly LookupService _lookupService;
        private readonly ISecurityService _securityService;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private string _fullName = string.Empty;

        [ObservableProperty]
        private ObservableCollection<LookupItem> _departments = new();

        [ObservableProperty]
        private LookupItem? _selectedDepartment;

        partial void OnSelectedDepartmentChanged(LookupItem? value)
        {
            if (value != null) _ = LoadTitlesAsync(value.Id);
        }

        [ObservableProperty]
        private ObservableCollection<string> _titles = new();

        [ObservableProperty]
        private string? _selectedTitle;

        [ObservableProperty]
        private bool _isAdmin;

        public AddUserViewModel(IUserApiService userApiService, LookupService lookupService, ISecurityService securityService)
        {
            _userApiService = userApiService;
            _lookupService = lookupService;
            _securityService = securityService;
            Title = "Add User";
            _ = LoadDepartmentsAsync();
        }

        private async Task LoadDepartmentsAsync()
        {
            var departments = await _lookupService.GetDepartmentsAsync();
            Departments.Clear();
            foreach (var dept in departments) Departments.Add(dept);
        }

        private async Task LoadTitlesAsync(string departmentId)
        {
            var titles = await _lookupService.GetTitlesForDepartmentAsync(departmentId);
            Titles.Clear();
            foreach (var title in titles) Titles.Add(title);
            if (Titles.Any()) SelectedTitle = Titles.First();
        }

        [RelayCommand(CanExecute = nameof(IsNotBusy))]
        private async Task AddUserAsync()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password) ||
                string.IsNullOrWhiteSpace(FullName) || SelectedDepartment == null || string.IsNullOrWhiteSpace(SelectedTitle))
            {
                ErrorMessage = "All fields are required.";
                return;
            }

            if (!_securityService.IsPasswordStrong(Password, out string passwordError))
            {
                ErrorMessage = passwordError;
                return;
            }

            IsBusy = true;
            try
            {
                var request = new CreateUserRequest
                {
                    Username = Username,
                    Password = Password,
                    FullName = FullName,
                    Department = SelectedDepartment.Id,
                    Title = SelectedTitle,
                    IsAdmin = IsAdmin
                };

                await _userApiService.CreateUserAsync(request);
                await Shell.Current.DisplayAlert("Success", "User added successfully", "OK");
                await Shell.Current.Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to add user: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
