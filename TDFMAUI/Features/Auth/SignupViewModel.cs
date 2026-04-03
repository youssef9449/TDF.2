using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TDFMAUI.Services;
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Common;
using TDFShared.Services;
using TDFMAUI.ViewModels;

namespace TDFMAUI.Features.Auth
{
    public partial class SignupViewModel : BaseViewModel
    {
        private readonly IApiService _apiService;
        private readonly ILookupService _lookupService;
        private readonly ILogger<SignupViewModel> _logger;
        private readonly IValidationService _validationService;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private string _confirmPassword = string.Empty;

        [ObservableProperty]
        private string _fullName = string.Empty;

        [ObservableProperty]
        private ObservableCollection<LookupItem> _departments = new();

        [ObservableProperty]
        private LookupItem? _selectedDepartment;

        partial void OnSelectedDepartmentChanged(LookupItem? value)
        {
            if (value != null) _ = LoadTitlesForDepartment(value.Name);
        }

        [ObservableProperty]
        private ObservableCollection<string> _titles = new();

        [ObservableProperty]
        private string _selectedTitle = string.Empty;

        public SignupViewModel(IApiService apiService, ILookupService lookupService, ILogger<SignupViewModel> logger, IValidationService validationService)
        {
            _apiService = apiService;
            _lookupService = lookupService;
            _logger = logger;
            _validationService = validationService;
            Title = "Sign Up";
            _ = LoadDepartmentsAsync();
        }

        private async Task LoadDepartmentsAsync()
        {
            try
            {
                var departments = await _lookupService.GetDepartmentsAsync();
                Departments.Clear();
                foreach (var dept in departments) Departments.Add(dept);
                if (Departments.Any()) SelectedDepartment = Departments.First();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading departments.");
                ErrorMessage = "Error loading departments.";
            }
        }

        private async Task LoadTitlesForDepartment(string department)
        {
            try
            {
                var titlesForDepartment = await _lookupService.GetTitlesForDepartmentAsync(department);
                Titles.Clear();
                foreach (var title in titlesForDepartment) Titles.Add(title);
                if (Titles.Any()) SelectedTitle = Titles.First();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading titles.");
                ErrorMessage = "Error loading titles.";
            }
        }

        [RelayCommand(CanExecute = nameof(IsNotBusy))]
        private async Task SignupAsync()
        {
            var registerRequest = new RegisterRequestDto
            {
                Username = Username,
                Password = Password,
                ConfirmPassword = ConfirmPassword,
                FullName = FullName,
                Department = SelectedDepartment?.Name ?? string.Empty,
                Title = SelectedTitle
            };

            // Basic DTO validation
            var validationResult = _validationService.ValidateObject(registerRequest);
            if (!validationResult.IsValid)
            {
                ErrorMessage = validationResult.Errors.FirstOrDefault() ?? "Validation failed.";
                return;
            }

            // Strong password validation
            var passwordResult = _validationService.ValidatePassword(Password);
            if (!passwordResult.IsValid)
            {
                ErrorMessage = passwordResult.Errors.FirstOrDefault() ?? "Password does not meet requirements.";
                return;
            }

            IsBusy = true;
            try
            {
                var response = await _apiService.RegisterAsync(registerRequest);
                if (response.Success)
                {
                    await Shell.Current.DisplayAlert("Success", "Registration successful! You can now log in.", "OK");
                    await Shell.Current.GoToAsync("//LoginPage");
                }
                else
                {
                    if (response.Errors != null && response.Errors.Any())
                    {
                        var allErrors = response.Errors.SelectMany(kvp => kvp.Value);
                        ErrorMessage = string.Join(" ", allErrors);
                    }
                    else
                    {
                        ErrorMessage = response.Message ?? "Registration failed.";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration.");
                ErrorMessage = "An error occurred during registration.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task GoToLoginAsync()
        {
            if (Shell.Current != null) await Shell.Current.GoToAsync("//LoginPage");
            else if (Application.Current?.MainPage?.Navigation != null) await Application.Current.MainPage.Navigation.PopAsync();
        }
    }
}
