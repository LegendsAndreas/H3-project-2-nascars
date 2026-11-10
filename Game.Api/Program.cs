using Game.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Game.Api.Mqtt;
using Game.Api.Racing;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddScoped<GameService>();
builder.Services.AddOptions<MqttOptions>().Bind(builder.Configuration.GetSection("Mqtt"))
    .ValidateDataAnnotations()
    .Validate(options => options.HasValidTopics(), "MQTT topics must be distinct, nonempty, and have no wildcards.")
    .ValidateOnStart();
builder.Services.AddSingleton<MqttService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<MqttService>());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new()
{
    Title = "Nasduino Game API",
    Version = "v1",
    Description = "Game sessions, final player results and leaderboard records."
}));
builder.Services.AddProblemDetails();
var gameOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("UnityWebGL", policy =>
    policy.WithOrigins(gameOrigins)
        .WithMethods("GET", "POST", "PUT")
        .WithHeaders("Content-Type")));
var connection = builder.Configuration.GetConnectionString("GameDatabase");
builder.Services.AddDbContext<GameDbContext>(options => options.UseNpgsql(
    string.IsNullOrWhiteSpace(connection)
        ? "Host=127.0.0.1;Port=5432;Database=nasduino_unconfigured;Username=postgres;Timeout=3"
        : connection));
var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Nasduino Game API v1"));
}
app.UseExceptionHandler(error => error.Run(async context =>
{
    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var unavailable = string.IsNullOrWhiteSpace(connection) || exception is NpgsqlException or TimeoutException
        || exception?.InnerException is NpgsqlException;
    await Results.Problem(statusCode: exception is GameRequestException expected ? expected.StatusCode : unavailable ? 503 : 500,
        title: exception is GameRequestException ? exception.Message
            : unavailable ? "Database unavailable. Configure ConnectionStrings:GameDatabase and apply migrations." : "Request failed.")
        .ExecuteAsync(context);
}));
app.UseRouting();
app.UseCors("UnityWebGL");
app.MapGet("/api/mqtt/status", (MqttService mqtt) => Results.Ok(mqtt.GetStatus()));
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "Game.Api" }));
app.MapGet("/health/database", async (GameDbContext db, CancellationToken token) =>
{
    if (string.IsNullOrWhiteSpace(connection)) return Results.Problem(statusCode: 503, title: "Database connection is not configured.");
    try
    {
        if (!await db.Database.CanConnectAsync(token)) return Results.Problem(statusCode: 503, title: "Database unreachable.");
        return (await db.Database.GetPendingMigrationsAsync(token)).Any()
            ? Results.Problem(statusCode: 503, title: "Database migrations are pending.")
            : Results.Ok(new { status = "ok", database = "PostgreSQL" });
    }
    catch (NpgsqlException) { return Results.Problem(statusCode: 503, title: "Database unreachable."); }
});
app.MapControllers();
var mqttOptions = app.Services.GetRequiredService<IOptions<MqttOptions>>().Value;
// Acquire before hosted services connect; Run disposes the host before releasing this handle.
using var mqttConsumerLock = mqttOptions.Enabled ? MqttConsumerLock.Acquire(mqttOptions) : null;
app.Run();
