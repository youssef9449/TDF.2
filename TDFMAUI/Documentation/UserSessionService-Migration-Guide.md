# UserSessionService Migration Guide

## Overview

This document outlines the consolidation of user data storage from multiple scattered locations into a centralized `UserSessionService`. This change improves data consistency, reduces memory usage, and provides a single source of truth for user session data.

## What Changed

### Before (Multiple Storage Locations)
- `App.CurrentUser` (static property)
- `UserProfileService._currentUser` (instance field)
- `AuthService._currentToken` and `_tokenExpiration` (instance fields)
- `ApiConfig.CurrentToken` and related static properties
- `RequestApprovalViewModel._cachedCurrentUser` (instance field with expiry)
- Various other ViewModels with their own user caching

### After (Centralized Storage)
- **Single source**: `UserSessionService` manages all user session data
- **Backward compatibility**: `App.CurrentUser` still works but delegates to `UserSessionService`
- **Automatic synchronization**: All components use the same data source
- **Event-driven updates**: Components can subscribe to user/token change events

## Key Benefits

1. **Data Consistency**: No more sync issues between different storage locations
2. **Memory Efficiency**: Single copy of user data instead of multiple duplicates
3. **Cache Management**: Centralized cache expiry and refresh logic
4. **Type Safety**: Consistent handling of `UserDto` vs `UserDetailsDto`
5. **Event System**: Components can react to user data changes automatically

## Migration Steps Completed

### 1. Created UserSessionService
- **File**: `TDFMAUI/Services/UserSessionService.cs`
- **Interface**: `TDFMAUI/Services/IUserSessionService.cs`
- **Features**:
  - Thread-safe user data storage
  - Token management with validation
  - Cache expiry handling
  - Event notifications for data changes
  - Role-based access checks

### 2. Updated Dependency Injection
- **File**: `TDFMAUI/MauiProgram.cs`
- **Changes**:
  - Registered `IUserSessionService` as singleton
  - Connected service to `App` and `ApiConfig`
  - Initialized during app startup

### 3. Updated AuthService
- **File**: `TDFMAUI/Services/AuthService.cs`
- **Changes**:
  - Added `IUserSessionService` dependency
  - Replaced `App.CurrentUser` assignments with `_userSessionService.SetCurrentUser()`
  - Updated token storage to use session service
  - Modified logout to clear session via service

### 4. Updated UserProfileService
- **File**: `TDFMAUI/Services/UserProfileService.cs`
- **Changes**:
  - Removed local `_currentUser` field
  - Delegated all operations to `UserSessionService`
  - Maintained interface compatibility

### 5. Updated RequestApprovalViewModel
- **File**: `TDFMAUI/ViewModels/RequestApprovalViewModel.cs`
- **Changes**:
  - Removed local user caching (`_cachedCurrentUser`)
  - Added `IUserSessionService` dependency
  - Updated methods to use centralized session service

### 6. Updated ApiConfig
- **File**: `TDFMAUI/Config/ApiConfig.cs`
- **Changes**:
  - Connected to `UserSessionService` for token management
  - Maintained backward compatibility for existing code
  - Added fallback mechanism for when service is not available

### 7. Updated App.xaml.cs
- **File**: `TDFMAUI/App.xaml.cs`
- **Changes**:
  - Replaced static `_currentUser` field with delegation to `UserSessionService`
  - Added initialization method for connecting session service
  - Maintained backward compatibility for `App.CurrentUser` property

## Usage Examples

### Getting Current User
```csharp
// Old way (still works)
var user = App.CurrentUser;

// New way (recommended)
var user = _userSessionService.CurrentUser;
```

### Setting User Data
```csharp
// Old way
App.CurrentUser = newUser;
_userProfileService.SetUserDetails(userDetails);

// New way (automatically syncs both)
_userSessionService.SetCurrentUser(newUser);
// OR
_userSessionService.SetCurrentUserDetails(userDetails);
```

### Checking Authentication
```csharp
// Old way
bool isLoggedIn = App.CurrentUser != null;

// New way
bool isLoggedIn = _userSessionService.IsLoggedIn;
```

### Token Management
```csharp
// Old way
ApiConfig.CurrentToken = token;
ApiConfig.TokenExpiration = expiry;

// New way
_userSessionService.SetTokens(token, expiry, refreshToken, refreshExpiry);
```

### Role Checking
```csharp
// Old way
bool isAdmin = App.CurrentUser?.IsAdmin ?? false;
bool hasRole = App.CurrentUser?.Roles?.Contains("Manager") ?? false;

// New way
bool isAdmin = _userSessionService.CurrentUser?.IsAdmin ?? false;
bool hasRole = _userSessionService.HasRole("Manager");
```

## Event Handling

### Subscribe to User Changes
```csharp
public class MyViewModel
{
    public MyViewModel(IUserSessionService userSessionService)
    {
        userSessionService.UserChanged += OnUserChanged;
        userSessionService.TokenChanged += OnTokenChanged;
    }

    private void OnUserChanged(object sender, UserChangedEventArgs e)
    {
        // React to user data changes
        OnPropertyChanged(nameof(IsUserLoggedIn));
        OnPropertyChanged(nameof(UserDisplayName));
    }

    private void OnTokenChanged(object sender, TokenChangedEventArgs e)
    {
        // React to token changes
        if (string.IsNullOrEmpty(e.Token))
        {
            // User logged out
            NavigateToLogin();
        }
    }
}
```

## Backward Compatibility

All existing code should continue to work without changes:

- `App.CurrentUser` still accessible (delegates to `UserSessionService`)
- `ApiConfig.CurrentToken` still works (backed by `UserSessionService`)
- `UserProfileService.CurrentUser` still available
- All ViewModels continue to function normally

## Testing Considerations

### Unit Tests
- Mock `IUserSessionService` in tests
- Test event firing when user data changes
- Verify thread safety of concurrent access

### Integration Tests
- Test user login/logout flows
- Verify data consistency across components
- Test token refresh scenarios

## Performance Improvements

1. **Reduced Memory Usage**: Single copy of user data instead of multiple duplicates
2. **Faster Access**: Direct property access instead of async token parsing
3. **Better Caching**: Centralized cache management with proper expiry
4. **Fewer Allocations**: Reuse of user objects instead of creating new instances

## Future Enhancements

1. **Persistence**: Add optional user data persistence across app restarts
2. **Encryption**: Add encryption for sensitive user data in memory
3. **Audit Trail**: Track user data changes for debugging
4. **Multi-User**: Support for multiple user sessions (if needed)

## Troubleshooting

### Common Issues

1. **Null Reference**: Ensure `UserSessionService` is properly injected
2. **Events Not Firing**: Check event subscription in constructors
3. **Data Not Syncing**: Verify all components use `UserSessionService`

### Debug Tips

1. Enable debug logging for `UserSessionService`
2. Check service registration in `MauiProgram.cs`
3. Verify initialization order in app startup

## Conclusion

The `UserSessionService` consolidation provides a robust, efficient, and maintainable solution for user session management. The migration maintains full backward compatibility while providing significant improvements in data consistency and performance.