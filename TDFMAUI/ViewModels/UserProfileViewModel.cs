using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Maui.Controls;
using TDFMAUI.Helpers;
using TDFMAUI.Services;
using TDFShared.Models.User;
using TDFShared.DTOs.Users;
using Microsoft.Maui.ApplicationModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;

namespace TDFMAUI.ViewModels
{
    public partial class UserProfileViewModel : ObservableObject
    {
        private readonly ApiService _apiService;
        private readonly ILocalStorageService _localStorageService;

        [ObservableProperty]
        private UserDto _currentUser;

        [ObservableProperty]
        private UserDto _editingUser;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string _errorMessage;

        [ObservableProperty]
        private bool _isDataLoaded;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private bool _isSaving;

        [ObservableProperty]
        private ImageSource _profileImage;

        public UserProfileViewModel(ApiService apiService, ILocalStorageService localStorageService)
        {
            _apiService = apiService;
            _localStorageService = localStorageService;

            CurrentUser = new UserDto();
            EditingUser = new UserDto();
        }

        // Update propertychanged logic for CurrentUser to also update ProfileImage
        partial void OnCurrentUserChanged(UserDto value)
        {
            UpdateProfileImageSource();
        }

        private void UpdateProfileImageSource()
        {
            if (CurrentUser?.ProfilePictureData != null && CurrentUser.ProfilePictureData.Length > 0)
            {
                // Create ImageSource from byte array
                ProfileImage = ImageSource.FromStream(() => new MemoryStream(CurrentUser.ProfilePictureData));
            }
            else
            {
                // Use default image if neither is available
                 ProfileImage = ImageSource.FromFile("default_profile.png"); // Ensure this file exists in Resources/Images
            }
        }

        [RelayCommand]
        public async Task LoadUserByIdAsync(int userId)
        {
            try
            {
                IsLoading = true;
                HasError = false;
                IsDataLoaded = false;

                // Load the specific user by ID
                var user = await _apiService.GetUserAsync(userId);
                if (user != null)
                {
                    CurrentUser = user;
                    EditingUser = (UserDto)user.Clone();

                    IsDataLoaded = true;
                }
                else
                {
                    HasError = true;
                    ErrorMessage = $"User with ID {userId} not found";
                }
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Error loading user profile: {ApiService.GetFriendlyErrorMessage(ex)}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task LoadUserProfile()
        {
            try
            {
                IsLoading = true;
                HasError = false;

                // Try to load from local storage first for quick display
                var cachedUser = await _localStorageService.GetItemAsync<UserDto>("CurrentUser");
                if (cachedUser != null)
                {
                    CurrentUser = cachedUser;
                    IsDataLoaded = true;
                }

                // Then load fresh data from API
                if (CurrentUser?.UserID > 0)
                {
                    var user = await _apiService.GetUserAsync(CurrentUser.UserID);
                    if (user != null)
                    {
                        CurrentUser = user;
                        await _localStorageService.SetItemAsync("CurrentUser", CurrentUser);
                    }
                }

                IsDataLoaded = true;
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Error loading profile: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public void EditProfile()
        {
            // Create a copy of the current user to edit
            EditingUser = (UserDto)CurrentUser.Clone();
            IsEditing = true;
        }

        [RelayCommand]
        public async Task SaveProfile()
        {
            if (string.IsNullOrWhiteSpace(EditingUser.FullName))
            {
                HasError = true;
                ErrorMessage = "Name is a required field";
                return;
            }

            try
            {
                IsSaving = true;
                HasError = false;

                // Create UpdateMyProfileRequest from EditingUser
                var profileRequest = new UpdateMyProfileRequest
                {
                    FullName = EditingUser.FullName,
                    Department = EditingUser.Department,
                    Title = EditingUser.Title
                };

                // Call API to update profile
                var success = await _apiService.UpdateUserProfileAsync(profileRequest);

                if (success)
                {
                    // Update the current user with edited values
                    CurrentUser = EditingUser;

                    // Update local storage
                    await _localStorageService.SetItemAsync("CurrentUser", CurrentUser);

                    IsEditing = false;
                }
                else
                {
                    HasError = true;
                    ErrorMessage = "Failed to update profile";
                }
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Error saving profile: {ex.Message}";
            }
            finally
            {
                IsSaving = false;
            }
        }

        [RelayCommand]
        public void CancelEdit()
        {
            IsEditing = false;
            HasError = false;
        }

        [RelayCommand]
        public async Task ChangeImage()
        {
            try
            {
                HasError = false;

                // Request permission
                var status = await Permissions.RequestAsync<Permissions.Photos>();
                if (status != PermissionStatus.Granted)
                {
                    HasError = true;
                    ErrorMessage = "Permission to access photos was denied";
                    return;
                }

                // Pick photo
                var photo = await MediaPicker.PickPhotoAsync();
                if (photo != null)
                {
                    // Load the photo as a stream
                    using var stream = await photo.OpenReadAsync();
                    if (stream == null)
                    {
                         HasError = true;
                         ErrorMessage = "Could not open the selected image.";
                         return;
                    }

                    // Immediately update the UI preview (optional but good UX)
                    // Need to read the stream into memory for preview if original stream is used for upload
                    using var memoryStream = new MemoryStream();
                    await stream.CopyToAsync(memoryStream);
                    memoryStream.Position = 0; // Reset stream position for reading
                    ProfileImage = ImageSource.FromStream(() => new MemoryStream(memoryStream.ToArray())); // Use a new memory stream for UI

                    // Reset stream position before uploading
                    memoryStream.Position = 0;

                    // Upload the image to the server
                    bool uploadSuccess = await _apiService.UploadProfilePictureAsync(memoryStream, photo.FileName, photo.ContentType);

                    if (uploadSuccess)
                    {
                        HasError = false; // Clear any previous errors
                        // Reload profile to get the updated byte data from the server
                        await LoadUserProfile();
                    }
                    else
                    {
                        HasError = true;
                        ErrorMessage = "Failed to upload profile picture to the server.";
                        // Revert UI preview if upload fails?
                        // ProfileImage = ImageSource.FromUri(new Uri(CurrentUser.ProfileImageUrl ?? "default_profile.png"));
                    }
                }
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Error changing image: {ex.Message}";
            }
            finally
            {
                IsSaving = false; // Ensure IsSaving is handled if this command implies saving state
            }
        }
    }
}