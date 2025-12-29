using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetworkMonitor.Server.Data;

namespace NetworkMonitor.Server.Data
{
    // Converted from top-level Program to helper initializer to avoid duplicate top-level statements
    public static class DataInitializer
    {
        public static void EnsureDatabaseCreated(IServiceProvider services)
        {
            using (var scope = services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                dbContext.Database.EnsureCreated();
            }
        }
    }
}