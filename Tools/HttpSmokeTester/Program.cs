using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using System.Text.Json;
using System.Net.Http; 

internal class HttpSmokeTester
{
    private static async Task Main()
    {
        Console.WriteLine("Starting HTTP smoke tests (in-memory host)...");

        // Launch the real server process and hit its HTTP endpoints (simpler than WebApplicationFactory in this workspace layout).
        var envServerPath = Environment.GetEnvironmentVariable("SERVER_DLL_PATH");
        var serverDll = !string.IsNullOrEmpty(envServerPath)
            ? Path.GetFullPath(envServerPath)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "NetworkMonitor.Server", "bin", "Release", "net6.0", "NetworkMonitor.Server.dll"));

        if (!File.Exists(serverDll))
        {
            Console.WriteLine($"Server DLL not found: {serverDll}");
            Environment.Exit(1);
            return;
        }

        var psi = new System.Diagnostics.ProcessStartInfo("dotnet", $"\"{serverDll}\" --urls \"http://localhost:5023\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = System.Diagnostics.Process.Start(psi)!;
        // read output asynchronously to avoid deadlocks and look for the listening line
        var stdout = proc.StandardOutput;
        var stderr = proc.StandardError;

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var started = false;
        while (!cts.IsCancellationRequested)
        {
            var readTask = stdout.ReadLineAsync();
            var completed = await Task.WhenAny(readTask, Task.Delay(Timeout.InfiniteTimeSpan, cts.Token));
            if (completed != readTask) break; // timed out or cancelled
            var line = await readTask;
            if (line is null) break;
            Console.WriteLine(line);
            if (line.Contains("Now listening on:") || line.Contains("Application started."))
            {
                started = true;
                break;
            }
        }

        if (!started)
        {
            Console.WriteLine("Server did not start in time; aborting tests.");
            try { proc.Kill(); } catch { }
            Environment.Exit(1);
            return;
        }

        using var client = new System.Net.Http.HttpClient { BaseAddress = new Uri("http://localhost:5023") };

        static void Fail(string msg)
        {
            Console.WriteLine("FAIL: " + msg);
            Environment.Exit(1);
        }

        static async Task<JsonDocument> AssertAndGetJson(HttpResponseMessage res, string op)
        {
            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"{op} failed: {(int)res.StatusCode} {res.ReasonPhrase}");
                Console.WriteLine(await res.Content.ReadAsStringAsync());
                Environment.Exit(1);
            }

            var s = await res.Content.ReadAsStringAsync();
            Console.WriteLine(s);
            try
            {
                var doc = JsonDocument.Parse(s);
                if (doc.RootElement.TryGetProperty("success", out var succ) && !succ.GetBoolean())
                {
                    Console.WriteLine($"{op} returned success=false");
                    Environment.Exit(1);
                }

                return doc;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{op} returned invalid JSON: {ex.Message}");
                Environment.Exit(1);
                throw;
            }
        }

        // GET clients (initial)
        var clientsRes = await client.GetAsync("/api/api/clients");
        Console.WriteLine($"GET /api/clients -> {(int)clientsRes.StatusCode} {clientsRes.ReasonPhrase}");
        var clientsDoc = await AssertAndGetJson(clientsRes, "GET /api/api/clients");

        // POST heartbeat
        var hbReq = new
        {
            ClientId = "http-smoke-1",
            ClientName = "HTTP Smoke Client",
            LocalIP = "10.10.0.1",
            PublicIP = "203.0.113.11",
            OnlineDevices = 1,
            TotalDevices = 1,
            Version = "1.0.0"
        };

        var hbRes = await client.PostAsJsonAsync("/api/api/heartbeat", hbReq);
        Console.WriteLine($"POST /api/heartbeat -> {(int)hbRes.StatusCode} {hbRes.ReasonPhrase}");
        var hbDoc = await AssertAndGetJson(hbRes, "POST /api/api/heartbeat");

        if (!hbDoc.RootElement.TryGetProperty("data", out var hbData) || !hbData.TryGetProperty("clientId", out var hbCid) || hbCid.GetString() != "http-smoke-1")
        {
            Fail("Heartbeat response did not contain expected clientId");
        }

        // GET clients (verify client exists)
        clientsRes = await client.GetAsync("/api/api/clients");
        Console.WriteLine($"GET /api/clients -> {(int)clientsRes.StatusCode} {clientsRes.ReasonPhrase}");
        clientsDoc = await AssertAndGetJson(clientsRes, "GET /api/api/clients after heartbeat");

        var found = false;
        if (clientsDoc.RootElement.TryGetProperty("data", out var clientsArr) && clientsArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in clientsArr.EnumerateArray())
            {
                if (el.TryGetProperty("clientId", out var cid) && cid.GetString() == "http-smoke-1") { found = true; break; }
            }
        }
        if (!found) Fail("Posted client not found in GET /clients response");

        // GET devices for client (should succeed)
        var clientId = "http-smoke-1";
        var devicesRes = await client.GetAsync($"/api/api/devices/{clientId}");
        Console.WriteLine($"GET /api/devices/{clientId} -> {(int)devicesRes.StatusCode} {devicesRes.ReasonPhrase}");
        await AssertAndGetJson(devicesRes, $"GET /api/api/devices/{clientId}");

        // POST device status
        var dsReq = new
        {
            ClientId = clientId,
            DeviceId = System.Guid.NewGuid(),
            DeviceName = "HTTP Smoke Device",
            IPAddress = "10.10.0.2",
            MACAddress = "AA:AA:AA:AA:AA:AA",
            Manufacturer = "SmokeCo",
            Status = "Online",
            PingTime = 7,
            LastSeen = DateTime.UtcNow
        };

        var dsRes = await client.PostAsJsonAsync("/api/api/devicestatus", dsReq);
        Console.WriteLine($"POST /api/devicestatus -> {(int)dsRes.StatusCode} {dsRes.ReasonPhrase}");
        var dsDoc = await AssertAndGetJson(dsRes, "POST /api/api/devicestatus");
        if (!dsDoc.RootElement.TryGetProperty("data", out var dsData) || !dsData.TryGetProperty("deviceId", out var deviceIdProp) || string.IsNullOrEmpty(deviceIdProp.GetString()))
        {
            Fail("Device status POST did not return deviceId");
        }

        // GET alerts (unresolvedOnly)
        var alertsRes = await client.GetAsync("/api/api/alerts?unresolvedOnly=true&count=10");
        Console.WriteLine($"GET /api/alerts -> {(int)alertsRes.StatusCode} {alertsRes.ReasonPhrase}");
        await AssertAndGetJson(alertsRes, "GET /api/api/alerts");

        Console.WriteLine("All HTTP smoke assertions passed.");

        try { proc.Kill(); } catch { }
        // give server a moment to shut down cleanly
        await Task.Delay(250);
        Environment.Exit(0);
    }
}