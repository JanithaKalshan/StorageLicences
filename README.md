# Storage Licences API

Backend API for the Storage Licences take-home assignment.

The API manages storage units, storage licences, items, and placements, including placement scheduling and validation according to the assignment business rules.

## Technology

- .NET 10
- ASP.NET Core Web API
- Entity Framework Core
- SQL Server
- C#
- Swagger / OpenAPI
- xUnit for automated tests

## Prerequisites

Before running the API, make sure the following are installed:

- .NET 10 SDK
- SQL Server
- Visual Studio / Visual Studio Code / another preferred .NET IDE

A SQL Server instance must be available locally or remotely.

## Configuration

### 1. Database Connection

Open the solution in your preferred IDE.

Open:

`StorageLicences.API/appsettings.Development.json`

Update the `DefaultConnection` connection string according to your SQL Server configuration.

Example using Windows Authentication:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=StorageLicences;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True;"
  }
}
```

Example using SQL Server authentication:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=StorageLicences;User ID=your_username;Password=your_password;Encrypt=True;TrustServerCertificate=True;"
  }
}
```

Use the connection string appropriate for your SQL Server environment.

### 2. Frontend URL / CORS

If the Vue frontend is running on a different origin, configure the frontend URL in:

`StorageLicences.API/appsettings.Development.json`

Example:

```json
{
  "AppSettings": {
    "FrontendUrl": "http://localhost:5173"
  }
}
```

This URL is used to allow the frontend application to communicate with the API through CORS.

## Database Setup

The application uses Entity Framework Core migrations to create and update the SQL Server database.

### Using Visual Studio Package Manager Console

Open:

`Tools → NuGet Package Manager → Package Manager Console`

Run:

```powershell
Update-Database -Context ApplicationDbContext
```

### Using the .NET CLI

Alternatively, use the EF Core CLI:

```bash
dotnet ef database update --context ApplicationDbContext
```

Make sure the appropriate EF Core tools are installed if using the CLI.

After the migration completes successfully, the required database tables will be created.

## Seed Data

The application includes deterministic seed data for the assignment.

The seed creates at least 5,000 storage units with realistic variations, including:

- different pallet and box capacities
- empty, partially occupied, and full units
- active licences
- licences close to expiry
- lapsed licences
- surrendered/historical licences
- scheduled placements
- completed placements
- cancelled placements
- recent pallet placements
- items with and without intake dates

The seed data is deterministic so that the same scenarios can be reproduced during development and testing.

The application is configured to run seed data automatically on startup, no separate seed command is required.

## Running the API

Open the solution and run the `StorageLicences.API` project.

The API will start using the configured HTTPS URL.

For example:

```
https://localhost:7089/
```

The exact port may vary depending on the project's launch settings.

## Swagger / OpenAPI

Once the API is running, open Swagger UI:

```
https://localhost:7089/index.html
```

or use the Swagger URL shown by your IDE/application output.

Swagger provides an interactive interface for testing the API endpoints.

## API Endpoints

### Get Units

`GET /api/units`

Returns a server-side filtered and paginated list of units.

Supported query parameters:

- `page`
- `pageSize`
- `hasActiveLicence`
- `minRemainingPalletCapacity`
- `minRemainingBoxCapacity`

Example:

```
GET /api/units?page=1&pageSize=50&hasActiveLicence=true
```

### Get Unit Details

`GET /api/units/{id}`

Returns unit details including capacity, occupancy, licence information/history, and placement history.

### Get Unit Availability

`GET /api/units/{id}/availability?asOf=2026-05-15`

Returns pallet and box availability for the specified calendar date and applicable reasons when a placement cannot be accepted.

### Schedule Placement

`POST /api/placements`

Schedules a placement after validating the applicable scheduling rules.

Example request:

```json
{
  "unitId": 1,
  "itemId": 2,
  "placementClass": "Pallet",
  "scheduledDate": "2026-05-15",
  "assertedRequesterId": "COMPANY-ABC"
}
```

Validation failures are returned as structured errors and all applicable failures are reported together.

## Running Tests

From the solution directory:

```bash
dotnet test
```

The test suite includes Domain tests covering the scheduling rules and API-level tests covering endpoint behavior, validation/error handling, and persistence behavior.

## Project Structure

The backend follows a lightweight Clean Architecture:

```
StorageLicences
├── StorageLicences.API
│   └── Controllers / API configuration
│
├── StorageLicences.Application
│   └── Application use cases and orchestration
│
├── StorageLicences.Domain
│   └── Entities and scheduling business rules
│
└── StorageLicences.Infrastructure
    └── EF Core / SQL Server persistence
```

Business rules are kept in the Domain layer rather than in API controllers.
