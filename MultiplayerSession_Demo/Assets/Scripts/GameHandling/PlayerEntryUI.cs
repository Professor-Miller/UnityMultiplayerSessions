using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerEntryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text usernameText;
    [SerializeField] private Image statusCircle;

    [SerializeField] private Color readyColor = Color.green;
    [SerializeField] private Color waitingColor = new Color(0.3f, 0.3f, 0.3f, 1.0f);

    public void Setup(string username, bool ready, bool isHost)
    {
        // DISPLAY USERNAME
        //
        // If isHost is true:
        // Add "(Host)" before the username.
        //
        // If isHost is false:
        // Display the username normally.
        //
        // Example:
        // Host   --> "(Host) Justin"
        // Client --> "Justin"
        usernameText.text = isHost ? $"(Host) {username}" : username;

        // DISPLAY READY STATUS
        //
        // Change the status circle's color based
        // on the player's role and readiness.
        //
        // If the player is the host OR is ready:
        // Display the ready color (green).
        //
        // Otherwise:
        // Display the waiting color (dark gray).
        //
        // Example:
        // Host, not ready   --> Green
        // Client, ready     --> Green
        // Client, not ready --> Gray
        statusCircle.color = (isHost || ready) ? readyColor : waitingColor;
    }
}
