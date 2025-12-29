using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using NetworkMonitor.Shared.Models;
using NetworkMonitor.Shared;

namespace NetworkMonitor.Service
{
    public class MonitorEngine
    {
        private readonly AppConfiguration _config;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _monitoringTask;
        private readonly Dictionary<Guid, DeviceStatus> _deviceStatuses = new Dictionary<Guid, DeviceStatus>();
        private readonly object _lock = new object();

        public event EventHandler<DeviceStatusChangedEventArgs> DeviceStatusChanged;
        public event EventHandler<string> LogMessage;

        public MonitorEngine(AppConfiguration config)
        {
            _config = config;
            
            // Initialize device statuses
            foreach (var device in _config.Devices)
            {
                _deviceStatuses[device.Id] = new DeviceStatus
                {
                    DeviceId = device.Id,
                    DeviceName = device.Name,
                    IPAddress = device.IPAddress,
                    IsOnline = false,
                    LastSeen = null,
                    PingTime = 0,
                    StatusChangeTime = DateTime.Now,
                    ConsecutiveOfflinePings = 0,
                    OfflineEmailSent = false
                };
            }
        }

        public void Start()
        {
            if (_monitoringTask != null && !_monitoringTask.IsCompleted)
                return;

            _cancellationTokenSource = new CancellationTokenSource();
            _monitoringTask = Task.Run(() => MonitorDevicesAsync(_cancellationTokenSource.Token));
            
            OnLogMessage("Monitoring engine started.");
        }

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
            
            try
            {
                _monitoringTask?.Wait(TimeSpan.FromSeconds(30));
            }
            catch (AggregateException)
            {
                // Task cancellation expected
            }
            
            OnLogMessage("Monitoring engine stopped.");
        }

