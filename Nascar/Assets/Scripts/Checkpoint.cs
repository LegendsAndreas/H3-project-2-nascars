using System;
using UnityEngine;

public class Checkpoint : MonoBehaviour
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
        Debug.Log("Checkpoint triggered by " + other.name);
        Car carScript = other.GetComponent<Car>();
        if (carScript == null)
        {
            Debug.Log("No car script found");
            return;
        }
        if (gameObject.name == "Checkpoint 1")
        {
            Debug.Log($"Car {other.name} hit Checkpoint 1");
            carScript.HasHitCheckpointOne = true;
            carScript.WrongDirection = false;
        }
        else if (gameObject.name == "Checkpoint 2")
        {
            Debug.Log($"Car {other.name} hit Checkpoint 2");
            if (carScript.HasHitCheckpointOne)
            {
                carScript.HasHitCheckpointTwo = true;
            }
            else
            {
                carScript.WrongDirection = true;
            }
        }
    }
}