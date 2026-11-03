using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using UnityEngine;

public class MqttClient : MonoBehaviour
{
    public string MqttHost;
    public int MqttPort;
    public string MqttUsername;
    public string MqttPassword;

    [Header("MQTT Settings")]
    public List<GameObject> Cars { get; set; } = new List<GameObject>();
    public GameObject CarPrefab;
    public GameObject GameManager;
    private GameManager _gameManagerScript;
    public List<SpawnPoint> CarSpawnPoints;
    
    private IMqttClient _client;
    private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();
    private readonly ConcurrentDictionary<string, (float throttle, float steering)> _latestInputs = new();

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

    private async void Start()
    {
        _gameManagerScript = GameManager.GetComponent<GameManager>();

        try
        {
            var factory = new MqttFactory();
            _client = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithWebSocketServer(o => o.WithUri("wss://c613c7e6.ala.eu-central-1.emqxsl.com:8084/mqtt"))
                .WithCredentials(MqttUsername, MqttPassword)
                .WithClientId("unity-test-01")
                .WithTlsOptions(o => o.UseTls())
                .Build();

            _client.ApplicationMessageReceivedAsync += e =>
            {
                string topic = e.ApplicationMessage.Topic;
                string payload = e.ApplicationMessage.ConvertPayloadToString();

                // Runs on a background thread: only parse here, defer Unity work.
                switch (topic)
                {
                    case "cars/input":
                        var input = JsonSerializer.Deserialize<NewInput>(payload);
                        if (input != null)
                            _latestInputs[input.CarId] = (input.Throttle, input.Steering);
                        break;
                    case "cars/new":
                        _mainThreadQueue.Enqueue(() => AddCar(payload));
                        break;
                    case "cars/remove":
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

        var car = Instantiate(CarPrefab, spawn, rot);
        car.name = GetCarId(carId);
        Cars.Add(car);
        _gameManagerScript.UpdatePlayerList(Cars.ToArray());
    }

    private void RemoveCar(string carId)
    {
        var car = Cars.Find(c => c.name == GetCarId(carId));
        if (car is not null)
        {
            Cars.Remove(car);
            Destroy(car);
        }
    }

    private void UpdateCarInput(string carId, float throttle, float steering)
    {
        // Debug.Log($"Received input for car {carId}: throttle={throttle}, steering={steering}");
        var car = Cars.Find(c => c.name == GetCarId(carId));
        if (car is not null)
        {
            var carScript = car.GetComponent<Car>();
            if (carScript is not null)
            {
                carScript.Movement(throttle, steering);
            }
        }
    }
    
    private string GetCarId(string carId) => "car-" + carId;
}