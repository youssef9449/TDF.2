using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using TDFMAUI.Features.Requests;
using TDFMAUI.Services;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Users;
using TDFShared.Enums;
using TDFShared.Services;

namespace TDFMAUI.ViewModels
{
    [QueryProperty(nameof(RequestId), "RequestId")]
    public partial class RequestDetailsViewModel : ObservableObject
    {
        private readonly TDFMAUI.Services.IApiService _apiService;
        private readonly TDFShared.Services.IAuthService _authService;
        private readonly ILogger<RequestDetailsViewModel> _logger;

        [ObservableProperty]
        private int _requestId;

        [ObservableProperty]
        private RequestResponseDto _request;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotLoading))]
        private bool _isLoading;

        public bool IsNotLoading => !IsLoading;

        [ObservableProperty] private bool _canApprove;
        [ObservableProperty] private bool _canReject;
        [ObservableProperty] private bool _canEdit;
        [ObservableProperty] private bool _canDelete;

        public RequestDetailsViewModel(TDFMAUI.Services.IApiService apiService, TDFShared.Services.IAuthService authService, ILogger<RequestDetailsViewModel> logger)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Initialize()
        {
            await LoadRequestDetailsAsync();
        }

        partial void OnRequestIdChanged(int value)
        {
            if (value > 0)
            {
                Task.Run(async () => await LoadRequestDetailsAsync());
            }
        }

        [RelayCommand]
        private async Task LoadRequestDetailsAsync()
        {
            if (RequestId <= 0) return;

            IsLoading = true;
            try
            {
                Request = await _apiService.GetRequestByIdAsync(RequestId);
                if (Request != null)
                {
                    // Validate if user can view this request using RequestStateManager
                    var currentUser = await _authService.GetCurrentUserAsync();
                    if (currentUser != null)
                    {
                        var userDto = new UserDto
                        {
                            UserID = currentUser.UserID,
                            IsAdmin = currentUser.IsAdmin,
                            IsHR = currentUser.IsHR,
                            IsManager = currentUser.IsManager,
                            Department = currentUser.Department
                        };

                        if (!RequestStateManager.CanViewRequest(Request, userDto))
                        {
                            await Shell.Current.DisplayAlert("Access Denied", "You do not have permission to view this request.", "OK");
                            await Shell.Current.GoToAsync("..");
                            return;
                        }
                    }

                    SetActionVisibility();
                }
                else
                {
                    await Shell.Current.DisplayAlert("Error", "Could not load request details.", "OK");
                    await Shell.Current.GoToAsync("..");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load request details for ID {RequestId}", RequestId);
                await Shell.Current.DisplayAlert("Error", $"Failed to load request details: {ex.Message}", "OK");
                await Shell.Current.GoToAsync("..");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void SetActionVisibility()
        {
            if (Request == null)
            {
                CanApprove = CanReject = CanEdit = CanDelete = false;
                return;
            }

            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser == null)
            {
                CanApprove = CanReject = CanEdit = CanDelete = false;
                return;
            }

            // Convert to UserDto for RequestStateManager
            var userDto = new UserDto
            {
                UserID = currentUser.UserID,
                IsAdmin = currentUser.IsAdmin,
                IsHR = currentUser.IsHR,
                IsManager = currentUser.IsManager,
                Department = currentUser.Department
            };

            bool isOwner = Request.RequestUserID == currentUser.UserID;

            // Use RequestStateManager for consistent authorization logic
            CanEdit = RequestStateManager.CanEdit(Request, userDto.IsAdmin, isOwner);
            CanDelete = RequestStateManager.CanDelete(Request, userDto.IsAdmin, isOwner);
            CanApprove = RequestStateManager.CanApproveOrRejectRequest(Request, userDto);
            CanReject = RequestStateManager.CanApproveOrRejectRequest(Request, userDto);
        }

        [RelayCommand]
        private async Task EditRequestAsync()
        {
            if (Request == null || !CanEdit) return;

            await Shell.Current.GoToAsync($"{nameof(AddRequestPage)}", new Dictionary<string, object>
            {
                {"ExistingRequest", Request}
            });
        }

        [RelayCommand]
        private async Task DeleteRequestAsync()
        {
            if (Request == null || !CanDelete) return;

            bool confirm = await Shell.Current.DisplayAlert("Confirm Delete", "Are you sure you want to delete this request?", "Yes", "No");
            if (confirm)
            {
                IsLoading = true;
                try
                {
                    await _apiService.DeleteRequestAsync(Request.RequestID);
                    await Shell.Current.DisplayAlert("Success", "Request deleted successfully.", "OK");
                    await Shell.Current.GoToAsync("..");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete request {RequestId}", Request.RequestID);
                    await Shell.Current.DisplayAlert("Error", $"Failed to delete request: {ex.Message}", "OK");
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        [RelayCommand]
        private async Task ApproveRequestAsync()
        {
            if (Request == null || !CanApprove) return;

            string comment = await Shell.Current.DisplayPromptAsync("Approve Request", "Optional comments:", "OK", "Cancel", keyboard: Keyboard.Text);
            if (comment == null) return; // User cancelled prompt

            IsLoading = true;
            try
            {
                var approvalDto = new RequestApprovalDto { Comment = comment };
                bool success = await _apiService.ApproveRequestAsync(Request.RequestID, approvalDto);

                if (success)
                {
                    await Shell.Current.DisplayAlert("Success", "Request approved.", "OK");
                    await LoadRequestDetailsAsync();
                }
                else
                {
                    await Shell.Current.DisplayAlert("Error", "Failed to approve request.", "OK");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to approve request {RequestId}", Request.RequestID);
                await Shell.Current.DisplayAlert("Error", $"Failed to approve request: {ex.Message}", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RejectRequestAsync()
        {
            if (Request == null || !CanReject) return;

            string reason = await Shell.Current.DisplayPromptAsync("Reject Request", "Reason for rejection:", "OK", "Cancel", keyboard: Keyboard.Text);
            if (string.IsNullOrWhiteSpace(reason))
            {
                if (reason != null) // Only show error if they didn't press Cancel
                    await Shell.Current.DisplayAlert("Validation Error", "Rejection reason is required.", "OK");
                return;
            }

            IsLoading = true;
            try
            {
                var rejectDto = new RequestRejectDto { RejectReason = reason };
                bool success = await _apiService.RejectRequestAsync(Request.RequestID, rejectDto);

                if (success)
                {
                    await Shell.Current.DisplayAlert("Success", "Request rejected.", "OK");
                    await LoadRequestDetailsAsync();
                }
                else
                {
                    await Shell.Current.DisplayAlert("Error", "Failed to reject request.", "OK");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reject request {RequestId}", Request.RequestID);
                await Shell.Current.DisplayAlert("Error", $"Failed to reject request: {ex.Message}", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task BackAsync()
        {
            await Shell.Current.GoToAsync("..");
        }


    }
}