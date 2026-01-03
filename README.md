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
