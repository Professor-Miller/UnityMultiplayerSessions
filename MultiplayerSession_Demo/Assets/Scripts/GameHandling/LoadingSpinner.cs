using UnityEngine;

public class LoadingSpinner : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 180f;

    private void Update()
    {
        // ROTATION CALCULATION
        //
        // Time.unscaledDeltaTime is the amount of
        // real elapsed time since the previous frame,
        // unaffected by Time.timeScale.
        //
        // IMPORTANT:
        // Using unscaledDeltaTime allows the spinner
        // to keep rotating even if Time.timeScale = 0.
        transform.Rotate(
            0f,
            0f,
            -rotationSpeed * Time.unscaledDeltaTime
        );
    }
}
