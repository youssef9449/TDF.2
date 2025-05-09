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
using TDFShared.Models.User;
using TDFShared.DTOs.Common;
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
        private string _FullName;

        [ObservableProperty]
        private ObservableCollection<LookupItem> _departments = new();

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

            Debug.WriteLine("[SignupViewModel] Dependencies injected. Calling LoadLookupsAsync...");
            // Load lookups asynchronously without blocking the constructor
            _ = LoadLookupsAsync(); 
            Debug.WriteLine("[SignupViewModel] Constructor - End (LoadLookupsAsync started)");
        }

        private async Task LoadLookupsAsync()
        {
            Debug.WriteLine("[SignupViewModel] LoadLookupsAsync - Start");
            _logger?.LogInformation("DIAGNOSTIC: LoadLookupsAsync - Starting to load departments");
            
            try
            {
                // Test API connectivity first
                bool connected = false;
                try {
                    connected = await _apiService.TestConnectivityAsync();
                    Debug.WriteLine($"[SignupViewModel] API connectivity test result: {connected}");
                    _logger?.LogInformation("DIAGNOSTIC: API connectivity test result: {Connected}", connected);
                } catch (Exception connEx) {
                    Debug.WriteLine($"[SignupViewModel] API connectivity test error: {connEx.Message}");
                    _logger?.LogError(connEx, "DIAGNOSTIC: API connectivity test failed");
                }
                
                _logger?.LogInformation("Loading departments for signup.");
                Debug.WriteLine("[SignupViewModel] LoadLookupsAsync - Calling _lookupService.GetDepartmentsAsync() via standard service flow.");
                
                // Use the standard service layer to load departments
                var departments = await _lookupService.GetDepartmentsAsync();
                Debug.WriteLine($"[SignupViewModel] LoadLookupsAsync - _lookupService.GetDepartmentsAsync() returned. Count: {departments?.Count ?? 0}");
                
                if (departments != null && departments.Any())
                {
                    Debug.WriteLine($"[SignupViewModel] Departments received. First item: {departments[0].Id} - {departments[0].Name}");
                    Departments = new ObservableCollection<LookupItem>(departments);
                    _logger?.LogInformation($"Loaded {departments.Count} departments");
                    Debug.WriteLine($"[SignupViewModel] Loaded {departments.Count} departments successfully");                 
                }
                else
                {
                    Debug.WriteLine("[SignupViewModel] No departments were returned");
                    _logger?.LogWarning("Failed to load departments.");
                    ErrorMessage = "Failed to load departments.";
                    HasError = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignupViewModel] Error loading departments: {ex.Message}");
                _logger?.LogError(ex, "Error loading departments.");
                ErrorMessage = "An error occurred while loading necessary data.";
                HasError = true;
            }
        }

        partial void OnSelectedDepartmentChanged(LookupItem value)
        {
            if (value != null)
            {
                _logger?.LogInformation($"Department selected: {value.Name} (ID: {value.Id})");
                _ = LoadTitlesForSelectedDepartmentAsync();
            }
            else
            {
                Titles.Clear();
            }
        }

        private async Task LoadTitlesForSelectedDepartmentAsync()
        {
            if (SelectedDepartment == null)
                return;

            try
            {
                HasError = false;
                Titles.Clear();
                
                var departmentId = SelectedDepartment.Id;
                _logger?.LogInformation($"Loading titles for department: {departmentId}");
                
                var titlesForDepartment = await _lookupService.GetTitlesForDepartmentAsync(departmentId);
                if (titlesForDepartment != null && titlesForDepartment.Any())
                {
                    foreach (var title in titlesForDepartment)
                    {
                        Titles.Add(title);
                    }
                    _logger?.LogInformation($"Loaded {titlesForDepartment.Count} titles for department {departmentId}");
                    
                    // Auto-select first title if available
                    if (Titles.Count > 0)
                    {
                        SelectedTitle = Titles[0];
                    }
                }
                else
                {
                    _logger?.LogWarning($"No titles found for department: {departmentId}");
                    ErrorMessage = $"No titles found for the selected department.";
                    HasError = true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error loading titles for department {SelectedDepartment?.Id}");
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
 
            // Password strength validation has been removed to allow any password
 
            var signupModel = new SignupModel
            {
                Username = Username,
                Password = Password,
                FullName = FullName,
                Department = SelectedDepartment?.Name ?? string.Empty,
                Title = SelectedTitle ?? string.Empty
            };

            try
            {
                _logger?.LogInformation("Attempting signup for user: {Username}", Username);
                bool success = await _apiService.SignupAsync(signupModel);

                if (success)
                {
                    _logger?.LogInformation("Signup successful for user: {Username}. Navigating to login.", Username);
                    await Shell.Current.GoToAsync("//LoginPage");
                }
                else
                {
                    ErrorMessage = "Signup failed. The username might already be taken, or an error occurred.";
                    HasError = true;
                    _logger?.LogWarning("Signup failed for user: {Username}. API returned false.", Username);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during signup for user: {Username}", Username);
                if (ex is TDFShared.Exceptions.ApiException apiEx)
                {
                    // Try to get a more specific message from ApiException
                    string apiErrorMessage = apiEx.Message;
                    if (!string.IsNullOrWhiteSpace(apiEx.ResponseContent) && apiEx.ResponseContent.Length < 500) // Avoid showing huge HTML error pages
                    {
                        // Check if ResponseContent is a JSON ApiResponse
                        try
                        {
                            var errorResponse = System.Text.Json.JsonSerializer.Deserialize<TDFShared.DTOs.Common.ApiResponse<object>>(apiEx.ResponseContent, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (errorResponse != null)
                            {
                                if (errorResponse.Errors != null && errorResponse.Errors.Any())
                                {
                                    // Prioritize detailed validation errors
                                    apiErrorMessage = string.Join("; ", errorResponse.Errors.SelectMany(kv => $"{kv.Key}: {string.Join(", ", kv.Value)}"));
                                }
                                else if (!string.IsNullOrWhiteSpace(errorResponse.Message))
                                {
                                    apiErrorMessage = errorResponse.Message;
                                }
                                // If neither Errors nor Message is present in errorResponse, apiErrorMessage remains apiEx.Message ("Bad Request")
                            }
                        }
                        catch { /* Ignore if not a standard ApiResponse format */ }
                    }
                    ErrorMessage = $"Signup Error: {apiErrorMessage}";
                }
                else
                {
                    ErrorMessage = "An unexpected error occurred. Please check your network and try again.";
                }
                HasError = true;
            }
        }

        [RelayCommand]
        private async Task GoToLoginAsync()
        {
            try
            {
                if (Shell.Current != null)
                {
                    // Use Shell navigation if available
                    await Shell.Current.GoToAsync("//LoginPage");
                }
                else
                {
                    // Fallback to direct page navigation
                    var loginPage = App.Services?.GetService<LoginPage>();
                    if (loginPage != null)
                    {
                        await Application.Current.MainPage.Navigation.PushAsync(loginPage);
                    }
                    else
                    {
                        // If we can't resolve LoginPage from DI, try to get the ViewModel
                        var loginViewModel = App.Services?.GetService<LoginPageViewModel>();
                        if (loginViewModel != null)
                        {
                            // Create LoginPage with the resolved ViewModel
                            await Application.Current.MainPage.Navigation.PushAsync(new LoginPage(loginViewModel));
                        }
                        else
                        {
                            // If all else fails, display an error
                            ErrorMessage = "Unable to navigate to login page. Please restart the application.";
                            HasError = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error
                System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
                
                // Display error to user
                ErrorMessage = $"Navigation error: {ex.Message}";
                HasError = true;
            }
        }

        // Command to load departments that can be called from the UI
        [RelayCommand]
        public async Task LoadDepartmentsAsync()
        {
            Debug.WriteLine("[SignupViewModel] LoadDepartmentsCommand executed");
            await LoadLookupsAsync();
        }
    }
} 