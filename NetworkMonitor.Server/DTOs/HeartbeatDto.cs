using System;

namespace NetworkMonitor.Server.DTOs
{
    public class HeartbeatDto
    {
        public string ClientId { get; set; } = null!;
        public string ClientName { get; set; } = null!;
        public string LocalIP { get; set; } = string.Empty;
        public string PublicIP { get; set; } = string.Empty;
        public int OnlineDevices { get; set; }
        public int TotalDevices { get; set; }
        public string Version { get; set; } = "1.0.0";
        public DateTime Timestamp { get; set; }
    }
    
    public class DeviceStatusDto
    {
        public string ClientId { get; set; } = null!;
        public Guid DeviceId { get; set; }
        public string DeviceName { get; set; } = null!;
        public string IPAddress { get; set; } = string.Empty;
        public string MACAddress { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int PingTime { get; set; }
        public DateTime? LastSeen { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    public class ServerDeviceDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string IPAddress { get; set; } = string.Empty;
        public string MACAddress { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public string Status { get; set; } = string.Empty;
        public int PingTime { get; set; }
        public DateTime? LastSeen { get; set; }
    }
    
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }
    }
    
    public class ApiResponse : ApiResponse<object> { }
}