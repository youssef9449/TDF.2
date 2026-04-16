using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TDFMAUI.Services;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Users;
using TDFShared.Enums;
using TDFShared.Exceptions;
using TDFShared.Services;
using TDFShared.Utilities;

namespace TDFMAUI.ViewModels
{
    public partial class AddRequestViewModel : BaseViewModel
    {
        private readonly IRequestService _requestService;
        private readonly IAuthService _authService;
        private readonly ILogger<AddRequestViewModel> _logger;
        private readonly TDFShared.Validation.IValidationService _validationService;
        private readonly TDFShared.Validation.IBusinessRulesService _businessRulesService;

        [ObservableProperty]
        private bool _isEditMode;

        [ObservableProperty]
        private int _requestId;

        [ObservableProperty]
        private string _selectedLeaveType = string.Empty;

        partial void OnSelectedLeaveTypeChanged(string value)
        {
            // Initialize time values for Permission and External Assignment
            if (value == "Permission" || value == "External Assignment")
            {
                if (!StartTime.HasValue)
                    StartTime = new TimeSpan(9, 0, 0); // Default to 9:00 AM

                if (!EndTime.HasValue)
                    EndTime = new TimeSpan(17, 0, 0); // Default to 5:00 PM

                // For these types, EndDate should always equal StartDate
                EndDate = StartDate;
            }

            OnPropertyChanged(nameof(RequestCreateDto));
            OnPropertyChanged(nameof(RequestUpdateDto));
        }

        [ObservableProperty]
        private ObservableCollection<string> _leaveTypes = new();

        [ObservableProperty]
        private string _requestReason = string.Empty;

        [ObservableProperty]
        private DateTime _startDate = DateTime.Today;

        partial void OnStartDateChanged(DateTime value)
        {
            // Prevent setting weekend dates
            if (IsWeekend(value))
            {
                Shell.Current.DisplayAlert("Invalid Date", "Start date cannot be on a weekend (Friday or Saturday). Please select a working day.", "OK");
                return;
            }

            // Ensure EndDate is not before StartDate
            if (EndDate.HasValue && EndDate.Value < value)
            {
                EndDate = value;
            }

            // For Permission and External Assignment, EndDate should always equal StartDate
            if (SelectedLeaveType == "Permission" || SelectedLeaveType == "External Assignment")
            {
                EndDate = value;
            }
        }

        [ObservableProperty]
        private DateTime? _endDate = DateTime.Today;

        partial void OnEndDateChanged(DateTime? value)
        {
            if (value.HasValue && value.Value.Date < StartDate.Date)
            {
                // Optionally adjust or handle invalid range
            }
        }

        [ObservableProperty]
        private TimeSpan? _startTime;

