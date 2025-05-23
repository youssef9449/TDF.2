using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TDFShared.DTOs.Requests;
using TDFShared.Exceptions;
using TDFShared.Services;
using TDFShared.Factories;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using TDFMAUI.Services;
using TDFShared.Enums;
using Microsoft.Extensions.Logging;

namespace TDFMAUI.ViewModels
{
    public class AddRequestViewModel : INotifyPropertyChanged
    {
        private readonly TDFMAUI.Services.IRequestService _requestService;
        private readonly TDFShared.Services.IAuthService _authService;
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

        public RequestCreateDto RequestCreateDto =>
            Enum.TryParse<LeaveType>(SelectedLeaveType, true, out LeaveType leaveType)
                ? RequestDtoFactory.CreateRequestDto(
                    leaveType,
                    StartDate,
                    EndDate,
                    StartTime,
                    EndTime,
                    RequestReason,
                    0) // UserId will be set in OnSubmit
                : new RequestCreateDto();

        public RequestUpdateDto RequestUpdateDto =>
            Enum.TryParse<LeaveType>(SelectedLeaveType, true, out LeaveType leaveType)
                ? RequestDtoFactory.CreateUpdateDto(
                    leaveType,
                    StartDate,
                    EndDate,
                    StartTime,
                    EndTime,
                    RequestReason)
                : new RequestUpdateDto();

        public ICommand SubmitRequestCommand { get; }
        public ICommand CancelCommand { get; }

        public AddRequestViewModel(IRequestService requestService, TDFShared.Services.IAuthService authService, ILogger<AddRequestViewModel> logger, TDFShared.Validation.IValidationService validationService, RequestResponseDto? existingRequest = null)
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
                LeaveTypes.Add(type.ToString());
            }
            SelectedLeaveType = LeaveTypes.FirstOrDefault() ?? string.Empty;
        }

        private async Task OnSubmit()
        {
            if (!Validate())
            {
                await Shell.Current.DisplayAlert("Validation Errors", string.Join("\n", ValidationErrors), "OK");
                return;
            }

            IsBusy = true;
            try
            {
                RequestResponseDto? response = null;
                int currentUserId = await _authService.GetCurrentUserIdAsync();

                if (currentUserId == 0)
                {
                    _logger.LogWarning("User is not authenticated or User ID could not be retrieved. Aborting submission.");
                    await Shell.Current.DisplayAlert("Authentication Error", "Could not verify user identity. Please log in again.", "OK");
                    // Optionally navigate to login page: await Shell.Current.GoToAsync("//LoginPage");
                    return; // Stop processing
                }

                if (IsEditMode)
                {
                    var updateDto = RequestUpdateDto;
                    _logger.LogInformation("Attempting to update request {RequestId}", RequestId);
                    response = await _requestService.UpdateRequestAsync(RequestId, updateDto);
                    _logger.LogInformation("Update request {RequestId} completed.", RequestId);
                }
                else
                {
                    var createDto = RequestCreateDto;
                    createDto.UserId = currentUserId; // Set the user ID
                    _logger.LogInformation("Attempting to create request for user {UserId}", createDto.UserId);
                    response = await _requestService.CreateRequestAsync(createDto);
                     _logger.LogInformation("Create request completed.");
                     // Assuming the response contains the new ID, update the ViewModel if needed
                     if (response != null) RequestId = response.RequestID;
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

            if (!Enum.TryParse<LeaveType>(SelectedLeaveType, true, out LeaveType leaveType))
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

            if (EndDate.HasValue && EndDate.Value.Date < StartDate.Date)
            {
                errors.Add("End date must be on or after start date.");
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