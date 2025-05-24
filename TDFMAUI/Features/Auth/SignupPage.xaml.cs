using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using TDFMAUI.Services;
using TDFShared.DTOs.Common; // For LookupItem
using TDFMAUI.Features.Auth; // Added for LoginPage reference
using System.Diagnostics; // Add Diagnostics

namespace TDFMAUI.Features.Auth;

public partial class SignupPage : ContentPage
{
    private readonly SignupViewModel _viewModel;

    // Constructor for DI (preferred way)
    public SignupPage(SignupViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        BindingContext = _viewModel;
        Debug.WriteLine("[SignupPage] DI constructor - ViewModel assigned successfully");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Log that the page appeared
        Debug.WriteLine("[SignupPage] OnAppearing");

        // Check if ViewModel is available
        if (_viewModel == null)
        {
            Debug.WriteLine("[SignupPage] OnAppearing - ViewModel is NULL, cannot load departments");
            await DisplayAlert("Error", "Application error: ViewModel not available. Please restart the app.", "OK");
            return;
        }

        Debug.WriteLine($"[SignupPage] OnAppearing - ViewModel is available, Departments count: {_viewModel.Departments?.Count ?? 0}");

        // Check if departments are already loaded
        if (_viewModel.Departments?.Any() == true)
        {
            Debug.WriteLine("[SignupPage] OnAppearing - Departments already loaded");
            return;
        }

        // Load departments
        Debug.WriteLine("[SignupPage] OnAppearing - Loading departments");

        try 
        {        
            // Clear any previous errors
            _viewModel.HasError = false;
            _viewModel.ErrorMessage = string.Empty;
            
            // Execute the load command
            if (_viewModel.LoadDepartmentsCommand.CanExecute(null))
            {
                Debug.WriteLine("[SignupPage] OnAppearing - Executing LoadDepartmentsCommand");
                await _viewModel.LoadDepartmentsCommand.ExecuteAsync(null);
                Debug.WriteLine($"[SignupPage] OnAppearing - LoadDepartmentsCommand completed. Departments loaded: {_viewModel.Departments?.Count ?? 0}");
                
                // If still no departments, show an error
                if (!_viewModel.Departments?.Any() == true) {
                    _viewModel.ErrorMessage = "Failed to load departments. Please check your connection and try again.";
                    _viewModel.HasError = true;
                    Debug.WriteLine("[SignupPage] OnAppearing - No departments were loaded");
                }
            }
            else
            {
                Debug.WriteLine("[SignupPage] OnAppearing - LoadDepartmentsCommand cannot execute at this time");
                _viewModel.ErrorMessage = "Cannot load departments at this time. Please try again later.";
                _viewModel.HasError = true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SignupPage] OnAppearing - Error loading departments: {ex.Message}");
            Debug.WriteLine($"[SignupPage] OnAppearing - Exception details: {ex}");
            
            _viewModel.ErrorMessage = "An error occurred while loading departments. Please check your connection and try again.";
            _viewModel.HasError = true;
        }
    }

    // All logic, properties, and event handling methods previously here
    // have been moved to the SignupViewModel to adhere to MVVM.
}