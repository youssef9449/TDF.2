using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TDFMAUI.Services;
using TDFShared.Models;
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Common;
using TDFShared.Constants;
using TDFMAUI.ViewModels;
using System.Diagnostics;

namespace TDFMAUI.Features.Auth
{
    public partial class SignupViewModel : ObservableObject
    {
        private readonly IApiService _apiService;
        private readonly ILookupService _lookupService;
        private readonly ILogger<SignupViewModel> _logger;

        [ObservableProperty]
        private string _username;

        [ObservableProperty]
        private string _password;

        [ObservableProperty]
        private string _confirmPassword;

        [ObservableProperty]
        private string _fullName;

        [ObservableProperty]
        private ObservableCollection<LookupItem> _departments = new();

        // Override the generated property to add debugging
        partial void OnDepartmentsChanged(ObservableCollection<LookupItem> value)
        {
            Debug.WriteLine($"[SignupViewModel] Departments property changed. New count: {value?.Count ?? 0}");
            if (value != null && value.Count > 0)
            {
                Debug.WriteLine($"[SignupViewModel] First department in new collection: {value[0].Name}");
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Titles))]
        private LookupItem _selectedDepartment;

        [ObservableProperty]
        private ObservableCollection<string> _titles = new();

        [ObservableProperty]
        private string _selectedTitle;

        [ObservableProperty]
        private string _errorMessage;

        [ObservableProperty]
        private bool _hasError;

