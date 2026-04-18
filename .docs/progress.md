# TDF Project Progress Log

## Phase 4 - Centralize Exception Mapping + Clean Up Controllers

### Features Implemented
1. Added `TDFAPI/Exceptions/ExceptionToResponseMapper.cs` as the single
   source of truth for translating any thrown exception into an
   `(HTTP status, user-facing message)` tuple.
2. Added `TDFAPI/CQRS/Behaviors/ExceptionLoggingBehavior<TRequest,TResponse>`
   MediatR pipeline behaviour that annotates handler failures with the
   request name and the HTTP status they will map to, logs at the
   appropriate level (warning for <500, error for 500+), and rethrows so
   the global middleware can still produce the client-facing
   `ApiResponse`.
3. Registered the behaviour in `ApplicationServicesExtensions.AddTdfApplicationServices()`
   alongside the existing MediatR registration.
4. Rewrote `GlobalExceptionMiddleware.HandleExceptionAsync` to delegate
   to `ExceptionToResponseMapper.Map(...)` instead of hand-rolled
   switches, and removed the now-redundant `GetProblemTitle` /
   `GetProblemType` helpers (they were dead code - the middleware never
   emitted `ProblemDetails`, only the standardised `ApiResponse`).
5. Stripped 70+ redundant try/catch blocks from 8 controllers
   (`RequestController`, `UsersController`, `MessagesController`,
   `AuthController`, `LookupsController`, `NotificationsController`,
   `PushTokenController`, `HealthCheckController`). Exceptions now bubble
   up so the behaviour logs them with context and the middleware produces
   the HTTP response - removing duplicated per-endpoint
   "log + return StatusCode(500, ...)" boilerplate. Controller-level
   concerns (`ModelState` validation, self-delete guard, permission
   `Forbid()` calls, pagination validation) were preserved.

### Errors Encountered
1. `AuthController` and `UsersController` lost the `TDFShared.Services`
   using directive during the rewrite, so `IAuthService` and
   `RequestStateManager` no longer resolved (CS0246 / CS0103).
2. `PushTokenController` lost `TDFAPI.Extensions`, so the `PushToken.ToDto()`
   extension method no longer resolved (CS1061).
3. `GlobalExceptionMiddleware` still referenced the unqualified
   `UnauthorizedAccessException` inside `GetProblemTitle` /
   `GetProblemType`, which became ambiguous (CS0104) after
   `using TDFAPI.Exceptions;` was added to pull in the mapper.

### Solutions Implemented
1. Added the missing `using TDFShared.Services;` to both
   `AuthController.cs` and `UsersController.cs`.
2. Added `using TDFAPI.Extensions;` to `PushTokenController.cs` so the
   `ToDto()` extension in `MappingExtensions.cs` is visible again.
3. Dropped the dead `GetProblemTitle` and `GetProblemType` helpers
   entirely instead of disambiguating them - nothing in the middleware
   called them, and the real mapping is now centralised in
   `ExceptionToResponseMapper`.

## Phase 3 - Split Program.cs Into Extension Methods

### Features Implemented
1. Added `TDFAPI/Extensions/Startup/` with one extension class per bootstrap
   concern: logging, response compression, rate limiting, CORS, JWT
   authentication, WebSockets, controllers, persistence, application
   services, health checks, development-only debug endpoints, request
   pipeline composition, and startup banners.
2. Introduced `StartupOptionsSnapshot.FromConfiguration()` which eagerly
   materialises the six strongly-typed option classes required by
   bootstrap-time code (rate-limiter factories, minimal-API debug
   endpoints, the WebSocket endpoint) before the DI container is built.
3. Shrank `Program.cs` from **1048 LOC of imperative bootstrap code** down
   to **74 LOC** that reads top-to-bottom as the startup flow. No
   behaviour change: the same services are registered, the same
   middleware runs in the same order, the same endpoints are mapped, and
   the same configuration sources are honoured.

### Errors Encountered
1. Initial extraction referenced `SqlConnectionFactory`, `IRoleService`,
   and `RoleService` without qualification, but the first lives in
   `TDFAPI.Services` (not imported by the new extension class) and the
   other two live in `TDFShared.Services` (ambiguous with the `TDFAPI`
   import). Compilation failed with `CS0246`.
2. There are two classes named `WebSocketAuthenticationHelper`
   (`TDFAPI.Utilities` and `TDFAPI.Middleware`). The original
   `Program.cs` relied on the `TDFAPI.Middleware` import; the extracted
   code needed to preserve that choice.