        [ObservableProperty]
        private TimeSpan? _endTime;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasErrors))]
        private List<string> _validationErrors = new();

        public bool HasErrors => ValidationErrors.Any();

        [ObservableProperty]
        private int _currentUserId;

        [ObservableProperty]
        private DateTime _minDate = DateTime.Today;

        [ObservableProperty]
        private DateTime _maxDate = DateTime.Today.AddYears(1);

        public RequestCreateDto RequestCreateDto
        {
            get
            {
                LeaveType leaveType = MapToLeaveType(SelectedLeaveType);
                if (leaveType == (LeaveType)(-1)) return new RequestCreateDto();

                return new RequestCreateDto
                {
                    LeaveType = leaveType,
                    RequestStartDate = StartDate,
                    RequestEndDate = (leaveType == LeaveType.Permission || leaveType == LeaveType.ExternalAssignment) ? StartDate : EndDate ?? StartDate,
                    RequestBeginningTime = (leaveType == LeaveType.Permission || leaveType == LeaveType.ExternalAssignment) ? StartTime : null,
                    RequestEndingTime = (leaveType == LeaveType.Permission || leaveType == LeaveType.ExternalAssignment) ? EndTime : null,
                    RequestReason = RequestReason ?? string.Empty,
                    UserId = CurrentUserId
                };
            }
        }

        public RequestUpdateDto RequestUpdateDto
        {
            get
            {
                LeaveType leaveType = MapToLeaveType(SelectedLeaveType);
                if (leaveType == (LeaveType)(-1)) return new RequestUpdateDto();

                return new RequestUpdateDto
                {
                    LeaveType = leaveType,
                    RequestStartDate = StartDate,
                    RequestEndDate = (leaveType == LeaveType.Permission || leaveType == LeaveType.ExternalAssignment) ? StartDate : EndDate ?? StartDate,
                    RequestBeginningTime = (leaveType == LeaveType.Permission || leaveType == LeaveType.ExternalAssignment) ? StartTime : null,
                    RequestEndingTime = (leaveType == LeaveType.Permission || leaveType == LeaveType.ExternalAssignment) ? EndTime : null,
                    RequestReason = RequestReason ?? string.Empty
                };
            }
        }

        public AddRequestViewModel(
            IRequestService requestService,
            IAuthService authService,
            ILogger<AddRequestViewModel> logger,
            TDFShared.Validation.IValidationService validationService,
            TDFShared.Validation.IBusinessRulesService businessRulesService,
            RequestResponseDto? existingRequest = null)
        {
            _requestService = requestService ?? throw new ArgumentNullException(nameof(requestService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _businessRulesService = businessRulesService ?? throw new ArgumentNullException(nameof(businessRulesService));

            LoadLeaveTypes();

            if (existingRequest != null)
            {
                IsEditMode = true;
                RequestId = existingRequest.RequestID;
                SelectedLeaveType = MapFromLeaveType(existingRequest.LeaveType);
                RequestReason = existingRequest.RequestReason;
                StartDate = existingRequest.RequestStartDate;
                EndDate = existingRequest.RequestEndDate;

                if (existingRequest.LeaveType == LeaveType.Permission || existingRequest.LeaveType == LeaveType.ExternalAssignment)
                {
                    StartTime = existingRequest.RequestBeginningTime;
                    EndTime = existingRequest.RequestEndingTime;
                }

                _ = ValidateEditAccessAsync(existingRequest);
            }
            else
            {
                IsEditMode = false;
                RequestId = 0;
                StartDate = DateTime.Today;
                EndDate = DateTime.Today;
            }
        }

        private void LoadLeaveTypes()
        {
            LeaveTypes.Clear();
            foreach (LeaveType type in Enum.GetValues(typeof(LeaveType)))
            {
                LeaveTypes.Add(MapFromLeaveType(type));
            }
            SelectedLeaveType = LeaveTypes.FirstOrDefault() ?? string.Empty;
        }

        private LeaveType MapToLeaveType(string displayName)
        {
            if (displayName == "Work From Home") return LeaveType.WorkFromHome;
            if (displayName == "External Assignment") return LeaveType.ExternalAssignment;
            if (Enum.TryParse(displayName, true, out LeaveType result)) return result;
            return (LeaveType)(-1);
        }

        private string MapFromLeaveType(LeaveType type)
        {
            return type switch
            {
                LeaveType.WorkFromHome => "Work From Home",
                LeaveType.ExternalAssignment => "External Assignment",
                _ => type.ToString()
            };
        }

        [RelayCommand(CanExecute = nameof(IsNotBusy))]
        private async Task SubmitAsync()
        {
            IsBusy = true;
            try
            {
                CurrentUserId = await _authService.GetCurrentUserIdAsync();
                if (CurrentUserId == 0)
                {
                    await Shell.Current.DisplayAlert("Authentication Error", "Could not verify user identity. Please log in again.", "OK");
                    return;
                }

                // Enhanced Validation using BusinessRulesService
                var businessRuleResult = await RunBusinessRuleValidationAsync();
                if (!businessRuleResult.IsValid)
                {
                    await Shell.Current.DisplayAlert("Validation Errors", string.Join("\n", businessRuleResult.Errors), "OK");
                    return;
                }

                if (!Validate())
                {
                    await Shell.Current.DisplayAlert("Validation Errors", string.Join("\n", ValidationErrors), "OK");
                    return;
                }

                RequestResponseDto? response = null;
                if (IsEditMode)
                {
                    var updateResponse = await _requestService.UpdateRequestAsync(RequestId, RequestUpdateDto);
                    if (updateResponse?.Success == true) response = updateResponse.Data;
                    else await HandleErrorResponse(updateResponse?.Message);
                }
                else
                {
                    var createResponse = await _requestService.CreateRequestAsync(RequestCreateDto);
                    if (createResponse?.Success == true) response = createResponse.Data;
                    else await HandleErrorResponse(createResponse?.Message);
                }

                if (response != null)
                {
                    await Shell.Current.DisplayAlert("Success", $"Request {(IsEditMode ? "updated" : "created")} successfully.", "OK");
                    await Shell.Current.GoToAsync("..");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during request submission");
                await Shell.Current.DisplayAlert("Error", "An unexpected error occurred.", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task HandleErrorResponse(string? message)
        {
            string errorMessage = message ?? "Operation failed.";
            if (!string.IsNullOrEmpty(message))
            {
                if (message.Contains("conflicting request")) errorMessage = "You already have a request for this date.";
                else if (message.Contains("balance")) errorMessage = message;
                else if (message.Contains("current state")) errorMessage = "This request cannot be edited in its current state.";
            }
            await Shell.Current.DisplayAlert("Request Error", errorMessage, "OK");
        }

        [RelayCommand(CanExecute = nameof(IsNotBusy))]
        private async Task CancelAsync()
        {
            await Shell.Current.GoToAsync("..");
        }

        private async Task ValidateEditAccessAsync(RequestResponseDto request)
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    await Shell.Current.GoToAsync("..");
                    return;
                }

                bool isOwner = request.RequestUserID == currentUser.UserID;
                if (!RequestStateManager.CanEdit(request, currentUser.IsAdmin ?? false, isOwner))
                {
                    await Shell.Current.DisplayAlert("Access Denied", "You do not have permission to edit this request.", "OK");
                    await Shell.Current.GoToAsync("..");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating edit access");
                await Shell.Current.GoToAsync("..");
            }
        }

        public bool Validate()
        {
            var errors = new List<string>();
            LeaveType leaveType = MapToLeaveType(SelectedLeaveType);

            if (leaveType == (LeaveType)(-1))
            {
                errors.Add("Invalid leave type selected.");
                ValidationErrors = errors;
                return false;
            }

            var dto = IsEditMode ? (object)RequestUpdateDto : RequestCreateDto;
            var validationResult = _validationService.ValidateObject(dto);
            if (!validationResult.IsValid) errors.AddRange(validationResult.Errors);

            if (StartDate.Date < DateTime.Today && !IsEditMode) errors.Add("Start date cannot be in the past.");

            if (leaveType != LeaveType.Permission && leaveType != LeaveType.ExternalAssignment)
            {
                if (EndDate.HasValue && EndDate.Value.Date < StartDate.Date) errors.Add("End date must be on or after start date.");
            }
            else
            {
                if (!StartTime.HasValue || !EndTime.HasValue) errors.Add($"{SelectedLeaveType} requires both start and end times.");
                else if (EndTime <= StartTime) errors.Add("End time must be after start time.");
            }

            ValidationErrors = errors;
            return !errors.Any();
        }

        private async Task<TDFShared.Validation.BusinessRuleValidationResult> RunBusinessRuleValidationAsync()
        {
            var context = new TDFShared.Validation.BusinessRuleContext
            {
                GetLeaveBalanceAsync = async (uid, type) =>
                {
                    var balancesResponse = await _requestService.GetLeaveBalancesAsync(uid);
                    if (balancesResponse?.Success == true && balancesResponse.Data != null)
                    {
                        var key = LeaveTypeHelper.GetBalanceKey(type);
                        return key != null && balancesResponse.Data.TryGetValue(key, out int balance) ? balance : 0;
                    }
                    return 0;
                },
                HasConflictingRequestsAsync = async (uid, start, end, excludeId) =>
                {
                    // Basic client-side check could be added here, but usually requires server call
                    return false;
                }
            };

            if (IsEditMode)
            {
                return await _businessRulesService.ValidateLeaveRequestUpdateAsync(RequestUpdateDto, RequestId, CurrentUserId, context);
            }
            else
            {
                return await _businessRulesService.ValidateLeaveRequestAsync(RequestCreateDto, CurrentUserId, context);
            }
        }

        private static bool IsWeekend(DateTime date) => date.DayOfWeek == DayOfWeek.Friday || date.DayOfWeek == DayOfWeek.Saturday;
    }
}
