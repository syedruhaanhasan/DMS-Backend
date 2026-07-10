using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace WDAS.Infrastructure.Persistence;

public enum DatabaseProviderKind
{
    Sqlite,
    PostgreSql,
    SqlServer
}

public static class DatabaseConnection
{
    public static string ResolveConnectionString(IConfiguration configuration)
    {
        var provider = configuration.GetValue<string>("Database:Provider");
        if (!string.IsNullOrWhiteSpace(provider))
        {
            var key = NormalizeProviderKey(provider);
            var named = configuration.GetConnectionString(key);
            if (!string.IsNullOrWhiteSpace(named))
            {
                return named;
            }
        }

        return configuration.GetConnectionString("DefaultConnection")
            ?? configuration.GetConnectionString("PostgreSql")
            ?? "Host=localhost;Port=5432;Database=DocumentManagementSystem;Username=postgres;Password=postgres;";
    }

    public static DatabaseProviderKind ResolveProvider(IConfiguration configuration) =>
        Detect(ResolveConnectionString(configuration));

    private static string NormalizeProviderKey(string provider) =>
        provider.Trim() switch
        {
            "PostgreSql" or "Postgres" or "postgresql" or "pgsql" => "PostgreSql",
            "SqlServer" or "MSSQL" or "sqlserver" or "mssql" => "SqlServer",
            "Sqlite" or "SQLite" or "sqlite" => "Sqlite",
            _ => provider.Trim()
        };

    public static DatabaseProviderKind Detect(string connectionString)
    {
        var cs = connectionString.Trim();

        if (cs.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) &&
            !LooksLikePostgreSql(cs))
        {
            return DatabaseProviderKind.Sqlite;
        }

        if (LooksLikePostgreSql(cs))
        {
            return DatabaseProviderKind.PostgreSql;
        }

        return DatabaseProviderKind.SqlServer;
    }

    public static void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString)
    {
        switch (Detect(connectionString))
        {
            case DatabaseProviderKind.Sqlite:
                options.UseSqlite(connectionString);
                break;
            case DatabaseProviderKind.PostgreSql:
                options.UseNpgsql(connectionString);
                break;
            default:
                options.UseSqlServer(connectionString);
                break;
        }
    }

    private static bool LooksLikePostgreSql(string connectionString) =>
        connectionString.Contains("Port=5432", StringComparison.OrdinalIgnoreCase) ||
        connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
        connectionString.Contains("Username=", StringComparison.OrdinalIgnoreCase);
}
