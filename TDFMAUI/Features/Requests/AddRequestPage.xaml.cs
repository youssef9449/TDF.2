using System;
using TDFMAUI.ViewModels;
using Microsoft.Maui.Controls;
using TDFShared.DTOs.Requests;
using TDFMAUI.Helpers;

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
        
        // Configure UI for current device
        ConfigureForCurrentDevice();
        
        // Listen for size changes to reconfigure UI
        SizeChanged += OnPageSizeChanged;
    }
    
    protected override void OnAppearing()
    {
        base.OnAppearing();
        ConfigureForCurrentDevice();
        
        // Initialize the view model if needed
        if (BindingContext is AddRequestViewModel viewModel)
        {
            // Ensure time pickers are initialized for Permission/External Assignment
            if (viewModel.SelectedLeaveType == "Permission" || viewModel.SelectedLeaveType == "ExternalAssignment")
            {
                if (!viewModel.StartTime.HasValue)
                    viewModel.StartTime = new TimeSpan(9, 0, 0); // Default to 9:00 AM
                
                if (!viewModel.EndTime.HasValue)
                    viewModel.EndTime = new TimeSpan(17, 0, 0); // Default to 5:00 PM
            }
        }
    }
    
    private void OnPageSizeChanged(object sender, EventArgs e)
    {
        ConfigureForCurrentDevice();
    }
    
    private void ConfigureForCurrentDevice()
    {
        bool isDesktop = DeviceHelper.IsDesktop;
        bool isMobile = !isDesktop && (DeviceHelper.IsAndroid || DeviceHelper.IsIOS);

        if (isDesktop)
        {
            VisualStateManager.GoToState(this, "Desktop");
            VisualStateManager.GoToState(FormFrame, "Desktop");
        }
        else if (isMobile)
        {
            VisualStateManager.GoToState(this, "Mobile");
            VisualStateManager.GoToState(FormFrame, "Mobile");
        }
        else
        {
            // Default state for other platforms
            VisualStateManager.GoToState(this, "Mobile");
            VisualStateManager.GoToState(FormFrame, "Mobile");
        }
    }
}