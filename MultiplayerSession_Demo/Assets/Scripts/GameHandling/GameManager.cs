using System;
using System.Collections;
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

    public static event Action OnRefreshBrowse;

    private LobbyManager _lobby;
    private SessionManager _sessions;
    private bool _roomOpen;

    void Start()
    {
        // Retrieve the globally accessible SessionManager
        _sessions = SessionManager.Instance;

        // EVENT SUBSCRIPTIONS
        //
        // Listen for successful session creation or joining.
        //
        // When SessionManager invokes OnSessionJoined,
        // our EnterRoom() method executes.
        _sessions.OnSessionJoined += EnterRoom;

        // Listen for the session ending.
        //
        // When SessionManager invokes OnSessionLeft,
        // our SessionLeft() method executes.
        _sessions.OnSessionLeft += SessionLeft;

        // INPUT VALIDATION
        //
        // Whenever the username changes,
        // check whether the relevant buttons
        // should be interactable.
        //
        // The lambda ignores the string value passed
        // by TMP_InputField and calls ValidateInputs().
        usernameInput.onValueChanged.AddListener(_ => ValidateInputs());

        // Do the same whenever the room name changes.
        roomNameInput.onValueChanged.AddListener(_ => ValidateInputs());

        // Ensure the loading overlay starts hidden.
        HideLoading();

        // Display the title screen.
        Show(titlePanel);

        // Validate initial input values.
        //
        // This ensures buttons are correctly enabled
        // or disabled before the player types anything.
        ValidateInputs();
    }

    void Update()
    {
        // Don't search for LobbyManager unless
        // the player has entered a room.
        if (!_roomOpen) return;

        // Only search if we don't already have a reference.
        if (_lobby == null)
        {
            // Search the scene for a LobbyManager.
            //
            // Unlike SessionManager, LobbyManager is a
            // NetworkBehaviour that must be spawned by NGO.
            //
            // It may not exist immediately after joining.
            _lobby = FindAnyObjectByType<LobbyManager>();

            // Check that the object exists AND has
            // completed its network spawning process.
            if (_lobby != null && _lobby.IsSpawned)
            {
                // Subscribe to local lobby change events.
                //
                // LobbyManager invokes OnLobbyChanged when
                // synchronized player data, match state,
                // or winner information changes.
                _lobby.OnLobbyChanged += RefreshLobby;

                // Immediately populate the UI using
                // the current synchronized lobby data.
                //
                // We don't want to wait for another change
                // before displaying the player list.
                RefreshLobby();
            }
            else
            {
                // The object either doesn't exist yet
                // or hasn't finished network spawning.
                //
                // Clear the reference so Update()
                // will try again on the next frame.
                _lobby = null;
            }
        }
    }

    private void ValidateInputs()
    {
        // Check whether the username contains
        // something other than whitespace.
        //
        // Examples:
        // "Justin" = valid
        // ""       = invalid
        // "   "    = invalid
        bool hasUsername = !string.IsNullOrWhiteSpace(usernameInput.text);

        // Players need a username before opening
        // the room creation screen.
        createNewRoomButton.interactable = hasUsername;

        // Players also need a username before browsing rooms.
        browseButton.interactable = hasUsername;

        // Creating a room requires a nonempty room name.
        createRoomButton.interactable = !string.IsNullOrWhiteSpace(roomNameInput.text);
    }

    public void OpenCreatePanel()
    {
        // Store the player's chosen username locally.
        //
        // SessionManager will retain this value,
        // and LobbyManager will later register it
        // with the server through an RPC.
        _sessions.SetUsername(usernameInput.text);

        // Display the room creation screen.
        Show(createPanel);
    }

    public async void OpenBrowsePanel()
    {
        // Store the local player's username.
        _sessions.SetUsername(usernameInput.text);

        // Switch to the room browser.
        Show(browsePanel);

        // Display the loading overlay while searching.
        ShowLoading();

        try
        {
            // Request the current list of available rooms.
            //
            // await allows this method to wait for
            // the asynchronous query without freezing Unity.
            await RefreshRooms();
        }

        finally
        {
            // Hide the loading overlay whether
            // the operation succeeds or fails.
            HideLoading();
        }
    }

    public async void RefreshRoomsButton()
    {
        // Refresh the available room list.
        await RefreshRooms();

        // Notify listeners that the refresh request finished.
        //
        // RefreshButtonAnimation responds by animating
        // the button after the query completes.
        OnRefreshBrowse?.Invoke();
    }

    private async System.Threading.Tasks.Task RefreshRooms()
    {
        // Remove existing room entry GameObjects.
        //
        // This prevents old entries from remaining
        // when the browser is refreshed.
        ClearChildren(roomListContainer);

        try
        {
            // Ask SessionManager for available sessions.
            //
            // GameManager does not communicate directly
            // with Unity Multiplayer Services.
            //
            // This preserves separation of responsibilities.
            List<ISessionInfo> rooms = await _sessions.BrowseRooms();

            // Create a UI entry for every discovered room.
            foreach (var room in rooms)
            {
                // Instantiate the room entry prefab
                // as a child of the room list container.
                var entry = Instantiate(roomEntryPrefab, roomListContainer);

                // Populate the entry with session information.
                //
                // Passing 'this' gives RoomEntryUI a reference
                // to this GameManager so its Join button
                // can call JoinRoom() with the room's ID.
                entry.Setup(room, this);
            }
        }

        catch (Exception e)
        {
            // Log any errors encountered while browsing.
            //
            // SessionManager also handles service-level errors,
            // but this catches failures from the overall
            // UI refresh operation.
            Debug.LogError($"Browse failed: {e}");
        }
    }

    public async void CreateRoom()
    {
        // Display the loading overlay
        ShowLoading();

        try
        {
            // Ask SessionManager to create a session
            // using the room name entered by the player.
            //
            // SessionManager handles:
            // - Unity Services initialization
            // - Authentication
            // - Session configuration
            // - Relay networking
            //
            // If successful, SessionManager invokes
            // OnSessionJoined, which triggers EnterRoom().
            await _sessions.CreateRoom(roomNameInput.text);
        }

        finally
        {
            // Hide the loading overlay when finished
            HideLoading();
        }
    }

    public async void JoinRoom(string sessionId)
    {
        // Show the loading overlay
        ShowLoading();

        try
        {
            // Ask SessionManager to join the selected session.
            //
            // sessionId identifies the room selected
            // from the browser.
            //
            // The SessionManager handles the actual
            // Unity Multiplayer Services operation.
            await _sessions.JoinRoom(sessionId);
        }

        finally
        {
            // Hide loading feedback when finished
            HideLoading();
        }
    }

    private void EnterRoom()
    {
        // Called when SessionManager invokes OnSessionJoined.
        //
        // This happens after successfully creating
        // or joining a session.

        // Mark the room as open.
        //
        // Update() will now begin searching for
        // the spawned LobbyManager.
        _roomOpen = true;

        // Display the current session's name.
        roomNameText.text = _sessions.CurrentSession.Name;

        // Display the session's join code
        joinCodeText.text = _sessions.CurrentSession.Code;

        // Switch to the lobby screen
        Show(roomPanel);
    }

    public void ReadyOrStart()
    {
        // Make sure LobbyManager exists
        if (_lobby == null) return;

        // HOST VS CLIENT
        //
        // The same UI button performs different actions
        // depending on the local player's network role.
        if (NetworkManager.Singleton.IsHost)
            // HOST:
            // Request the match to begin.
            //
            // LobbyManager checks CanStartMatch()
            // before actually changing the game state.
            _lobby.StartMatch();
        else
            // CLIENT:
            // Request a readiness change.
            //
            // ToggleReady() sends an RPC to the server.
            //
            // The server modifies the synchronized
            // player list if the request is valid.
            _lobby.ToggleReady();
    }

    private void RefreshLobby()
    {
        // Called when LobbyManager invokes OnLobbyChanged.
        //
        // This can happen when:
        // - A player joins.
        // - A player leaves.
        // - A player changes readiness.
        // - The match state changes.
        // - A winner is selected.

        // Ensure the networked lobby exists and is spawned.
        if (_lobby == null || !_lobby.IsSpawned) return;

        // MATCH STATE UI
        //
        // The server controls State.Value.
        //
        // Netcode synchronizes the state to clients,
        // and each client's GameManager displays
        // the appropriate screen.
        switch (_lobby.State.Value)
        {
            case MatchState.Lobby:
                // Display the normal lobby screen
                Show(roomPanel);
                break;

            case MatchState.Rolling:
                // Display the winner-selection animation
                Show(rollingPanel);
                return;

            case MatchState.Results:
                // Read the synchronized winner value
                //
                // FixedString64Bytes needs to be converted
                // to a regular string for TMPro
                winnerText.text = _lobby.Winner.Value.ToString();
                // Display the results screen
                Show(resultsPanel);
                return;
        }

        // PLAYER LIST
        //
        // Clear existing player entries before rebuilding.
        //
        // This is simple for a small lobby,
        // although larger games might update entries
        // individually instead of recreating everything.
        ClearChildren(playerListContainer);

        // Determine whether this machine is the host
        bool isHost = NetworkManager.Singleton.IsHost;

        // Get the server's ClientId.
        //
        // In a typical NGO host session,
        // the host is also the server client.
        ulong hostId = NetworkManager.ServerClientId;

        // Loop through the synchronized player information
        foreach (var player in _lobby.Players)
        {
            // Add the host entry first when encountered
            if (player.ClientId == hostId)
            {
                AddPlayerEntry(player, true);
            }

            // Add entries for all non-host players
            if (player.ClientId != hostId)
            {
                AddPlayerEntry(player, false);
            }
        }

        // Update the displayed player count.
        //
        // Example: "3/4 Players"
        //
        // The current count comes from the synchronized list.
        // The maximum comes from the active session.
        playerCountText.text =
            $"{_lobby.Players.Count}/{_sessions.CurrentSession.MaxPlayers} Players";

        // HOST UI
        if (isHost)
        {
            // The host starts matches instead of readying up.
            readyButtonText.text = "Play Match";

            // Only allow the host to press Play Match
            // when LobbyManager says the match can start.
            //
            // Requirements include:
            // - At least two players.
            // - All non-host players are ready.
            // - The match is in the Lobby state.
            readyButton.interactable = _lobby.CanStartMatch();
        }

        else
        {
            // CLIENT UI
            //
            // Determine whether the local player is ready.

            // Default to unready until we find the player.
            bool ready = false;

            // Get this client's unique network ID.
            ulong localId = NetworkManager.Singleton.LocalClientId;

            // Find the local player's synchronized entry.
            foreach (var player in _lobby.Players)
            {
                if (player.ClientId == localId)
                {
                    // Read the server-authoritative ready value.
                    ready = player.Ready;

                    // Stop searching once the player is found.
                    break;
                }
            }

            // Change the button label based on readiness.
            //
            // Ready = true  --> "Unready"
            // Ready = false --> "Ready Up"
            readyButtonText.text = ready ? "Unready" : "Ready Up";

            // Clients can toggle their readiness.
            readyButton.interactable = true;
        }

        // Only the host can return the game to the lobby
        // after a match.
        //
        // This controls visibility of the return button,
        // not the authoritative permission itself.
        returnButton.gameObject.SetActive(isHost);
    }

    private void AddPlayerEntry(LobbyPlayer player, bool isHost)
    {
        // Create a new player entry under the player list.
        var entry = Instantiate(playerEntryPrefab, playerListContainer);

        // Pass synchronized player information
        // into the UI component.
        //
        // PlayerEntryUI decides how to display:
        // - Username
        // - Ready status
        // - Host indicator
        //
        // This keeps the visual formatting separate
        // from the network gameplay logic.
        entry.Setup(player.Username.ToString(), player.Ready, isHost);
    }

    public void BackToRoom()
    {
        // Ensure the networked lobby exists.
        if (_lobby == null) return;

        // Only the host can request the reset
        // through this UI.
        if (NetworkManager.Singleton.IsHost)
        {
            // Ask LobbyManager to reset the match.
            //
            // LobbyManager resets player readiness,
            // clears the winner, and returns State
            // to MatchState.Lobby.
            _lobby.ReturnToLobby();
        }
    }

    public async void LeaveRoom()
    {
        // Show loading feedback during the operation.
        ShowLoading();

        try
        {
            // Ask SessionManager to leave the session.
            //
            // SessionManager handles the distinction:
            //
            // HOST: Delete the session.
            // CLIENT: Leave the session.
            //
            // SessionManager also invokes OnSessionLeft
            // when it clears the local session.
            await _sessions.LeaveRoom();
        }

        finally
        {
            // Refresh available rooms after leaving.
            //
            // NOTE:
            // SessionLeft() also calls RefreshRooms(),
            // so this implementation can request
            // overlapping UI refreshes.
            await RefreshRooms();

            // Hide the loading overlay
            HideLoading();
        }
    }

    private async void SessionLeft()
    {
        // EVENT CALLBACK
        //
        // Invoked when SessionManager announces
        // that the current session has ended.
        //
        // This can happen because the player leaves
        // or because the session is deleted/removed.

        // Stop searching for LobbyManager in Update().
        _roomOpen = false;

        // Remove player entries from the old lobby.
        ClearChildren(playerListContainer);

        // If we previously subscribed to LobbyManager,
        // unsubscribe before discarding the reference.
        if (_lobby != null)
        {
            // Stop receiving lobby update events
            // from the previous networked object.
            _lobby.OnLobbyChanged -= RefreshLobby;

            // Clear the old lobby reference.
            //
            // A future session will have its own
            // spawned LobbyManager.
            _lobby = null;
        }

        // Return the player to the room browser
        Show(browsePanel);

        // Refresh the list of discoverable rooms
        await RefreshRooms();
    }

    public void BackToTitle()
    {
        Show(titlePanel);
        // Switch to the title screen.
        //
        // This only changes UI visibility.
        // It does not leave an active multiplayer session.
    }

    private void Show(GameObject panel)
    {
        // Only activate the panel passed into this method.
        //
        // Every other main panel is disabled.
        //
        // This avoids manually turning off each panel
        // whenever the player changes screens.

        titlePanel.SetActive(panel == titlePanel);
        createPanel.SetActive(panel == createPanel);
        browsePanel.SetActive(panel == browsePanel);
        roomPanel.SetActive(panel == roomPanel);
        rollingPanel.SetActive(panel == rollingPanel);
        resultsPanel.SetActive(panel == resultsPanel);

        // NOTE:
        // The loading panel is intentionally not included.
        //
        // Loading is an overlay that can appear
        // on top of any main panel.
    }

    private void ClearChildren(Transform parent)
    {
        // Loop through every child of the provided container
        foreach (Transform child in parent)
        {
            // Schedule each child GameObject for destruction.
            //
            // Destroy() is deferred until later in the frame.
            //
            // This removes old room or player UI entries
            // before new entries are created.
            Destroy(child.gameObject);
        }
    }

    private void ShowLoading()
    {
        // Activate the loading overlay
        loadingPanel.SetActive(true);
    }

    private void HideLoading()
    {
        // Deactivate the loading overlay
        loadingPanel.SetActive(false);
    }

    void OnDestroy()
    {
        // EVENT CLEANUP
        //
        // Remove subscriptions when GameManager is destroyed.
        //
        // This prevents old callbacks from remaining
        // registered after the object is gone.
        if (_sessions != null)
        {
            // Stop listening for session joins.
            _sessions.OnSessionJoined -= EnterRoom;

            // Stop listening for session departures.
            _sessions.OnSessionLeft -= SessionLeft;
        }

        if (_lobby != null)
        {
            // Stop listening for synchronized lobby changes.
            _lobby.OnLobbyChanged -= RefreshLobby;
        }
    }
}
