# TDF Project Progress Log

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