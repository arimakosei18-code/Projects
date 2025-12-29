using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NetworkMonitor.Server.Models
{
    public class Client
    {
        [Key]
        [MaxLength(100)]
        public string ClientId { get; set; } = Guid.NewGuid().ToString();
        
        [Required]
        [MaxLength(200)]
        public string ClientName { get; set; } = Environment.MachineName;
        
        [MaxLength(50)]
        public string LocalIP { get; set; } = "";
        
        [MaxLength(50)]
        public string PublicIP { get; set; } = "";
        
        [Required]
        public DateTime FirstSeen { get; set; } = DateTime.UtcNow;
        
        public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
        
        public bool IsOnline { get; set; } = false;
        
        public int OnlineDevices { get; set; } = 0;
        public int TotalDevices { get; set; } = 0;
        
        [MaxLength(500)]
        public string Location { get; set; } = "Unknown";
        
        [MaxLength(1000)]
        public string Notes { get; set; } = "";
        
        public bool IsEnabled { get; set; } = true;
        
        [MaxLength(50)]
        public string Version { get; set; } = "1.0.0";
        
        // Navigation properties
        public virtual ICollection<ClientDevice> Devices { get; set; } = new List<ClientDevice>();
        public virtual ICollection<HeartbeatLog> HeartbeatLogs { get; set; } = new List<HeartbeatLog>();
        
        // Calculated property
        [NotMapped]
        public TimeSpan Uptime => LastHeartbeat - FirstSeen;
        
        [NotMapped]
        public string Status => IsOnline ? "🟢 Online" : "🔴 Offline";
        
        [NotMapped]
        public bool IsStale => (DateTime.UtcNow - LastHeartbeat).TotalMinutes > 10;
    }
    
    public class ClientDevice
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = null!;
        
        [Required]
        [MaxLength(100)]
        public string ClientId { get; set; } = null!;
        
        [Required]
        [MaxLength(50)]
        public string IPAddress { get; set; } = string.Empty;
        
        [MaxLength(20)]
        public string MACAddress { get; set; } = "";
        
        [MaxLength(200)]
        public string Manufacturer { get; set; } = "Unknown";
        
        public bool IsEnabled { get; set; } = true;
        
        [MaxLength(50)]
        public string Status { get; set; } = "Unknown";
        
        public int PingTime { get; set; } = 0;
        
        public DateTime? LastSeen { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public DateTime Created { get; set; } = DateTime.UtcNow;
        
        public int ConsecutiveOfflinePings { get; set; } = 0;
        public bool OfflineEmailSent { get; set; } = false;
        
        [MaxLength(500)]
        public string Description { get; set; } = "";
        
        [MaxLength(50)]
        public string DeviceType { get; set; } = "Generic";
        
        // Navigation properties
        public virtual Client Client { get; set; } = null!;
        public virtual ICollection<DeviceStatusUpdate> StatusHistory { get; set; } = new List<DeviceStatusUpdate>();
        
        // Calculated properties
        [NotMapped]
        public bool IsCritical => DeviceType == "Router" || DeviceType == "Firewall" || DeviceType == "Server";
        
        [NotMapped]
        public string StatusColor
        {
            get
            {
                return Status switch
                {
                    string s when s.Contains("Online") => "success",
                    string s when s.Contains("Offline") => "danger",
                    string s when s.Contains("Checking") => "info",
                    string s when s.Contains("Disabled") => "secondary",
                    _ => "warning"
                };
            }
        }
    }
    
    public class DeviceStatusUpdate
    {
        [Key]
        public long Id { get; set; }
        
        [Required]
        public Guid DeviceId { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string ClientId { get; set; }
        
        [MaxLength(50)]
        public string Status { get; set; }
        
        public int PingTime { get; set; }
        
        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual ClientDevice Device { get; set; } = null!;
    }
    
    public class HeartbeatLog
    {
        [Key]
        public long Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string ClientId { get; set; }
        
        public int OnlineDevices { get; set; }
        public int TotalDevices { get; set; }
        
        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        public double ResponseTimeMs { get; set; }
        
        // Navigation properties
        public virtual Client Client { get; set; }
    }
    
    public class Alert
    {
        [Key]
        public long Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string ClientId { get; set; }
        
        public Guid? DeviceId { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string AlertType { get; set; } // "DeviceOffline", "ClientOffline", "HighLatency"
        
        [Required]
        [MaxLength(500)]
        public string Message { get; set; }
        
        [Required]
        public DateTime Created { get; set; } = DateTime.UtcNow;
        
        public DateTime? Resolved { get; set; }
        
        [NotMapped]
        public bool IsResolved => Resolved.HasValue;
        
        [MaxLength(50)]
        public string Severity { get; set; } = "Medium"; // Low, Medium, High, Critical
        
        // Navigation properties
        public virtual Client Client { get; set; }
        public virtual ClientDevice Device { get; set; }
    }
}