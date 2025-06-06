using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOpenId
{
    public class MyOpenIdDbContext : DbContext
    {
        public MyOpenIdDbContext(DbContextOptions<MyOpenIdDbContext> options)
            : base(options)
        {
        }

        public DbSet<OIDCClient> Clients { get; set; }
        public DbSet<MyAccessToken> Tokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OIDCClient>(entity =>
            {
                entity.HasKey(e => e.ClientId);
                entity.Property(e => e.ClientSecret)
                    .IsRequired()
                    .HasMaxLength(200);
                entity.Property(e => e.AllowedScopes)
                    .IsRequired()
                    .HasMaxLength(200);
            });

            modelBuilder.Entity<MyAccessToken>(entity =>
            {
                entity.HasKey(e => e.TokenId);
                entity.Property(e => e.ClientId)
                    .IsRequired()
                    .HasMaxLength(200);
                entity.Property(e => e.Scopes)
                    .IsRequired()
                    .HasMaxLength(200);
                entity.Property(e => e.Expires);
                entity.Property(e => e.Created);
            });
        }
    }

    namespace MyOpenId
    {
        public class MyOpenIdDbContextFactory : IDesignTimeDbContextFactory<MyOpenIdDbContext>
        {
            public MyOpenIdDbContext CreateDbContext(string[] args)
            {
                //IConfigurationRoot configuration = new ConfigurationBuilder()
                //    .SetBasePath(Directory.GetCurrentDirectory())
                //    .AddJsonFile("appsettings.json")
                //    .Build();

                //var connectionString = configuration.GetConnectionString("DefaultConnection");
                string connectionString = "Server=sqlserver,1433;Database=MyOpenId;User Id=sa;Password=YourStrong@Passw0rd123;Integrated Security=True;";
                var optionsBuilder = new DbContextOptionsBuilder<MyOpenIdDbContext>();
                optionsBuilder.UseSqlServer(connectionString);

                return new MyOpenIdDbContext(optionsBuilder.Options);
            }
        }
    }
}
