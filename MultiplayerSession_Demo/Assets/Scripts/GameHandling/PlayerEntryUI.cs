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
        usernameText.text = isHost ? $"(Host) {username}" : username;

        statusCircle.color = ready ? readyColor : waitingColor;
    }
}
