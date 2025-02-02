// Data/SpendWiseContextFactory.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace SpendWise.Data
{
    public class SpendWiseContextFactory : IDesignTimeDbContextFactory<SpendWiseContext>
    {
        public SpendWiseContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var builder = new DbContextOptionsBuilder<SpendWiseContext>();
            var connectionString = configuration.GetConnectionString("SpendWiseDatabase");

            builder.UseMySql(
                connectionString,
                ServerVersion.AutoDetect(connectionString),
                options => options.EnableRetryOnFailure());

            return new SpendWiseContext(builder.Options);
        }
    }
}