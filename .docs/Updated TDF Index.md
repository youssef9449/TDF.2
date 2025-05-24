TDF Modular Project Suite Documentation
Overview
The TDF suite is composed of three interrelated projects:

TDFShared: Shared data models, DTOs, enums, helpers, and core logic.
TDFMAUI: Cross-platform UI application built with .NET MAUI, consuming shared logic and API.
TDFAPI: ASP.NET Core Web API backend, implementing business logic, CQRS, and service orchestration.
Each project is structured for modularity, maintainability, and extensibility, following modern .NET best practices.


TDFShared/
├── Constants/
│   └── AppConstants.cs       # Centralized static config constants
├── DTOs/
│   └── MessageDTO.cs         # Data Transfer Object for messages
├── Enums/
│   └── RoleType.cs           # Enum for user roles or access levels
├── Exceptions/
│   └── ValidationException.cs# Custom exception for validation failures
├── Helpers/
│   └── ValidationHelper.cs   # Helper for enforcing business validations
├── Interfaces/
│   └── IMessageService.cs    # Abstraction for message-related operations
├── Models/
│   ├── Lookup.cs             # Represents lookup values (e.g., key-value pairs)
│   ├── Message.cs            # Domain model for chat or message entity
│   └── User.cs               # Core user model with role/type info
├── Services/
│   └── MessageService.cs     # Implements IMessageService
├── TDFShared.csproj

📌 Folder Descriptions

Constants/: Application-wide constant definitions for configuration or magic strings.

DTOs/: Defines the shapes of objects exchanged between API/UI layers.

Enums/: Centralized types like roles, statuses, etc.

Exceptions/: Custom typed exceptions for domain-specific error handling.

Helpers/: Reusable utilities for validation, formatting, etc.

Interfaces/: Shared contracts implemented by services (helps with DI).

Models/: Core domain objects (persisted or business-relevant).

Services/: Business logic implementations (e.g., message handling).

🛠️ Notes
DTOs and Models use serialization-friendly structures.

Designed to be DI-free for maximum reuse.

Follows domain-driven principles with clean separation of models and contracts.


2. TDFMAUI
Directory Structure

TDFMAUI/
├── App.xaml, App.xaml.cs         # Application entry point and resources
├── AppShell.xaml, AppShell.xaml.cs # Shell navigation and layout
├── Config/                       # App configuration files
├── Controls/                     # Custom UI controls
├── Converters/                   # Data binding converters
├── DTO/                          # UI-specific DTOs (if any)
├── Features/                     # Feature modules (MVVM pattern)
├── Helpers/                      # UI helpers/utilities
├── MainPage.xaml, MainPage.xaml.cs # Main landing page
├── MauiProgram.cs                # App builder and DI setup
├── Pages/                        # Page views (MVVM)
├── Platforms/                    # Platform-specific code (Android, iOS, Windows, etc.)
├── Properties/                   # Assembly and app properties
├── README-fixes.md               # Project notes and fixes
├── Resources/                    # Images, fonts, raw assets
├── Services/                     # Service classes for API, storage, etc.
├── TDFMAUI.csproj
├── ViewModels/                   # ViewModels for MVVM
├── appsettings.json              # App configuration
├── bin/
└── obj/

Core Systems
MVVM Architecture: Features, Pages, and ViewModels follow the MVVM pattern.
Dependency Injection: Configured in MauiProgram.cs for services and viewmodels.
Converters/Helpers: For data binding and UI logic.
Platform Support: Code in Platforms/ for device-specific features.
Services: API communication, local storage, and business logic.
Implementation Notes
Uses .NET MAUI for cross-platform UI.
All API calls use DTOs from TDFShared for consistency.
Navigation and state management handled via AppShell and MVVM.


2. 🌐 TDFAPI
ASP.NET Core Web API with business logic, CQRS, auth, real-time messaging, and validation middleware.

