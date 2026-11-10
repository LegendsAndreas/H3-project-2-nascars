using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public bool GameHasBegun = false;
    public bool GameHasEnded = false;
    private Car _winnerCar = null;
    public GameObject MqttService;
    private MqttClient _mqttServiceScript;

    public Canvas JoinGameCanvas;
    private JoinGame _joinGameScript;
    public int MaxLaps = 10;
    public Canvas EndScreenCanvas;
    private EndScreen _endScreenScript;
    
    public SoundManager SoundManager;

    void Start()
    {
        _mqttServiceScript = MqttService.GetComponent<MqttClient>();
        _joinGameScript = JoinGameCanvas.GetComponent<JoinGame>();
        _endScreenScript = EndScreenCanvas.GetComponent<EndScreen>();
        EndScreenCanvas.gameObject.SetActive(false);
        tag = "GameManager";
    }

    void Update()
    {
        if (GameHasEnded)
        {
            // Handle game end logic
        }
    }

    public void CarFinishedLap(Car car)
    {
        // Publish the lap completion to MQTT
        _mqttServiceScript.PublishLapCompletion(car.name, car.Laps);

        if (car.Laps >= MaxLaps)
        {
            GameHasEnded = true;
            _winnerCar = car;
            _mqttServiceScript.PublishCarToDb(car.DisplayName, car.Laps);
            _endScreenScript.EndGame(car);
            Debug.Log("Game has ended!");
        }
    }

    public void StartGame()
    {
        if (!GameHasBegun)
        {
            GameHasBegun = true;
            JoinGameCanvas.enabled = false;
            Debug.Log("Game has started!");
        }
    }

    public void UpdatePlayerList(GameObject[] cars)
    {
        _joinGameScript.UpdatePlayerList(cars);
    }

    public void RestartGame()
    {
        GameHasBegun = false;
        GameHasEnded = false;
        _winnerCar = null;
        JoinGameCanvas.enabled = true;
        EndScreenCanvas.gameObject.SetActive(false);

        foreach (MqttClient.SpawnPoint carSpawnPoint in _mqttServiceScript.CarSpawnPoints)
        {
            carSpawnPoint.IsFree = true;
        }

        // Get all cars with the tag "Car" and reset their properties
        GameObject[] cars = GameObject.FindGameObjectsWithTag("Car");

        foreach (GameObject car in cars)
        {
            Car carScript = car.GetComponent<Car>();
            carScript.ResetCar();
            
            var sp = _mqttServiceScript.CarSpawnPoints.Find(s => s.IsFree && s.Transform != null);
            if (sp != null)
            {
                car.transform.position = sp.Transform.position;
                car.transform.rotation = sp.Transform.rotation;
                sp.IsFree = false;
            }
        }

        Debug.Log("Game has been restarted!");
    }
    
    public void PlayCarStartUpSound() => SoundManager.PlayCarStartUpSound();
}