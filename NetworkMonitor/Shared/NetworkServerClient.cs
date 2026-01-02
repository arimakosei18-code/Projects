using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NetworkMonitor.Shared.Models;

namespace NetworkMonitor.Shared
{
    public class NetworkServerClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _serverUrl;
        private readonly string _clientId;

        public NetworkServerClient(string serverUrl, string clientId)
        {
            _serverUrl = serverUrl.TrimEnd('/');
            _clientId = clientId;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        public async Task<ServerResponse> SendHeartbeatAsync(HeartbeatData heartbeat)
        {
            try
            {
                var json = JsonConvert.SerializeObject(heartbeat);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{_serverUrl}/api/heartbeat", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    return new ServerResponse
                    {
                        Success = true,
                        Message = "Heartbeat sent successfully",
                        Data = responseContent
                    };
                }
                
                return new ServerResponse
                {
                    Success = false,
                    Message = $"Server error: {response.StatusCode}",
                    Data = responseContent
                };
            }
            catch (Exception ex)
            {
                return new ServerResponse
                {
                    Success = false,
                    Message = $"Error sending heartbeat: {ex.Message}"
                };
            }
        }

        public async Task<ServerResponse<List<Device>>> GetDevicesFromServerAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_serverUrl}/api/devices/{_clientId}");
                var responseContent = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    var devices = JsonConvert.DeserializeObject<List<Device>>(responseContent);
                    return new ServerResponse<List<Device>>
                    {
                        Success = true,
                        Message = "Devices retrieved successfully",
                        Data = devices
                    };
                }
                
                return new ServerResponse<List<Device>>
                {
                    Success = false,
                    Message = $"Server error: {response.StatusCode}",
                    Data = new List<Device>()
                };
            }
            catch (Exception ex)
            {
                return new ServerResponse<List<Device>>
                {
                    Success = false,
                    Message = $"Error retrieving devices: {ex.Message}",
                    Data = new List<Device>()
                };
            }
        }

        public async Task<ServerResponse> SendDeviceStatusAsync(DeviceStatusData status)
        {
            try
            {
                var json = JsonConvert.SerializeObject(status);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{_serverUrl}/api/devicestatus", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    return new ServerResponse
                    {
                        Success = true,
                        Message = "Device status sent successfully"
                    };
                }
                
                return new ServerResponse
                {
                    Success = false,
                    Message = $"Server error: {response.StatusCode}"
                };
            }
            catch (Exception ex)
            {
                return new ServerResponse
                {
                    Success = false,
                    Message = $"Error sending device status: {ex.Message}"
                };
            }
        }

        public async Task<ServerResponse<string>> GetPublicIPAsync()
        {
            try
            {
                var response = await _httpClient.GetStringAsync("https://api.ipify.org");
                return new ServerResponse<string>
                {
                    Success = true,
                    Message = "Public IP retrieved",
                    Data = response.Trim()
                };
            }
            catch (Exception ex)
            {
                return new ServerResponse<string>
                {
                    Success = false,
                    Message = $"Error getting public IP: {ex.Message}",
                    Data = ""
                };
            }
        }
    }

    // Data classes for server communication
    public class HeartbeatData
    {
        public string ClientId { get; set; }
        public string ClientName { get; set; }
        public string LocalIP { get; set; }
        public string PublicIP { get; set; }
        public int OnlineDevices { get; set; }
        public int TotalDevices { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class DeviceStatusData
    {
        public string ClientId { get; set; }
        public Guid DeviceId { get; set; }
        public string DeviceName { get; set; }
        public string IPAddress { get; set; }
        public string Status { get; set; }
        public int PingTime { get; set; }
        public DateTime? LastSeen { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class ServerResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Data { get; set; }
    }

    public class ServerResponse<T> : ServerResponse
    {
        public new T Data { get; set; }
    }
}