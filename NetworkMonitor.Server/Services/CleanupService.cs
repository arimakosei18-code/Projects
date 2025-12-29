using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Server.Data;
using NetworkMonitor.Server.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkMonitor.Server.Services
{
    public class CleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CleanupService> _logger;
        
        public CleanupService(IServiceProvider serviceProvider, ILogger<CleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Cleanup Service is starting.");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        await PerformCleanupAsync(dbContext);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in cleanup service");
                }
            }
            
            _logger.LogInformation("Cleanup Service is stopping.");
        }
        
        private async Task PerformCleanupAsync(ApplicationDbContext dbContext)
        {
            try
            {
                _logger.LogInformation("Starting database cleanup...");
                
                // Mark stale clients as offline
                var staleThreshold = DateTime.UtcNow.AddMinutes(-10);
                var staleClients = await dbContext.Clients
                    .Where(c => c.IsOnline && c.LastHeartbeat < staleThreshold)
                    .ToListAsync();
                
                foreach (var client in staleClients)
                {
                    client.IsOnline = false;
                    client.OnlineDevices = 0;
                    _logger.LogInformation($"Marked client {client.ClientName} as offline (last heartbeat: {client.LastHeartbeat})");
                }
                
                // Cleanup old status updates (older than 30 days)
                var statusCutoff = DateTime.UtcNow.AddDays(-30);
                var oldStatusUpdates = await dbContext.DeviceStatusUpdates
                    .Where(s => s.Timestamp < statusCutoff)
                    .ToListAsync();
                
                if (oldStatusUpdates.Any())
                {
                    dbContext.DeviceStatusUpdates.RemoveRange(oldStatusUpdates);
                    _logger.LogInformation($"Removed {oldStatusUpdates.Count} old status updates");
                }
                
                // Cleanup old heartbeat logs (older than 7 days, but keep at least 1000 per client)
                var heartbeatCutoff = DateTime.UtcNow.AddDays(-7);
                var oldHeartbeats = await dbContext.HeartbeatLogs
                    .Where(h => h.Timestamp < heartbeatCutoff)
                    .ToListAsync();
                
                if (oldHeartbeats.Any())
                {
                    // Group by client and keep recent ones
                    var heartbeatsByClient = oldHeartbeats.GroupBy(h => h.ClientId);
                    var heartbeatsToRemove = new List<HeartbeatLog>();
                    
                    foreach (var group in heartbeatsByClient)
                    {
                        var recentHeartbeats = await dbContext.HeartbeatLogs
                            .Where(h => h.ClientId == group.Key && h.Timestamp >= heartbeatCutoff)
                            .CountAsync();
                        
                        var toKeepCount = Math.Max(0, 1000 - recentHeartbeats);
                        var toKeep = group
                            .OrderByDescending(h => h.Timestamp)
                            .Take(toKeepCount)
                            .ToList();
                            
                        heartbeatsToRemove.AddRange(group.Except(toKeep));
                    }
                    
                    if (heartbeatsToRemove.Any())
                    {
                        dbContext.HeartbeatLogs.RemoveRange(heartbeatsToRemove);
                        _logger.LogInformation($"Removed {heartbeatsToRemove.Count} old heartbeat logs");
                    }
                }
                
                // Auto-resolve old alerts (older than 7 days)
                var alertCutoff = DateTime.UtcNow.AddDays(-7);
                var oldAlerts = await dbContext.Alerts
                    .Where(a => !a.IsResolved && a.Created < alertCutoff)
                    .ToListAsync();
                
                foreach (var alert in oldAlerts)
                {
                    alert.Resolved = DateTime.UtcNow;
                    _logger.LogInformation($"Auto-resolved alert: {alert.Message}");
                }
                
                await dbContext.SaveChangesAsync();
                _logger.LogInformation("Database cleanup completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during database cleanup");
            }
        }
    }
}