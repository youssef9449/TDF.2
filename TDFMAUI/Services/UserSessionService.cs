using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TDFShared.DTOs.Auth;
using TDFShared.DTOs.Users;

namespace TDFMAUI.Services
{
    /// <summary>
    /// Centralized service for managing user session data in memory
    /// Consolidates all user-related data storage and provides a single source of truth
    /// </summary>
    public class UserSessionService : IUserSessionService
    {
        private readonly ILogger<UserSessionService> _logger;
        private readonly SecureStorageService _secureStorageService;
        private readonly ILocalStorageService? _localStorageService;
        private readonly object _lockObject = new object();
        
        // Single source of truth for current user data
        private UserDto? _currentUser;
        private UserDetailsDto? _currentUserDetails;
        private string? _currentToken;
        private DateTime _tokenExpiration = DateTime.MinValue;
        private string? _currentRefreshToken;
        private DateTime _refreshTokenExpiration = DateTime.MinValue;
        
        // Cache management
        private DateTime _userCacheExpiry = DateTime.MinValue;
        private readonly TimeSpan _userCacheTimeout = TimeSpan.FromMinutes(5);
        
        // Initialization flag
        private bool _initialized = false;
        
        // Events for notifying components of user data changes
        public event EventHandler<UserChangedEventArgs>? UserChanged;
        public event EventHandler<TokenChangedEventArgs>? TokenChanged;

        public UserSessionService(ILogger<UserSessionService> logger, SecureStorageService secureStorageService, ILocalStorageService? localStorageService = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _secureStorageService = secureStorageService ?? throw new ArgumentNullException(nameof(secureStorageService));
            _localStorageService = localStorageService; // Optional dependency for user data caching
        }

        #region User Data Management

        /// <summary>
        /// Gets the current user (UserDto format)
        /// </summary>
        public UserDto? CurrentUser
        {
            get
            {
                // Ensure initialization for mobile devices
                if (!_initialized)
                {
                    // For synchronous access, we can't await, so trigger async init and return current state
                    _ = Task.Run(async () => await EnsureInitializedAsync());
                }

                lock (_lockObject)
                {
                    return _currentUser;
                }
            }
        }

        /// <summary>
        /// Gets the current user details (UserDetailsDto format)
        /// </summary>
        public UserDetailsDto? CurrentUserDetails
        {
            get
            {
                // Ensure initialization for mobile devices
                if (!_initialized)
                {
                    // For synchronous access, we can't await, so trigger async init and return current state
                    _ = Task.Run(async () => await EnsureInitializedAsync());
                }

                lock (_lockObject)
                {
                    return _currentUserDetails;
                }
            }
        }