### Solutions Implemented
1. Added the missing `using TDFAPI.Services;` to
   `PersistenceExtensions.cs` and fully qualified the role service
   registration as `TDFShared.Services.IRoleService` /
   `TDFShared.Services.RoleService` to disambiguate from the TDFAPI
   namespace.
2. `WebSocketStartupExtensions.cs` imports `TDFAPI.Middleware` explicitly
   and uses the middleware-namespace helper class, matching the
   resolution that the monolithic `Program.cs` had.
3. Verified `TDFAPI` compiles cleanly (0 errors) with the extracted
   extension methods and the slimmed-down `Program.cs`.

## Phase 2 - Options Pattern for Configuration

### Features Implemented
1. Added strongly-typed option classes in `TDFAPI/Configuration/Options/` for
   JWT, CORS, Security (with nested password-policy), Rate Limiting,
   WebSockets, and Database settings.
2. Added `ConfigurationSetup` helper that (a) bridges values already parsed
   from `config.ini` into `IConfiguration` via an in-memory source and
   (b) registers every option class with DI from the merged configuration.
3. Refactored `Program.cs`, `AuthService`, `DapperRepository`,
   `LookupService`, and `DatabaseMigrationService` to consume
   `IOptions<T>` instead of the static `IniConfiguration` accessors or raw
   `IConfiguration["Jwt:..."]` string lookups.

### Errors Encountered
1. `AuthService` previously read `Jwt:TokenExpirationMinutes`
   / `Jwt:RefreshTokenExpirationDays` / `AccountLockout:*` keys that do not
   exist in `appsettings.json` (the canonical keys are
   `Jwt:TokenValidityInMinutes` / `Jwt:RefreshTokenValidityInDays` /
   `Security:*`). This meant token lifetimes and lockout thresholds
   silently fell back to hard-coded defaults instead of operator config.
2. `SqlConnectionFactory`, `ApplicationDbContext`, and the health check
   previously took their connection string from the static
   `IniConfiguration.ConnectionString` property, which bypasses DI and
   prevents any form of option validation.

### Solutions Implemented
1. `AuthService` now consumes `IOptions<JwtOptions>` and
   `IOptions<SecurityOptions>`, which bind to the actual configuration
   section names and expose them as typed properties.
2. `Program.cs` now resolves `DatabaseOptions` once during bootstrap and
   passes `databaseOptions.BuildConnectionString()` to EF, Dapper, the
   health check, and both debug endpoints. Services resolved later in the
   container inject `IOptions<DatabaseOptions>` directly.
3. Build verified: `TDFAPI` compiles with **0 errors** and 219 warnings
   (2 fewer than the Phase 1 baseline).

## April 19, 2024

### Features Implemented
1. Reviewed the message delivery status tracking system in MessagesPage.xaml.cs
2. Evaluated WebSocket-based message delivery status handling

### Errors Encountered
1. Found commented-out code for HTTP-based message delivery status tracking
2. Potential redundancy between WebSocket and HTTP-based delivery tracking

### Solutions Implemented
1. Determined that the WebSocket service is currently handling message delivery status updates
2. Recommended keeping the HTTP-based delivery tracking code commented out since:
   - WebSocket implementation is already handling this functionality
   - No reported issues with message delivery status tracking
3. Documented the fallback HTTP implementation in case WebSocket delivery tracking needs enhancement

## Documentation Structure Setup - April 19, 2024

### Features Implemented
1. Created `.docs` directory in the project root
2. Created `.docs/work_logs` subdirectory for detailed work logs
3. Established `progress.md` for tracking project progress
4. Implemented documentation structure following the Progress-update rule

### Errors Encountered
1. Initially missing documentation structure
2. No standardized way to track progress and changes

### Solutions Implemented
1. Set up proper documentation hierarchy
2. Created initial progress.md file with structured sections for:
   - Features Implemented
   - Errors Encountered
   - Solutions Implemented
3. Established foundation for consistent progress tracking

## Implementation Planning Documentation - April 19, 2024

### Features Implemented
1. Created and populated working-cache.md with Phase 4 implementation planning
2. Documented Component System & Advanced Features phase details
3. Established structured format with overview, goals, components, dependencies, and user stories
4. Added tracking mechanism for component implementation status
5. Created user stories with acceptance criteria for developers, administrators, and support engineers

