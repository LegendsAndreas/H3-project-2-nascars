using System;
using UnityEngine;

public class CarNameTag : MonoBehaviour
{
    public Transform target;
    public float offsetY = 2;
    public float offsetX = 0;

    private void Start()
    {
        if (target == null) throw new Exception("Target not assigned for CarNameTag.");
    }

    void Update()
    {
        Vector3 newPosition = target.position + new Vector3(offsetX, offsetY, 0);
        transform.position = newPosition;
        transform.rotation = Quaternion.Euler(0, 0, 0); // Keep the name tag upright
    }
}