using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace EventHub;

public sealed class PostgresDb(DbContextOptions options) : AppDb(options);

public sealed class PlatformDbFactory(DbContextOptions<AppDb> options) : IDbContextFactory<AppDb>
{
    public AppDb CreateDbContext() => options.Extensions.Any(x => x.GetType().Namespace?.StartsWith("Npgsql.") == true)
        ? new PostgresDb(options)
        : new AppDb(options);
}

public static class DatabaseConfiguration
{
    public static string PostgresConnection(IConfiguration config)
    {
        var input = config["DATABASE_URL"] ?? config.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Configure DATABASE_URL for PostgreSQL.");
        if (!input.StartsWith("postgres://") && !input.StartsWith("postgresql://")) return input;
        var uri = new Uri(input);
        var credentials = uri.UserInfo.Split(':', 2);
        var b = new NpgsqlConnectionStringBuilder {
            Host = uri.Host, Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : "",
            SslMode = SslMode.VerifyFull, Timeout = 30, CommandTimeout = 30,
            MaxPoolSize = 20, IncludeErrorDetail = false, ChannelBinding = ChannelBinding.Require
        };
        return b.ConnectionString;
    }
}

public class DesignTimePostgresDb : IDesignTimeDbContextFactory<PostgresDb>
{
    public PostgresDb CreateDbContext(string[] args) {
        var b = new DbContextOptionsBuilder<AppDb>();
        b.UseNpgsql("Host=localhost;Database=eventhub_design;Username=postgres", n => n.MigrationsHistoryTable("__EFMigrationsHistory", "eventhub"));
        return new(b.Options);
    }
}
