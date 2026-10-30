namespace Game.Api.Contracts;

public sealed record MqttStatusResponse(
    bool Connected,
    string Host,
    int Port,
    string ClientId,
    string InputTopic,
    string StateTopic,
    DateTimeOffset? LastConnectedAt,
    long ReceivedMessages,
    string? LastError);
