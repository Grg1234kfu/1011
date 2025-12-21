using Microsoft.EntityFrameworkCore;
using SolarConnect.Models;

namespace SolarConnect.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<Vendor> Vendors { get; set; }
        public DbSet<Request> Requests { get; set; }
        public DbSet<Quote> Quotes { get; set; }

        public DbSet<Product> Products { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure relationships
            modelBuilder.Entity<Client>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Vendor>()
                .HasOne(v => v.User)
                .WithMany()
                .HasForeignKey(v => v.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Seed Admin User
            // Seed Admin User
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Email = "admin@solarconnect.com",
                    Password = "jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg=", // "Admin123"
                    FirstName = "Admin",
                    LastName = "User",
                    PhoneNumber = "1234567890",
                    Role = "Admin",
                    IsActive = true,
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );
            modelBuilder.Entity<Request>()
    .HasOne(r => r.Client)
    .WithMany()
    .HasForeignKey(r => r.ClientId)
    .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Quote>()
    .HasOne(q => q.Request)
    .WithMany()
    .HasForeignKey(q => q.RequestId)
    .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Quote>()
                .HasOne(q => q.Vendor)
                .WithMany()
                .HasForeignKey(q => q.VendorId)
                .OnDelete(DeleteBehavior.NoAction);

        }
    }
}