using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Game.Api.Contracts;
using Game.Api.Racing;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;

namespace Game.Api.Mqtt;

public sealed class MqttService : BackgroundService
{
    private const int MaxPayloadBytes = 16384;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.Strict
    };
    private readonly MqttOptions _options;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<MqttService> _logger;
    private readonly IMqttClient _client = new MqttClientFactory().CreateMqttClient();
    private readonly MqttClientOptions _connection;
    private readonly string _clientId;
    private readonly Channel<Incoming> _messages = Channel.CreateBounded<Incoming>(
        new BoundedChannelOptions(128) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly object _sync = new();
    private bool _subscribed;
    private string? _connectionError;
    private string? _messageError;
    private DateTimeOffset? _lastConnectedAt;
    private DateTimeOffset? _lastSavedAt;
    private long _savedReports;
    private long _rejectedMessages;

    public MqttService(IOptions<MqttOptions> options, IServiceScopeFactory scopes, ILogger<MqttService> logger)
    {
        _options = options.Value;
        _scopes = scopes;
        _logger = logger;
        _clientId = $"{_options.ClientIdPrefix}-{Guid.NewGuid():N}";
        var connection = new MqttClientOptionsBuilder()
            .WithTcpServer(_options.Host, _options.Port)
            .WithClientId(_clientId)
            .WithProtocolVersion(MqttProtocolVersion.V311)
            .WithCleanSession()
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(20));
        if (_options.UseTls) connection.WithTlsOptions(tls => tls.UseTls());
        if (!string.IsNullOrWhiteSpace(_options.Username))
            connection.WithCredentials(_options.Username, _options.Password);
        _connection = connection.Build();
        _client.ApplicationMessageReceivedAsync += ReceiveAsync;
        _client.DisconnectedAsync += args =>
        {
            lock (_sync)
            {
                _subscribed = false;
                _connectionError = args.Exception?.Message ?? args.Reason.ToString();
            }
            return Task.CompletedTask;
        };
    }

    public object GetStatus()
    {
        lock (_sync) return new
        {
            enabled = _options.Enabled, connected = _subscribed && _client.IsConnected,
            host = _options.Host, port = _options.Port, clientId = _clientId,
            resultTopic = _options.ResultTopic, resultReplyTopic = _options.ResultReplyTopic,
            leaderboardRequestTopic = _options.LeaderboardRequestTopic,
            leaderboardReplyTopic = _options.LeaderboardReplyTopic,
            lastConnectedAt = _lastConnectedAt, lastSavedAt = _lastSavedAt,
            savedReports = _savedReports, rejectedMessages = _rejectedMessages,
            lastConnectionError = _connectionError, lastMessageError = _messageError
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled) return;
        var processing = ProcessAsync(stoppingToken);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                bool ready;
                lock (_sync) ready = _subscribed && _client.IsConnected;
                if (!ready)
                {
                    try
                    {
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        timeout.CancelAfter(TimeSpan.FromSeconds(10));
                        if (_client.IsConnected) await _client.DisconnectAsync(cancellationToken: timeout.Token);
                        await _client.ConnectAsync(_connection, timeout.Token);
                        var subscriptions = new MqttClientSubscribeOptionsBuilder()
                            .WithTopicFilter(f => f.WithTopic(_options.ResultTopic).WithAtLeastOnceQoS())
                            .WithTopicFilter(f => f.WithTopic(_options.LeaderboardRequestTopic).WithAtLeastOnceQoS())
                            .Build();
                        var response = await _client.SubscribeAsync(subscriptions, timeout.Token);
                        if (response.Items.Any(item => (int)item.ResultCode >= 128))
                            throw new InvalidOperationException("Broker rejected an MQTT subscription.");
                        lock (_sync)
                        {
                            _subscribed = true;
                            _connectionError = null;
                            _lastConnectedAt = DateTimeOffset.UtcNow;
                        }
                        _logger.LogInformation("MQTT connected to {Host}:{Port}; listening on {Results} and {Leaderboard}.",
                            _options.Host, _options.Port, _options.ResultTopic, _options.LeaderboardRequestTopic);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                    catch (Exception error)
                    {
                        lock (_sync) { _subscribed = false; _connectionError = error.Message; }
                        _logger.LogWarning("MQTT connection failed: {Error}. Retrying in 3 seconds.", error.Message);
                    }
                }
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            _messages.Writer.TryComplete();
            try { await processing; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            if (_client.IsConnected)
            {
                try
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await _client.DisconnectAsync(cancellationToken: timeout.Token);
                }
                catch (Exception error) { _logger.LogDebug(error, "MQTT shutdown disconnect failed."); }
            }
        }
    }

    private Task ReceiveAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var message = args.ApplicationMessage;
        if (message.Topic != _options.ResultTopic && message.Topic != _options.LeaderboardRequestTopic)
            return Task.CompletedTask;
        // Do not replay old retained results after every restart/reconnect.
        if (message.Retain || message.Payload.Length == 0 || message.Payload.Length > MaxPayloadBytes
            || !_messages.Writer.TryWrite(new Incoming(message.Topic, message.Payload.ToArray())))
        {
            lock (_sync) { _rejectedMessages++; _messageError = "Retained, oversized, empty message or full receive queue."; }
            _logger.LogWarning("Ignored MQTT message on {Topic}: retained/invalid size/full queue.", message.Topic);
        }
        return Task.CompletedTask;
    }

    private async Task ProcessAsync(CancellationToken token)
    {
        await foreach (var message in _messages.Reader.ReadAllAsync(token))
        {
            var isResult = message.Topic == _options.ResultTopic;
            var replyTopic = isResult ? _options.ResultReplyTopic : _options.LeaderboardReplyTopic;
            string? requestId = null;
            string? carName = null;
            object reply;
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var game = scope.ServiceProvider.GetRequiredService<GameService>();
                if (isResult)
                {
                    var request = JsonSerializer.Deserialize<MqttResultRequest>(message.Payload, Json)
                        ?? throw new GameRequestException(400, "Expected a JSON object with carName and laps.");
                    carName = request.CarName;
                    var result = await game.RecordUnityReportAsync(request, _options.TrackId, _options.TargetLaps, token);
                    lock (_sync) { _savedReports++; _lastSavedAt = DateTimeOffset.UtcNow; _messageError = null; }
                    reply = new { success = true, carName, result };
                    _logger.LogInformation("Saved Unity report: {CarName}, laps {Laps}, session {SessionId}.",
                        result.DisplayName, result.Laps, result.SessionId);
                }
                else
                {
                    var request = JsonSerializer.Deserialize<MqttLeaderboardRequest>(message.Payload, Json)
                        ?? throw new GameRequestException(400, "Expected a leaderboard query JSON object.");
                    requestId = request.RequestId;
                    GameService.Validate(request);
                    var records = await game.LeaderboardAsync(request.TrackId, request.Limit, token);
                    reply = new { success = true, requestId, trackId = request.TrackId.Trim(), records };
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception error)
            {
                var status = error switch
                {
                    GameRequestException expected => expected.StatusCode,
                    JsonException => 400,
                    _ => 503
                };
                var description = error switch
                {
                    GameRequestException => error.Message,
                    JsonException => "Invalid JSON. Send carName as a string and laps as a number.",
                    _ => "Could not process the request. Check the backend database connection."
                };
                lock (_sync) { _rejectedMessages++; _messageError = description; }
                _logger.LogWarning(error, "MQTT message on {Topic} was not processed.", message.Topic);
                reply = new { success = false, status, error = description, requestId, carName };
            }
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                var result = await _client.PublishAsync(new MqttApplicationMessageBuilder()
                    .WithTopic(replyTopic).WithPayload(JsonSerializer.Serialize(reply, Json))
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithRetainFlag(false).Build(), timeout.Token);
                if ((int)result.ReasonCode >= 128) throw new InvalidOperationException("Broker rejected the response.");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception error)
            {
                // A saved result is not undone if the reply cannot be delivered.
                _logger.LogWarning("MQTT response on {Topic} could not be delivered: {Error}.", replyTopic, error.Message);
            }
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        _client.Dispose();
    }

    private sealed record Incoming(string Topic, byte[] Payload);
}
