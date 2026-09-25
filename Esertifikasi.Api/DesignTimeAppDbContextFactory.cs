using Esertifikasi.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Esertifikasi.Api;

public sealed class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext> {
  public AppDbContext CreateDbContext(string[] args) {
    var cwd = Directory.GetCurrentDirectory();
    var projectDirectory = File.Exists(Path.Combine(cwd, "appsettings.json"))
        ? cwd : Path.Combine(cwd, "Esertifikasi.Api");
    var configuration = new ConfigurationBuilder().SetBasePath(projectDirectory)
        .AddJsonFile("appsettings.json").Build();
    var connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection wajib dikonfigurasi.");
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(connectionString, postgres =>
            postgres.MigrationsHistoryTable(
                DatabaseConstants.MigrationHistoryTable,
                DatabaseConstants.Schema))
        .Options;
    return new AppDbContext(options);
  }
}
