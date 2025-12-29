using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Server.Models;

namespace NetworkMonitor.Server.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        
        public DbSet<Client> Clients { get; set; } = null!;
        public DbSet<ClientDevice> ClientDevices { get; set; } = null!;
        public DbSet<DeviceStatusUpdate> DeviceStatusUpdates { get; set; } = null!;
        public DbSet<HeartbeatLog> HeartbeatLogs { get; set; } = null!;
        public DbSet<Alert> Alerts { get; set; } = null!;
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Client configuration
            modelBuilder.Entity<Client>(entity =>
            {
                entity.HasIndex(e => e.ClientId).IsUnique();
                entity.HasIndex(e => e.LastHeartbeat);
                entity.HasIndex(e => e.IsOnline);
                entity.HasIndex(e => e.PublicIP);
                
                entity.Property(e => e.ClientId).HasMaxLength(100);
                entity.Property(e => e.ClientName).HasMaxLength(200).IsRequired();
                entity.Property(e => e.PublicIP).HasMaxLength(50);
                entity.Property(e => e.LocalIP).HasMaxLength(50);
                entity.Property(e => e.Version).HasMaxLength(50);
                
                entity.HasMany(e => e.Devices)
                    .WithOne(d => d.Client)
                    .HasForeignKey(d => d.ClientId)
                    .OnDelete(DeleteBehavior.Cascade);
                    
                entity.HasMany(e => e.HeartbeatLogs)
                    .WithOne(h => h.Client)
                    .HasForeignKey(h => h.ClientId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            
            // ClientDevice configuration
            modelBuilder.Entity<ClientDevice>(entity =>
            {
                entity.HasIndex(e => new { e.ClientId, e.IPAddress }).IsUnique();
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.LastUpdated);
                entity.HasIndex(e => e.DeviceType);
                
                entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
                entity.Property(e => e.IPAddress).HasMaxLength(50).IsRequired();
                entity.Property(e => e.MACAddress).HasMaxLength(20);
                entity.Property(e => e.Status).HasMaxLength(50);
                entity.Property(e => e.DeviceType).HasMaxLength(50);
                
                entity.HasMany(e => e.StatusHistory)
                    .WithOne(s => s.Device)
                    .HasForeignKey(s => s.DeviceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            
            // DeviceStatusUpdate configuration
            modelBuilder.Entity<DeviceStatusUpdate>(entity =>
            {
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => new { e.DeviceId, e.Timestamp });
                
                entity.Property(e => e.Status).HasMaxLength(50);
            });
            
            // HeartbeatLog configuration
            modelBuilder.Entity<HeartbeatLog>(entity =>
            {
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => new { e.ClientId, e.Timestamp });
            });
            
            // Alert configuration
            modelBuilder.Entity<Alert>(entity =>
            {
                entity.HasIndex(e => e.Created);
                // Index the underlying Resolved column instead of the calculated IsResolved property
                entity.HasIndex(e => e.Resolved);
                entity.HasIndex(e => e.Severity);
                
                entity.Property(e => e.AlertType).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Message).HasMaxLength(500).IsRequired();
                entity.Property(e => e.Severity).HasMaxLength(50);
            });
        }
    }
}