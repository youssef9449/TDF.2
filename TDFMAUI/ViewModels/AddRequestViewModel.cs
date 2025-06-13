using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TDFShared.DTOs.Requests;
using TDFShared.DTOs.Users;
using TDFShared.Exceptions;
using TDFShared.Services;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using TDFMAUI.Services;
using TDFShared.Enums;
using Microsoft.Extensions.Logging;
using TDFShared.Utilities;

namespace TDFMAUI.ViewModels
{
    public class AddRequestViewModel : INotifyPropertyChanged
    {
        private readonly IRequestService _requestService;
        private readonly IAuthService _authService;
        private readonly ILogger<AddRequestViewModel> _logger;
        private readonly TDFShared.Validation.IValidationService _validationService;

        private bool _isEditMode;
        private int _requestId;
        private string _requestType = string.Empty;
        private string _requestReason = string.Empty;
        private DateTime _requestFromDay = DateTime.Today;
        private DateTime? _requestToDay = DateTime.Today;
        private TimeSpan? _requestBeginningTime;
        private TimeSpan? _requestEndingTime;
        private List<string> _validationErrors = new List<string>();

        // New private fields
        private ObservableCollection<string> _leaveTypes = new ObservableCollection<string>(); // Use ObservableCollection if types might change
        private string _selectedLeaveType = string.Empty;
        private bool _isBusy;
        private int _currentUserId = 0; // <-- Add this field
        private DateTime _minDate = DateTime.Today;
        private DateTime _maxDate = DateTime.Today.AddYears(1);

        public bool IsEditMode
        {
            get => _isEditMode;
            set => SetProperty(ref _isEditMode, value);
        }

        public int RequestId
        {
            get => _requestId;
            set => SetProperty(ref _requestId, value);
        }

        public string SelectedLeaveType
        {
            get => _selectedLeaveType;
            set {
                SetProperty(ref _selectedLeaveType, value);
                // Optionally update the internal _requestType if DTO mapping relies on it
                _requestType = value;
                
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
                
                OnPropertyChanged(nameof(RequestCreateDto)); // Ensure DTO updates
                OnPropertyChanged(nameof(RequestUpdateDto)); // Ensure DTO updates
            }
        }

        public ObservableCollection<string> LeaveTypes
        {
            get => _leaveTypes;
            set => SetProperty(ref _leaveTypes, value);
        }

        public string RequestReason
        {
            get => _requestReason;
            set => SetProperty(ref _requestReason, value);
        }

