using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    private bool _gameHasBegun = false;
    private bool _gameHasEnded = false;
    private Car _winnerCar = null;
    public GameObject MqttService;
    private MqttClient _mqttServiceScript;
    
    public Canvas JoinGameCanvas;
    private JoinGame _joinGameScript;
    
    void Start()
    {
        _mqttServiceScript = MqttService.GetComponent<MqttClient>();
        _joinGameScript = JoinGameCanvas.GetComponent<JoinGame>();
        tag = "GameManager";
    }

    void Update()
    {
        if (_gameHasEnded)
        {
            // Handle game end logic
        }
        
        
    }

    public void CarFinishedLap(Car car)
    {
        // Publish the lap completion to MQTT
        _mqttServiceScript.PublishLapCompletion(car.name, car.Laps);
        
        if (car.Laps >= 10)
        {
            _gameHasEnded = true;
            _winnerCar = car;
            _mqttServiceScript.PublishCarToDb(car.name, car.Laps);
            Debug.Log("Game has ended!");
        }
    }
    
    public void StartGame()
    {
        if (!_gameHasBegun)
        {
            _gameHasBegun = true;
            JoinGameCanvas.enabled = false;
            Debug.Log("Game has started!");
        }
    }
    
    public void UpdatePlayerList(GameObject[] cars)
    {
        _joinGameScript.UpdatePlayerList(cars);
    }
}
