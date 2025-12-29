using Microsoft.AspNetCore.Mvc;
using NetworkMonitor.Server.DTOs;
using NetworkMonitor.Server.Services;
using System;
using System.Threading.Tasks;

namespace NetworkMonitor.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ApiController : ControllerBase
    {
        private readonly ClientService _clientService;
        private readonly DeviceService _deviceService;
        private readonly ILogger<ApiController> _logger;
        
        public ApiController(ClientService clientService, DeviceService deviceService, ILogger<ApiController> logger)
        {
            _clientService = clientService;
            _deviceService = deviceService;
            _logger = logger;
        }
        
        // POST: api/heartbeat
        [HttpPost("heartbeat")]
        public async Task<ActionResult<ApiResponse>> Heartbeat([FromBody] HeartbeatRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.ClientId))
                {
                    return BadRequest(new ApiResponse
                    {
                        Success = false,
                        Message = "ClientId is required"
                    });
                }
                
                var heartbeatDto = new HeartbeatDto
                {
                    ClientId = request.ClientId,
                    ClientName = request.ClientName,
                    LocalIP = request.LocalIP,
                    PublicIP = request.PublicIP,
                    OnlineDevices = request.OnlineDevices,
                    TotalDevices = request.TotalDevices,
                    Version = request.Version,
                    Timestamp = DateTime.UtcNow
                };
                
                var client = await _clientService.ProcessHeartbeatAsync(heartbeatDto);
                
                return Ok(new ApiResponse<HeartbeatResponse>
                {
                    Success = true,
                    Message = "Heartbeat received",
                    Data = new HeartbeatResponse
                    {
                        ServerTime = DateTime.UtcNow,
                        ClientId = client.ClientId,
                        Message = "Heartbeat processed successfully",
                        NextHeartbeatInSeconds = 300 // 5 minutes
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing heartbeat for client {ClientId}", request.ClientId);
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Message = $"Server error: {ex.Message}"
                });
            }
        }
        
        // GET: api/devices/{clientId}
        [HttpGet("devices/{clientId}")]
        public async Task<ActionResult<ApiResponse<List<DeviceResponse>>>> GetDevices(string clientId)
        {
            try
            {
                var devices = await _deviceService.GetDevicesForClientAsync(clientId);
                
                var response = devices.Select(d => new DeviceResponse
                {
                    Id = d.Id,
                    Name = d.Name,
                    IPAddress = d.IPAddress,
                    MACAddress = d.MACAddress,
                    Manufacturer = d.Manufacturer,
                    IsEnabled = d.IsEnabled,
                    Status = d.Status,
                    PingTime = d.PingTime,
                    LastSeen = d.LastSeen,
                    DeviceType = d.DeviceType,
                    Description = d.Description
                }).ToList();
                
                return Ok(new ApiResponse<List<DeviceResponse>>
                {
                    Success = true,
                    Message = $"Found {response.Count} devices",
                    Data = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting devices for client {ClientId}", clientId);
                return StatusCode(500, new ApiResponse<List<DeviceResponse>>
                {
                    Success = false,
                    Message = $"Server error: {ex.Message}",
                    Data = new List<DeviceResponse>()
                });
            }
        }
        
        // POST: api/devicestatus
        [HttpPost("devicestatus")]
        public async Task<ActionResult<ApiResponse>> UpdateDeviceStatus([FromBody] DeviceStatusRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.ClientId) || request.DeviceId == Guid.Empty)
                {
                    return BadRequest(new ApiResponse
                    {
                        Success = false,
                        Message = "ClientId and DeviceId are required"
                    });
                }
                
                var statusDto = new DeviceStatusDto
                {
                    ClientId = request.ClientId,
                    DeviceId = request.DeviceId,
                    DeviceName = request.DeviceName,
                    IPAddress = request.IPAddress,
                    MACAddress = request.MACAddress,
                    Manufacturer = request.Manufacturer,
                    Status = request.Status,
                    PingTime = request.PingTime,
                    LastSeen = request.LastSeen,
                    Timestamp = DateTime.UtcNow
                };
                
                var device = await _deviceService.UpdateDeviceStatusAsync(statusDto);
                
                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Device status updated",
                    Data = new
                    {
                        DeviceId = device.Id,
                        DeviceName = device.Name,
                        Status = device.Status,
                        Updated = device.LastUpdated
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating device status for device {DeviceId}", request.DeviceId);
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Message = $"Server error: {ex.Message}"
                });
            }
        }
        
        // GET: api/clients
        [HttpGet("clients")]
        public async Task<ActionResult<ApiResponse<List<ClientResponse>>>> GetClients()
        {
            try
            {
                var clients = await _clientService.GetAllClientsAsync(true);
                
                var response = clients.Select(c => new ClientResponse
                {
                    ClientId = c.ClientId,
                    ClientName = c.ClientName,
                    LocalIP = c.LocalIP,
                    PublicIP = c.PublicIP,
                    FirstSeen = c.FirstSeen,
                    LastHeartbeat = c.LastHeartbeat,
                    IsOnline = c.IsOnline,
                    OnlineDevices = c.OnlineDevices,
                    TotalDevices = c.TotalDevices,
                    Version = c.Version,
                    Uptime = c.Uptime,
                    IsStale = c.IsStale,
                    DeviceCount = c.Devices.Count
                }).ToList();
                
                return Ok(new ApiResponse<List<ClientResponse>>
                {
                    Success = true,
                    Message = $"Found {response.Count} clients",
                    Data = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting clients");
                return StatusCode(500, new ApiResponse<List<ClientResponse>>
                {
                    Success = false,
                    Message = $"Server error: {ex.Message}",
                    Data = new List<ClientResponse>()
                });
            }
        }
        
        // GET: api/stats
        [HttpGet("stats")]
        public async Task<ActionResult<ApiResponse<object>>> GetStats()
        {
            try
            {
                var clientStats = await _clientService.GetStatisticsAsync();
                var deviceStats = await _deviceService.GetDeviceStatisticsAsync();
                
                var alerts = await _clientService.GetAlertsAsync(10, true);
                
                var stats = new
                {
                    Server = new
                    {
                        Time = DateTime.UtcNow,
                        Uptime = TimeSpan.FromSeconds(Environment.TickCount / 1000)
                    },
                    Clients = clientStats,
                    Devices = deviceStats,
                    RecentAlerts = alerts.Select(a => new
                    {
                        a.Id,
                        a.AlertType,
                        a.Message,
                        a.Severity,
                        a.Created,
                        ClientName = a.Client?.ClientName,
                        DeviceName = a.Device?.Name
                    })
                };
                
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Server statistics",
                    Data = stats
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stats");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = $"Server error: {ex.Message}"
                });
            }
        }
        
        // GET: api/alerts
        [HttpGet("alerts")]
        public async Task<ActionResult<ApiResponse<List<AlertResponse>>>> GetAlerts(
            [FromQuery] bool unresolvedOnly = false,
            [FromQuery] int count = 50)
        {
            try
            {
                var alerts = await _clientService.GetAlertsAsync(count, unresolvedOnly);
                
                var response = alerts.Select(a => new AlertResponse
                {
                    Id = a.Id,
                    ClientId = a.ClientId,
                    DeviceId = a.DeviceId,
                    AlertType = a.AlertType,
                    Message = a.Message,
                    Created = a.Created,
                    Resolved = a.Resolved,
                    IsResolved = a.IsResolved,
                    Severity = a.Severity,
                    ClientName = a.Client?.ClientName,
                    DeviceName = a.Device?.Name
                }).ToList();
                
                return Ok(new ApiResponse<List<AlertResponse>>
                {
                    Success = true,
                    Message = $"Found {response.Count} alerts",
                    Data = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting alerts");
                return StatusCode(500, new ApiResponse<List<AlertResponse>>
                {
                    Success = false,
                    Message = $"Server error: {ex.Message}",
                    Data = new List<AlertResponse>()
                });
            }
        }
        
        // POST: api/alerts/{id}/resolve
        [HttpPost("alerts/{id}/resolve")]
        public async Task<ActionResult<ApiResponse>> ResolveAlert(long id)
        {
            try
            {
                // Implementation to resolve an alert
                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Alert resolved"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving alert {AlertId}", id);
                return StatusCode(500, new ApiResponse
                {
                    Success = false,
                    Message = $"Server error: {ex.Message}"
                });
            }
        }
    }
    
    // DTO classes
    public class HeartbeatRequest
    {
        public string ClientId { get; set; } = null!;
        public string ClientName { get; set; } = null!;
        public string LocalIP { get; set; } = string.Empty;
        public string PublicIP { get; set; } = string.Empty;
        public int OnlineDevices { get; set; }
        public int TotalDevices { get; set; }
        public string Version { get; set; } = "1.0.0";
    }
    
    public class HeartbeatResponse
    {
        public DateTime ServerTime { get; set; }
        public string ClientId { get; set; } = null!;
        public string Message { get; set; } = string.Empty;
        public int NextHeartbeatInSeconds { get; set; }
    }
    
    public class DeviceStatusRequest
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
    }
    
    public class DeviceResponse
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
        public string DeviceType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
    
    public class ClientResponse
    {
        public string ClientId { get; set; } = null!;
        public string ClientName { get; set; } = null!;
        public string LocalIP { get; set; } = string.Empty;
        public string PublicIP { get; set; } = string.Empty;
        public DateTime FirstSeen { get; set; }
        public DateTime LastHeartbeat { get; set; }
        public bool IsOnline { get; set; }
        public int OnlineDevices { get; set; }
        public int TotalDevices { get; set; }
        public string Version { get; set; } = string.Empty;
        public TimeSpan Uptime { get; set; }
        public bool IsStale { get; set; }
        public int DeviceCount { get; set; }
    }
    
    public class AlertResponse
    {
        public long Id { get; set; }
        public string ClientId { get; set; } = null!;
        public Guid? DeviceId { get; set; }
        public string AlertType { get; set; } = null!;
        public string Message { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public DateTime? Resolved { get; set; }
        public bool IsResolved { get; set; }
        public string Severity { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
    }
    
    public class ApiResponse<T> : ApiResponse
    {
        public T Data { get; set; } = default!;
    }
    
    public class ApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public object Data { get; set; } = new object();
    }
}