CI: Add HTTP smoke test workflow

Summary
- Adds a GitHub Actions workflow that builds `NetworkMonitor.Server`, publishes the server artifacts, and runs `Tools/HttpSmokeTester` to perform end-to-end HTTP smoke tests. The job fails if any smoke assertion fails.

What I changed
- `.github/workflows/http-smoke-test.yml` — new workflow to build, publish server, and run `HttpSmokeTester`.
- `Tools/HttpSmokeTester` — improved tester to accept `SERVER_DLL_PATH` and perform strict JSON assertions.
- `README.md` — placeholder CI badge and instructions.

Testing
- I ran the smoke tests locally: server started and all smoke assertions passed (heartbeat, clients, device status, alerts). Please run CI to verify in GitHub Actions.

Checklist
- [ ] Build passes on Actions
- [ ] Smoke tests pass on Actions (workflow exit code 0)
- [ ] Add any follow-up fixes if CI fails

Notes
- If the repo is private, CI run links will be needed to review the job logs. Please paste the Actions run URL here when the workflow completes.
