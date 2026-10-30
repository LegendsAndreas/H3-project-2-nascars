using System.Text.Json;

namespace Game.Api.Contracts;

public sealed record MqttMessageResponse(
    string Topic,
    JsonElement Data,
    DateTimeOffset ReceivedAt,
    bool Retained);
