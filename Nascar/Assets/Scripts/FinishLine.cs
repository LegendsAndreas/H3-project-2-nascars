using System;
using UnityEngine;

public class FinishLine : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    // Update is called once per frame
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
                Debug.Log($"Car {other.name} completed lap {carScript.Laps}");
            }
            else
            {
                Debug.Log($"Car {other.name} crossed the finish line but did not hit all checkpoints.");
            }
        }
    }
}