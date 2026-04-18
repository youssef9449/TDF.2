using System;
using TDFMAUI.ViewModels;
using Microsoft.Maui.Controls;
using TDFShared.DTOs.Requests;
using TDFMAUI.Helpers;

namespace TDFMAUI.Features.Requests;

[QueryProperty(nameof(ExistingRequestDto), "ExistingRequest")]
public partial class AddRequestPage : ContentPage
{
    private RequestResponseDto? _existingRequestDto;

    /// <summary>
    /// Navigation parameter: the request to edit. When set, the bound
    /// <see cref="AddRequestViewModel"/> is switched to edit mode.
    /// </summary>
    public RequestResponseDto? ExistingRequestDto
    {
        get => _existingRequestDto;
        set
        {
            _existingRequestDto = value;
            if (BindingContext is AddRequestViewModel vm)
            {
                vm.InitializeWithRequest(value);
            }
            OnPropertyChanged();
        }
    }

    // ViewModel is injected via constructor
    public AddRequestPage(AddRequestViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // If the query parameter arrived before the BindingContext was set,
        // propagate it now.
        if (_existingRequestDto != null)
        {
            viewModel.InitializeWithRequest(_existingRequestDto);
        }
    }
}
