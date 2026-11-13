using TMPro;
using UnityEngine;

public class RollingDotsAnimation : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private float interval = 0.4f;

    private float timer;
    private int dots;

    private void OnEnable()
    {
        // Reset the elapsed time when the animation starts
        timer = 0f;

        // Start the animation with zero dots
        dots = 0;

        // Reset the text to its original message
        text.text = "Picking Random Winner (Totally Legit)";
    }

    private void Update()
    {
        // Start the timer
        timer += Time.deltaTime;

        // Check whether enough time has passed to update the dots
        if (timer < interval) return;

        // Reset the timer
        timer = 0f;

        // Increment the dot count and wrapp back to 0 after reaching 3.
        dots = (dots + 1) % 4;

        // Rebuild the text string to include the dots based on the current value
        text.text = "Picking Random Winner (Totally Legit)" + new string('.', dots);
    }
}
