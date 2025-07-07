using aspnet_biometric.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace aspnet_biometric.Data
{
    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Fido2Credential> Fido2Credentials { get; set; }
        
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            
            // Add a unique index on Fido2Id to ensure no two users have the same FIDO2 handle
            builder.Entity<User>()
                .HasIndex(u => u.Fido2Id)
                .IsUnique();

            // Configure the relationship between User and Fido2Credential as required
            builder.Entity<Fido2Credential>()
                .HasOne(c => c.User)
                .WithMany(u => u.Fido2Credentials)
                .HasForeignKey(c => c.UserId)
                .IsRequired();
        }
    }
} 