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

    [Header("Loading")]
    [SerializeField] private GameObject loadingPanel;

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

        HideLoading();
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
        bool hasUsername = !string.IsNullOrWhiteSpace(usernameInput.text);

        createNewRoomButton.interactable = hasUsername;
        browseButton.interactable = hasUsername;

        createRoomButton.interactable = !string.IsNullOrWhiteSpace(roomNameInput.text);
    }

    public void OpenCreatePanel()
    {
        _sessions.SetUsername(usernameInput.text);
        Show(createPanel);
    }

    public async void OpenBrowsePanel()
    {
        _sessions.SetUsername(usernameInput.text);
        Show(browsePanel);
        ShowLoading();

        try
        {
            await RefreshRooms();
        }

        finally
        {
            HideLoading();
        }
    }

    private async System.Threading.Tasks.Task RefreshRooms()
    {
        ClearChildren(roomListContainer);

        try
        {
            List<ISessionInfo> rooms = await _sessions.BrowseRooms();

            foreach (var room in rooms)
            {
                var entry = Instantiate(roomEntryPrefab, roomListContainer);

                entry.Setup(room, this);
            }
        }

        catch (Exception e)
        {
            Debug.LogError($"Browse failed: {e}");
        }
    }

    public async void CreateRoom()
    {
        ShowLoading();

        try
        {
            await _sessions.CreateRoom(roomNameInput.text);
        }

        finally
        {
            HideLoading();
        }
    }

    public async void JoinRoom(string sessionId)
    {
        ShowLoading();

        try
        {
            await _sessions.JoinRoom(sessionId);
        }

        finally
        {
            HideLoading();
        }
    }

    private void EnterRoom()
    {
        _roomOpen = true;

        roomNameText.text = _sessions.CurrentSession.Name;
        joinCodeText.text = _sessions.CurrentSession.Code;

        Show(roomPanel);
    }

    public void ReadyOrStart()
    {
        if (_lobby == null) return;

        if (NetworkManager.Singleton.IsHost)
            _lobby.StartMatch();
        else
            _lobby.ToggleReady();
    }

    private void RefreshLobby()
    {
        if (_lobby == null || !_lobby.IsSpawned) return;

        switch (_lobby.State.Value)
        {
            case MatchState.Lobby:
                Show(roomPanel);
                break;

            case MatchState.Rolling:
                Show(rollingPanel);
                return;

            case MatchState.Results:
                winnerText.text = _lobby.Winner.Value.ToString();
                Show(resultsPanel);
                return;
        }

        ClearChildren(playerListContainer);

        bool isHost = NetworkManager.Singleton.IsHost;
        ulong hostId = NetworkManager.ServerClientId;

        foreach (var player in _lobby.Players)
        {
            if (player.ClientId == hostId)
            {
                AddPlayerEntry(player, true);
            }

            if (player.ClientId != hostId)
            {
                AddPlayerEntry(player, false);
            }
        }

        playerCountText.text =
            $"{_lobby.Players.Count}/{_sessions.CurrentSession.MaxPlayers} Players";

        if (isHost)
        {
            readyButtonText.text = "Play Match";
            readyButton.interactable = _lobby.CanStartMatch();
        }

        else
        {
            bool ready = false;
            ulong localId = NetworkManager.Singleton.LocalClientId;

            foreach (var player in _lobby.Players)
            {
                if (player.ClientId == localId)
                {
                    ready = player.Ready;
                    break;
                }
            }

            readyButtonText.text = ready ? "Unready" : "Ready Up";
            readyButton.interactable = true;
        }

        returnButton.gameObject.SetActive(isHost);
    }

    private void AddPlayerEntry(LobbyPlayer player, bool isHost)
    {
        var entry = Instantiate(playerEntryPrefab, playerListContainer);

        entry.Setup(player.Username.ToString(), player.Ready, isHost);
    }

    public void BackToRoom()
    {
        if (_lobby == null) return;

        if (NetworkManager.Singleton.IsHost)
        {
            _lobby.ReturnToLobby();
        }
    }

    public async void LeaveRoom()
    {
        ShowLoading();

        try
        {
            await _sessions.LeaveRoom();
        }

        finally
        {
            await RefreshRooms();
            HideLoading();
        }
    }

    private async void SessionLeft()
    {
        _roomOpen = false;

        if (_lobby != null)
        {
            _lobby.OnLobbyChanged -= RefreshLobby;
            _lobby = null;
        }

        Show(browsePanel);

        await RefreshRooms();
    }

    public void BackToTitle()
    {
        Show(titlePanel);
    }

    private void Show(GameObject panel)
    {
        titlePanel.SetActive(panel == titlePanel);
        createPanel.SetActive(panel == createPanel);
        browsePanel.SetActive(panel == browsePanel);
        roomPanel.SetActive(panel == roomPanel);
        rollingPanel.SetActive(panel == rollingPanel);
        resultsPanel.SetActive(panel == resultsPanel);
    }

    private void ClearChildren(Transform parent)
    {
        foreach (Transform child in parent)
        {
            Destroy(child.gameObject);
        }
    }

    private void ShowLoading()
    {
        loadingPanel.SetActive(true);
    }

    private void HideLoading()
    {
        loadingPanel.SetActive(false);
    }

    void OnDestroy()
    {
        if (_sessions != null)
        {
            _sessions.OnSessionJoined -= EnterRoom;
            _sessions.OnSessionLeft -= SessionLeft;
        }

        if (_lobby != null)
        {
            _lobby.OnLobbyChanged -= RefreshLobby;
        }
    }
}
