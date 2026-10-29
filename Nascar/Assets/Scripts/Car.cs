using System;
using System.Text;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using UnityEngine;
using UnityEngine.InputSystem;

public class Car : MonoBehaviour
{
    [Header("Movement")] public float MaxSpeed = 8f;
    public float MaxReverseSpeed = 3f;
    public float Acceleration = 6f;
    public float BrakeForce = 10f;
    public float Friction = 3f;

    [Header("Steering")] public float TurnSpeed = 120f; // degrees per second at full speed

    private float _currentSpeed;

    private void Start()
    {
    }
    void Update()
    {
        // Movement();
    }

    public void GetArduinoInput()
    {
    }

    public void Movement(float throttleInput, float steerInput)
    {
        // It would be more efficient to just set it once in Start(), but in case the keyboard is disconnected and reconnected,
        // that would not be enough to reconnect the keyboard to the actual game.
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        float steer = 0f;
        float throttle = 0f;

        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer += 1f; // left = counterclockwise
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer -= 1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) throttle += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) throttle -= 1f;

        float dt = Time.deltaTime;

        // If the throttle is more/less than 0, we apply acceleration/braking.
        if (throttle > 0f)
        {
            float rate = _currentSpeed < 0f ? BrakeForce : Acceleration;
            _currentSpeed += rate * dt;
        }
        else if (throttle < 0f)
        {
            float rate = _currentSpeed > 0f ? BrakeForce : Acceleration;
            _currentSpeed -= rate * dt;
        }
        // No input: friction slows the car toward 0
        else
        {
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, Friction * dt);
        }

        _currentSpeed = Mathf.Clamp(_currentSpeed, -MaxReverseSpeed, MaxSpeed);

        // Steering: scaled by speed so the car can't spin on the spot,
        // and flipped when reversing like a real car
        // This shouldn't be needed float speedFactor = Mathf.Clamp(_currentSpeed / MaxSpeed, -1f, 1f);
        float speedFactor = _currentSpeed / MaxSpeed;
        transform.Rotate(0f, 0f, steer * TurnSpeed * speedFactor * dt);

        // Move along the direction the car is facing
        transform.position += transform.up * (_currentSpeed * dt);
    }
}