        private async Task MonitorDevicesAsync(CancellationToken cancellationToken)
        {
            OnLogMessage($"Starting to monitor {_config.Devices.Count} devices...");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAllDevicesAsync(cancellationToken);
                    
                    // Wait for the configured interval
                    await Task.Delay(_config.PingIntervalSeconds * 1000, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // Expected when stopping
                    break;
                }
                catch (Exception ex)
                {
                    OnLogMessage($"Error in monitoring loop: {ex.Message}");
                    await Task.Delay(5000, cancellationToken); // Wait 5 seconds on error
                }
            }
        }

        private async Task CheckAllDevicesAsync(CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            
            foreach (var device in _config.Devices.Where(d => d.IsEnabled))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                tasks.Add(CheckDeviceAsync(device, cancellationToken));
                
                // Small delay between starting each device check to avoid overwhelming
                await Task.Delay(100, cancellationToken);
            }

            await Task.WhenAll(tasks);
        }

        private async Task CheckDeviceAsync(Device device, CancellationToken cancellationToken)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(device.IPAddress, _config.PingTimeoutMs);
                    
                    bool isOnline = reply.Status == IPStatus.Success;
                    int pingTime = (int)reply.RoundtripTime;
                    
                    UpdateDeviceStatus(device, isOnline, pingTime);
                }
            }
            catch (Exception ex)
            {
                OnLogMessage($"Error pinging {device.Name} ({device.IPAddress}): {ex.Message}");
                UpdateDeviceStatus(device, false, 0);
            }
        }

        private void UpdateDeviceStatus(Device device, bool isOnline, int pingTime)
        {
            lock (_lock)
            {
                var previousStatus = _deviceStatuses.ContainsKey(device.Id) 
                    ? _deviceStatuses[device.Id] 
                    : null;

                var newStatus = new DeviceStatus
                {
                    DeviceId = device.Id,
                    DeviceName = device.Name,
                    IPAddress = device.IPAddress,
                    IsOnline = isOnline,
                    LastSeen = isOnline ? DateTime.Now : previousStatus?.LastSeen,
                    PingTime = pingTime,
                    StatusChangeTime = DateTime.Now,
                    ConsecutiveOfflinePings = isOnline ? 0 : (previousStatus?.ConsecutiveOfflinePings ?? 0) + 1,
                    OfflineEmailSent = previousStatus?.OfflineEmailSent ?? false
                };

                // Store the previous online status before updating
                bool wasOnline = previousStatus?.IsOnline ?? false;
                
                _deviceStatuses[device.Id] = newStatus;

                // Check if status changed
                if (previousStatus == null || wasOnline != isOnline)
                {
                    var statusText = isOnline ? "Online" : "Offline";
                    OnLogMessage($"Device {device.Name} is now {statusText} (Ping: {pingTime}ms)");
                    
                    OnDeviceStatusChanged(new DeviceStatusChangedEventArgs
                    {
                        Device = device,
                        IsOnline = isOnline,
                        PingTime = pingTime,
                        PreviousStatus = wasOnline
                    });

                    // Reset offline counters if device came back online
                    if (isOnline)
                    {
                        newStatus.ConsecutiveOfflinePings = 0;
                        newStatus.OfflineEmailSent = false;
                    }
                }

                // Check if we need to send offline email
                if (!isOnline && 
                    !newStatus.OfflineEmailSent && 
                    newStatus.ConsecutiveOfflinePings >= _config.OfflinePingThreshold)
                {
                    SendOfflineEmail(device, newStatus.ConsecutiveOfflinePings);
                    newStatus.OfflineEmailSent = true;
                    OnLogMessage($"Offline email sent for {device.Name} after {newStatus.ConsecutiveOfflinePings} pings");
                }
            }
        }

        private void SendOfflineEmail(Device device, int consecutiveOfflinePings)
        {
            try
            {
                using (var smtpClient = new SmtpClient(_config.SmtpServer, _config.SmtpPort))
                {
                    smtpClient.EnableSsl = _config.EnableSsl;
                    smtpClient.Credentials = new NetworkCredential(_config.Username, _config.Password);
                    
                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(_config.FromEmail),
                        Subject = $"🚨 ALERT: Device {device.Name} is OFFLINE after {consecutiveOfflinePings} checks",
                        Body = $@"
                        Network Monitor Alert - Device Offline

                        Device Name: {device.Name}
                        IP Address: {device.IPAddress}
                        Status: OFFLINE
                        Consecutive Offline Pings: {consecutiveOfflinePings}
                        Threshold: {_config.OfflinePingThreshold}
                        Alert Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

                        This device has been offline for {consecutiveOfflinePings} consecutive checks.
                        No further emails will be sent for this device until it comes back online.

                        ---
                        Network Monitor System
                        Auto-generated Alert
                        ",
                        IsBodyHtml = false
                    };
                    
                    foreach (var recipient in _config.Recipients)
                    {
                        mailMessage.To.Add(recipient);
                    }
                    
                    smtpClient.Send(mailMessage);
                }
            }
            catch (Exception ex)
            {
                OnLogMessage($"❌ Failed to send offline email: {ex.Message}");
            }
        }

        private void OnDeviceStatusChanged(DeviceStatusChangedEventArgs e)
        {
            DeviceStatusChanged?.Invoke(this, e);
        }

        private void OnLogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logMessage = $"[{timestamp}] {message}";
            
            LogMessage?.Invoke(this, logMessage);
        }

        public List<DeviceStatus> GetCurrentStatuses()
        {
            lock (_lock)
            {
                return _deviceStatuses.Values.ToList();
            }
        }

        public DeviceStatus GetDeviceStatus(Guid deviceId)
        {
            lock (_lock)
            {
                return _deviceStatuses.ContainsKey(deviceId) ? _deviceStatuses[deviceId] : null;
            }
        }
    }

    public class DeviceStatusChangedEventArgs : EventArgs
    {
        public Device Device { get; set; }
        public bool IsOnline { get; set; }
        public int PingTime { get; set; }
        public bool? PreviousStatus { get; set; }
    }

    public class DeviceStatus
    {
        public Guid DeviceId { get; set; }
        public string DeviceName { get; set; }
        public string IPAddress { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastSeen { get; set; }
        public int PingTime { get; set; }
        public DateTime StatusChangeTime { get; set; }
        public int ConsecutiveOfflinePings { get; set; }
        public bool OfflineEmailSent { get; set; }
    }
}