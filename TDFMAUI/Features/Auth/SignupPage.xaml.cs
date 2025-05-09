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

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // Log that the page appeared
        Debug.WriteLine("[SignupPage] OnAppearing");
        
        // Explicitly load data when page appears (ensure ViewModel is not null)
        // Removed explicit call to LoadDepartmentsCommand as ViewModel handles this in constructor.
        // if (_viewModel != null)
        // {
        //     Debug.WriteLine("[SignupPage] OnAppearing - ViewModel is available, attempting to load departments");
        //     try
        //     {
        //         // Use the command if available
        //         if (_viewModel.LoadDepartmentsCommand?.CanExecute(null) ?? false)
        //         {
        //             Debug.WriteLine("[SignupPage] OnAppearing - Executing LoadDepartmentsCommand");
        //             _viewModel.LoadDepartmentsCommand.Execute(null);
        //         }
        //         else
        //         {
        //             Debug.WriteLine("[SignupPage] OnAppearing - LoadDepartmentsCommand is null or cannot execute. Check ViewModel state.");
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         Debug.WriteLine($"[SignupPage] OnAppearing - Error executing LoadDepartmentsCommand: {ex.Message}");
        //     }
        // }
        // else
        // {
        //     Debug.WriteLine("[SignupPage] OnAppearing - ViewModel is NULL, cannot load departments");
        //     // Optionally display an error to the user here
        //     DisplayAlert("Error", "ViewModel not available. Cannot load signup data.", "OK");
        // }
    }

    // All logic, properties, and event handling methods previously here
    // have been moved to the SignupViewModel to adhere to MVVM.
} 