        public DateTime StartDate
        {
            get => _requestFromDay;
            set {
                SetProperty(ref _requestFromDay, value);
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
        }

        public DateTime? EndDate
        {
            get => _requestToDay;
            set {
                 if (value.HasValue && value.Value.Date < StartDate.Date)
                 {
                     // Optionally show error or adjust StartDate
                     // For now, just don't set if invalid
                     return;
                 }
                SetProperty(ref _requestToDay, value);
            }
        }

        public TimeSpan? StartTime
        {
            get => _requestBeginningTime;
            set => SetProperty(ref _requestBeginningTime, value);
        }

        public TimeSpan? EndTime
        {
            get => _requestEndingTime;
            set => SetProperty(ref _requestEndingTime, value);
        }

        public List<string> ValidationErrors
        {
            get => _validationErrors;
            private set {
                SetProperty(ref _validationErrors, value);
                OnPropertyChanged(nameof(HasErrors)); // Notify HasErrors changed too
            }
        }

        public bool HasErrors => ValidationErrors.Any();

        public bool IsBusy
        {
            get => _isBusy;
            set {
                SetProperty(ref _isBusy, value);
                OnPropertyChanged(nameof(IsNotBusy)); // Update dependent property
                // Optionally update command CanExecute
                ((Command)SubmitRequestCommand).ChangeCanExecute();
                ((Command)CancelCommand).ChangeCanExecute();
            }
        }

        public bool IsNotBusy => !IsBusy;

        public DateTime MinDate
        {
            get => _minDate;
            set => SetProperty(ref _minDate, value);
        }

        public DateTime MaxDate
        {
            get => _maxDate;
            set => SetProperty(ref _maxDate, value);
        }

        public RequestCreateDto RequestCreateDto
        {
            get
            {
                // Convert display name to enum value
                LeaveType leaveType;
                if (SelectedLeaveType == "Work From Home")
                    leaveType = LeaveType.WorkFromHome;
                else if (SelectedLeaveType == "External Assignment")
                    leaveType = LeaveType.ExternalAssignment;
                else if (!Enum.TryParse(SelectedLeaveType, true, out leaveType))
                    return new RequestCreateDto();

                return new RequestCreateDto
                {
                    LeaveType = leaveType,
                    RequestStartDate = StartDate,
                    RequestEndDate = (leaveType == LeaveType.Permission || leaveType == LeaveType.ExternalAssignment) ? StartDate : EndDate ?? StartDate,
                    RequestBeginningTime = (leaveType == LeaveType.Permission || leaveType == LeaveType.ExternalAssignment) ? StartTime : null,
                    RequestEndingTime = (leaveType == LeaveType.Permission || leaveType == LeaveType.ExternalAssignment) ? EndTime : null,
                    RequestReason = RequestReason ?? string.Empty,
                    UserId = _currentUserId // Use backing field
                };
            }
        }

        public RequestUpdateDto RequestUpdateDto
        {
            get
            {
                // Convert display name to enum value
                LeaveType leaveType;
                if (SelectedLeaveType == "Work From Home")
                    leaveType = LeaveType.WorkFromHome;
                else if (SelectedLeaveType == "External Assignment")
                    leaveType = LeaveType.ExternalAssignment;
                else if (!Enum.TryParse(SelectedLeaveType, true, out leaveType))
                    return new RequestUpdateDto();

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

        public ICommand SubmitRequestCommand { get; }
        public ICommand CancelCommand { get; }

        public AddRequestViewModel(IRequestService requestService, IAuthService authService, ILogger<AddRequestViewModel> logger, TDFShared.Validation.IValidationService validationService, RequestResponseDto? existingRequest = null)
        {
            _requestService = requestService ?? throw new ArgumentNullException(nameof(requestService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));

            SubmitRequestCommand = new Command(async () => await OnSubmit(), () => IsNotBusy);
            CancelCommand = new Command(async () => await OnCancel(), () => IsNotBusy);

            LoadLeaveTypes();

            if (existingRequest != null)
            {
                IsEditMode = true;
                RequestId = existingRequest.RequestID;
                SelectedLeaveType = existingRequest.LeaveType.ToString();
                RequestReason = existingRequest.RequestReason;
                StartDate = existingRequest.RequestStartDate;
                EndDate = existingRequest.RequestEndDate;

                // If it's a type that uses time, populate StartTime and EndTime
                if (existingRequest.LeaveType == LeaveType.Permission || existingRequest.LeaveType == LeaveType.ExternalAssignment)
                {
                    StartTime = existingRequest.RequestBeginningTime;
                    EndTime = existingRequest.RequestEndingTime;
                }
                else
                {
                    StartTime = null;
                    EndTime = null;
                }

                _requestType = existingRequest.LeaveType.ToString();
                _requestFromDay = existingRequest.RequestStartDate;
                _requestToDay = existingRequest.RequestEndDate;
                // _requestBeginningTime and _requestEndingTime are directly bound to StartTime and EndTime properties

                // Validate access control for editing using RequestStateManager
                _ = ValidateEditAccessAsync(existingRequest);
            }
            else
            {
                IsEditMode = false;
                RequestId = 0;
                StartDate = DateTime.Today;
                EndDate = DateTime.Today;
                StartTime = null; // Initialize to null for new requests
                EndTime = null;   // Initialize to null for new requests
            }
        }

        private void LoadLeaveTypes()
        {
            LeaveTypes.Clear();
            foreach (LeaveType type in Enum.GetValues(typeof(LeaveType)))
            {
                string displayName = type.ToString();
                
                // Format specific leave types with spaces
                if (type == LeaveType.WorkFromHome)
                    displayName = "Work From Home";
                else if (type == LeaveType.ExternalAssignment)
                    displayName = "External Assignment";
                
                LeaveTypes.Add(displayName);
            }
            SelectedLeaveType = LeaveTypes.FirstOrDefault() ?? string.Empty;
        }

        private async Task OnSubmit()
        {
            IsBusy = true;
            try
            {
                RequestResponseDto? response = null;
                int currentUserId = await _authService.GetCurrentUserIdAsync();
                _currentUserId = currentUserId; // Set backing field for use in RequestCreateDto

                if (currentUserId == 0)
                {
                    _logger.LogWarning("User is not authenticated or User ID could not be retrieved. Aborting submission.");
                    await Shell.Current.DisplayAlert("Authentication Error", "Could not verify user identity. Please log in again.", "OK");
                    // Optionally navigate to login page: await Shell.Current.GoToAsync("//LoginPage");
                    return; // Stop processing
                }

                // Validate with correct UserId
                if (!Validate())
                {
                    await Shell.Current.DisplayAlert("Validation Errors", string.Join("\n", ValidationErrors), "OK");
                    return;
                }

                if (IsEditMode)
                {
                    var updateResponse = await _requestService.UpdateRequestAsync(RequestId, RequestUpdateDto);
                    if (updateResponse?.Success == true && updateResponse.Data != null)
                    {
                        response = updateResponse.Data;
                    }
                    else
                    {
                        string errorMessage = "Failed to update request.";
                        
                        // Check for specific error messages
                        if (!string.IsNullOrEmpty(updateResponse?.Message))
                        {
                            // Handle specific error cases
                            if (updateResponse.Message.Contains("conflicting request"))
                            {
                                errorMessage = "You already have a request for this date. Please choose a different date.";
                            }
                            else if (updateResponse.Message.Contains("balance"))
                            {
                                errorMessage = updateResponse.Message; // Use the exact leave balance error message
                            }
                            // We allow requests for past dates now
                            else if (updateResponse.Message.Contains("current state"))
                            {
                                errorMessage = "This request cannot be edited in its current state.";
                            }
                            else
                            {
                                // Use the server's error message for other cases
                                errorMessage = updateResponse.Message;
                            }
                        }
                        
                        _logger.LogWarning("Failed to update request {RequestId}: {ErrorMessage}", RequestId, errorMessage);
                        await Shell.Current.DisplayAlert("Request Error", errorMessage, "OK");
                        return;
                    }
                }
                else
                {
                    var createDto = RequestCreateDto;
                    var createResponse = await _requestService.CreateRequestAsync(createDto);
                    if (createResponse?.Success == true && createResponse.Data != null)
                    {
                        response = createResponse.Data;
                    }
                    else
                    {
                        string errorMessage = "Failed to create request.";
                        
                        // Check for specific error messages
                        if (!string.IsNullOrEmpty(createResponse?.Message))
                        {
                            // Handle specific error cases
                            if (createResponse.Message.Contains("conflicting request"))
                            {
                                errorMessage = "You already have a request for this date. Please choose a different date.";
                            }
                            else if (createResponse.Message.Contains("balance"))
                            {
                                errorMessage = createResponse.Message; // Use the exact leave balance error message
                            }
                            // We allow requests for past dates now
                            else
                            {
                                // Use the server's error message for other cases
                                errorMessage = createResponse.Message;
                            }
                        }
                        
                        _logger.LogWarning("Failed to create request: {ErrorMessage}", errorMessage);
                        await Shell.Current.DisplayAlert("Request Error", errorMessage, "OK");
                        return;
                    }
                }

                if (response != null) // Check if API call was successful (HandleApiResponse should handle errors)
                {
                    await Shell.Current.DisplayAlert("Success", $"Request {(IsEditMode ? "updated" : "created")} successfully.", "OK");
                    await Shell.Current.GoToAsync(".."); // Navigate back
                }
                else
                {
                    // HandleApiResponse might return null/default in some cases, or throw.
                    // If it returns null without throwing, it indicates a potential issue.
                    _logger.LogWarning("API operation completed but returned no response data.");
                    await Shell.Current.DisplayAlert("Warning", "Operation completed, but no response data was received.", "OK");
                     await Shell.Current.GoToAsync(".."); // Navigate back even if response is null?
                }
            }
            catch (ApiException apiEx)
            {
                _logger.LogError(apiEx, "API Error during request submission: {ErrorMessage}", apiEx.Message);
                await Shell.Current.DisplayAlert("API Error", $"Failed to submit request: {apiEx.Message}", "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during request submission");
                await Shell.Current.DisplayAlert("Error", "An unexpected error occurred. Please try again.", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task OnCancel()
        {
            await Shell.Current.GoToAsync("..");
        }

        /// <summary>
        /// Validates if the current user can edit the specified request using RequestStateManager
        /// </summary>
        private async Task ValidateEditAccessAsync(RequestResponseDto request)
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    _logger.LogWarning("User not authenticated when validating edit access");
                    await Shell.Current.DisplayAlert("Access Denied", "You must be logged in to edit requests.", "OK");
                    await Shell.Current.GoToAsync("..");
                    return;
                }

                var userDto = new UserDto
                {
                    UserID = currentUser.UserID,
                    IsAdmin = currentUser.IsAdmin,
                    IsHR = currentUser.IsHR,
                    IsManager = currentUser.IsManager,
                    Department = currentUser.Department
                };

                bool isOwner = request.RequestUserID == currentUser.UserID;

                // Use RequestStateManager for state-based checks
                if (!RequestStateManager.CanEdit(request, userDto.IsAdmin ?? false, isOwner))
                {
                    _logger.LogWarning("User {UserId} attempted to edit request {RequestId} without proper state permissions", 
                        currentUser.UserID, request.RequestID);
                    await Shell.Current.DisplayAlert("Access Denied", "You do not have permission to edit this request.", "OK");
                    await Shell.Current.GoToAsync("..");
                    return;
                }

                // Use AuthorizationUtilities for action-specific checks
                if (!AuthorizationUtilities.CanPerformRequestAction(userDto, request, TDFShared.Utilities.RequestAction.Edit))
                {
                    _logger.LogWarning("User {UserId} attempted to edit request {RequestId} without proper action permissions", 
                        currentUser.UserID, request.RequestID);
                    await Shell.Current.DisplayAlert("Access Denied", "You do not have permission to edit this request.", "OK");
                    await Shell.Current.GoToAsync("..");
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating edit access for request {RequestId}", request.RequestID);
                await Shell.Current.DisplayAlert("Error", "An error occurred while validating access.", "OK");
                await Shell.Current.GoToAsync("..");
            }
        }

        public bool Validate()
        {
            var errors = new List<string>();

            // Basic validation
            if (string.IsNullOrWhiteSpace(SelectedLeaveType))
            {
                errors.Add("Leave type is required.");
                ValidationErrors = errors;
                return false;
            }

            if (!Enum.TryParse(SelectedLeaveType, true, out LeaveType leaveType))
            {
                errors.Add("Invalid leave type selected.");
                ValidationErrors = errors;
                return false;
            }

            // Use shared validation service for DTO validation
            var dto = IsEditMode ? (object)RequestUpdateDto : RequestCreateDto;
            var validationResult = _validationService.ValidateObject(dto);

            if (!validationResult.IsValid)
            {
                errors.AddRange(validationResult.Errors);
            }

            // Additional client-side validation
            if (StartDate.Date < DateTime.Today)
            {
                errors.Add("Start date cannot be in the past.");
            }

            // Only validate end date for leave types that use it
            if (!(leaveType == LeaveType.Permission || leaveType == LeaveType.ExternalAssignment))
            {
                if (EndDate.HasValue && EndDate.Value.Date < StartDate.Date)
                {
                    errors.Add("End date must be on or after start date.");
                }
            }

            // Time validation for specific leave types
            if (leaveType == LeaveType.Permission || leaveType == LeaveType.ExternalAssignment)
            {
                if (!StartTime.HasValue || !EndTime.HasValue)
                {
                    errors.Add($"{leaveType} requires both start and end times.");
                }
                else if (EndTime <= StartTime)
                {
                    errors.Add("End time must be after start time.");
                }
            }

            ValidationErrors = errors;
            return !errors.Any();
        }

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T backingStore, T value,
            [CallerMemberName] string propertyName = "",
            Action? onChanged = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            onChanged?.Invoke();
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}