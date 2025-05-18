# TDF API

A .NET 8.0 API application for the TDF Application.

## Architecture Improvements

The codebase has been improved with several architectural enhancements:

### Data Access
- Implemented Generic Repository pattern using Entity Framework Core
- Added Unit of Work pattern for transaction management
- Created repository interfaces for better design

### Performance
- Added memory caching service with sliding and absolute expiration support
- Implemented response compression for HTTP responses
- Added async pattern throughout the codebase

### Security
- Enhanced the GlobalExceptionMiddleware with RFC 7807 Problem Details
- Improved JWT token handling with proper validation
- Added comprehensive security headers
- Implemented rate limiting for API endpoints

### Code Organization
- Consolidated Utils and Utilities folders
- Applied consistent naming conventions
- Documented code with XML comments

### API Documentation
- Added custom API documentation endpoint at `/api/docs` (using ApiRoutes.Docs constant)
- Added XML documentation generation for API endpoints

## Getting Started

### Prerequisites
- .NET 8.0 SDK
- SQL Server (or compatible database)

### Configuration
Configuration is managed through the `config.ini` file which is created automatically on first run.
Important configuration sections:

- Database: Connection settings
- JWT: Authentication settings
- Security: Password requirements and lockout settings
- WebSockets: WebSocket configuration
- RateLimiting: Rate limiting settings

### Running the Application
```bash
dotnet restore
dotnet run
```

## Features

- User Authentication with JWT
- WebSocket support for real-time notifications
- Message handling
- Request processing
- User presence tracking

## Architecture

The application follows a layered architecture:

1. Controllers: API endpoints
2. Services: Business logic
3. Repositories: Data access
4. Models: Domain entities
5. DTOs: Data transfer objects
6. Middleware: Cross-cutting concerns

## Deployment

For production deployment, ensure the following:

1. Set appropriate connection strings
2. Configure JWT secrets
3. Set CORS allowed origins
4. Configure rate limiting settings

## Monitoring

The application includes built-in health checks at the `/api/healthcheck` endpoint (using ApiRoutes.Health.Base) to monitor:

- Application status
- Database connectivity
- System resources