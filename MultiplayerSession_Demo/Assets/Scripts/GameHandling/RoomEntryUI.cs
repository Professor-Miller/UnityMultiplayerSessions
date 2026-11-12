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
        roomNameText.text = room.Name;

        int playerCount = room.MaxPlayers - room.AvailableSlots;

        playerCountText.text =
            $"{playerCount}/{room.MaxPlayers} Players";

        bool isFull = playerCount >= room.MaxPlayers;

        joinButton.interactable = !isFull;

        joinButton.onClick.RemoveAllListeners();
        joinButton.onClick.AddListener(() => ui.JoinRoom(room.Id));
    }
}
