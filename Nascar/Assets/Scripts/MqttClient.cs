using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using UnityEngine;

public class MqttClient : MonoBehaviour
{
    private string _host = "localhost";
    private int _port = 1883;

    [Header("MQTT Settings")]
    public List<GameObject> Cars { get; set; } = new List<GameObject>();
    public GameObject CarPrefab;
    public Transform CarsSpawnPoint;
    public GameObject GameManager;
    private GameManager _gameManagerScript;
    
    private IMqttClient _client;
    private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

    public class NewInput
    {
        public string CarId { get; set; }
        public float Throttle { get; set; }
        public float Steering { get; set; }
    }

    private async void Start()
    {
        _gameManagerScript = GameManager.GetComponent<GameManager>();

        try
        {
            var factory = new MqttFactory();
            _client = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(_host, _port)
                .WithClientId("csharp-dashboard")
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
                            _mainThreadQueue.Enqueue(() => UpdateCarInput(input.CarId, input.Throttle, input.Steering));
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
    }
    
    private async void OnDestroy()
    {
        if (_client != null && _client.IsConnected)
            await _client.DisconnectAsync();
        _client?.Dispose();
    }

    private void AddCar(string carId)
    {
        var spawn = CarsSpawnPoint != null ? CarsSpawnPoint.position : Vector3.zero;
        var rot = CarsSpawnPoint != null ? CarsSpawnPoint.rotation : Quaternion.identity;

        var car = Instantiate(CarPrefab, spawn, rot);
        car.name = GetCarId(carId);
        Cars.Add(car);
        _gameManagerScript.UpdatePlayerList(Cars.ToArray());
    }

    private void RemoveCar(string carId)
    {
        var car = Cars.Find(c => c.name == GetCarId(carId));
        if (car != null)
        {
            Cars.Remove(car);
            Destroy(car);
        }
    }

    private void UpdateCarInput(string carId, float throttle, float steering)
    {
        var car = Cars.Find(c => c.name == GetCarId(carId));
        if (car != null)
        {
            var carScript = car.GetComponent<Car>();
            if (carScript != null)
            {
                carScript.Movement(throttle, steering);
            }
        }
    }
    
    private string GetCarId(string carId) => "car-" + carId;
}