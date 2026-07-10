using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WDAS.Infrastructure.Persistence;

public class WdasDbContextFactory : IDesignTimeDbContextFactory<WdasDbContext>
{
    public WdasDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("WDAS_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=DocumentManagementSystem;Username=postgres;Password=S.ruhaanhasan;";

        var optionsBuilder = new DbContextOptionsBuilder<WdasDbContext>();
        DatabaseConnection.ConfigureDbContext(optionsBuilder, connectionString);
        return new WdasDbContext(optionsBuilder.Options);
    }
}
