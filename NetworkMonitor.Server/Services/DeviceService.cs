using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Server.Data;
using NetworkMonitor.Server.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NetworkMonitor.Server.Services
{
    public class DeviceService
    {
        private readonly ApplicationDbContext _context;

        public DeviceService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Return server-side device model `ClientDevice`
        public async Task<List<ClientDevice>> GetAllDevicesAsync(string status = null, string type = null)
        {
            var query = _context.ClientDevices.AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(d => d.Status.Contains(status));

            if (!string.IsNullOrEmpty(type))
                query = query.Where(d => d.DeviceType == type);

            return await query.ToListAsync();
        }
        
        public async Task<ClientDevice> GetDeviceAsync(Guid id)
        {
            return await _context.ClientDevices.FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task<List<ClientDevice>> GetDevicesForClientAsync(string clientId)
        {
            return await _context.ClientDevices.Where(d => d.ClientId == clientId).ToListAsync();
        }

        public async Task<ClientDevice> UpdateDeviceStatusAsync(NetworkMonitor.Server.DTOs.DeviceStatusDto dto)
        {
            // Find existing device
            var device = await _context.ClientDevices.FirstOrDefaultAsync(d => d.Id == dto.DeviceId && d.ClientId == dto.ClientId);

            if (device == null)
            {
                // Create new device if not exists
                device = new ClientDevice
                {
                    Id = dto.DeviceId == Guid.Empty ? Guid.NewGuid() : dto.DeviceId,
                    ClientId = dto.ClientId,
                    Name = dto.DeviceName ?? "Unknown",
                    IPAddress = dto.IPAddress ?? "",
                    MACAddress = "",
                    Manufacturer = "",
                    Status = dto.Status ?? "Unknown",
                    PingTime = dto.PingTime,
                    LastSeen = dto.LastSeen,
                    LastUpdated = DateTime.UtcNow
                };
                _context.ClientDevices.Add(device);
            }
            else
            {
                device.Name = dto.DeviceName ?? device.Name;
                device.IPAddress = dto.IPAddress ?? device.IPAddress;
                device.MACAddress = dto.MACAddress ?? device.MACAddress;
                device.Manufacturer = dto.Manufacturer ?? device.Manufacturer;
                device.Status = dto.Status ?? device.Status;
                device.PingTime = dto.PingTime;
                device.LastSeen = dto.LastSeen ?? device.LastSeen;
                device.LastUpdated = DateTime.UtcNow;
            }

            // Add status history
            var statusUpdate = new DeviceStatusUpdate
            {
                DeviceId = device.Id,
                ClientId = dto.ClientId,
                Status = dto.Status ?? device.Status,
                PingTime = dto.PingTime,
                Timestamp = dto.Timestamp
            };

            _context.DeviceStatusUpdates.Add(statusUpdate);

            await _context.SaveChangesAsync();

            return device;
        }

        public async Task<Dictionary<string, object>> GetDeviceStatisticsAsync()
        {
            var stats = new Dictionary<string, object>();
            stats["TotalDevices"] = await _context.ClientDevices.CountAsync();
            stats["OnlineDevices"] = await _context.ClientDevices.CountAsync(d => d.Status.Contains("Online"));
            stats["OfflineDevices"] = await _context.ClientDevices.CountAsync(d => d.Status.Contains("Offline"));

            stats["TopDeviceTypes"] = await _context.ClientDevices
                .GroupBy(d => d.DeviceType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync();

            return stats;
        }

        public async Task<List<DeviceStatusUpdate>> GetDeviceHistoryAsync(Guid deviceId, int lastHours = 24)
        {
            var cutoff = DateTime.UtcNow.AddHours(-lastHours);
            return await _context.DeviceStatusUpdates
                .Where(s => s.DeviceId == deviceId && s.Timestamp >= cutoff)
                .OrderByDescending(s => s.Timestamp)
                .ToListAsync();
        }
    }
}