        public SignupViewModel(IApiService apiService, ILookupService lookupService, ILogger<SignupViewModel> logger)
        {
            Debug.WriteLine("[SignupViewModel] Constructor - Start");

            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _lookupService = lookupService ?? throw new ArgumentNullException(nameof(lookupService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Debug.WriteLine("[SignupViewModel] Dependencies injected. Constructor complete - departments will be loaded when page appears.");
            Debug.WriteLine("[SignupViewModel] Constructor - End");

            LoadDepartmentsAsync().ConfigureAwait(false);
        }

        private async Task LoadDepartmentsAsync()
        {
            Debug.WriteLine("[SignupViewModel] LoadDepartmentsAsync - Start");
            _logger?.LogInformation("DIAGNOSTIC: LoadDepartmentsAsync - Starting to load departments");

            try
            {
                var departments = await _lookupService.GetDepartmentsAsync();
                            Departments = new ObservableCollection<LookupItem>(departments);

                Debug.WriteLine($"[SignupViewModel] Successfully loaded {departments.Count} departments");
                _logger?.LogInformation("DIAGNOSTIC: Successfully loaded {Count} departments", departments.Count);

                // Ensure we're on the main thread for UI updates
                await MainThread.InvokeOnMainThreadAsync(() => {
                    try
                    {
                        Debug.WriteLine($"[SignupViewModel] Updating UI with {departments.Count} departments");
                        _logger?.LogInformation("DIAGNOSTIC: Updating UI with {Count} departments", departments.Count);

                        // Clear any previous errors first
                        HasError = false;
                        ErrorMessage = string.Empty;

                        Debug.WriteLine($"[SignupViewModel] UI updated - Departments.Count: {Departments.Count}");
                        _logger?.LogInformation("DIAGNOSTIC: UI updated - Departments.Count: {Count}", Departments.Count);

                        // Auto-select the first department
                        if (Departments.Count > 0)
                        {
                            SelectedDepartment = Departments[0];
                            Debug.WriteLine($"[SignupViewModel] Auto-selected department: {SelectedDepartment.Name}");
                            _logger?.LogInformation("DIAGNOSTIC: Auto-selected department: {Name}",
                                SelectedDepartment.Name);
                        }

                        Debug.WriteLine("[SignupViewModel] Department loading completed successfully");
                        _logger?.LogInformation("DIAGNOSTIC: Department loading completed successfully");
                    }
                    catch (Exception uiEx)
                    {
                        Debug.WriteLine($"[SignupViewModel] Error updating UI: {uiEx.Message}");
                        _logger?.LogError(uiEx, "DIAGNOSTIC: Error updating UI with departments");
                        ErrorMessage = "Error updating department list. Please try again.";
                        HasError = true;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignupViewModel] Error loading departments: {ex.Message}");
                _logger?.LogError(ex, "DIAGNOSTIC: Error loading departments.");

                await MainThread.InvokeOnMainThreadAsync(() => {
                    ErrorMessage = "An error occurred while loading departments. Please try again.";
                    HasError = true;
                    Departments.Clear();
                });
            }
            finally
            {
                Debug.WriteLine("[SignupViewModel] LoadDepartmentsAsync - End");
                _logger?.LogInformation("DIAGNOSTIC: LoadDepartmentsAsync - End");
            }
        }

        partial void OnSelectedDepartmentChanged(LookupItem value)
        {
            if (value != null)
            {
                _logger?.LogInformation("Department selected: Name={Name}", value.Name);
                LoadTitlesForDepartment(value.Name).ConfigureAwait(false);
            }
        }

        private async Task LoadTitlesForDepartment(string department)
        {
            try
            {
                HasError = false;
                Titles.Clear();

                _logger?.LogInformation("Loading titles for department: {DepartmentName}", department);

                var titlesForDepartment = await _lookupService.GetTitlesForDepartmentAsync(department);
                if (titlesForDepartment != null && titlesForDepartment.Any())
                {
                    foreach (var title in titlesForDepartment)
                    {
                        Titles.Add(title);
                    }
                    _logger?.LogInformation("Loaded {Count} titles for department {DepartmentName}", titlesForDepartment.Count, department);

                    // Auto-select first title if available
                    if (Titles.Count > 0)
                    {
                        SelectedTitle = Titles[0];
                    }
                }
                else
                {
                    _logger?.LogWarning("No titles found for department: {DepartmentName}", department);
                    ErrorMessage = $"No titles found for the selected department.";
                    HasError = true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading titles for department {DepartmentName}", department);
                ErrorMessage = "Failed to load titles for the selected department.";
                HasError = true;
            }
        }

        [RelayCommand]
        private async Task SignupAsync()
        {
            HasError = false;
            ErrorMessage = string.Empty;

            // Validation
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password) ||
                string.IsNullOrWhiteSpace(ConfirmPassword) || string.IsNullOrWhiteSpace(FullName) ||
                SelectedDepartment == null || string.IsNullOrWhiteSpace(SelectedTitle))
            {
                ErrorMessage = "All fields are required.";
                HasError = true;
                return;
            }

            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Passwords do not match.";
                HasError = true;
                return;
            }

            var registerRequest = new RegisterRequestDto
            {
                Username = Username,
                Password = Password,
                ConfirmPassword = ConfirmPassword,
                FullName = FullName,
                Department = SelectedDepartment?.Name ?? string.Empty,
                Title = SelectedTitle ?? string.Empty
            };

            try
            {
                _logger?.LogInformation("Attempting registration for user: {Username}", Username);
                var response = await _apiService.RegisterAsync(registerRequest);

                if (response.Success)
                {
                    _logger?.LogInformation("Registration successful for user: {Username}. Navigating to login.", Username);
                        await Shell.Current.GoToAsync("//LoginPage");
                }
                else
                {
                    ErrorMessage = response.Message ?? "Registration failed. Please try again.";
                    HasError = true;
                    _logger?.LogWarning("Registration failed for user: {Username}. API returned: {Message}", Username, response.Message);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during registration for user: {Username}", Username);
                ErrorMessage = "An unexpected error occurred. Please try again.";
                HasError = true;
            }
        }

        [RelayCommand]
        private async Task GoToLoginAsync()
        {
                    await Shell.Current.GoToAsync("//LoginPage");
        }
    }
}