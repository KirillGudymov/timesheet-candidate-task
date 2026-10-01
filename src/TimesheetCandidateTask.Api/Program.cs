using Microsoft.EntityFrameworkCore;
using TimesheetCandidateTask.Api.Application;
using TimesheetCandidateTask.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<TimesheetDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("TimesheetDatabase")));
builder.Services.AddScoped<TimesheetService>();
builder.Services.AddControllers(options => options.Filters.Add<TimesheetExceptionFilter>());

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TimesheetDbContext>();
    db.Database.EnsureCreated();
    TimesheetSeeder.Seed(db);
}

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.Run();

public partial class Program
{
}
