using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using TDFMAUI.Helpers;
using TDFMAUI.Services;
using TDFShared.Models.User;
using TDFShared.DTOs.Users;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TDFMAUI.ViewModels
{
    public partial class UserProfileViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        private readonly ILocalStorageService _localStorageService;

        [ObservableProperty]
        private UserDto _currentUser = new();

        [ObservableProperty]
        private UserDto _editingUser = new();

        [ObservableProperty]
        private bool _isDataLoaded;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private ImageSource _profileImage = ImageSource.FromFile("default_profile.png");

        public UserProfileViewModel(ApiService apiService, ILocalStorageService localStorageService)
        {
            _apiService = apiService;
            _localStorageService = localStorageService;
            Title = "Profile";
        }

        partial void OnCurrentUserChanged(UserDto value) => UpdateProfileImageSource();

        private void UpdateProfileImageSource()
        {
            if (CurrentUser?.Picture != null && CurrentUser.Picture.Length > 0)
            {
                ProfileImage = ImageSource.FromStream(() => new MemoryStream(CurrentUser.Picture));
            }
            else
            {
                 ProfileImage = ImageSource.FromFile("default_profile.png");
            }
        }

        [RelayCommand]
        public async Task LoadUserByIdAsync(int userId)
        {
            IsBusy = true;
            try
            {
                var user = await _apiService.GetUserAsync(userId);
                if (user != null)
                {
                    CurrentUser = user;
                    EditingUser = (UserDto)user.Clone();
                    IsDataLoaded = true;
                }
                else ErrorMessage = $"User {userId} not found";
            }
            catch (Exception ex) { ErrorMessage = ApiService.GetFriendlyErrorMessage(ex); }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task LoadUserProfileAsync()
        {
            IsBusy = true;
            try
            {
                var cachedUser = await _localStorageService.GetItemAsync<UserDto>("CurrentUser");
                if (cachedUser != null) CurrentUser = cachedUser;

                if (CurrentUser.UserID > 0)
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
            catch (Exception ex) { ErrorMessage = ex.Message; }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public void EditProfile()
        {
            EditingUser = (UserDto)CurrentUser.Clone();
            IsEditing = true;
        }

        [RelayCommand]
        public async Task SaveProfileAsync()
        {
            if (string.IsNullOrWhiteSpace(EditingUser.FullName))
            {
                ErrorMessage = "Name is required.";
                return;
            }

            IsBusy = true;
            try
            {
                var request = new UpdateMyProfileRequest
                {
                    FullName = EditingUser.FullName,
                    Department = EditingUser.Department,
                    Title = EditingUser.Title
                };

                if (await _apiService.UpdateUserProfileAsync(request))
                {
                    CurrentUser = EditingUser;
                    await _localStorageService.SetItemAsync("CurrentUser", CurrentUser);
                    IsEditing = false;
                }
                else ErrorMessage = "Failed to update profile.";
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public void CancelEdit()
        {
            IsEditing = false;
            ErrorMessage = string.Empty;
        }

        [RelayCommand]
        public async Task ChangeImageAsync()
        {
            try
            {
                var status = await Permissions.RequestAsync<Permissions.Photos>();
                if (status != PermissionStatus.Granted)
                {
                    ErrorMessage = "Photo permission denied.";
                    return;
                }

                var photo = await MediaPicker.PickPhotoAsync();
                if (photo != null)
                {
                    using var stream = await photo.OpenReadAsync();
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    ms.Position = 0;

                    if (await _apiService.UploadProfilePictureAsync(ms, photo.FileName, photo.ContentType))
                    {
                        await LoadUserProfileAsync();
                    }
                    else ErrorMessage = "Failed to upload picture.";
                }
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }
    }
}
