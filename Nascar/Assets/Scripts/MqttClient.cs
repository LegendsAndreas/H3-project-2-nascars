using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using UnityEngine;

public class MqttClient : MonoBehaviour
{
    private string _host;
    private int _port;

    [Header("MQTT Settings")]
    public List<GameObject> Cars { get; set; } = new List<GameObject>();
    public GameObject CarPrefab;
    public Transform CarsSpawnPoint;

    public class NewInput
    {
        public string CarId { get; set; }
        public float Throttle { get; set; }
        public float Steering { get; set; }
    }

    public MqttClient(string host, int port = 1883)
    {
        _host = host;
        _port = port;
    }

    public async Task StartAsync()
    {
        MqttFactory factory = new MqttFactory();
        IMqttClient client = factory.CreateMqttClient();

        MqttClientOptions options = new MqttClientOptionsBuilder()
            .WithTcpServer(_host, _port)
            .WithClientId("csharp-dashboard")
            .Build();

        client.ApplicationMessageReceivedAsync += e =>
        {
            string topic = e.ApplicationMessage.Topic;
            // Car ID is sent if new car or remove car, otherwise it's a NewInput object.
            string payload = e.ApplicationMessage.ConvertPayloadToString();

            if (topic == "cars/input")
            {
                NewInput input = JsonSerializer.Deserialize<NewInput>(payload);
                if (input != null)
                {
                    UpdateCarInput(input.CarId, input.Throttle, input.Steering);
                }
            }
            else if (topic == "cars/new")
            {
                AddCar(payload);
            }
            else if (topic == "cars/remove")
            {
                RemoveCar(payload);
            }

            return Task.CompletedTask;
        };

        await client.ConnectAsync(options);
        await client.SubscribeAsync("cars/#");
    }

    public void AddCar(string carId)
    {
        var car = Instantiate(CarPrefab);
        car.name = GetCarId(carId);
        Cars.Add(car);
    }

    public void RemoveCar(string carId)
    {
        var car = Cars.Find(c => c.name == GetCarId(carId));
        if (car != null)
        {
            Cars.Remove(car);
            Destroy(car);
        }
    }

    public void UpdateCarInput(string carId, float throttle, float steering)
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