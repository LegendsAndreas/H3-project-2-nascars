using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
                .WithClientId("csharp-dashboard")
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
                    default:
                        Debug.LogWarning($"Unknown topic: {topic}");
                        break;
                }

                return Task.CompletedTask;
            };

            await _client.ConnectAsync(options);
            await _client.SubscribeAsync("cars/#");
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
        // apply only the latest input per car, once per frame
        if (!_gameManagerScript.GameHasBegun && !_gameManagerScript.GameHasEnded) return;
        
        foreach (var kvp in _latestInputs)
            UpdateCarInput(kvp.Key, kvp.Value.throttle, kvp.Value.steering);
    }
    
    private async void OnDestroy()
    {
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
            
            _gameManagerScript.UpdatePlayerList(Cars.ToArray());

            Cars.Remove(car);
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
    
    public void PublishLapCompletion(string carName, int lapCount)
    {
        if (_client != null && _client.IsConnected)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic("cars/laps")
                .WithPayload(JsonSerializer.Serialize(new { carId = carName.Split('-')[1], content = lapCount }))
                .Build();

            _client.PublishAsync(message);
        }
        else
        {
            Debug.LogWarning("MQTT client is not connected. Cannot publish lap completion.");
        }
    }

    private void PublishCarName(string carId, string username)
    {
        if (_client != null && _client.IsConnected)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic("cars/name")
                .WithPayload(JsonSerializer.Serialize(new { carId, content = username }))
                .Build();

            _client.PublishAsync(message);
        }
        else
        {
            Debug.LogWarning("MQTT client is not connected. Cannot publish car name.");
        }
    }

    public void PublishCarToDb(string carDisplayName, int laps)
    {
        if (_client != null && _client.IsConnected)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic("cars/db")
                .WithPayload(JsonSerializer.Serialize(new { carName = carDisplayName, laps }))
                .Build();

            _client.PublishAsync(message);
        }
        else
        {
            Debug.LogWarning("MQTT client is not connected. Cannot publish car to database.");
        }
    }
    
    private string GetCarIdWithPrefix(string carId) => "car-" + carId;
}