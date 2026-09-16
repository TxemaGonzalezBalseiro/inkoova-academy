using Inkoova.Academy.Infrastructure;
using Inkoova.Academy.Jobs;

// Scheduled maintenance for the academy. Runs as a separate container so a long job can
// never hold a request thread, and so it can be restarted without touching the API.

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAcademyInfrastructure(builder.Configuration);
builder.Services.AddHostedService<DailyMaintenanceJob>();
builder.Services.AddHostedService<MonthlySettlementJob>();
builder.Services.AddHostedService<VerifactuSubmissionJob>();

var host = builder.Build();
await host.RunAsync();
