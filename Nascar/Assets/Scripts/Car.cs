using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class Car : MonoBehaviour
{
    [Header("Movement")] public float MaxSpeed = 8f;
    public float MaxReverseSpeed = 3f;
    public float Acceleration = 6f;
    public float BrakeForce = 10f;
    public float Friction = 3f;
    public bool ManualControl = false;
    public float OnCollisionFriction = 5f;
    public float OnCollisionMinumumSpeed = 1f;

    [Header("Steering")] public float TurnSpeed = 120f; // degrees per second at full speed

    [SerializeField] private float _currentSpeed;
    [SerializeField] private float _currentThrottle;
    private float _previousThrottle = 0f;
    [SerializeField] private SpriteRenderer carRenderer;

    [Header("Race Info")] public int Laps = 0;
    public bool HasHitCheckpointOne = false;
    public bool HasHitCheckpointTwo = false;
    public bool WrongDirection = false;
    public string DisplayName = "Car";

    [Header("Audio")] public AudioSource CarAudioSource;
    public float Volume = 0.1f;
    public AudioClip CarEngineSound;
    public AudioClip CarSpeedingUpSound;
    public AudioClip CarHandBreakingSound;
    public AudioClip CarReversingSound;
    private AudioClip _currentClip;

    // private GameManager _gameManagerScript;

    private void Start()
    {
        if (CarEngineSound == null)
        {
            throw new Exception("CarEngineSound is not assigned in the inspector.");
        }
        if (CarSpeedingUpSound == null)
        {
            throw new Exception("CarSpeedingUpSound is not assigned in the inspector.");
        }

        if (CarHandBreakingSound == null)
        {
            throw new Exception("CarHandBreakingSound is not assigned in the inspector.");
        }
        if (CarReversingSound == null)
        {
            throw new Exception("CarReversingSound is not assigned in the inspector.");
        }
    }

    void Update()
    {
        CarAudioSource.volume = Volume;
        if (ManualControl) ManualMovement();
    }

    public void ManualMovement()
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

    public void Movement(float throttle, float steer)
    {
        _currentThrottle = throttle;
        float dt = Time.deltaTime;

        // If the throttle is more/less than 0, we apply acceleration/braking.
        if (throttle > 0f)
        {
            float rate = _currentSpeed > 0f ? BrakeForce : Acceleration;
            _currentSpeed -= rate * throttle * dt;

            CarAudioSource.clip = CarHandBreakingSound;
            if (!CarAudioSource.isPlaying)
            {
                CarAudioSource.Play();
            }
        }
        else if (throttle < 0f)
        {
            float rate = _currentSpeed < 0f ? BrakeForce : Acceleration;
            _currentSpeed += rate * -throttle * dt;

            CarAudioSource.clip = CarSpeedingUpSound;
            if (!CarAudioSource.isPlaying)
            {
                CarAudioSource.Play();
            }
        }
        // No input: friction slows the car toward 0
        else
        {
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, Friction * dt);
        }

        _currentSpeed = Mathf.Clamp(_currentSpeed, -MaxReverseSpeed, MaxSpeed);

        CalculateSound(_previousThrottle, throttle, _currentSpeed);
        _previousThrottle = throttle;

        // Steering: scaled by speed so the car can't spin on the spot,
        // and flipped when reversing like a real car
        // This shouldn't be needed float speedFactor = Mathf.Clamp(_currentSpeed / MaxSpeed, -1f, 1f);
        float speedFactor = _currentSpeed / MaxSpeed;
        transform.Rotate(0f, 0f, -steer * TurnSpeed * speedFactor * dt);

        // Move along the direction the car is facing
        transform.position += transform.up * (_currentSpeed * dt);
    }

    public void SetSprite(Sprite newSprite)
    {
        carRenderer.sprite = newSprite;
    }

    public void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("Barrier") || other.gameObject.CompareTag("Obstacle"))
        {
            Debug.Log("Car hit a barrier!");
            _currentSpeed = Mathf.Clamp(_currentSpeed - OnCollisionFriction, OnCollisionMinumumSpeed, MaxSpeed);
        }
    }

    public void ResetCar()
    {
        _currentSpeed = 0f;
        Laps = 0;
        HasHitCheckpointOne = false;
        HasHitCheckpointTwo = false;
        WrongDirection = false;
    }

    private void CalculateSound(float previousThrottle, float currentThrottle, float currentSpeed)
    {
        // Remember that throttle is inverted, so that when we tilt the arduino forward, the throttle is negative, and when we tilt it backward, the throttle is positive.
        // So, to make it more readable, we reverse it.
        currentThrottle = -currentThrottle;
        Debug.Log($"Throttle: {currentThrottle}, Speed: {currentSpeed}");
        if (currentThrottle > 0f)
        {
            CarAudioSource.clip = CarSpeedingUpSound;
        }
        else if (currentThrottle < 0f && currentSpeed > 0f)
        {
            CarAudioSource.clip = CarHandBreakingSound;
        }
        else if (currentThrottle < 0f && currentSpeed < 0f)
        {
            CarAudioSource.clip = CarReversingSound;
        }
        else
        {
            CarAudioSource.clip = CarEngineSound;
        }

        if (CarAudioSource.isPlaying)
        {
            Debug.Log("Car audio is already playing the correct clip, no need to restart.");
            return;
        }
        Debug.Log($"Car audio clip changed from {_currentClip?.name ?? "null"} to {CarAudioSource.clip?.name ?? "null"}, restarting audio. Audio play: {CarAudioSource.isPlaying}");
        CarAudioSource.Play();
    }
}