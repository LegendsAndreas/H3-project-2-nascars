using System;
using UnityEngine;

public class FinishLine : MonoBehaviour
{
    private GameManager _gameManagerScript;
    void Start()
    {
        _gameManagerScript = GameObject.FindGameObjectWithTag("GameManager").GetComponent<GameManager>();
        if (_gameManagerScript == null)
        {
            throw new Exception("GameManager script not found on GameManager object.");
        }
    }

    void Update()
    {
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Car"))
        {
            Car carScript = other.GetComponent<Car>();
            if (carScript == null) return;

            if (carScript.HasHitCheckpointOne && carScript.HasHitCheckpointTwo)
            {
                carScript.Laps++;
                carScript.HasHitCheckpointOne = false;
                carScript.HasHitCheckpointTwo = false;
                _gameManagerScript.CarFinishedLap(carScript);
                Debug.Log($"Car {other.name} completed lap {carScript.Laps}");
            }
            else
            {
                Debug.Log($"Car {other.name} crossed the finish line but did not hit all checkpoints.");
            }
        }
    }
}