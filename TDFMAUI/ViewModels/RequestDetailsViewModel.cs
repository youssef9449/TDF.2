using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TDFMAUI.Features.Requests;
using TDFMAUI.Services;
using TDFShared.DTOs.Common;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Users;
using TDFShared.Enums;
using TDFShared.Services;
using TDFShared.Utilities;

namespace TDFMAUI.ViewModels
{
    [QueryProperty(nameof(RequestId), "RequestId")]
    public partial class RequestDetailsViewModel : BaseViewModel
    {
        private readonly IRequestApiService _requestApiService;
        private readonly IAuthService _authService;
        private readonly ILogger<RequestDetailsViewModel> _logger;

        [ObservableProperty]
        private int _requestId;

        [ObservableProperty]
        private RequestResponseDto? _request;

        [ObservableProperty] private bool _canApprove;
        [ObservableProperty] private bool _canReject;
        [ObservableProperty] private bool _canEdit;
        [ObservableProperty] private bool _canDelete;

        public RequestDetailsViewModel(IRequestApiService requestApiService, IAuthService authService, ILogger<RequestDetailsViewModel> logger)
        {
            _requestApiService = requestApiService;
            _authService = authService;
            _logger = logger;
            Title = "Request Details";
        }

        public async Task Initialize() => await LoadRequestDetailsAsync();

        partial void OnRequestIdChanged(int value)
        {
            if (value > 0) _ = LoadRequestDetailsAsync();
        }

        [RelayCommand]
        private async Task LoadRequestDetailsAsync()
        {
            if (RequestId <= 0) return;
            IsBusy = true;
            try
            {
                var response = await _requestApiService.GetRequestByIdAsync(RequestId);
                if (response?.Data != null)
                {
                    Request = response.Data;
                    var currentUser = await _authService.GetCurrentUserAsync();
                    if (currentUser != null && !RequestStateManager.CanViewRequest(Request, currentUser))
                    {
                        await Shell.Current.DisplayAlert("Access Denied", "You do not have permission to view this request.", "OK");
                        await Shell.Current.GoToAsync("..");
                        return;
                    }
                    SetActionVisibility(currentUser);
                }
                else
                {
                    ErrorMessage = "Could not load request details.";
                    await Shell.Current.GoToAsync("..");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load request details.");
                ErrorMessage = "Error loading request details.";
                await Shell.Current.GoToAsync("..");
            }
            finally { IsBusy = false; }
        }

        private void SetActionVisibility(UserDto? currentUser)
        {
            if (Request == null || currentUser == null)
            {
                CanApprove = CanReject = CanEdit = CanDelete = false;
                return;
            }

            bool isOwner = Request.RequestUserID == currentUser.UserID;
            CanEdit = RequestStateManager.CanEdit(Request, currentUser.IsAdmin ?? false, isOwner);
            CanDelete = RequestStateManager.CanDelete(Request, currentUser.IsAdmin ?? false, isOwner);
            CanApprove = AuthorizationUtilities.CanPerformRequestAction(currentUser, Request, RequestAction.Approve);
            CanReject = AuthorizationUtilities.CanPerformRequestAction(currentUser, Request, RequestAction.Reject);
        }

        [RelayCommand]
        private async Task EditRequestAsync()
        {
            if (Request != null && CanEdit) await Shell.Current.GoToAsync($"{nameof(AddRequestPage)}", new Dictionary<string, object> { {"ExistingRequest", Request} });
        }

        [RelayCommand]
        private async Task DeleteRequestAsync()
        {
            if (Request == null || !CanDelete) return;
            if (await Shell.Current.DisplayAlert("Confirm Delete", "Are you sure?", "Yes", "No"))
            {
                IsBusy = true;
                try
                {
                    await _requestApiService.DeleteRequestAsync(Request.RequestID);
                    await Shell.Current.GoToAsync("..");
                }
                catch (Exception ex) { _logger.LogError(ex, "Failed to delete"); ErrorMessage = "Delete failed."; }
                finally { IsBusy = false; }
            }
        }

        [RelayCommand]
        private async Task ApproveRequestAsync()
        {
            if (Request == null || !CanApprove) return;
            string comment = await Shell.Current.DisplayPromptAsync("Approve", "Optional comments:", "OK", "Cancel");
            if (comment == null) return;

            IsBusy = true;
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                ApiResponse<bool>? response = null;
                if (currentUser?.IsManager == true) response = await _requestApiService.ManagerApproveRequestAsync(Request.RequestID, new ManagerApprovalDto { ManagerRemarks = comment });
                else if (currentUser?.IsHR == true) response = await _requestApiService.HRApproveRequestAsync(Request.RequestID, new HRApprovalDto { HRRemarks = comment });

                if (response?.Success == true) await LoadRequestDetailsAsync();
            }
            catch (Exception ex) { _logger.LogError(ex, "Approval failed"); }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task RejectRequestAsync()
        {
            if (Request == null || !CanReject) return;
            string reason = await Shell.Current.DisplayPromptAsync("Reject", "Reason for rejection:", "OK", "Cancel");
            if (string.IsNullOrWhiteSpace(reason)) return;

            IsBusy = true;
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                ApiResponse<bool>? response = null;
                if (currentUser?.IsManager == true) response = await _requestApiService.ManagerRejectRequestAsync(Request.RequestID, new ManagerRejectDto { ManagerRemarks = reason });
                else if (currentUser?.IsHR == true) response = await _requestApiService.HRRejectRequestAsync(Request.RequestID, new HRRejectDto { HRRemarks = reason });

                if (response?.Success == true) await LoadRequestDetailsAsync();
            }
            catch (Exception ex) { _logger.LogError(ex, "Rejection failed"); }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task BackAsync() => await Shell.Current.GoToAsync("..");
    }
}