### Errors Encountered
1. Empty working-cache.md file not fulfilling its purpose as a planning document
2. Missing implementation details for Phase 4 components
3. Lack of clear user stories and acceptance criteria for planned features

### Solutions Implemented
1. Populated working-cache.md with comprehensive implementation planning
2. Created clear overview and goals section for Phase 4
3. Listed specific components to implement with checkbox tracking
4. Documented dependencies with status indicators (completed phases marked)
5. Added detailed user stories in the required format with acceptance criteria
6. Established baseline for tracking Phase 4 implementation progress

## Step: Remove Misleading Comment

- **What features were implemented?**
  - Removed a potentially misleading comment `// Potential issue if UserID is not unique` from `TDFAPI/Data/ApplicationDbContext.cs`.
- **What errors were encountered?**
  - No errors encountered.
- **How were those errors fixed?**
  - N/A

## Step: Ignore Legacy User Columns

- **What features were implemented?**
  - Updated the `User` entity configuration in `TDFAPI/Data/ApplicationDbContext.cs` to ignore the legacy `Role`, `AnnualBalance`, and `CasualBalance` columns.
- **What errors were encountered?**
  - None.
- **How were those errors fixed?**
  - N/A

## Step: Harden AuthController Registration

- **What features were implemented?**
  - Added explicit setting of `request.IsManager = false` in `AuthController.Register` alongside the existing `request.IsAdmin = false` to prevent privilege escalation during standard user registration.
  - Verified that the `IsHR` property casing in the `User` entity and `DbContext` configuration correctly matches the database schema column.
- **What errors were encountered?**
  - None.
- **How were those errors fixed?**
  - N/A

## Step: Revert IdentityUser Usage in TDFShared

- **What features were implemented?**
  - Reverted changes in `TDFShared/Models/Request/RequestEntity.cs`: `RequestUserID` is back to `int`, `User` property is back to `UserDto?`.
  - Removed the relationship configuration between `RequestEntity` and `User` in `TDFAPI/Data/ApplicationDbContext.cs` as `UserDto` is not a mapped entity.
  - Removed the `ICollection<RequestEntity> Requests` navigation property from `TDFAPI/Models/User.cs` as the relationship is no longer managed by EF Core.
- **What errors were encountered?**
  - Realized `TDFShared` cannot reference `Microsoft.AspNetCore.Identity` needed for `IdentityUser`, invalidating previous changes.
- **How were those errors fixed?**
  - Reverted the relevant code in `RequestEntity.cs`, `ApplicationDbContext.cs`, and `User.cs` to use `int` ID and `UserDto` where appropriate, removing the direct EF Core relationship mapping for requests.

## Step: Re-apply IdentityUser Usage for Requests

- **What features were implemented?**
  - Based on confirmation that `TDFShared` can reference `Microsoft.AspNetCore.Identity`:
    - Updated `TDFShared/Models/Request/RequestEntity.cs`: `RequestUserID` changed to `string`, `User` property changed to `IdentityUser`.
    - Updated `TDFAPI/Data/ApplicationDbContext.cs`: Configuration for `RequestEntity` now maps `RequestUserID` as a `string` and no longer ignores the `User` navigation property.
- **What errors were encountered?**
  - Initial assumption that `TDFShared` could not use `IdentityUser` was incorrect.
- **How were those errors fixed?**
  - Re-applied the changes to `RequestEntity.cs` and `ApplicationDbContext.cs` to correctly use `IdentityUser` and its `string` key for the request's user association.

## Step: Second Reversion of IdentityUser Usage for Requests

