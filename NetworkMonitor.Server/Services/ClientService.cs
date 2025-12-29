using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Server.Data;
using NetworkMonitor.Server.Models;
using NetworkMonitor.Server.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkMonitor.Server.Services
{
    public class ClientService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ClientService> _logger;
        
        public ClientService(ApplicationDbContext context, ILogger<ClientService> logger)
        {
            _context = context;
            _logger = logger;
        }
        
        public async Task<Client> ProcessHeartbeatAsync(HeartbeatDto heartbeat)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                var client = await _context.Clients
                    .Include(c => c.Devices)
                    .FirstOrDefaultAsync(c => c.ClientId == heartbeat.ClientId);
                
                if (client == null)
                {
                    // New client
                    client = new Client
                    {
                        ClientId = heartbeat.ClientId,
                        ClientName = heartbeat.ClientName,
                        LocalIP = heartbeat.LocalIP,
                        PublicIP = heartbeat.PublicIP,
                        FirstSeen = DateTime.UtcNow,
                        IsOnline = true,
                        OnlineDevices = heartbeat.OnlineDevices,
                        TotalDevices = heartbeat.TotalDevices,
                        Version = heartbeat.Version ?? "1.0.0"
                    };
                    
                    _context.Clients.Add(client);
                    _logger.LogInformation($"New client registered: {heartbeat.ClientName} ({heartbeat.ClientId})");
                }
                else
                {
                    // Update existing client
                    client.ClientName = heartbeat.ClientName;
                    client.LocalIP = heartbeat.LocalIP;
                    client.PublicIP = heartbeat.PublicIP;
                    client.LastHeartbeat = DateTime.UtcNow;
                    client.IsOnline = true;
                    client.OnlineDevices = heartbeat.OnlineDevices;
                    client.TotalDevices = heartbeat.TotalDevices;
                    client.Version = heartbeat.Version ?? client.Version;
                }
                
                // Log heartbeat
                var heartbeatLog = new HeartbeatLog
                {
                    ClientId = heartbeat.ClientId,
                    OnlineDevices = heartbeat.OnlineDevices,
                    TotalDevices = heartbeat.TotalDevices,
                    Timestamp = DateTime.UtcNow,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds
                };
                
                _context.HeartbeatLogs.Add(heartbeatLog);
                
                // Check for client coming back online after being offline
                if (!client.IsOnline && (DateTime.UtcNow - client.LastHeartbeat).TotalMinutes > 10)
                {
                    await CreateAlertAsync(client.ClientId, null, "ClientOnline", 
                        $"Client {client.ClientName} is back online", "Medium");
                }
                
                await _context.SaveChangesAsync();
                
                // Cleanup old heartbeat logs (keep last 1000 per client)
                await CleanupHeartbeatLogsAsync(client.ClientId);
                
                return client;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing heartbeat for client {ClientId}", heartbeat.ClientId);
                throw;
            }
        }
        
        public async Task<List<Client>> GetAllClientsAsync(bool includeDevices = false)
        {
            var query = _context.Clients.AsQueryable();
            
            if (includeDevices)
            {
                query = query.Include(c => c.Devices);
            }
            
            return await query
                .OrderByDescending(c => c.LastHeartbeat)
                .ToListAsync();
        }
        
        public async Task<Client> GetClientAsync(string clientId, bool includeDevices = false)
        {
            var query = _context.Clients.Where(c => c.ClientId == clientId);
            
            if (includeDevices)
            {
                query = query.Include(c => c.Devices);
            }
            
            return await query.FirstOrDefaultAsync();
        }
        
        public async Task UpdateClientStatusAsync()
        {
            var offlineThreshold = DateTime.UtcNow.AddMinutes(-10);
            
            var staleClients = await _context.Clients
                .Where(c => c.IsOnline && c.LastHeartbeat < offlineThreshold)
                .ToListAsync();
            
            foreach (var client in staleClients)
            {
                client.IsOnline = false;
                client.OnlineDevices = 0;
                
                // Mark all devices as offline
                var devices = await _context.ClientDevices
                    .Where(d => d.ClientId == client.ClientId && d.Status.Contains("Online"))
                    .ToListAsync();
                
                foreach (var device in devices)
                {
                    device.Status = "Offline (Client Offline)";
                    device.LastUpdated = DateTime.UtcNow;
                    
                    await CreateAlertAsync(client.ClientId, device.Id, "DeviceOffline", 
                        $"Device {device.Name} is offline because client is offline", "High");
                }
                
                await CreateAlertAsync(client.ClientId, null, "ClientOffline", 
                    $"Client {client.ClientName} is offline", "High");
            }
            
            await _context.SaveChangesAsync();
        }
        
        public async Task<List<Alert>> GetAlertsAsync(int count = 50, bool unresolvedOnly = false)
        {
            IQueryable<Alert> query = _context.Alerts
                .Include(a => a.Client)
                .Include(a => a.Device)
                .OrderByDescending(a => a.Created);
                
            if (unresolvedOnly)
            {
                // Use Resolved (mappable) instead of IsResolved (computed) so EF can translate the query
                query = query.Where(a => a.Resolved == null);
            }
            
            return await query.Take(count).ToListAsync();
        }
        
        public async Task<Dictionary<string, object>> GetStatisticsAsync()
        {
            var stats = new Dictionary<string, object>();
            
            stats["TotalClients"] = await _context.Clients.CountAsync();
            stats["OnlineClients"] = await _context.Clients.CountAsync(c => c.IsOnline);
            stats["TotalDevices"] = await _context.ClientDevices.CountAsync();
            stats["OnlineDevices"] = await _context.ClientDevices.CountAsync(d => d.Status.Contains("Online"));
            stats["OfflineDevices"] = await _context.ClientDevices.CountAsync(d => d.Status.Contains("Offline"));
            
            // Device types breakdown
            stats["DeviceTypes"] = await _context.ClientDevices
                .GroupBy(d => d.DeviceType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync();
            
            // Recent activity
            var lastHour = DateTime.UtcNow.AddHours(-1);
            stats["RecentHeartbeats"] = await _context.HeartbeatLogs
                .CountAsync(h => h.Timestamp > lastHour);
            
            stats["RecentAlerts"] = await _context.Alerts
                .CountAsync(a => a.Created > lastHour && a.Resolved == null);
            
            // Top clients by device count
            stats["TopClients"] = await _context.Clients
                .OrderByDescending(c => c.TotalDevices)
                .Take(5)
                .Select(c => new { c.ClientName, c.TotalDevices, c.OnlineDevices })
                .ToListAsync();
            
            return stats;
        }
        
        private async Task CleanupHeartbeatLogsAsync(string clientId)
        {
            var logsToKeep = await _context.HeartbeatLogs
                .Where(h => h.ClientId == clientId)
                .OrderByDescending(h => h.Timestamp)
                .Skip(1000)
                .ToListAsync();
                
            if (logsToKeep.Any())
            {
                _context.HeartbeatLogs.RemoveRange(logsToKeep);
                await _context.SaveChangesAsync();
            }
        }
        
        private async Task CreateAlertAsync(string clientId, Guid? deviceId, string alertType, string message, string severity)
        {
            var alert = new Alert
            {
                ClientId = clientId,
                DeviceId = deviceId,
                AlertType = alertType,
                Message = message,
                Severity = severity,
                Created = DateTime.UtcNow
            };
            
            _context.Alerts.Add(alert);
            await _context.SaveChangesAsync();
        }
    }
}