📁 Folder & File Structure
graphql
Copy
Edit
TDFAPI/
├── CQRS/
│   ├── Behaviors/
│   │   ├── LoggingBehavior.cs           # Logs execution of CQRS commands/queries
│   │   └── ValidationBehavior.cs        # Validates commands and queries via FluentValidation
│   ├── Commands/
│   │   └── CreateMessageCommand.cs      # Command to create a message
│   ├── Core/
│   │   ├── ICommand.cs                  # Interface for command
│   │   └── IQuery.cs                    # Interface for query
│   └── Queries/
│       └── GetUserQuery.cs              # Query to fetch a user by ID
├── Configuration/
│   └── IniConfiguration.cs              # INI file-based app configuration
├── Controllers/
│   ├── AuthController.cs                # Handles user authentication/login
│   ├── HealthCheckController.cs         # Health check endpoint
│   ├── LookupsController.cs             # Lookup data for UI (e.g., dropdowns)
│   ├── MessagesController.cs            # CRUD operations for messages
│   ├── NotificationsController.cs       # Push notifications / SignalR
│   ├── RequestController.cs             # Handles user service/support requests
│   └── UsersController.cs               # User CRUD and info endpoints
├── Data/
│   └── ApplicationDbContext.cs          # EF Core DB context
├── Domain/
│   └── Auth/
│       └── RevokedToken.cs              # Represents a token blacklisted after logout
├── Exceptions/
│   ├── ConcurrencyException.cs
│   ├── DomainException.cs
│   ├── EntityNotFoundException.cs
│   └── UnauthorizedAccessException.cs
├── Extensions/
│   └── HttpContextExtensions.cs         # Adds extension methods to work with HttpContext
├── Messaging/
│   ├── EventMediator.cs                 # Mediates user/system events
│   ├── MessageStore.cs                  # In-memory or persistent store for messages
│   ├── UserEvents.cs                    # Defines user-related events
│   ├── WebSocketMessage.cs              # DTO for socket messages
│   ├── Commands/
│   │   └── MessageCommands.cs           # Commands related to message system
│   ├── Interfaces/
│   │   ├── IEvent.cs
│   │   ├── IEventMediator.cs
│   │   ├── IMessageService.cs
│   │   └── IUserPresenceService.cs
│   └── Services/
│       └── UserPresenceService.cs       # Tracks user presence using WebSockets
├── Middleware/
│   ├── GlobalExceptionMiddleware.cs     # Global error handling middleware
│   ├── RequestLoggingMiddleware.cs      # Logs incoming HTTP requests
│   ├── SecurityHeadersMiddleware.cs     # Adds security headers
│   ├── SecurityMonitoringMiddleware.cs  # Middleware for monitoring threats
│   ├── WebSocketAuthenticationHelper.cs # Auth logic for socket connections
│   └── WebSocketMiddleware.cs           # Core logic to manage WebSocket connections
├── Migrations/
│   └── CreateRevokedTokensTable.sql     # SQL script for DB migration
├── Program.cs                            # App entry point + WebHost builder
├── TDFAPI.csproj
├── appsettings.json
├── appsettings.Development.json

🧩 Key Architectural Highlights
CQRS Pattern: With behaviors for logging and validation.

WebSocket Support: For real-time notifications and presence.

Custom Middleware: For logging, error handling, security.

Service-Oriented Controllers: Thin controllers relying on injected services.

Dependency Injection: Fully leveraged via MauiProgram.cs

[ TDFMAUI ] ⟶ [ TDFShared ] ⟶ [ TDFAPI ]
    |             |               |
 UI Views     DTOs/Models      Controllers
 ViewModels   Enums/Helpers    CQRS Commands/Queries
 Services     Constants        Business Services
                            ⟵ WebSocket Channel ⟵


Implementation Notes
Follows SOLID and clean architecture principles.
Uses dependency injection for all services and repositories.
Exception handling and validation centralized in middleware.
API versioning and documentation provided in README.md.

Shared Development Guidelines
Follow SOLID and DRY principles.
Maintain clear separation of concerns between projects.
Use DTOs and models from TDFShared for all data exchange.
Implement comprehensive error handling and logging.
Document all public APIs and key classes.
Write unit and integration tests for core systems.

Technical Notes
All projects use modern .NET (6/7/8) and C# features.
Signal-based communication (events/delegates) for decoupled modules.
Resource-based data management in API and UI.
Cross-platform support in TDFMAUI.
Comprehensive documentation in key directories and README.md files.
Debugging and logging integrated across all layers.

Key Documentation Files
TDFAPI/README.md – API documentation and usage
TDFMAUI/README-fixes.md – UI project notes and fixes
TDFShared/ – Source of truth for DTOs, enums, and models
TDFAPI/table update.txt – Database/schema change log

Implementation Progress
Completed Features
Core shared models and DTOs (TDFShared)
Initial API endpoints and CQRS structure (TDFAPI)
UI shell, navigation, and API integration (TDFMAUI)
Dependency injection and configuration in all projects
Unit and integration testing setup

In Development
Advanced feature modules in UI (TDFMAUI/Features)
Extended business logic and messaging (TDFAPI)
Additional helpers and utilities (TDFShared)

Planned Features
Advanced error handling and user feedback
Real-time messaging/event handling
Plugin/extensibility support
Enhanced UI theming and customization
API documentation and versioning improvements


## Development Guidelines
1. Follow SOLID principles
2. Maintain clear separation of concerns
3. Use typed arrays and dictionaries
4. Implement comprehensive error handling
5. Document all public APIs
6. Write unit tests for core systems

## Technical Notes
- Comprehensive documentation in key directories (`src/resources/README.md`)


## Key Documentation Files
- `.docs/TDF-Index.md` - This file, overall project structure
- `.docs/implementation-plan.md` - Implementation plan for
- `.docs/progress.md` - Development log tracking feature implementations, challenges, and solutions

Project Relationships Diagram
[TDFShared] <----> [TDFAPI] <----> [TDFMAUI]
  ^              (references)   (consumes API & DTOs)
  |_________________________________________|
