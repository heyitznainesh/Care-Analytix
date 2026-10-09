# Care Analytix Web Chat - Setup & Execution Guide

This guide explains step-by-step how to set up, configure, and run the Care Analytix project on a new system.

## 1. Prerequisites (What you need to install)

To run this project, you must install the following software on the target system:

1. **.NET 10 SDK**: The project targets the new `.NET 10.0` framework. 
   - You must download and install the **.NET 10.0 SDK**.
   - Download link: [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)

2. **Microsoft SQL Server**: The application uses a SQL Server database to store and retrieve patient data.
   - You can install **SQL Server Express** (free) or use **SQL Server LocalDB** (which is often included with Visual Studio).
   - Download SQL Server Express: [https://www.microsoft.com/en-us/sql-server/sql-server-downloads](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)

> **Note on AI Models:** The project has been configured to use the `FoundryPhiService`. It will **automatically download** the required Microsoft Phi-4 ONNX model from HuggingFace the very first time you run the app. You do NOT need to install Ollama or download models manually.

---

## 2. Configuration Steps

Before running the application, you need to point it to your local SQL Server instance.

1. Open the project folder and navigate to `CareAnalytixWebChat\appsettings.json`.
2. Find the `ConnectionStrings` section:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=DESKTOP-M1VK350\\SQLEXPRESS;Database=Care Analytix;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"
   }
   ```
3. **Change the `Server` value** to match your new system's SQL Server name.
   - If you installed SQL Express default instance: `Server=.\\SQLEXPRESS`
   - If you are using LocalDB: `Server=(localdb)\\mssqllocaldb`
   - Example: `"DefaultConnection": "Server=.\\SQLEXPRESS;Database=Care Analytix;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"`

---

## 3. Database Initialization & Seeding

You do **not** need to manually create the database. 
When the application starts, it will automatically:
1. Create the `Care Analytix` database.
2. Read the `200_Patients.xlsx` and `200_Claim.xlsx` files located in the `CareAnalytixWebChat/Models/` folder.
3. Seed the database with all the patient and claims data automatically.

---

## 4. How to Run the Application

You can run the application using either the Command Line (Terminal) or Visual Studio.

### Option A: Using Command Line (CMD / PowerShell)
1. Open a Command Prompt or PowerShell window.
2. Navigate to the `CareAnalytixWebChat` folder:
   ```cmd
   cd "C:\path\to\Care Analytix\CareAnalytixWebChat"
   ```
3. Run the application using the dotnet CLI:
   ```cmd
   dotnet run
   ```
4. The terminal will output the local URL (e.g., `Now listening on: https://localhost:7082`). Open that link in your web browser.
5. **Important:** On the first run, the app will download the Phi-4 AI model (~several gigabytes). Look at the console output to track the download progress.

### Option B: Using Visual Studio
1. Open Visual Studio.
2. Click **Open a project or solution** and select the `CareAnalytixWebChat.csproj` file (or the `.sln` file if you have one).
3. Ensure the connection string in `appsettings.json` is correct for your machine.
4. Press **F5** (Start Debugging) or **Ctrl+F5** (Start without Debugging) to launch the web app.

---

## Summary Checklist for the New System:
- [ ] Install .NET 10 SDK
- [ ] Install SQL Server Express
- [ ] Update `appsettings.json` with the new SQL Server name
- [ ] Run `dotnet run` inside the `CareAnalytixWebChat` folder
- [ ] Wait for the AI model to download on first startup
