using Microsoft.EntityFrameworkCore;
using SolarConnect.Models;
using System;

namespace SolarConnect.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // === DbSets ===
        public DbSet<User> Users { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<Vendor> Vendors { get; set; }
        public DbSet<Request> Requests { get; set; }
        public DbSet<Quote> Quotes { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Project> Projects { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)

        {
            base.OnModelCreating(modelBuilder);

            // === Relationships ===

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
                .OnDelete(DeleteBehavior.Restrict); // ✅ Prevent multiple cascade paths

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Vendor)
                .WithMany()
                .HasForeignKey(p => p.VendorId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.Client)
                .WithMany()
                .HasForeignKey(r => r.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.Vendor)
                .WithMany()
                .HasForeignKey(r => r.VendorId)
                .OnDelete(DeleteBehavior.Restrict); // ✅ Fix cascade cycle

            // === Precision Fix for decimals ===
            modelBuilder.Entity<Product>()
                .Property(p => p.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Quote>()
                .Property(q => q.TotalPrice)
                .HasPrecision(18, 2);
            modelBuilder.Entity<Project>()
           .HasOne(p => p.Quote)
           .WithMany()
           .HasForeignKey(p => p.QuoteId)
           .OnDelete(DeleteBehavior.Cascade);


            // === Seed Admin User ===
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
        }
    }
}
