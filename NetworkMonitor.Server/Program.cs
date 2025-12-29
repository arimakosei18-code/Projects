using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Server.Data;
using NetworkMonitor.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
    });

builder.Services.AddControllers();

// Add DbContext - Use SQL Server or InMemory for development
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("NetworkMonitorServer"));
}

// Register services
builder.Services.AddScoped<ClientService>();
builder.Services.AddScoped<DeviceService>();

// Add Swagger for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Network Monitor Server API", Version = "v1" });
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add background service for cleanup
builder.Services.AddHostedService<CleanupService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI(c => 
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Network Monitor API v1");
        c.RoutePrefix = "api-docs";
    });
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
    
    // Seed sample data for development
    if (app.Environment.IsDevelopment())
    {
        await SeedData.InitializeAsync(dbContext);
    }
}

// Support a quick in-process smoke test mode: run a few service-level checks and exit
if (args.Contains("--smoke-test"))
{
    using (var scope = app.Services.CreateScope())
    {
        var clientService = scope.ServiceProvider.GetRequiredService<ClientService>();
        var deviceService = scope.ServiceProvider.GetRequiredService<DeviceService>();

        Console.WriteLine("Running in-process smoke tests...");

        // Simple test sequence
        var clients = await clientService.GetAllClientsAsync(includeDevices: true);
        Console.WriteLine($"Clients seeded: {clients.Count}");

        var hb = new NetworkMonitor.Server.DTOs.HeartbeatDto
        {
            ClientId = "smoke-client-1",
            ClientName = "Smoke Client",
            LocalIP = "10.0.0.10",
            PublicIP = "203.0.113.10",
            OnlineDevices = 1,
            TotalDevices = 1,
            Version = "test"
        };

        var processed = await clientService.ProcessHeartbeatAsync(hb);
        Console.WriteLine($"Processed heartbeat: {processed.ClientName} (IsOnline: {processed.IsOnline})");

        var ds = new NetworkMonitor.Server.DTOs.DeviceStatusDto
        {
            ClientId = processed.ClientId,
            DeviceName = "Smoke Device",
            IPAddress = "10.0.0.11",
            MACAddress = "AA:BB:CC:DD:EE:FF",
            Manufacturer = "SmokeCo",
            Status = "Online",
            PingTime = 5,
            Timestamp = DateTime.UtcNow
        };

        await deviceService.UpdateDeviceStatusAsync(ds);
        var devices = await deviceService.GetAllDevicesAsync();
        Console.WriteLine($"Devices after update: {devices.Count}");

        var alerts = await clientService.GetAlertsAsync(10);
        Console.WriteLine($"Alerts count: {alerts.Count}");

        Console.WriteLine("Smoke tests finished. Exiting application.");

        // Stop the host gracefully (will cause app.Run to return)
        var lifetime = app.Services.GetRequiredService<Microsoft.Extensions.Hosting.IHostApplicationLifetime>();
        lifetime.StopApplication();
    }
}

app.Run();

// Expose a partial Program class for in-memory HTTP testing
public partial class Program { }

// Public marker type for in-memory testing
public class TestEntryPoint { }