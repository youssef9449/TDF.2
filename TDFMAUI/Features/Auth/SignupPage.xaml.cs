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
        Debug.WriteLine($"[SignupPage] Constructor called. ViewModel null? {viewModel == null}");
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        BindingContext = _viewModel;
        Debug.WriteLine("[SignupPage] DI constructor - ViewModel assigned successfully");
        // Subscribe to window maximization changes
        Helpers.DeviceHelper.WindowMaximizationChanged += OnWindowMaximizationChanged;
    }
    
    private void OnWindowMaximizationChanged(object sender, bool isMaximized)
    {
        // Window maximization changes are now handled by the responsive layout
        // No need for manual adjustments
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

        // The ViewModel handles department loading in its constructor.
        // No need to explicitly load departments here unless there's a specific refresh requirement.
    }
    
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Unsubscribe from events to prevent memory leaks
        Helpers.DeviceHelper.WindowMaximizationChanged -= OnWindowMaximizationChanged;
    }

    // All logic, properties, and event handling methods previously here
    // have been moved to the SignupViewModel to adhere to MVVM.
}