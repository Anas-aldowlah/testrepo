using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Data;

public class NeondbContextFactory : IDesignTimeDbContextFactory<NeondbContext>
{
    public NeondbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();
        var optionsBuilder = new DbContextOptionsBuilder<NeondbContext>();
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("MYDB"));
        return new NeondbContext(optionsBuilder.Options);
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }
}

public class UsersDbContextFactory : IDesignTimeDbContextFactory<UsersDbContext>
{
    public UsersDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();
        var optionsBuilder = new DbContextOptionsBuilder<UsersDbContext>();
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("User"));
        return new UsersDbContext(optionsBuilder.Options);
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }
}
