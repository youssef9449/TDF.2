TDF Modular Project Suite Documentation
Overview
The TDF suite is composed of three interrelated projects:

TDFShared: Shared data models, DTOs, enums, helpers, and core logic.
TDFMAUI: Cross-platform UI application built with .NET MAUI, consuming shared logic and API.
TDFAPI: ASP.NET Core Web API backend, implementing business logic, CQRS, and service orchestration.
Each project is structured for modularity, maintainability, and extensibility, following modern .NET best practices.


1. TDFShared
Directory Structure
TDFShared/
├── Constants/      # Constant values and configuration keys
├── DTOs/           # Data Transfer Objects for API/UI communication
├── Enums/          # Enumerations shared across projects
├── Exceptions/     # Custom exception types
├── Helpers/        # Utility/helper classes
├── Models/         # Core domain models
├── TDFShared.csproj
├── TDFShared.sln
├── bin/
└── obj/

Key Components
DTOs: Define the data contracts exchanged between API and UI.
Enums: Centralized enums for status codes, types, etc.
Helpers: Utility classes for common operations.
Models: Core data models used throughout the suite.
Constants: Application-wide constant values.
Exceptions: Custom exception classes for consistent error handling.
Implementation Notes
Focus on immutability and serialization compatibility.
All DTOs and models are versioned for backward compatibility.
Shared logic is dependency-free for maximum portability.


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


3. TDFAPI
Directory Structure

TDFAPI/
├── CQRS/             # Command and Query handlers
├── Configuration/    # App configuration and DI setup
├── Controllers/      # API endpoint controllers
├── Data/             # Database context and migrations
├── Domain/           # Domain entities and logic
├── Exceptions/       # API-specific exception handling
├── Messaging/        # Message bus integration (if any)
├── Middleware/       # Custom middleware (auth, error handling, etc.)
├── Program.cs        # Main entry point
├── Properties/       # Assembly info
├── README.md         # API documentation
├── Repositories/     # Data access repositories
├── Services/         # Business logic services
├── TDFAPI.csproj
├── TDFAPI.http       # HTTP request samples
├── TDFAPI.sln
├── TDFShared/        # Linked/shared project reference
├── TDFShared.Tests/  # Shared logic tests
├── Utilities/        # Utility classes
├── appsettings.json
├── appsettings.Development.json
├── bin/
├── obj/
└── table update.txt  # (possibly migration or schema notes)


Core Systems
CQRS: Command/Query separation for scalable business logic.
Controllers: REST API endpoints, returning DTOs from TDFShared.
Repositories/Services: Data access and business logic layers.
Middleware: Custom request/response handling, error reporting.
Messaging: (If present) integration with message bus/event system.
Testing: Shared and API-specific tests in TDFShared.Tests.

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
