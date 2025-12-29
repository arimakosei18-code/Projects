using System;
using System.Collections.Generic;
using System.Linq;

namespace NetworkMonitor.Shared.Models
{
    public class AppConfiguration
    {
        public List<Device> Devices { get; set; } = new List<Device>();
        public int PingIntervalSeconds { get; set; } = 60;
        public int PingTimeoutMs { get; set; } = 2000;
        public int EmailNotificationHours { get; set; } = 24;
        public bool SendImmediateAlerts { get; set; } = true;
        public string SmtpServer { get; set; } = "smtp.gmail.com";
        public int SmtpPort { get; set; } = 587;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string FromEmail { get; set; } = "";
        public List<string> Recipients { get; set; } = new List<string>();
        public string LogFilePath { get; set; } = @"C:\NetworkMonitor\Logs\service.log";
        
        // ADDED: SSL property
        public bool EnableSsl { get; set; } = true;
        
        // NEW: Offline ping threshold setting
        public int OfflinePingThreshold { get; set; } = 30;
        
        public void AddDevice(Device device)
        {
            Devices.Add(device);
        }
        
        public bool RemoveDevice(Guid id)
        {
            var device = Devices.FirstOrDefault(d => d.Id == id);
            if (device != null)
            {
                Devices.Remove(device);
                return true;
            }
            return false;
        }
        
        public Device GetDeviceByIp(string ip)
        {
            return Devices.FirstOrDefault(d => d.IPAddress == ip);
        }
        
        public Device GetDeviceById(Guid id)
        {
            return Devices.FirstOrDefault(d => d.Id == id);
        }
        
        public void SetRecipientsFromString(string recipients)
        {
            Recipients = recipients.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(r => r.Trim())
                .Where(r => !string.IsNullOrEmpty(r))
                .ToList();
        }
    }

    


}