        /// <summary>
        /// Sets the current user data from UserDto
        /// </summary>
        public void SetCurrentUser(UserDto? user)
        {
            UserDetailsDto? userDetails = null;
            
            lock (_lockObject)
            {
                var previousUser = _currentUser;
                _currentUser = user;
                _userCacheExpiry = DateTime.UtcNow.Add(_userCacheTimeout);
                
                // Convert to UserDetailsDto for compatibility
                if (user != null)
                {
                    _currentUserDetails = new UserDetailsDto
                    {
                        UserId = user.UserID,
                        UserName = user.UserName,
                        FullName = user.FullName,
                        Department = user.Department,
                        IsAdmin = user.IsAdmin ?? false,
                        IsManager = user.IsManager ?? false,
                        IsHR = user.IsHR ?? false,
                        Roles = user.Roles ?? new()
                    };
                    userDetails = _currentUserDetails;
                }
                else
                {
                    _currentUserDetails = null;
                }

                _logger.LogInformation("Current user updated: {UserName} (ID: {UserId})", 
                    user?.UserName ?? "null", user?.UserID ?? 0);
            }

            // Persist user data to local storage for mobile devices
            _ = Task.Run(async () =>
            {
                try
                {
                    if (_localStorageService != null)
                    {
                        if (user != null)
                        {
                            await _localStorageService.SetItemAsync("CurrentUser", user);
                            _logger.LogDebug("User data persisted to local storage");
                        }
                        else
                        {
                            await _localStorageService.RemoveItemAsync("CurrentUser");
                            _logger.LogDebug("User data removed from local storage");
                        }
                    }
                    else
                    {
                        _logger.LogDebug("LocalStorageService not available - user data not persisted");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error persisting user data to local storage");
                }
            });

            // Notify subscribers of user change
            UserChanged?.Invoke(this, new UserChangedEventArgs(user, userDetails));
        }

        /// <summary>
        /// Sets the current user data from UserDetailsDto
        /// </summary>
        public void SetCurrentUserDetails(UserDetailsDto? userDetails)
        {
            lock (_lockObject)
            {
                _currentUserDetails = userDetails;
                _userCacheExpiry = DateTime.UtcNow.Add(_userCacheTimeout);
                
                // Convert to UserDto for compatibility
                if (userDetails != null)
                {
                    _currentUser = new UserDto
                    {
                        UserID = userDetails.UserId,
                        UserName = userDetails.UserName ?? string.Empty,
                        FullName = userDetails.FullName ?? string.Empty,
                        Department = userDetails.Department ?? string.Empty,
                        IsAdmin = userDetails.IsAdmin,
                        IsManager = userDetails.IsManager,
                        IsHR = userDetails.IsHR,
                        Roles = userDetails.Roles ?? new()
                    };
                }
                else
                {
                    _currentUser = null;
                }

                _logger.LogInformation("Current user details updated: {UserName} (ID: {UserId})", 
                    userDetails?.UserName ?? "null", userDetails?.UserId ?? 0);
            }

            // Notify subscribers of user change
            UserChanged?.Invoke(this, new UserChangedEventArgs(_currentUser, userDetails));
        }

        /// <summary>
        /// Clears all user data
        /// </summary>
        public void ClearUserData()
        {
            lock (_lockObject)
            {
                _currentUser = null;
                _currentUserDetails = null;
                _userCacheExpiry = DateTime.MinValue;
                
                _logger.LogInformation("User data cleared");
            }

            // Notify subscribers of user change
            UserChanged?.Invoke(this, new UserChangedEventArgs(null, null));
        }

        #endregion

        #region Token Management

        /// <summary>
        /// Gets the current authentication token
        /// </summary>
        public string? CurrentToken
        {
            get
            {
                // Ensure initialization for mobile devices
                if (!_initialized)
                {
                    // For synchronous access, we can't await, so trigger async init and return current state
                    _ = Task.Run(async () => await EnsureInitializedAsync());
                }

                lock (_lockObject)
                {
                    return IsTokenValid ? _currentToken : null;
                }
            }
        }

        /// <summary>
        /// Gets the current refresh token
        /// </summary>
        public string? CurrentRefreshToken
        {
            get
            {
                // Ensure initialization for mobile devices
                if (!_initialized)
                {
                    // For synchronous access, we can't await, so trigger async init and return current state
                    _ = Task.Run(async () => await EnsureInitializedAsync());
                }

                lock (_lockObject)
                {
                    return IsRefreshTokenValid ? _currentRefreshToken : null;
                }
            }
        }

        /// <summary>
        /// Gets the token expiration time
        /// </summary>
        public DateTime TokenExpiration
        {
            get
            {
                lock (_lockObject)
                {
                    return _tokenExpiration;
                }
            }
        }

        /// <summary>
        /// Gets the refresh token expiration time
        /// </summary>
        public DateTime RefreshTokenExpiration
        {
            get
            {
                lock (_lockObject)
                {
                    return _refreshTokenExpiration;
                }
            }
        }

        /// <summary>
        /// Sets the authentication tokens
        /// </summary>
        public void SetTokens(string? token, DateTime tokenExpiration, string? refreshToken = null, DateTime? refreshTokenExpiration = null)
        {
            lock (_lockObject)
            {
                _currentToken = token;
                _tokenExpiration = tokenExpiration;
                _currentRefreshToken = refreshToken;
                _refreshTokenExpiration = refreshTokenExpiration ?? DateTime.MinValue;
                
                _logger.LogInformation("Tokens updated. Token expires: {TokenExpiry}, Refresh token expires: {RefreshExpiry}", 
                    tokenExpiration, refreshTokenExpiration);
            }

            // Persist tokens to secure storage for mobile devices
            _ = Task.Run(async () =>
            {
                try
                {
                    if (!string.IsNullOrEmpty(token))
                    {
                        await _secureStorageService.SaveTokenAsync(token, tokenExpiration, refreshToken!, refreshTokenExpiration);
                        _logger.LogDebug("Tokens persisted to secure storage");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error persisting tokens to secure storage");
                }
            });

            // Notify subscribers of token change
            TokenChanged?.Invoke(this, new TokenChangedEventArgs(token, tokenExpiration, refreshToken, refreshTokenExpiration));
        }

        /// <summary>
        /// Clears all token data
        /// </summary>
        public void ClearTokens()
        {
            lock (_lockObject)
            {
                _currentToken = null;
                _tokenExpiration = DateTime.MinValue;
                _currentRefreshToken = null;
                _refreshTokenExpiration = DateTime.MinValue;
                
                _logger.LogInformation("Tokens cleared");
            }

            // Notify subscribers of token change
            TokenChanged?.Invoke(this, new TokenChangedEventArgs(null, DateTime.MinValue, null, null));
        }

        #endregion

        #region Validation and Status

        /// <summary>
        /// Checks if the current user is logged in
        /// </summary>
        public bool IsLoggedIn
        {
            get
            {
                lock (_lockObject)
                {
                    return _currentUser != null && IsTokenValid;
                }
            }
        }

        /// <summary>
        /// Checks if the current token is valid
        /// </summary>
        public bool IsTokenValid
        {
            get
            {
                lock (_lockObject)
                {
                    return !string.IsNullOrEmpty(_currentToken) && 
                           _tokenExpiration > DateTime.UtcNow.AddMinutes(5); // 5 min buffer
                }
            }
        }

        /// <summary>
        /// Checks if the current refresh token is valid
        /// </summary>
        public bool IsRefreshTokenValid
        {
            get
            {
                lock (_lockObject)
                {
                    return !string.IsNullOrEmpty(_currentRefreshToken) && 
                           _refreshTokenExpiration > DateTime.UtcNow.AddMinutes(5); // 5 min buffer
                }
            }
        }

        /// <summary>
        /// Checks if the user cache is still valid
        /// </summary>
        public bool IsUserCacheValid
        {
            get
            {
                lock (_lockObject)
                {
                    return DateTime.UtcNow < _userCacheExpiry;
                }
            }
        }

        /// <summary>
        /// Checks if the current user has a specific role
        /// </summary>
        public bool HasRole(string role)
        {
            if (string.IsNullOrEmpty(role)) return false;
            
            lock (_lockObject)
            {
                return _currentUser?.Roles?.Contains(role, StringComparer.OrdinalIgnoreCase) ?? false;
            }
        }

        /// <summary>
        /// Gets the current user ID
        /// </summary>
        public int GetCurrentUserId()
        {
            lock (_lockObject)
            {
                return _currentUser?.UserID ?? 0;
            }
        }

        #endregion

        #region Session Management

        /// <summary>
        /// Initializes the session by loading data from persistent storage (for mobile devices)
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_initialized) return;

            try
            {
                _logger.LogInformation("Initializing UserSessionService from persistent storage");

                // Load tokens from secure storage
                var (token, tokenExpiration) = await _secureStorageService.GetTokenAsync();
                var (refreshToken, refreshTokenExpiration) = await _secureStorageService.GetRefreshTokenAsync();

                if (!string.IsNullOrEmpty(token))
                {
                    lock (_lockObject)
                    {
                        _currentToken = token;
                        _tokenExpiration = tokenExpiration;
                        _currentRefreshToken = refreshToken;
                        _refreshTokenExpiration = refreshTokenExpiration;
                    }

                    _logger.LogInformation("Restored tokens from secure storage. Token expires: {TokenExpiry}", tokenExpiration);

                    // Try to load user data from local storage if available
                    await TryLoadUserDataFromStorageAsync();
                }
                else
                {
                    _logger.LogInformation("No tokens found in secure storage - user not logged in");
                }

                _initialized = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing UserSessionService from persistent storage");
                _initialized = true; // Mark as initialized even on error to prevent retry loops
            }
        }

