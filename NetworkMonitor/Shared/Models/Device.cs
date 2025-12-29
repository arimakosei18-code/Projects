using System;

namespace NetworkMonitor.Shared.Models
{
    public class Device
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; }
        public string IPAddress { get; set; }
        public bool IsEnabled { get; set; } = true;
        public DateTime? LastSeen { get; set; }
        public string Status { get; set; } = "Unknown";
        public string Manufacturer { get; set; } = "Unknown";
    }

    public class ServerConnectionInfo
    {
        public string ServerUrl { get; set; } = "";
        public string ClientId { get; set; } = Guid.NewGuid().ToString();
        public string ClientName { get; set; } = Environment.MachineName;
        public DateTime LastHeartbeat { get; set; }
        public bool IsConnected { get; set; } = false;
        public string LocalIP { get; set; } = "";
        public string PublicIP { get; set; } = "";
        public bool EnableServerSync { get; set; } = false;
        public int HeartbeatIntervalMinutes { get; set; } = 5;
    }

}