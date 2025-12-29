using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Server.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkMonitor.Server.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(ApplicationDbContext context)
        {
            if (!await context.Clients.AnyAsync())
            {
                // Add sample clients
                var sampleClients = new[]
                {
                    new Client
                    {
                        ClientId = "sample-client-1",
                        ClientName = "Office Main Server",
                        LocalIP = "192.168.1.100",
                        PublicIP = "203.0.113.25",
                        FirstSeen = DateTime.UtcNow.AddDays(-30),
                        LastHeartbeat = DateTime.UtcNow,
                        IsOnline = true,
                        OnlineDevices = 5,
                        TotalDevices = 8,
                        Version = "1.2.0",
                        Location = "Main Office",
                        Notes = "Primary monitoring server"
                    },
                    new Client
                    {
                        ClientId = "sample-client-2",
                        ClientName = "Branch Office Router",
                        LocalIP = "192.168.2.1",
                        PublicIP = "198.51.100.42",
                        FirstSeen = DateTime.UtcNow.AddDays(-15),
                        LastHeartbeat = DateTime.UtcNow.AddMinutes(-5),
                        IsOnline = true,
                        OnlineDevices = 3,
                        TotalDevices = 5,
                        Version = "1.1.5",
                        Location = "Branch Office",
                        Notes = "Remote site monitoring"
                    }
                };
                
                await context.Clients.AddRangeAsync(sampleClients);
                await context.SaveChangesAsync();
                
                // Add sample devices
                var sampleDevices = new[]
                {
                    new ClientDevice
                    {
                        Id = Guid.NewGuid(),
                        ClientId = "sample-client-1",
                        Name = "Main Office Router",
                        IPAddress = "192.168.1.1",
                        MACAddress = "00:1A:2B:3C:4D:5E",
                        Manufacturer = "Cisco",
                        IsEnabled = true,
                        Status = "Online",
                        PingTime = 12,
                        LastSeen = DateTime.UtcNow,
                        DeviceType = "Router",
                        Description = "Primary network gateway"
                    },
                    new ClientDevice
                    {
                        Id = Guid.NewGuid(),
                        ClientId = "sample-client-1",
                        Name = "Google DNS",
                        IPAddress = "8.8.8.8",
                        Manufacturer = "Google",
                        IsEnabled = true,
                        Status = "Online",
                        PingTime = 25,
                        LastSeen = DateTime.UtcNow,
                        DeviceType = "DNS Server"
                    },
                    new ClientDevice
                    {
                        Id = Guid.NewGuid(),
                        ClientId = "sample-client-2",
                        Name = "Branch Firewall",
                        IPAddress = "192.168.2.254",
                        MACAddress = "00:1B:63:45:67:89",
                        Manufacturer = "Fortinet",
                        IsEnabled = true,
                        Status = "Offline",
                        PingTime = 0,
                        LastSeen = DateTime.UtcNow.AddHours(-2),
                        DeviceType = "Firewall",
                        Description = "Site firewall appliance"
                    }
                };
                
                await context.ClientDevices.AddRangeAsync(sampleDevices);
                await context.SaveChangesAsync();
                
                // Add sample alerts
                var sampleAlerts = new[]
                {
                    new Alert
                    {
                        ClientId = "sample-client-2",
                        DeviceId = sampleDevices[2].Id,
                        AlertType = "DeviceOffline",
                        Message = "Branch Firewall has been offline for 3 checks",
                        Severity = "High",
                        Created = DateTime.UtcNow.AddHours(-1)
                    }
                };
                
                await context.Alerts.AddRangeAsync(sampleAlerts);
                await context.SaveChangesAsync();
                
                Console.WriteLine("Sample data seeded successfully.");
            }
        }
    }
}