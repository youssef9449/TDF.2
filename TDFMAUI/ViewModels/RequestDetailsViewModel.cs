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

namespace TDFMAUI.ViewModels
{
    [QueryProperty(nameof(RequestId), "RequestId")]
    public partial class RequestDetailsViewModel : ObservableObject
    {
        private readonly IApiService _apiService;
        private readonly IAuthService _authService;
        private readonly ILogger<RequestDetailsViewModel> _logger;

        [ObservableProperty] 
        private Guid _requestId;

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

        public RequestDetailsViewModel(IApiService apiService, IAuthService authService, ILogger<RequestDetailsViewModel> logger)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        public async Task Initialize()
        {
            await LoadRequestDetailsAsync();
        }
        
        partial void OnRequestIdChanged(Guid value)
        {
            if (value != Guid.Empty)
            {
                Task.Run(async () => await LoadRequestDetailsAsync());
            }
        }

        [RelayCommand]
        private async Task LoadRequestDetailsAsync()
        {
            if (RequestId == Guid.Empty) return;
            
            IsLoading = true;
            try
            {
                Request = await _apiService.GetRequestByIdAsync(RequestId);
                if (Request != null)
                {
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

            bool isOwner = Request.RequestUserID == currentUser.UserID;
            bool isAdmin = currentUser.Roles.Contains("Admin");
            bool isHR = currentUser.Roles.Contains("HR");
            bool isManager = currentUser.Roles.Contains("Manager");
            bool isManagerOfRequestDept = isManager && currentUser.Department == Request.RequestDepartment;

            // Edit/Delete Visibility (Owner only, if pending)
            bool isPending = Request.Status == "Pending";
            CanEdit = isOwner && isPending;
            CanDelete = isOwner && isPending;

            // Approval Visibility
            CanApprove = (isAdmin || isManagerOfRequestDept || isHR) && isPending;

            // Rejection Visibility
            CanReject = (isAdmin || isManagerOfRequestDept || isHR) && isPending;
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
                    await _apiService.DeleteRequestAsync(Request.Id);
                    await Shell.Current.DisplayAlert("Success", "Request deleted successfully.", "OK");
                    await Shell.Current.GoToAsync("..");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete request {RequestId}", Request.Id);
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
                bool success = await _apiService.ApproveRequestAsync(Request.Id, approvalDto);
                
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
                _logger.LogError(ex, "Failed to approve request {RequestId}", Request.Id);
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
                bool success = await _apiService.RejectRequestAsync(Request.Id, rejectDto);
                
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
                _logger.LogError(ex, "Failed to reject request {RequestId}", Request.Id);
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