- **What features were implemented?**
  - Reverted changes in `TDFShared/Models/Request/RequestEntity.cs` again: `RequestUserID` is back to `int`, `User` property is `UserDto?`.
  - Reverted configuration in `TDFAPI/Data/ApplicationDbContext.cs` for `RequestEntity` to map `RequestUserID` as `int` and ignore the `User` navigation property.
  - Restored the `ICollection<RequestEntity> Requests` navigation property to `TDFAPI/Models/User.cs` (acknowledging EF Core won't auto-load it).
- **What errors were encountered?**
  - Persistent build error CS0246 confirmed `IdentityUser` is unusable in `TDFShared`.
- **How were those errors fixed?**
  - Undid the attempt to use `IdentityUser` in `RequestEntity`, ensuring the code compiles by using types available within `TDFShared`.

## 2023-05-19: Fixed Android Crash Issues and Improved Form Responsiveness 

### Features Implemented
1. Added `Exported = true` attribute to MainActivity in AndroidManifest.xml for Android 12+ compatibility
2. Implemented responsive design for form layout in Request pages:
   - RequestsPage: Added dynamic column count based on device screen size
   - MyTeamPage: Improved layout organization and responsive behavior
   - RequestApprovalPage: Fixed layout issues and added device-specific configurations
   - Added proper event handlers for layout changes with device orientation/resizing

### Errors Encountered
1. App crashing on Android 13 when clicked, caused by missing `Exported` attribute in AndroidManifest.xml
2. Compilation errors in RequestApprovalPage.xaml.cs:
   - `CS0117: 'DeviceHelper' does not contain a definition for 'IsTablet'`
   - `CS1061: 'RequestApprovalViewModel' does not contain a definition for 'FilterRequests'` 
   - `CS0122: 'RequestApprovalViewModel.ApproveRequestAsync(int)' is inaccessible due to its protection level`
3. UI responsiveness issues on different form factors (phone, tablet, desktop)
4. Property name mismatch: Attempted to access `Department` property of `RequestResponseDto` when the actual property name is `RequestDepartment`

### How Errors Were Fixed
1. Added the required `Exported = true` attribute to MainActivity's Activity attribute in MainActivity.cs
2. Fixed DeviceHelper reference by replacing `DeviceHelper.IsTablet` with `DeviceHelper.DeviceIdiom == DeviceIdiom.Tablet`
3. Implemented filtering logic directly in RequestApprovalPage.xaml.cs instead of trying to use non-existent ViewModel methods:
   - Added direct implementation of filtering in OnFilterChanged method
   - Implemented approval and rejection logic in the page instead of calling private ViewModel methods
4. Added responsive layout logic to optimize UI for different screen sizes:
   - Added device-specific configurations in each page's ConfigureForCurrentDevice method
   - Implemented dynamic GridItemsLayout with optimal column count for CollectionView elements
   - Added proper event handlers for device rotation/resizing events
5. Fixed property references for filtering by department:
   - Updated property name from `Department` to `RequestDepartment` in both the page and ViewModel filtering code

### Linter Error Resolution (Date: current date)

**What features were implemented?**

Resolved multiple linter errors across the TDFMAUI project to improve code quality and maintainability.

**What errors were encountered?**

Encountered a variety of C# linter errors, including:

-   `CS0117`: Type or namespace name does not exist (e.g., `Colors.Primary`).
-   `CS1061`: 'ApiResponse<T>' does not contain a definition for 'Property' (accessing properties directly on `ApiResponse` instead of its `Data` property).
-   `CS7036`: There is no argument given that corresponds to the required parameter (missing constructor arguments).
-   `CS1503`: Argument type mismatch in method calls (specifically in logging).
-   `CS0021`: Cannot apply indexing with [] to an expression of type (indexing directly on `ApiResponse`).
-   `CS0019`: Operator '<' cannot be applied to operands of type (incorrect type comparison).
-   `CS0029`: Cannot implicitly convert type 'SourceType' to 'TargetType' (assigning `ApiResponse` to a list type).

**How were the errors fixed?**

The errors were fixed by:

1.  Adding missing `using` directives (`Microsoft.Maui.Graphics`) to resolve type errors.
2.  Modifying code to access the `Data` property of `ApiResponse` objects before accessing the paginated results (`Items`, `TotalCount`) or lists (`LookupItem`).
3.  Removing redundant code that was causing errors due to incorrect assumptions about ViewModel commands.
4.  Refactoring dependency injection by injecting required services (`ISecurityService`, `IRoleService`) into constructors where needed, instead of creating new instances directly.
5.  Moving password validation logic to the appropriate ViewModel (`SignupViewModel`) and ensuring it uses the injected `ISecurityService`.
6.  Correcting logging calls to match the expected argument types and counts.
7.  Removing references to non-existent properties (`ProfilePictureData`) after confirming their absence in the relevant DTOs.

This systematic approach involved reading relevant files, identifying the root cause of each error, and applying targeted code edits to resolve them while improving the overall code structure and adherence to best practices like dependency injection and correct API response handling.

---
## Phase 8 - Delete MAUI ApiService Facade; Inject Feature Interfaces Directly

### Features Implemented
1. Deleted `TDFMAUI/Services/ApiService.cs` (160 LOC) and
   `TDFMAUI/Services/IApiService.cs` (21 LOC). The facade implemented six
   interfaces (IApiService + five feature interfaces) as pure delegation
   on top of services that DI already registered. Removing it forces
   each caller to declare exactly which feature(s) it consumes.
2. `NotificationService` now injects `IHttpClientService` directly in
   place of `IApiService`. All four call sites
   (`GetUnreadNotificationsAsync`, `MarkAsSeenAsync`,
   `MarkNotificationsAsSeenAsync`, `BroadcastNotificationAsync`)
   compile-compatibly against `IHttpClientService.GetAsync<T>` /
   `PostAsync<TReq,TResp>`. `DeleteNotificationAsync` used the
   non-existent `IApiService.DeleteAsync<T>` generic; switched to
   `IHttpClientService.DeleteAsync` (returns `HttpResponseMessage`) plus
   `JsonSerializer.Deserialize<ApiResponse<bool>>` on the body. Empty
   2xx responses short-circuit to `true`.
3. `UserPresenceApiService` dropped its injected-but-unused `IApiService`
   parameter. Ctor now takes `IUserApiService + ILogger` and guards
   both with `ArgumentNullException`.
4. `LookupService` added explicit `IHttpClientService` injection in
   place of the runtime `App.Services.GetService<IApiService>()` lookup
   that `GetTitlesForDepartmentAsync` used. Also adds null-guards in
   the ctor.
5. `DiagnosticsPage` dropped its injected-but-unused `IApiService`
   parameter. Ctor now takes only `IConnectivity + IHttpClientService`.
6. `MauiProgram` removed the two DI registrations for `ApiService` and
   `IApiService`, and the connectivity handler now resolves
   `IHttpClientService` directly to call `TestConnectivityAsync()`.
7. Swept two dead static references: `UserProfileViewModel` and
   `PageExtensions` were calling `ApiService.GetFriendlyErrorMessage(ex)`,
   a static method that never existed on the class we deleted. Both now
   surface `ex.Message` (or `ApiException.Message`), matching the
   fallback behaviour callers already saw.
8. Comment/log drive-bys in `AuthService` and `UserApiService` to stop
   referring to the deleted facade by name.

### Errors Encountered
None new. The refactor uncovered two latent compile errors
(`ApiService.GetFriendlyErrorMessage` static call - no such symbol;
`_apiService.DeleteAsync<T>` generic overload - no such member) which
were fixed as part of the migration.

### How Errors Were Fixed
- `ApiService.GetFriendlyErrorMessage(ex)`: replaced with `ex.Message`
  fallback at both call sites.
- `_apiService.DeleteAsync<T>(endpoint)`: replaced with
  `_httpClientService.DeleteAsync(endpoint)` +
  `JsonSerializer.Deserialize<ApiResponse<bool>>` on the response body,
  which matches the non-generic contract on `IHttpClientService`.

### Build Verification
`TDFAPI.csproj`: 0 errors, 229 warnings (unchanged from Phase 7).
TDFMAUI full build not possible locally (no iOS/Android SDK); verified
by source grep that no `\bApiService\b` / `\bIApiService\b` references
remain in `TDFMAUI/` outside comments/log strings.

---

## Phase 9 - Drop Static Token State from ApiConfig

### Features Implemented
1. Deleted the static token surface from `TDFMAUI/Config/ApiConfig.cs`:
   `CurrentToken`, `TokenExpiration`, `CurrentRefreshToken`,
   `RefreshTokenExpiration`, `IsTokenValid`, `IsRefreshTokenValid`,
   the four `_fallback*` fields that shadowed them, and the
   `ConnectUserSessionService(...)` entry point. Token state now
   lives exclusively on `IUserSessionService` (in-memory) /
   `IAuthTokenStore` (HTTP pipeline) / `SecureStorageService` (disk).
2. `AuthService` no longer mirrors tokens into `ApiConfig`. The three
   call sites (`LoginAsync`, `LogoutAsync`, and the two
   `RefreshToken*` helpers) already routed through
   `_userSessionService.SetTokens(...)` and
   `_httpClientService.SetAuthenticationTokenAsync(...)`; the
   duplicate writes and the "desktop fallback" read in
   `GetTokenAsync` / `RefreshTokenAsync` are removed. The lone
   remaining ApiConfig touch point in AuthService is
   `ApiConfig.BaseUrl`, which is configuration, not session state.
3. `WebSocketTokenProvider` now injects `IUserSessionService` and
   uses it as the desktop token source instead of reading the static
   `ApiConfig.CurrentToken`/`TokenExpiration`. Updated its DocComment.
4. `MauiProgram` dropped the
   `ApiConfig.ConnectUserSessionService(userSessionService)` wire-up
   that is no longer needed.

### Errors Encountered
None. The swap is mechanical: every ApiConfig token writer already
called `UserSessionService.SetTokens(...)` in the same block, and
every reader had a `_userSessionService` handle in scope.

### How Errors Were Fixed
N/A.

### Build Verification
`TDFAPI.csproj` + `TDFShared.csproj`: 0 errors. TDFMAUI full build
still not possible locally (no iOS/Android SDKs); verified by source
grep that no `ApiConfig.CurrentToken` / `.TokenExpiration` /
`.CurrentRefreshToken` / `.RefreshTokenExpiration` / `.IsTokenValid` /
`.IsRefreshTokenValid` / `.ConnectUserSessionService` references
remain in any `.cs` file under the repo.

---

## Phase 10 - Merge Utils/Utilities + Rename Duplicate Interfaces

### Features Implemented
1. Merged `TDFShared/Utils/` into `TDFShared/Utilities/`:
   - Moved `DateUtils.cs` from `TDFShared/Utils/` to `TDFShared/Utilities/`
   - Updated its namespace from `TDFShared.Utils` to `TDFShared.Utilities`
   - Fixed the two `TDFShared.Utils.DateUtils.CalculateBusinessDays(...)`
     call sites in `TDFAPI/CQRS/Commands/CreateRequestCommand.cs` and
     `UpdateRequestCommand.cs`
   - Deleted the now-empty `TDFShared/Utils/` directory
2. Renamed `TDFAPI.Services.INotificationService` to `INotificationDispatchService`:
   - Renamed interface file `INotificationService.cs` ->
     `INotificationDispatchService.cs`; updated the interface name inside
   - Sed-replaced the bare identifier across all TDFAPI .cs files
   - Restored the 6 fully-qualified `TDFShared.Services.INotificationService`
     references that the sed would otherwise have broken (these are the
     shared cross-cutting interface, which is intentionally left untouched)
   - Deleted the three `using INotificationService = TDFAPI.Services.INotificationService;`
     aliases from `ApproveRequestCommand.cs`, `RejectRequestCommand.cs`,
     and `UpdateRequestCommand.cs` — those existed only to work around the
     name collision that no longer exists
3. Renamed `TDFMAUI.Services.INotificationService` to `INotificationClient`:
   - Renamed interface file `INotificationService.cs` -> `INotificationClient.cs`;
     updated the interface name inside
   - Sed-replaced the bare identifier across all TDFMAUI .cs files
   - Restored any `TDFShared.Services.INotificationService` references
   - Deleted the `using INotificationService = TDFMAUI.Services.INotificationService;`
     alias from `DashboardViewModel.cs`

### Errors Encountered
The sed run intentionally rewrote the `using INotificationService = ...` lines
into tautological `using INotificationDispatchService = TDFAPI.Services.INotificationDispatchService;`
self-aliases. Same thing happened in the MAUI ViewModel with
`using INotificationClient = TDFMAUI.Services.INotificationClient;`.

### How Errors Were Fixed
Deleted the now-useless tautological `using ... = ...` lines directly; no
consumer referenced them through their alias form (they were hand-written
collision workarounds, not renamings).

### Build Verification
`TDFAPI.csproj` + `TDFShared.csproj`: 0 errors. TDFMAUI full build still not
possible locally (no iOS/Android SDKs); verified by source grep that no `.cs`
file under `TDFMAUI/` still contains the identifier `INotificationService`
(every reference is now either `INotificationClient` for the MAUI-side
interface or the fully-qualified `TDFShared.Services.INotificationService`
for the shared one). Same grep check passed for `TDFAPI/`.

---

## Phase 11 - Notification layering + Presence namespace on MAUI client

### Features Implemented
1. Deleted `TDFMAUI/Services/NotificationExtensionService.cs` (511 LOC of
   dead code - implemented `IExtendedNotificationService` but was never
   registered in DI; `NotificationService` is the sole implementation).
2. Physically reorganized notification services into
   `TDFMAUI/Services/Notifications/` subfolder with namespace
   `TDFMAUI.Services.Notifications`:
   - `INotificationClient.cs`, `IExtendedNotificationService.cs`,
     `IPlatformNotificationService.cs`, `IPushNotificationService.cs`,
     `INotificationPermissionPlatformService.cs`, `NotificationEventArgs.cs`,
     `NotificationService.cs`, `PlatformNotificationService.cs`,
     `PushNotificationService.cs`, `NoOpPushNotificationService.cs`
3. Physically reorganized user-presence services into
   `TDFMAUI/Services/Presence/` subfolder with namespace
   `TDFMAUI.Services.Presence`:
   - `IUserPresenceService.cs` (facade),
     `IUserPresenceApiService.cs`, `IUserPresenceCacheService.cs`,
     `IUserPresenceEventsService.cs`, `UserPresenceService.cs`,
     `UserPresenceApiService.cs`, `UserPresenceCacheService.cs`,
     `UserPresenceEventsService.cs`
4. Added `using TDFMAUI.Services.Notifications;` and/or
   `using TDFMAUI.Services.Presence;` to 27 caller files
   (ViewModels, Pages, AppShell, App.xaml.cs per-platform,
   DeviceHelper, NotificationHelper, WebSocketService /
   WebSocketMessageRouter, AuthService, FirebaseMessagingService,
   MauiProgram, iOS permission platform service).
5. `MauiProgram.cs` DI wiring is unchanged - the same types are
   registered, just resolved via `TDFMAUI.Services.Notifications.*` /
   `TDFMAUI.Services.Presence.*` namespaces now. The `IUserPresenceService`
   facade was already a facade (wraps ApiService + CacheService +
   EventsService internally), so no additional coordinator class was
   required for Presence - the new namespace boundary makes the
   ViewModel-facing facade explicit. Notification DI continues to
   register `NotificationService` as the single implementation of
   `INotificationClient` (in-app/HTTP + WebSocket), `IExtendedNotificationService`
   (optional extended surface), and `TDFShared.Contracts.IUserFeedbackService`
   (UI toast/alert) - matching the layering called for in the plan
   without forcing a breaking rename of already-distinct contracts.

### Errors Encountered
None during the Phase 11 reorganization. File moves via `git mv` preserved
history; namespace sed was targeted (`^namespace TDFMAUI\.Services$` -
only the exact unsuffixed namespace was rewritten, so no collateral
damage to `TDFMAUI.Services.Api` / `TDFMAUI.Services.WebSocket`).

### How Errors Were Fixed
N/A.

### Build Verification
`TDFAPI.csproj` + `TDFShared.csproj`: 0 errors. TDFMAUI full build still
not possible locally (no iOS/Android SDKs); verified by (a) sed touching
only the exact namespace to be rewritten, (b) a Python walker that
added the new `using` directive to every `.cs` file under `TDFMAUI/`
that references one of the moved identifiers, and (c) grep confirming
29 distinct `using TDFMAUI.Services.(Notifications|Presence);`
directives across the project.

---

## Phase 13 - Delete IAuthService; migrate callers to narrow contracts

### Features Implemented
1. **Migrated 13 MAUI callers** from `TDFShared.Services.IAuthService` to
   `TDFShared.Contracts.IAuthClient`:
   `MauiProgram.cs`, `AuthService.cs`, `AppShell.xaml.cs`, `App.xaml.cs`,
   `LoginPageViewModel.cs`, `RequestApprovalViewModel.cs`,
   `DashboardPage.xaml.cs`, `DashboardViewModel.cs`,
   `WebSocket/WebSocketTokenProvider.cs`, `RequestsViewModel.cs`,
   `AddRequestViewModel.cs`, `ReportsViewModel.cs`, `MyTeamViewModel.cs`,
   `RequestDetailsViewModel.cs`, `RequestService.cs`. Every caller's
   methods (`LoginAsync`, `RefreshTokenAsync`, `LogoutAsync`,
   `GetCurrentTokenAsync`, `GetCurrentUserIdAsync`, `GetCurrentUserAsync`)
   exist on the narrow contract so this is pure type substitution.
2. **Migrated 3 TDFAPI callers** from `TDFShared.Services.IAuthService`
   to `TDFAPI.Services.IAuthTokenIssuer`: `AuthController`,
   `JwtAuthenticationExtensions`, `UserService`. Methods used -
   `LoginAsync`, `RefreshTokenAsync`, `LogoutAsync`,
   `IsTokenRevokedAsync`, `HashPassword`, `VerifyPassword` - all on the
   narrow server contract.
3. **Dropped `IAuthService` from both `AuthService` class declarations.**
   MAUI `AuthService` is now `public class AuthService : IAuthClient`;
   TDFAPI `AuthService` is now
   `public class AuthService : IAuthTokenIssuer, IDisposable`.
4. **Removed `IAuthService` DI registrations** from both
   `TDFMAUI/MauiProgram.cs` and
   `TDFAPI/Extensions/Startup/ApplicationServicesExtensions.cs`.
5. **Deleted `TDFShared/Services/IAuthService.cs`** entirely and
   updated the `IAuthClient` xmldoc cref that previously pointed at it.

### Errors Encountered
None. Every caller's method call was verified against the narrow
interface surface before migration (one pass of grep
`_authService\.|authService\.` confirmed 30 call sites, all of whose
methods exist on `IAuthClient` or `IAuthTokenIssuer` as appropriate).

### How Errors Were Fixed
N/A.

### Build Verification
`TDFAPI.csproj` (which transitively compiles `TDFShared.csproj`):
**0 errors**. MAUI full build still not possible locally (no iOS/Android
SDKs); verified by (a) all `IAuthService` references in MAUI replaced
1:1 with `IAuthClient`, (b) `using TDFShared.Contracts;` added to every
updated file that didn't already have it, (c)
`grep -r "IAuthService" TDFMAUI TDFAPI TDFShared` returns zero matches
after the migration.

---

## Phase 12 - Validation clarity + server/client IAuthService split

### Features Implemented
1. **Validation envelope unification:** registered a global
   `InvalidModelStateResponseFactory` in `ControllersStartupExtensions`
   that wraps every `[ApiController]` model-binding failure in
   `ApiResponse<object>.FromModelState(...)`, matching the envelope every
   controller emits for its own errors. Removed the four now-redundant
   `if (!ModelState.IsValid) return BadRequest(...)` guards:
   `AuthController.Login`, `AuthController.Register`,
   `RequestController.CreateRequest`, `RequestController.UpdateRequest`.
   Downstream handler-level validation (MediatR `ValidationBehavior` +
   `BusinessRulesService`) is unaffected - it runs against the command
   object, not the DTO.
2. **Client-side auth contract:** new `TDFShared.Contracts.IAuthClient`
   narrows the surface MAUI callers actually use - `LoginAsync`,
   `LogoutAsync`, `RefreshTokenAsync`, `GetCurrentTokenAsync`,
   `SetAuthenticationTokenAsync`, `GetCurrentUserIdAsync`,
   `GetUserRolesAsync`, `GetCurrentUserDepartmentAsync`,
   `GetCurrentUserAsync`. `TDFMAUI.Services.AuthService` now implements
   both `IAuthService` (back-compat) and `IAuthClient`; `MauiProgram`
   registers the class against both interface keys.
3. **Server-side auth contract:** new `TDFAPI.Services.IAuthTokenIssuer`
   gathers the server-only operations - JWT issuance / refresh,
   server-side token revocation, password hashing/verification, and the
   server's claim-principal reads. `TDFAPI.Services.AuthService` now
   implements both `IAuthService` and `IAuthTokenIssuer`; DI registers
   `AuthService` once and resolves both interfaces to the same scoped
   instance (no behavioural change - it was already `AddScoped`).
4. **Removed dead `IAuthService` dependency from `HttpClientService`:**
   the field was assigned in the constructor but never referenced -
   leftover from pre-Phase-5 when `HttpClientService` owned the
   refresh-on-401 logic. Phase 5 moved that into
   `AuthenticationHeaderHandler` using `IAuthTokenStore`, so the
   `IAuthService` parameter was dead. Dropped the field, the parameter,
   and the xmldoc.

### Errors Encountered
None. The dead `IAuthService` field was verified unused via
`grep '_authService\.' TDFShared/**/*.cs` returning zero hits before
removing it. No external caller constructs `HttpClientService` directly
(`grep 'new HttpClientService(' .` returned zero hits), so the
constructor-signature change is DI-only.

### How Errors Were Fixed
N/A.

### Build Verification
`TDFAPI.csproj` + `TDFShared.csproj`: 0 errors. MAUI full build still
not possible locally (no iOS/Android SDKs); verified by code review
that the two `AuthService` implementations now satisfy both their old
`IAuthService` contract and their new narrow contract
(`IAuthClient` / `IAuthTokenIssuer`) - no methods added, just an
additional interface declaration.

---
