using Game.Api.Mqtt;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddOptions<MqttOptions>()
    .BindConfiguration(MqttOptions.SectionName)
    .ValidateDataAnnotations()
    .Validate(options => MqttOptions.IsTopicName(options.InputTopic)
        && MqttOptions.IsTopicName(options.StateTopic), "MQTT topics must be exact topic names without wildcards.")
    .Validate(options => options.InputTopic != options.StateTopic, "Input and state topics must be different.")
    .ValidateOnStart();
builder.Services.AddSingleton<MqttService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<MqttService>());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHttpsRedirection();
}

app.MapControllers();

app.Run();
