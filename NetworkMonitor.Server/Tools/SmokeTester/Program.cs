using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NetworkMonitor.Server.Data;
using NetworkMonitor.Server.Services;
using NetworkMonitor.Server.DTOs;

internal class SmokeTester
{
    private static async Task Main()
    {
        Console.WriteLine("Starting smoke tests...");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("SmokeTestDb")
            .Options;

        using var context = new ApplicationDbContext(options);

        // Seed data
        await SeedData.InitializeAsync(context);

        var clientLogger = NullLogger<ClientService>.Instance;
var clientService = new ClientService(context, clientLogger);
var deviceService = new DeviceService(context);

        // Test 1: Get clients
        var clients = await clientService.GetAllClientsAsync(includeDevices: true);
        Console.WriteLine($"Clients seeded: {clients.Count}");

        // Test 2: Post heartbeat
        var hb = new HeartbeatDto
        {
            ClientId = "test-client-1",
            ClientName = "Test Client 1",
            LocalIP = "192.168.1.100",
            PublicIP = "203.0.113.1",
            OnlineDevices = 2,
            TotalDevices = 3,
            Version = "1.2.3"
        };

        var processed = await clientService.ProcessHeartbeatAsync(hb);
        Console.WriteLine($"Processed heartbeat for client: {processed.ClientName}, IsOnline: {processed.IsOnline}");

        // Test 3: Get alerts
        var alerts = await clientService.GetAlertsAsync(10);
        Console.WriteLine($"Alerts count after heartbeat: {alerts.Count}");

        // Test 4: Post device status update
        var ds = new NetworkMonitor.Server.DTOs.DeviceStatusDto
        {
            ClientId = processed.ClientId,
            DeviceName = "Test Device",
            IPAddress = "192.168.1.101",
            MACAddress = "00:11:22:33:44:55",
            Manufacturer = "TestCo",
            Status = "Online",
        PingTime = 10,
        Timestamp = DateTime.UtcNow
        };

        await deviceService.UpdateDeviceStatusAsync(ds);
        var devices = await deviceService.GetAllDevicesAsync();
        Console.WriteLine($"Devices count after status update: {devices.Count}");

        Console.WriteLine("Smoke tests completed.");
    }
}
