using System;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject titlePanel;
    [SerializeField] private GameObject createPanel;
    [SerializeField] private GameObject browsePanel;
    [SerializeField] private GameObject roomPanel;
    [SerializeField] private GameObject rollingPanel;
    [SerializeField] private GameObject resultsPanel;

    [Header("Inputs")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField roomNameInput;

    [Header("Buttons")]
    [SerializeField] private Button createNewRoomButton;
    [SerializeField] private Button browseButton;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private Button leaveButton;
    [SerializeField] private Button returnButton;

    [Header("Room")]
    [SerializeField] private TMP_Text roomNameText;
    [SerializeField] private TMP_Text joinCodeText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private TMP_Text readyButtonText;

    [Header("Results")]
    [SerializeField] private TMP_Text winnerText;

    [Header("Prefabs")]
    [SerializeField] private RoomEntryUI roomEntryPrefab;
    [SerializeField] private PlayerEntryUI playerEntryPrefab;

    [Header("Containers")]
    [SerializeField] private Transform roomListContainer;
    [SerializeField] private Transform playerListContainer;

    private LobbyManager _lobby;
    private SessionManager _sessions;
    private bool _roomOpen;

    void Start()
    {
        _sessions = SessionManager.Instance;

        _sessions.OnSessionJoined += EnterRoom;
        _sessions.OnSessionLeft += SessionLeft;

        usernameInput.onValueChanged.AddListener(_ => ValidateInputs());
        roomNameInput.onValueChanged.AddListener(_ => ValidateInputs());

        Show(titlePanel);
        ValidateInputs();
    }

    void Update()
    {
        if (!_roomOpen) return;

        if (_lobby == null)
        {
            _lobby = FindAnyObjectByType<LobbyManager>();

            if (_lobby != null && _lobby.IsSpawned)
            {
                _lobby.OnLobbyChanged += RefreshLobby;
                RefreshLobby();
            }
            else
            {
                _lobby = null;
            }
        }
    }

    private void ValidateInputs()
    {
        //TODO
        // Turn buttons on an off and shit
    }
}
