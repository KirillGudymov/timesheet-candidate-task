using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimesheetCandidateTask.Api.Infrastructure;

namespace TimesheetCandidateTask.Tests;
public sealed class TimesheetApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"timesheet-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.Single(item => item.ServiceType == typeof(DbContextOptions<TimesheetDbContext>));
            services.Remove(descriptor);
            services.AddDbContext<TimesheetDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        SqliteConnection.ClearAllPools();
        try
        {
            File.Delete(_databasePath);
        }
        catch (IOException)
        {
        }
    }
}
