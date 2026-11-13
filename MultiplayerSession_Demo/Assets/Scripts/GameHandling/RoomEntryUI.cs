using TMPro;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

public class RoomEntryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text roomNameText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private Button joinButton;

    public void Setup(ISessionInfo room, GameManager ui)
    {
        // Sets the room name to equal the name in the input
        roomNameText.text = room.Name;

        // Temp variable for holding the current number of players
        int playerCount = room.MaxPlayers - room.AvailableSlots;

        // Sets the text to read x/x Players (using the calculations)
        playerCountText.text =
            $"{playerCount}/{room.MaxPlayers} Players";

        // Determines if a room is full
        bool isFull = playerCount >= room.MaxPlayers;

        // The Join Button is disabled and enabled
        // based on whether the room is full
        joinButton.interactable = !isFull;

        // Prevent duplicate JoinRoom() calls
        joinButton.onClick.RemoveAllListeners();

        // Register a callback that executes when the button is clicked
        // It calls Gamemanager.JoinRoom(), passing the unique ID of this room
        // so SessionManager knows which multiplayer session to join
        joinButton.onClick.AddListener(() => ui.JoinRoom(room.Id));
    }
}
