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
        private string _FullName;

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
        }

        private async Task LoadLookupsAsync()
        {
            Debug.WriteLine("[SignupViewModel] LoadLookupsAsync - Start");
            _logger?.LogInformation("DIAGNOSTIC: LoadLookupsAsync - Starting to load departments");

            try
            {
                // Load departments directly from API service to bypass LookupService
                Debug.WriteLine("[SignupViewModel] Loading departments directly from API");
                _logger?.LogInformation("DIAGNOSTIC: Loading departments directly from API");

                List<LookupItem> departments = null;

                try
                {
                    Debug.WriteLine("[SignupViewModel] Calling departments API endpoint directly");
                    _logger?.LogInformation("DIAGNOSTIC: Calling departments API endpoint directly using ApiRoutes");


                    // Use ApiRoutes directly like the LookupService does
                    departments = await _apiService.GetAsync<List<LookupItem>>(ApiRoutes.Lookups.GetDepartments);

                    // Debug the response details
                    Debug.WriteLine($"[SignupViewModel] Departments received. Null: {departments == null}");
                    if (departments == null)
                    {
                        Debug.WriteLine("[SignupViewModel] API returned null departments list");
                        _logger?.LogError("API returned null departments list");
                        throw new InvalidOperationException("No departments received from the server");
                    }

                    Debug.WriteLine($"[SignupViewModel] Departments count: {departments.Count}");
                    _logger?.LogInformation("DIAGNOSTIC: Departments count: {Count}", departments.Count);

                    if (departments.Count > 0)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            Departments = new ObservableCollection<LookupItem>(departments);
                            HasError = false;
                            ErrorMessage = string.Empty;
                        });
                    }
                    else
                    {
                        Debug.WriteLine("[SignupViewModel] Failed to load departments - list is null or empty.");
                        _logger?.LogError("DIAGNOSTIC: Failed to load departments. Departments list is null or empty. Count: {Count}", departments.Count);
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            Departments.Clear();
                            ErrorMessage = "No departments found. Please contact support.";
                            HasError = true;
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SignupViewModel] Error calling departments API: {ex.Message}");
                    Debug.WriteLine($"[SignupViewModel] Exception details: {ex}");
                    _logger?.LogError(ex, "DIAGNOSTIC: Error calling departments API: {Message}. Full exception: {Exception}", ex.Message, ex.ToString());

                    // Set departments to empty list to ensure we handle the error properly
                    departments = new List<LookupItem>();
                }

                // Check if departments were loaded successfully
                if (departments == null || !departments.Any())
                {
                    Debug.WriteLine("[SignupViewModel] Failed to load departments - list is null or empty.");
                    _logger?.LogError("DIAGNOSTIC: Failed to load departments. Departments list is null or empty. Count: {Count}", departments?.Count ?? -1);

                    await MainThread.InvokeOnMainThreadAsync(() => {
                        ErrorMessage = "Unable to load departments. Please check your connection and try again.";
                        HasError = true;
                        Departments.Clear();
                    });
                    return;
                }

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
                Debug.WriteLine("[SignupViewModel] LoadLookupsAsync - End");
                _logger?.LogInformation("DIAGNOSTIC: LoadLookupsAsync - End");
            }
        }

        partial void OnSelectedDepartmentChanged(LookupItem value)
        {
            if (value != null)
            {
                _logger?.LogInformation("Department selected: Name={Name}", value.Name);
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

                // Use Name property for department name
                var departmentName = SelectedDepartment.Name;
                _logger?.LogInformation("Loading titles for department: {DepartmentName}", departmentName);

                var titlesForDepartment = await _lookupService.GetTitlesForDepartmentAsync(departmentName);
                if (titlesForDepartment != null && titlesForDepartment.Any())
                {
                    foreach (var title in titlesForDepartment)
                    {
                        Titles.Add(title);
                    }
                    _logger?.LogInformation("Loaded {Count} titles for department {DepartmentName}", titlesForDepartment.Count, departmentName);

                    // Auto-select first title if available
                    if (Titles.Count > 0)
                    {
                        SelectedTitle = Titles[0];
                    }
                }
                else
                {
                    _logger?.LogWarning("No titles found for department: {DepartmentName}", departmentName);
                    ErrorMessage = $"No titles found for the selected department.";
                    HasError = true;
                }
            }
            catch (Exception ex)
            {
                var deptName = SelectedDepartment?.Name ?? "unknown";
                _logger?.LogError(ex, "Error loading titles for department {DepartmentName}", deptName);
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
                    if (Shell.Current != null)
                    {
                        await Shell.Current.GoToAsync("//LoginPage");
                    }
                    else
                    {
                        _logger?.LogWarning("Shell.Current is null. Attempting fallback navigation to LoginPage.");
                        try
                        {
                            var loginPage = App.Services?.GetService<LoginPage>();
                            if (loginPage != null)
                            {
                                // Reset the navigation stack to LoginPage
                                if (Application.Current?.MainPage?.Navigation != null)
                                {
                                    // Clear existing modal pages first if any, then reset main page
                                    if (Application.Current.MainPage.Navigation.ModalStack.Any())
                                    {
                                        await Application.Current.MainPage.Navigation.PopModalAsync(false); // No animation
                                    }
                                    Application.Current.MainPage = new NavigationPage(loginPage);
                                }
                                else if (Application.Current != null)
                                {
                                     Application.Current.MainPage = new NavigationPage(loginPage);
                                }
                                else
                                {
                                    throw new InvalidOperationException("Application.Current is null.");
                                }
                            }
                            else
                            {
                                throw new InvalidOperationException("LoginPage could not be resolved from DI container.");
                            }
                        }
                        catch (Exception navEx)
                        {
                            _logger?.LogError(navEx, "Fallback navigation to LoginPage failed.");
                            ErrorMessage = "Signup successful, but automatic navigation to login failed. Please restart the app or navigate manually.";
                            HasError = true;
                        }
                    }
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
                    if (!string.IsNullOrWhiteSpace(apiEx.ResponseContent) && apiEx.ResponseContent.Length < 500)
                    {
                        try
                        {
                            var errorResponse = TDFShared.Helpers.JsonSerializationHelper.Deserialize<TDFShared.DTOs.Common.ApiResponse<object>>(apiEx.ResponseContent);
                            if (errorResponse != null)
                            {
                                if (errorResponse.Errors != null && errorResponse.Errors.Any())
                                {
                                    // Show all validation errors, one per line, and prepend a user-friendly label
                                    apiErrorMessage = string.Join("\n", errorResponse.Errors.SelectMany(kv => $"{ToFriendlyFieldName(kv.Key)}: {string.Join(", ", kv.Value)}"));
                                }
                                else if (!string.IsNullOrWhiteSpace(errorResponse.Message))
                                {
                                    apiErrorMessage = errorResponse.Message;
                                }
                            }
                        }
                        catch { /* Ignore if not a standard ApiResponse format */ }
                    }
                    ErrorMessage = apiErrorMessage;
                }
                else
                {
                    ErrorMessage = $"An unexpected error occurred. Please check your network and try again.";
                }
                HasError = true;
            }
        }

        // Helper to convert backend field names to user-friendly labels
        private string ToFriendlyFieldName(string field)
        {
            return field switch
            {
                "Username" => "Username",
                "username" => "Username",
                "Password" => "Password",
                "password" => "Password",
                "FullName" => "Full Name",
                "fullName" => "Full Name",
                "Department" => "Department",
                "department" => "Department",
                "Title" => "Title",
                "title" => "Title",
                _ => field
            };
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