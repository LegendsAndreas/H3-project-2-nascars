using System.ComponentModel.DataAnnotations;

namespace Game.Api.Mqtt;

public sealed class MqttOptions
{
    public bool Enabled { get; init; } = true;
    [Required] public string Host { get; init; } = "138.199.155.78";
    [Range(1, 65535)] public int Port { get; init; } = 1883;
    public bool UseTls { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    [Required, StringLength(40)] public string ClientIdPrefix { get; init; } = "nasduino-db";
    [Required] public string ResultTopic { get; init; } = "cars/db";
    [Required] public string ResultReplyTopic { get; init; } = "cars/db/ack";
    [Required] public string LeaderboardRequestTopic { get; init; } = "cars/leaderboard/get";
    [Required] public string LeaderboardReplyTopic { get; init; } = "cars/leaderboard";
    [Required, StringLength(100)] public string TrackId { get; init; } = "main";
    [Range(1, 10000)] public int TargetLaps { get; init; } = 10;

    public string[] Topics => [ResultTopic, ResultReplyTopic, LeaderboardRequestTopic, LeaderboardReplyTopic];

    public bool HasValidTopics() => Topics.Distinct(StringComparer.Ordinal).Count() == Topics.Length
        && Topics.All(topic => !string.IsNullOrWhiteSpace(topic)
            && topic.Length <= 256 && topic.IndexOfAny(['+', '#', '\0']) < 0);
}