        /// <summary>
        /// Tries to load user data from local storage
        /// </summary>
        private async Task TryLoadUserDataFromStorageAsync()
        {
            try
            {
                // Try to get cached user data from local storage
                if (_localStorageService != null)
                {
                    var cachedUser = await _localStorageService.GetItemAsync<UserDto>("CurrentUser");
                    if (cachedUser != null)
                    {
                        lock (_lockObject)
                        {
                            _currentUser = cachedUser;
                            _currentUserDetails = new UserDetailsDto
                            {
                                UserId = cachedUser.UserID,
                                UserName = cachedUser.UserName,
                                FullName = cachedUser.FullName,
                                Department = cachedUser.Department,
                                IsAdmin = cachedUser.IsAdmin ?? false,
                                IsManager = cachedUser.IsManager ?? false,
                                IsHR = cachedUser.IsHR ?? false,
                                Roles = cachedUser.Roles ?? new()
                            };
                            _userCacheExpiry = DateTime.UtcNow.Add(_userCacheTimeout);
                        }

                        _logger.LogInformation("Restored user data from local storage: {UserName}", cachedUser.UserName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load user data from local storage");
            }
        }

        /// <summary>
        /// Ensures the session is initialized before accessing data
        /// </summary>
        private async Task EnsureInitializedAsync()
        {
            if (!_initialized)
            {
                await InitializeAsync();
            }
        }

        /// <summary>
        /// Completely clears the user session
        /// </summary>
        public void ClearSession()
        {
            ClearUserData();
            ClearTokens();
            _logger.LogInformation("User session completely cleared");
        }

        /// <summary>
        /// Completely clears the user session including persistent storage
        /// </summary>
        public async Task ClearSessionAsync()
        {
            ClearUserData();
            ClearTokens();
            
            // Clear from persistent storage as well
            try
            {
                await _secureStorageService.RemoveTokenAsync();
                _logger.LogInformation("User session and persistent storage cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing persistent storage during session clear");
            }
        }

        /// <summary>
        /// Refreshes the user cache expiry
        /// </summary>
        public void RefreshUserCache()
        {
            lock (_lockObject)
            {
                _userCacheExpiry = DateTime.UtcNow.Add(_userCacheTimeout);
            }
        }

        #endregion
    }

    #region Event Args

    public class UserChangedEventArgs : EventArgs
    {
        public UserDto? User { get; }
        public UserDetailsDto? UserDetails { get; }

        public UserChangedEventArgs(UserDto? user, UserDetailsDto? userDetails)
        {
            User = user;
            UserDetails = userDetails;
        }
    }

    public class TokenChangedEventArgs : EventArgs
    {
        public string? Token { get; }
        public DateTime TokenExpiration { get; }
        public string? RefreshToken { get; }
        public DateTime? RefreshTokenExpiration { get; }

        public TokenChangedEventArgs(string? token, DateTime tokenExpiration, string? refreshToken, DateTime? refreshTokenExpiration)
        {
            Token = token;
            TokenExpiration = tokenExpiration;
            RefreshToken = refreshToken;
            RefreshTokenExpiration = refreshTokenExpiration;
        }
    }

    #endregion
}