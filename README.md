```markdown
# NetworkMonitor

[![HTTP smoke tests](https://github.com/aldwindelgado18-ui/Projects/actions/workflows/http-smoke-test.yml/badge.svg)](https://github.com/aldwindelgado18-ui/Projects/actions/workflows/http-smoke-test.yml)

This repository contains NetworkMonitor server and client projects.

Notes:
- Replace `aldwindelgado18-ui/Projects` in the badge URL with your GitHub repository owner and name to activate the CI badge.
- The workflow file is at `.github/workflows/http-smoke-test.yml` and runs the HTTP smoke tester located at `Tools/HttpSmokeTester`.

Running the smoke tester locally:

```powershell
dotnet build NetworkMonitor.Server/NetworkMonitor.Server.csproj -c Release
dotnet run --project Tools/HttpSmokeTester/HttpSmokeTester.csproj -c Release
```

## Running the smoke tester locally (one-line)

Use one of the one-line commands below to restore, build, publish the server, and run the HTTP smoke tester.

Bash (Linux / macOS / WSL / Git Bash):
```bash
dotnet restore && dotnet build -c Release && dotnet publish NetworkMonitor.Server/NetworkMonitor.Server.csproj -c Release -o Tools/HttpSmokeTester/published && SERVER_DLL_PATH=Tools/HttpSmokeTester/published/NetworkMonitor.Server.dll dotnet run --project Tools/HttpSmokeTester/HttpSmokeTester.csproj -c Release --no-build
```

PowerShell (Windows):
```powershell
$env:SERVER_DLL_PATH = "Tools/HttpSmokeTester/published/NetworkMonitor.Server.dll"; dotnet restore; dotnet build -c Release; dotnet publish NetworkMonitor.Server/NetworkMonitor.Server.csproj -c Release -o Tools/HttpSmokeTester/published; dotnet run --project Tools/HttpSmokeTester/HttpSmokeTester.csproj -c Release --no-build
```

Notes:
- These commands assume .NET SDK 6.0.x is installed (the CI uses .NET 6).
- If the tester reports "DLL not found", ensure the publish step completed and the path matches `Tools/HttpSmokeTester/published/NetworkMonitor.Server.dll`.
```