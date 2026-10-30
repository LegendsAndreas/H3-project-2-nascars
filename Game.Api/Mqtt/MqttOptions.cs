using System.ComponentModel.DataAnnotations;

namespace Game.Api.Mqtt;

public sealed class MqttOptions
{
    public const string SectionName = "Mqtt";

    [Required]
    public string Host { get; set; } = "127.0.0.1";

    [Range(1, 65535)]
    public int Port { get; set; } = 1883;

    public bool UseTls { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }

    [Required, StringLength(64)]
    public string ClientIdPrefix { get; set; } = "game-api";

    [Required, StringLength(512)]
    public string InputTopic { get; set; } = "game/input";

    [Required, StringLength(512)]
    public string StateTopic { get; set; } = "game/state";

    [Range(1, 60)]
    public int ReconnectDelaySeconds { get; set; } = 3;

    [Range(64, 65536)]
    public int MaxPayloadBytes { get; set; } = 4096;

    public static bool IsTopicName(string? topic) =>
        !string.IsNullOrWhiteSpace(topic) && topic.IndexOfAny(['#', '+', '\0']) < 0;
}
