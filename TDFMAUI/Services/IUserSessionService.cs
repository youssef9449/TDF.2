using System;
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Users;

namespace TDFMAUI.Services
{
    /// <summary>
    /// Interface for centralized user session management
    /// Provides a single source of truth for user data and authentication tokens
    /// </summary>
    public interface IUserSessionService
    {
        #region Events
        
        /// <summary>
        /// Event raised when user data changes
        /// </summary>
        event EventHandler<UserChangedEventArgs>? UserChanged;
        
        /// <summary>
        /// Event raised when authentication tokens change
        /// </summary>
        event EventHandler<TokenChangedEventArgs>? TokenChanged;
        
        #endregion

        #region User Data Properties
        
        /// <summary>
        /// Gets the current user (UserDto format)
        /// </summary>
        UserDto? CurrentUser { get; }
        
        /// <summary>
        /// Gets the current user details (UserDetailsDto format)
        /// </summary>
        UserDetailsDto? CurrentUserDetails { get; }
        
        #endregion

        #region Token Properties
        
        /// <summary>
        /// Gets the current authentication token (only if valid)
        /// </summary>
        string? CurrentToken { get; }
        
        /// <summary>
        /// Gets the current refresh token (only if valid)
        /// </summary>
        string? CurrentRefreshToken { get; }
        
        /// <summary>
        /// Gets the token expiration time
        /// </summary>
        DateTime TokenExpiration { get; }
        
        /// <summary>
        /// Gets the refresh token expiration time
        /// </summary>
        DateTime RefreshTokenExpiration { get; }
        
        #endregion

        #region Status Properties
        
        /// <summary>
        /// Checks if the current user is logged in
        /// </summary>
        bool IsLoggedIn { get; }
        
        /// <summary>
        /// Checks if the current token is valid
        /// </summary>
        bool IsTokenValid { get; }
        
        /// <summary>
        /// Checks if the current refresh token is valid
        /// </summary>
        bool IsRefreshTokenValid { get; }
        
        /// <summary>
        /// Checks if the user cache is still valid
        /// </summary>
        bool IsUserCacheValid { get; }
        
        #endregion

        #region User Data Management
        
        /// <summary>
        /// Sets the current user data from UserDto
        /// </summary>
        /// <param name="user">The user data to set</param>
        void SetCurrentUser(UserDto? user);
        
        /// <summary>
        /// Sets the current user data from UserDetailsDto
        /// </summary>
        /// <param name="userDetails">The user details to set</param>
        void SetCurrentUserDetails(UserDetailsDto? userDetails);
        
        /// <summary>
        /// Clears all user data
        /// </summary>
        void ClearUserData();
        
        #endregion

        #region Token Management
        
        /// <summary>
        /// Sets the authentication tokens
        /// </summary>
        /// <param name="token">The authentication token</param>
        /// <param name="tokenExpiration">When the token expires</param>
        /// <param name="refreshToken">The refresh token (optional)</param>
        /// <param name="refreshTokenExpiration">When the refresh token expires (optional)</param>
        void SetTokens(string? token, DateTime tokenExpiration, string? refreshToken = null, DateTime? refreshTokenExpiration = null);
        
        /// <summary>
        /// Clears all token data
        /// </summary>
        void ClearTokens();
        
        #endregion

        #region Utility Methods
        
        /// <summary>
        /// Checks if the current user has a specific role
        /// </summary>
        /// <param name="role">The role to check for</param>
        /// <returns>True if the user has the role, false otherwise</returns>
        bool HasRole(string role);
        
        /// <summary>
        /// Gets the current user ID
        /// </summary>
        /// <returns>The user ID, or 0 if no user is logged in</returns>
        int GetCurrentUserId();
        
        /// <summary>
        /// Initializes the session by loading data from persistent storage
        /// </summary>
        /// <returns>Task representing the async operation</returns>
        Task InitializeAsync();
        
        /// <summary>
        /// Completely clears the user session
        /// </summary>
        void ClearSession();
        
        /// <summary>
        /// Completely clears the user session including persistent storage
        /// </summary>
        /// <returns>Task representing the async operation</returns>
        Task ClearSessionAsync();
        
        /// <summary>
        /// Refreshes the user cache expiry
        /// </summary>
        void RefreshUserCache();
        
        #endregion
    }
}