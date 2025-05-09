using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TDFShared.DTOs.Requests;
using TDFShared.Exceptions; 
using System.Windows.Input; // Needed for ICommand
using Microsoft.Maui.Controls; // Needed for Command
using System.Collections.ObjectModel; // Needed for ObservableCollection
using System.Threading.Tasks; // Needed for async Task
using TDFMAUI.Services; // Added for IRequestService, IAuthService
using Microsoft.Extensions.Logging; // Added for logging

namespace TDFMAUI.ViewModels
{
    public class AddRequestViewModel : INotifyPropertyChanged
    {
        private readonly IRequestService _requestService;
        private readonly IAuthService _authService;
        private readonly ILogger<AddRequestViewModel> _logger;

        private bool _isEditMode;
        private Guid _requestId;
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
        private bool _isPartialDay;
        private bool _isBusy;

        public bool IsEditMode
        {
            get => _isEditMode;
            set => SetProperty(ref _isEditMode, value);
        }

        public Guid RequestId
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

        public bool IsPartialDay
        {
            get => _isPartialDay;
            set {
                SetProperty(ref _isPartialDay, value);
                if (!value) // If not partial day, clear times
                {
                    StartTime = null;
                    EndTime = null;
                }
                 // Trigger validation potentially?
                 Validate();
            }
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

        public RequestCreateDto RequestCreateDto => new RequestCreateDto
        {
            // Don't set UserId here; it will be set in OnSubmit
            LeaveType = this._selectedLeaveType,
            RequestStartDate = this._requestFromDay,
            RequestEndDate = this._requestToDay ?? this._requestFromDay,
            RequestBeginningTime = this.IsPartialDay ? this._requestBeginningTime : null,
            RequestEndingTime = this.IsPartialDay ? this._requestEndingTime : null,
            RequestReason = this._requestReason
        };

        public RequestUpdateDto RequestUpdateDto => new RequestUpdateDto
        {
            LeaveType = this._selectedLeaveType,
            RequestStartDate = this._requestFromDay,
            RequestEndDate = this._requestToDay ?? this._requestFromDay,
            RequestBeginningTime = this.IsPartialDay ? this._requestBeginningTime : null,
            RequestEndingTime = this.IsPartialDay ? this._requestEndingTime : null,
            RequestReason = this._requestReason,
            Remarks = null
        };

        public ICommand SubmitRequestCommand { get; }
        public ICommand CancelCommand { get; }

        public AddRequestViewModel(IRequestService requestService, IAuthService authService, ILogger<AddRequestViewModel> logger, RequestResponseDto? existingRequest = null)
        {
            _requestService = requestService ?? throw new ArgumentNullException(nameof(requestService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            SubmitRequestCommand = new Command(async () => await OnSubmit(), () => IsNotBusy);
            CancelCommand = new Command(async () => await OnCancel(), () => IsNotBusy);

            LoadLeaveTypes();

            if (existingRequest != null)
            {
                IsEditMode = true;
                RequestId = existingRequest.Id;
                SelectedLeaveType = existingRequest.LeaveType;
                RequestReason = existingRequest.RequestReason;
                StartDate = existingRequest.RequestStartDate;
                EndDate = existingRequest.RequestEndDate;

                IsPartialDay = existingRequest.RequestBeginningTime.HasValue || existingRequest.RequestEndingTime.HasValue;
                StartTime = existingRequest.RequestBeginningTime;
                EndTime = existingRequest.RequestEndingTime;

                _requestType = existingRequest.LeaveType;
                _requestFromDay = existingRequest.RequestStartDate;
                _requestToDay = existingRequest.RequestEndDate;
                _requestBeginningTime = existingRequest.RequestBeginningTime;
                _requestEndingTime = existingRequest.RequestEndingTime;
            }
            else
            {
                IsEditMode = false;
                RequestId = Guid.NewGuid();
                StartDate = DateTime.Today;
                EndDate = DateTime.Today;
                IsPartialDay = false;
            }
        }

        private void LoadLeaveTypes()
        {
            LeaveTypes.Clear();
            LeaveTypes.Add("Annual");
            LeaveTypes.Add("Work From Home");
            LeaveTypes.Add("Unpaid");
            LeaveTypes.Add("Emergency");
            LeaveTypes.Add("Permission");
            LeaveTypes.Add("External Assignment");
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
                     if (response != null) RequestId = response.Id;
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

            if (string.IsNullOrWhiteSpace(SelectedLeaveType))
            {
                errors.Add("Leave type is required.");
            }

            if (StartDate.Date < DateTime.Today)
            {
                errors.Add("Start date cannot be in the past.");
            }

            if (EndDate.HasValue && EndDate.Value.Date < StartDate.Date)
            {
                errors.Add("End date cannot be before the start date.");
            }

            if (IsPartialDay)
            {
                if (!StartTime.HasValue || !EndTime.HasValue)
                {
                    errors.Add("Both start and end times must be provided for a partial day request.");
                }
                else if (EndTime.Value <= StartTime.Value)
                {
                    errors.Add("End time must be after the start time for a partial day request.");
                }

                if (!EndDate.HasValue || EndDate.Value.Date != StartDate.Date)
                {
                    errors.Add("Partial day requests must start and end on the same calendar day.");
                }
            }

            ValidationErrors = errors;
            return !HasErrors;
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