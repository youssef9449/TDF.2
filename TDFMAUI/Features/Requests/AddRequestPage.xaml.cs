using System;
using TDFMAUI.ViewModels;
using Microsoft.Maui.Controls;
using TDFShared.DTOs.Requests;

namespace TDFMAUI.Features.Requests;

[QueryProperty(nameof(ExistingRequestDto), "ExistingRequest")]
public partial class AddRequestPage : ContentPage
{
    private RequestResponseDto _existingRequestDto;

    // Property to receive the navigation parameter
    public RequestResponseDto ExistingRequestDto
    {
        get => _existingRequestDto;
        set
        {
            _existingRequestDto = value;
            // The ViewModel should handle initialization based on this DTO
            // when it's passed during construction or via a method.
            // Re-evaluate if explicit ViewModel update is needed here if DI setup changes.
             if (BindingContext is AddRequestViewModel vm)
             {
                  // Example: Consider adding an Initialize method to the ViewModel
                  // if the existingRequest needs processing *after* construction.
                  // vm.InitializeWithRequest(_existingRequestDto);
             }
            OnPropertyChanged(); 
        }
    }

    // ViewModel is injected via constructor
    public AddRequestPage(AddRequestViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        // The ViewModel constructor now handles initializing state based on existingRequest 
        // (if passed correctly via DI or navigation parameter handling)
    }

    // Removed OnAppearing - ViewModel initialization is handled in constructor
    // Removed SetupFormControls - Handled by XAML bindings and OnIdiom
    // Removed InitializeFormValues - Handled by XAML bindings and ViewModel constructor
    // Removed OnSaveClicked - Logic moved to AddRequestViewModel.SubmitRequestCommand
    // Removed UpdateViewModelFromControls - Handled by two-way XAML bindings
    // Removed OnSubmitClicked - Button should use Command binding in XAML
    // Removed OnCancelClicked - Button should use Command binding in XAML
}