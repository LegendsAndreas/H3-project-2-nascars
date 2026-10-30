using System.Text;
using System.Text.Json;
using Game.Api.Contracts;
using Game.Api.Models;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;

namespace Game.Api.Mqtt;

public sealed class MqttService : BackgroundService
{
    private readonly MqttOptions _options;
    private readonly ILogger<MqttService> _logger;
    private readonly MqttClientFactory _factory = new();
    private readonly IMqttClient _client;
    private readonly MqttClientOptions _clientOptions;
    private readonly object _sync = new();
    private readonly string _clientId;
    private bool _subscribed;
    private DateTimeOffset? _lastConnectedAt;
    private string? _lastError;
    private long _receivedMessages;
    private MqttMessageResponse? _lastInput;
    private MqttMessageResponse? _lastState;

    public MqttService(IOptions<MqttOptions> options, ILogger<MqttService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _clientId = $"{_options.ClientIdPrefix}-{Guid.NewGuid():N}";
        _client = _factory.CreateMqttClient();

        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(_options.Host, _options.Port)
            .WithClientId(_clientId)
            .WithProtocolVersion(MqttProtocolVersion.V311)
            .WithCleanSession()
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(15));

        if (_options.UseTls)
        {
            builder.WithTlsOptions(tls => tls.UseTls());
        }

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            builder.WithCredentials(_options.Username, _options.Password);
        }

        _clientOptions = builder.Build();
        _client.ApplicationMessageReceivedAsync += HandleMessageAsync;
        _client.DisconnectedAsync += args =>
        {
            lock (_sync)
            {
                if (args.Exception is not null) { _lastError = args.Exception.Message; }
                else if (_subscribed) { _lastError = args.Reason.ToString(); }
                _subscribed = false;
            }
            return Task.CompletedTask;
        };
    }

    public MqttStatusResponse GetStatus()
    {
        lock (_sync)
        {
            return new(_subscribed && _client.IsConnected, _options.Host, _options.Port,
                _clientId, _options.InputTopic, _options.StateTopic, _lastConnectedAt,
                _receivedMessages, _lastError);
        }
    }

    public MqttMessageResponse? GetLastInput()
    {
        lock (_sync) { return _lastInput; }
    }

    public MqttMessageResponse? GetLastState()
    {
        lock (_sync) { return _lastState; }
    }

    public bool IsValidState(JsonElement state) =>
        state.ValueKind == JsonValueKind.Object
        && Encoding.UTF8.GetByteCount(state.GetRawText()) <= _options.MaxPayloadBytes;

    public async Task<bool> PublishStateAsync(JsonElement state, CancellationToken cancellationToken)
    {
        if (!GetStatus().Connected || !IsValidState(state))
        {
            return false;
        }

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(_options.StateTopic)
            .WithPayload(state.GetRawText())
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(false)
            .Build();

        try
        {
            var result = await _client.PublishAsync(message, cancellationToken);
            if ((int)result.ReasonCode < 128) { return true; }
            lock (_sync) { _lastError = $"Broker rejected publish: {result.ReasonCode}."; }
            return false;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Could not publish MQTT state.");
            lock (_sync) { _lastError = exception.Message; }
            return false;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken))
                    {
                        timeout.CancelAfter(TimeSpan.FromSeconds(10));
                        var connection = await _client.ConnectAsync(_clientOptions, timeout.Token);
                        if (connection.ResultCode != MqttClientConnectResultCode.Success)
                        {
                            throw new InvalidOperationException($"Broker rejected connection: {connection.ResultCode}.");
                        }

                        var subscriptions = _factory.CreateSubscribeOptionsBuilder()
                            .WithTopicFilter(filter => filter.WithTopic(_options.InputTopic)
                                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
                            .WithTopicFilter(filter => filter.WithTopic(_options.StateTopic)
                                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
                            .Build();
                        var result = await _client.SubscribeAsync(subscriptions, timeout.Token);
                        if (result.Items.Count != 2 || result.Items.Any(item => (int)item.ResultCode >= 128))
                        {
                            throw new InvalidOperationException("Broker rejected the MQTT subscriptions.");
                        }
                    }

                    lock (_sync)
                    {
                        _subscribed = true;
                        _lastConnectedAt = DateTimeOffset.UtcNow;
                        _lastError = null;
                    }
                    _logger.LogInformation("MQTT connected to {Host}:{Port}; listening on {InputTopic} and {StateTopic}.",
                        _options.Host, _options.Port, _options.InputTopic, _options.StateTopic);

                    while (_client.IsConnected && !stoppingToken.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    lock (_sync) { _lastError = exception.Message; }
                    _logger.LogWarning("MQTT unavailable: {Reason}. Retrying in {Delay} seconds.",
                        exception.Message, _options.ReconnectDelaySeconds);
                }
                finally
                {
                    lock (_sync) { _subscribed = false; }
                    await DisconnectAsync();
                }

                await Task.Delay(TimeSpan.FromSeconds(_options.ReconnectDelaySeconds), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown while waiting to reconnect.
        }
    }

    private Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var message = args.ApplicationMessage;
        if (message.Payload.Length > _options.MaxPayloadBytes)
        {
            _logger.LogWarning("Ignoring oversized MQTT payload on {Topic}.", message.Topic);
            return Task.CompletedTask;
        }

        try
        {
            using var document = JsonDocument.Parse(message.ConvertPayloadToString());
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                _logger.LogWarning("Ignoring MQTT payload on {Topic}: expected a JSON object.", message.Topic);
                return Task.CompletedTask;
            }

            if (message.Topic == _options.InputTopic && !Coordinates.TryRead(document.RootElement, out _))
            {
                _logger.LogWarning("Ignoring MQTT input on {Topic}: x, y and z must all be finite JSON numbers.", message.Topic);
                return Task.CompletedTask;
            }

            var received = new MqttMessageResponse(message.Topic, document.RootElement.Clone(),
                DateTimeOffset.UtcNow, message.Retain);
            lock (_sync)
            {
                if (message.Topic == _options.InputTopic) { _lastInput = received; }
                else if (message.Topic == _options.StateTopic) { _lastState = received; }
                else { return Task.CompletedTask; }
                _receivedMessages++;
            }
            _logger.LogDebug("Received MQTT JSON on {Topic}.", message.Topic);
        }
        catch (JsonException)
        {
            _logger.LogWarning("Ignoring invalid JSON on MQTT topic {Topic}.", message.Topic);
        }

        return Task.CompletedTask;
    }

    private async Task DisconnectAsync()
    {
        if (!_client.IsConnected) { return; }
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), timeout.Token);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "MQTT disconnect did not complete.");
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        _client.Dispose();
    }
}
