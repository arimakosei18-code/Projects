using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Server.Data;
using NetworkMonitor.Server.Models;
using NetworkMonitor.Server.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkMonitor.Server.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ClientService _clientService;
        private readonly DeviceService _deviceService;
        
        public HomeController(ApplicationDbContext context, ClientService clientService, DeviceService deviceService)
        {
            _context = context;
            _clientService = clientService;
            _deviceService = deviceService;
        }
        
        public async Task<IActionResult> Index()
        {
            var stats = await _clientService.GetStatisticsAsync();
            ViewBag.Stats = stats;
            
            var recentAlerts = await _clientService.GetAlertsAsync(10, true);
            ViewBag.RecentAlerts = recentAlerts;
            
            var topClients = await _context.Clients
                .Include(c => c.Devices)
                .OrderByDescending(c => c.TotalDevices)
                .Take(5)
                .ToListAsync();
            ViewBag.TopClients = topClients;
            
            return View();
        }
        
        public async Task<IActionResult> Clients()
        {
            var clients = await _clientService.GetAllClientsAsync(true);
            return View(clients);
        }
        
        public async Task<IActionResult> ClientDetails(string id)
        {
            var client = await _clientService.GetClientAsync(id, true);
            if (client == null)
            {
                return NotFound();
            }
            
            ViewBag.DeviceStats = await _deviceService.GetDeviceStatisticsAsync();
            return View(client);
        }
        
        public async Task<IActionResult> Devices(string status = null, string type = null)
        {
            var devices = await _deviceService.GetAllDevicesAsync(status, type);
            
            ViewBag.StatusFilter = status;
            ViewBag.TypeFilter = type;
            ViewBag.StatusOptions = new[] { "Online", "Offline", "Checking", "Disabled" };
            ViewBag.TypeOptions = new[] { "Router", "Switch", "Firewall", "Server", "DNS Server", "NAS", "Printer", "Generic" };
            
            return View(devices);
        }
        
        public async Task<IActionResult> DeviceDetails(Guid id)
        {
            var device = await _deviceService.GetDeviceAsync(id);
            if (device == null)
            {
                return NotFound();
            }
            
            var history = await _deviceService.GetDeviceHistoryAsync(id, 24);
            ViewBag.History = history;
            
            return View(device);
        }
        
        public async Task<IActionResult> Alerts(bool unresolvedOnly = false)
        {
            var alerts = await _clientService.GetAlertsAsync(100, unresolvedOnly);
            
            ViewBag.UnresolvedOnly = unresolvedOnly;
            return View(alerts);
        }
        
        public async Task<IActionResult> Stats()
        {
            var clientStats = await _clientService.GetStatisticsAsync();
            var deviceStats = await _deviceService.GetDeviceStatisticsAsync();
            
            ViewBag.ClientStats = clientStats;
            ViewBag.DeviceStats = deviceStats;
            
            return View();
        }
        
        public IActionResult ApiDocs()
        {
            return View();
        }
        
        public IActionResult Settings()
        {
            return View();
        }
        
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
    
    public class ErrorViewModel
    {
        public string RequestId { get; set; }
        
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}