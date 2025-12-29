using System;

namespace NetworkMonitor.Shared
{
    public class DeviceStatusForEmail
    {
        public string DeviceName { get; set; }
        public string IPAddress { get; set; }
        public string Status { get; set; }
        public DateTime? LastSeen { get; set; }
        public int PingTime { get; set; }
        
        // NEW: Add ConsecutiveOfflinePings property
        public int ConsecutiveOfflinePings { get; set; } = 0;
    }
}