using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Game.Api.Data;

public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<GameDbContext>
{
    public GameDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<DesignTimeFactory>(optional: true).AddEnvironmentVariables().Build();
        var connection = config.GetConnectionString("GameDatabase")
            ?? "Host=localhost;Database=nasduino_design_only;Username=postgres";
        return new(new DbContextOptionsBuilder<GameDbContext>().UseNpgsql(connection).Options);
    }
}
