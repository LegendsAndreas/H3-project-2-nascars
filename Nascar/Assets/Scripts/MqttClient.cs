using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class MqttClient : MonoBehaviour
{
    public string MqttHost;
    public int MqttPort;
    public string MqttUri;
    public string MqttUsername;
    public string MqttPassword;

    [Header("MQTT Settings")]
    public List<GameObject> Cars { get; set; } = new List<GameObject>();
    public GameObject CarPrefab;
    public GameObject GameManager;
    private GameManager _gameManagerScript;
    public List<SpawnPoint> CarSpawnPoints;
    
    [Header("Car Settings")]
    public List<CarSprite> CarSprites = new List<CarSprite>();
    
    private IMqttClient _client;
    private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();
    private readonly ConcurrentDictionary<string, (float throttle, float steering)> _latestInputs = new();

    [Header("Backend feedback (optional)")]
    public TMP_Text ResultStatusText;
    public TMP_Text LeaderboardText;
    private bool _subscribed;
    private string _pendingResultName;
    private int _pendingResultLaps;
    private float _resultReplyDeadline;
    private string _leaderboardRequestId;
    private float _leaderboardReplyDeadline;
    
    private List<DisplayName> _carDisplayNames = new List<DisplayName>()
    {
        new DisplayName { Name = "Benny the Grapist", IsFree = true },
        new DisplayName { Name = "Boss Man", IsFree = true },
        new DisplayName { Name = "Jimmy the Ringer", IsFree = true },
        new DisplayName { Name = "Willy the Free", IsFree = true },
        new DisplayName { Name = "William the Rigger", IsFree = true },
        new DisplayName { Name = "Anton the Ripper", IsFree = true },
        new DisplayName { Name = "Eggs Benedict", IsFree = true },
        new DisplayName { Name = "Eggs Scrambled", IsFree = true },
        new DisplayName { Name = "Scrambled Eggs & Benedict Eggs", IsFree = true },
        new DisplayName { Name = "Eggs Benedict & Scrambled Eggs", IsFree = true },
        new DisplayName { Name = "The Wife", IsFree = true },
        new DisplayName { Name = "The Kids", IsFree = true },
        new DisplayName { Name = "The Dog", IsFree = true },
        new DisplayName { Name = "Givin' lips George", IsFree = true },
        new DisplayName { Name = "My Dad", IsFree = true },
        new DisplayName { Name = "Idunno", IsFree = true },
        new DisplayName { Name = "The step Dad", IsFree = true },
        new DisplayName { Name = "Uncle Tommy", IsFree = true },
        new DisplayName { Name = "\"\"", IsFree = true },
        new DisplayName { Name = "Cousin Vinny", IsFree = true },
    };

    public class NewInput
    {
        [JsonPropertyName("carId")]
        public string CarId { get; set; }
        [JsonPropertyName("throttle")]
        public float Throttle { get; set; }
        [JsonPropertyName("steering")]
        public float Steering { get; set; }
    }
    
    [Serializable]
    public class SpawnPoint
    {
        public Transform Transform;
        public bool IsFree = true;
    }

    public class DisplayName
    {
        public string Name { get; set; }
        public bool IsFree { get; set; }
    }
    
    [Serializable]
    public class CarSprite
    {
        public bool IsFree = true;
        public Sprite Sprite;
    }

    private async void Start()
    {
        _gameManagerScript = GameManager.GetComponentInChildren<GameManager>();

        try
        {
            var factory = new MqttFactory();
            _client = factory.CreateMqttClient();
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(MqttHost, MqttPort)
                .WithClientId($"unity-{Guid.NewGuid():N}")
                .Build();

            // wss://c613c7e6.ala.eu-central-1.emqxsl.com:8084/mqtt
            /*var options = new MqttClientOptionsBuilder()
                .WithWebSocketServer(o => o.WithUri(MqttUri))
                .WithCredentials(MqttUsername, MqttPassword)
                .WithClientId("unity-test-01")
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(15))
                .WithTlsOptions(o => o.UseTls())
                .Build();*/

            _client.ApplicationMessageReceivedAsync += e =>
            {
                string topic = e.ApplicationMessage.Topic;
                string payload = e.ApplicationMessage.ConvertPayloadToString();

                // Runs on a background thread: only parse here, defer Unity work.
                switch (topic)
                {
                    case "cars/input":
                        var input = JsonSerializer.Deserialize<NewInput>(payload);
                        // Debug.Log($"Received input: {input.CarId}, Throttle: {input.Throttle}, Steering: {input.Steering}");
                        _latestInputs[input.CarId] = (input.Throttle, input.Steering);
                        break;
                    case "cars/new":
                        Debug.Log($"Received new car request: {payload}");
                        _mainThreadQueue.Enqueue(() => AddCar(payload));
                        break;
                    case "cars/remove":
                        Debug.Log($"Received remove car request: {payload}");
                        _mainThreadQueue.Enqueue(() => RemoveCar(payload));
                        break;
                    case "cars/db/ack":
                    case "cars/leaderboard":
                        QueueBackendReply(topic, payload);
                        break;
                    case "cars/leaderboard/get":
                        break;
                    default:
                        Debug.LogWarning($"Unknown topic: {topic}");
                        break;
                }

                return Task.CompletedTask;
            };

            await _client.ConnectAsync(options);
            await _client.SubscribeAsync("cars/#");
            _subscribed = true;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }
    
    private void Update()
    {
        while (_mainThreadQueue.TryDequeue(out var action))
        {
            try { action(); }
            catch (Exception ex) { Debug.LogException(ex); }
        }
        if (_resultReplyDeadline > 0 && Time.realtimeSinceStartup >= _resultReplyDeadline)
        {
            _resultReplyDeadline = 0;
            SetResultStatus("Save confirmation not received. Check the backend before resending.");
        }
        if (_leaderboardReplyDeadline > 0 && Time.realtimeSinceStartup >= _leaderboardReplyDeadline)
        {
            _leaderboardReplyDeadline = 0;
            SetLeaderboardText("Leaderboard response not received.");
        }
        if (!_gameManagerScript.GameHasBegun || _gameManagerScript.GameHasEnded) return;
        
        // apply only the latest input per car, once per frame
        foreach (var kvp in _latestInputs)
            UpdateCarInput(kvp.Key, kvp.Value.throttle, kvp.Value.steering);
    }
    
    private async void OnDestroy()
    {
        _subscribed = false;
        if (_client != null && _client.IsConnected)
            await _client.DisconnectAsync();
        _client?.Dispose();
    }

    private void AddCar(string carId)
    {
        Vector3 spawn = Vector3.zero;
        Quaternion rot = Quaternion.identity;

        var sp = CarSpawnPoints.Find(s => s.IsFree && s.Transform != null);
        if (sp != null)
        {
            spawn = sp.Transform.position;
            rot = sp.Transform.rotation;
            sp.IsFree = false;
        }
        else
        {
            Debug.LogWarning("No spawn point found for new car.");
        }
        
        CarSprite carSprite = CarSprites.Find(s => s.IsFree);
        if (carSprite == null)
        {
            Debug.LogWarning("No free car sprite found for new car.");
            return;
        }

        var car = Instantiate(CarPrefab, spawn, rot);
        car.name = GetCarIdWithPrefix(carId);
        Car carScript = car.GetComponentInChildren<Car>();
        carScript.name = GetCarIdWithPrefix(carId);
        carScript.SetSprite(carSprite.Sprite);
        carSprite.IsFree = false;
        
        // randomly chosen display name from the list of free names
        DisplayName displayName = _carDisplayNames
            .Where(dn => dn.IsFree)
            .OrderBy(_ => Random.value)
            .FirstOrDefault()!;
        car.GetComponentInChildren<TextMeshPro>().text = displayName.Name;
        
        carScript.DisplayName = displayName.Name;
        
        PublishCarName(carId, displayName.Name);
        displayName.IsFree = false;
        
        Cars.Add(car);
        _gameManagerScript.UpdatePlayerList(Cars.ToArray());
        _gameManagerScript.PlayCarStartUpSound();
    }

    private void RemoveCar(string carId)
    {
        var car = Cars.Find(c => c.name == GetCarIdWithPrefix(carId));
        if (car != null)
        {
            var spawnPoint = CarSpawnPoints.First(sp => !sp.IsFree);
            spawnPoint.IsFree = true;
            
            var carSprite = CarSprites.First(cs => !cs.IsFree);
            carSprite.IsFree = true;
            
            Cars.Remove(car);

            _gameManagerScript.UpdatePlayerList(Cars.ToArray());
            Destroy(car);
        }
        else
        {
            Debug.LogWarning($"Car with ID {carId} not found for removal.");
        }
    }

    private void UpdateCarInput(string carId, float throttle, float steering)
    {
        // Debug.Log($"Received input for car {carId}: throttle={throttle}, steering={steering}");
        var car = Cars.Find(c => c.name == GetCarIdWithPrefix(carId));
        if (car != null)
        {
            var carScript = car.GetComponentInChildren<Car>();
            if (carScript != null)
            {
                carScript.Movement(throttle, steering);
            }
        }
        else
        {
            Debug.LogWarning($"Car with ID {carId} not found for input update.");
        }
    }
    
    public async void PublishLapCompletion(string carName, int lapCount)
    {
        if (_client != null && _client.IsConnected)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic("cars/laps")
                .WithPayload(JsonSerializer.Serialize(new { carId = carName.Split('-')[1], content = lapCount }))
                .Build();

            await _client.PublishAsync(message);
        }
        else
        {
            Debug.LogWarning("MQTT client is not connected. Cannot publish lap completion.");
        }
    }

    private async void PublishCarName(string carId, string username)
    {
        if (_client != null && _client.IsConnected)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic("cars/name")
                .WithPayload(JsonSerializer.Serialize(new { carId, content = username }))
                .Build();

            await _client.PublishAsync(message);
        }
        else
        {
            Debug.LogWarning("MQTT client is not connected. Cannot publish car name.");
        }
    }

    public async void PublishCarToDb(string carDisplayName, int laps)
    {
        if (_client == null || !_client.IsConnected || !_subscribed)
        {
            SetResultStatus("MQTT is not ready. Result was not sent.");
            return;
        }
        if (_pendingResultName != null) return;
        _pendingResultName = carDisplayName;
        _pendingResultLaps = laps;
        _resultReplyDeadline = Time.realtimeSinceStartup + 20f;
        SetResultStatus("Saving result...");
        try
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic("cars/db")
                .WithPayload(JsonSerializer.Serialize(new { carName = carDisplayName, laps }))
                .WithRetainFlag(false)
                .Build();
            await _client.PublishAsync(message);
        }
        catch (Exception ex)
        {
            _resultReplyDeadline = 0;
            SetResultStatus("Save not confirmed. Check the backend before resending.");
            Debug.LogWarning(ex.Message);
        }
    }

    // Can be connected to a UI Button's OnClick without changing the scene in code.
    public async void RequestLeaderboard()
    {
        if (_client == null || !_client.IsConnected || !_subscribed)
        {
            SetLeaderboardText("MQTT is not ready.");
            return;
        }
        var requestId = Guid.NewGuid().ToString("N");
        _leaderboardRequestId = requestId;
        _leaderboardReplyDeadline = Time.realtimeSinceStartup + 20f;
        SetLeaderboardText("Loading leaderboard...");
        try
        {
            await _client.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic("cars/leaderboard/get")
                .WithPayload(JsonSerializer.Serialize(new { requestId, trackId = "main", limit = 10 }))
                .WithRetainFlag(false).Build());
        }
        catch (Exception ex)
        {
            if (_leaderboardRequestId != requestId) return;
            _leaderboardReplyDeadline = 0;
            SetLeaderboardText("Could not request leaderboard.");
            Debug.LogWarning(ex.Message);
        }
    }

    private void QueueBackendReply(string topic, string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var reply = document.RootElement.Clone();
            _mainThreadQueue.Enqueue(() => HandleBackendReply(topic, reply));
        }
        catch (JsonException)
        {
            _mainThreadQueue.Enqueue(() => Debug.LogWarning("Ignored invalid backend JSON on " + topic));
        }
    }

    private void HandleBackendReply(string topic, JsonElement reply)
    {
        if (reply.ValueKind != JsonValueKind.Object || !reply.TryGetProperty("success", out var success)
            || (success.ValueKind != JsonValueKind.True && success.ValueKind != JsonValueKind.False)) return;
        if (topic == "cars/db/ack")
        {
            // The legacy result format has no request ID: only match a locally pending name/lap pair.
            if (_pendingResultName == null || ReplyString(reply, "carName") != _pendingResultName) return;
            if (success.GetBoolean())
            {
                if (!reply.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object
                    || !result.TryGetProperty("laps", out var laps) || laps.ValueKind != JsonValueKind.Number
                    || !laps.TryGetInt32(out var lapCount)
                    || lapCount != _pendingResultLaps) return;
                _pendingResultName = null;
                _resultReplyDeadline = 0;
                SetResultStatus("Result saved.");
                RequestLeaderboard();
            }
            else
            {
                _resultReplyDeadline = 0;
                SetResultStatus("Result not saved: " + ReplyString(reply, "error"));
            }
            return;
        }
        if (_leaderboardRequestId == null || ReplyString(reply, "requestId") != _leaderboardRequestId) return;
        _leaderboardReplyDeadline = 0;
        if (!success.GetBoolean())
        {
            SetLeaderboardText("Leaderboard error: " + ReplyString(reply, "error"));
            return;
        }
        if (!reply.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array) return;
        _leaderboardRequestId = null;
        var text = new StringBuilder();
        var place = 1;
        foreach (var record in records.EnumerateArray())
        {
            if (record.ValueKind != JsonValueKind.Object || !record.TryGetProperty("score", out var score)
                || score.ValueKind != JsonValueKind.Number || !score.TryGetInt32(out var points)
                || !record.TryGetProperty("laps", out var laps) || laps.ValueKind != JsonValueKind.Number
                || !laps.TryGetInt32(out var lapCount)) continue;
            text.AppendLine($"{place++}. {ReplyString(record, "displayName")} - {points} points ({lapCount} laps)");
        }
        SetLeaderboardText(text.Length == 0 ? "No results yet." : text.ToString());
    }

    private static string ReplyString(JsonElement reply, string property) =>
        reply.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : "";

    private void SetResultStatus(string text)
    {
        if (ResultStatusText != null) ResultStatusText.text = text;
        Debug.Log(text);
    }

    private void SetLeaderboardText(string text)
    {
        if (LeaderboardText != null) LeaderboardText.text = text;
        Debug.Log(text);
    }
    
    private string GetCarIdWithPrefix(string carId) => "car-" + carId;
}
