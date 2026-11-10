using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Api.Mqtt;

// Prevent duplicate consumers for the same result stream under this OS user.
internal static class MqttConsumerLock
{
    public static FileStream Acquire(MqttOptions options)
    {
        var streamIdentity = JsonSerializer.Serialize(new
        {
            Host = options.Host.Trim().TrimEnd('.').ToLowerInvariant(),
            options.Port,
            options.ResultTopic
        });
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(streamIdentity)));
        var directory = Path.Combine(Path.GetTempPath(), "nasduino-mqtt-consumers");
        Directory.CreateDirectory(directory);
        try
        {
            // Keep the file: the OS-held handle is the lock, not the file's existence.
            // Reopening it succeeds after normal shutdown or a process crash.
            return new FileStream(Path.Combine(directory, key + ".lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException error) when ((error.HResult & 0xFFFF) is 11 or 32 or 33)
        {
            throw new InvalidOperationException(
                $"Another MQTT backend is already handling '{options.ResultTopic}' on {options.Host}:{options.Port} "
                + "for this user. Stop the existing Game.Api before starting another one. "
                + "Use Mqtt:Enabled=false if this copy only needs HTTP diagnostics.", error);
        }
    }
}
