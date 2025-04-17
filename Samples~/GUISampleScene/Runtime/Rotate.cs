using UnityEngine;

public class Rotate : MonoBehaviour
{
    public float rotationSpeedX = 60f; // Degrees per second for X-axis
    public float rotationSpeedY = 90f; // Degrees per second for Y-axis
    public float rotationSpeedZ = 90f; // Degrees per second for Y-axis

    void Update()
    {
        // Calculate rotation for this frame
        float rotationX = rotationSpeedX * Time.deltaTime;
        float rotationY = rotationSpeedY * Time.deltaTime;
        float rotationZ = rotationSpeedZ * Time.deltaTime;

        // Apply rotation
        transform.Rotate(rotationX, rotationY, rotationZ);
    }
}
