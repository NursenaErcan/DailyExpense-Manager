# Expense Tracker

A full-stack daily expense tracker with user authentication, expense management, budgets, charts, and admin support.

## Project structure

- `Backend/ExpenseTracker.API/` — ASP.NET Core Web API with Entity Framework Core and SQLite
- `ExpenseTrackerFrontend/` — vanilla HTML/CSS/JavaScript frontend

## Features

- User registration and login with JWT authentication
- Add, update, delete expenses
- Monthly budget categories and spending tracking
- Dashboard summary with charts and recent expenses
- Admin role support
- Automatic database migration and admin seeding on startup

## Prerequisites

- .NET 8 SDK
- Node.js (optional, for local frontend server)
- `live-server` or another static file server to serve the frontend

## Run the backend

1. Open a terminal in `Backend/ExpenseTracker.API`
2. Run:

```bash
cd "Backend/ExpenseTracker.API"
dotnet run
```

The API will start and apply pending EF Core migrations automatically.



## Run the frontend

The frontend is static and can be opened from a local server.

### Option 1: Using `live-server`

```bash
cd "ExpenseTrackerFrontend"
live-server
```

### Option 2: Using Python built-in server

```bash
cd "ExpenseTrackerFrontend"
python -m http.server 5500
```

Then open the served URL in your browser, for example:

- `http://127.0.0.1:5500`

## API connection

The frontend expects the backend API at:

- `http://localhost:5000/api`

If your backend runs on a different origin or port, update `ExpenseTrackerFrontend/app.js` and modify the `API_BASE` constant accordingly.

## Notes

- The backend uses SQLite via `DefaultConnection` from `appsettings.json`
- The API enables CORS for all origins so the frontend can call it from a local server
- Budget creation and expense summaries are managed through the API endpoints and reflected in the frontend UI

## Useful commands

```bash
# Build backend
cd "Backend/ExpenseTracker.API"
dotnet build

# Run backend
dotnet run
```

```bash
# Start frontend server
cd "ExpenseTrackerFrontend"
live-server
```
