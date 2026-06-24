# Project Setup Guide

This guide explains how to set up the **Nacka Företagarträff Matchmaking** project on a new local machine.

## Prerequisites

Ensure you have the following installed:

1.  **Git**: [Download](https://git-scm.com/downloads)
2.  **Node.js (LTS)**: [Download](https://nodejs.org/) (Required for Angular)
3.  **.NET 8 SDK**: [Download](https://dotnet.microsoft.com/download/dotnet/8.0) (Required for Backend)
4.  **SQL Server**: Either **SQL Server Express** or **LocalDB** (included with Visual Studio).
    - [Download SQL Server Express](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)

---

## Automated Setup

For a quicker setup, you can run the included batch script:

1.  Open the project folder.
2.  Double-click `setup.bat`.
3.  Follow the prompts.

If you prefer manual setup, follow the steps below.

---

## 1. Clone the Repository

Open your terminal (PowerShell, Command Prompt, or Git Bash) and run:

```bash
git clone <repository-url>
cd nackatraffmatchmaking
```

---

## 2. Backend Setup (.NET API)

The backend is an ASP.NET Core Web API that uses Entity Framework Core with SQL Server.

1.  Navigate to the API directory:
    ```bash
    cd api/NackaMatchmaking.API
    ```

2.  **Update Database Connection String**:
    - Open `appsettings.json`.
    - Locate the `ConnectionStrings` section.
    - Update `DefaultConnection` to point to your local SQL Server instance.
    
    *Example for LocalDB:*
    ```json
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=NackaMatchmaking;Trusted_Connection=True;MultipleActiveResultSets=true"
    ```

    *Example for SQL Express:*
    ```json
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=NackaMatchmaking;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
    ```

3.  **Restore Dependencies & Create Database**:
    Run the following commands to install dependencies and apply database migrations (this creates the database structure):
    ```bash
    dotnet restore
    dotnet tool install --global dotnet-ef  # If not already installed
    dotnet ef database update
    ```

4.  **Start the Backend**:
    ```bash
    dotnet run
    ```
    - Note the URL the API starts on (e.g., `http://localhost:5109` or similar).

---

## 3. Frontend Setup (Angular)

The frontend is an Angular 17 application.

1.  Open a **new** terminal window (keep the backend running).
2.  Navigate to the project root (where `package.json` is located):
    ```bash
    cd nackatraffmatchmaking
    ```

3.  **Install Dependencies**:
    ```bash
    npm install
    ```

4.  **Check API URL**:
    - Open `src/app/services/api.service.ts`.
    - Ensure `private apiUrl` matches your running backend URL (e.g., `http://localhost:5109/api`).

5.  **Start the Frontend**:
    ```bash
    npm start
    ```
    - This runs `ng serve`.

6.  Open your browser and navigate to: `http://localhost:4200`

---

## Troubleshooting

-   **Database Errors**: Ensure your SQL Server instance is running and the connection string in `appsettings.json` is correct.
-   **CORS Errors**: If the frontend cannot talk to the backend, check the browser console. The backend is configured to allow `http://localhost:4200` (or similar) in `Program.cs`.
-   **Port Conflicts**: If port 4200 or 5109 is in use, the applications may fail to start. You can change the Angular port with `ng serve --port 4300`.
