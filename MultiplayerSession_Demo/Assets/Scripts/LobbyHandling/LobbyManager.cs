using System;
using System.Collections;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class LobbyManager : NetworkBehaviour
{
    public NetworkList<LobbyPlayer> Players;
    public NetworkVariable<MatchState> State = new(MatchState.Lobby);
    public NetworkVariable<FixedString64Bytes> Winner = new();
    public event Action OnLobbyChanged;

    void Awake()
    {
        // Create the NetworkList before the object is spawned.
        //
        // NetworkList must be initialized before Netcode
        // begins synchronizing the NetworkObject.
        //
        // We initialize it in Awake rather than OnNetworkSpawn
        // so it exists when network spawning begins.
        Players = new NetworkList<LobbyPlayer>();
    }

    public override void OnNetworkSpawn()
    {
        // Listen for changes to the synchronized player list.
        //
        // Adding, removing, or modifying a player can
        // trigger this callback.
        Players.OnListChanged += OnPlayersChanged;

        // Listen for changes to the match state
        State.OnValueChanged += OnStateChanged;

        // Listen for changes to the winner
        Winner.OnValueChanged += OnWinnerChanged;

        // SERVER-ONLY SETUP
        //
        // Only the server is responsible for maintaining
        // the authoritative list of connected players.
        if (IsServer)
        {
            // Subscribe to Netcode's connection event.
            //
            // Called when a client successfully connects.
            NetworkManager.OnClientConnectedCallback += PlayerConnected;

            // Subscribe to Netcode's disconnection event.
            //
            // Called when a client disconnects.
            NetworkManager.OnClientDisconnectCallback += PlayerDisconnected;

            // Register players who were already connected
            // before this LobbyManager spawned.
            //
            // This is necessary because connection callbacks
            // may have happened before we subscribed to them.
            foreach (ulong id in NetworkManager.ConnectedClientsIds)
            {
                PlayerConnected(id);
            }
        }

        // CLIENT SETUP
        //
        // Don't Forget! The host is both a server and a client.
        //
        // Therefore, this also runs on the host.
        if (IsClient)
        {
            // Send our chosen username to the server.
            //
            // SessionManager stores the username locally.
            // This RPC registers it in the synchronized lobby list.
            RegisterUsernameRpc(SessionManager.Instance.Username);
        }

        // Notify local listeners that the lobby is available.
        //
        // GameManager may use this to display initial lobby data.
        OnLobbyChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        // Called when this NetworkObject is removed
        // from the active network session.
        //
        // Clean up subscriptions to avoid stale callbacks
        // or duplicate event handling.

        Players.OnListChanged -= OnPlayersChanged;
        State.OnValueChanged -= OnStateChanged;
        Winner.OnValueChanged -= OnWinnerChanged;

        // Only the server subscribed to connection events,
        // so only the server needs to unsubscribe.
        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback -= PlayerConnected;
            NetworkManager.OnClientDisconnectCallback -= PlayerDisconnected;
        }
    }

    private void OnPlayersChanged(NetworkListEvent<LobbyPlayer> change)
    {
        // Called whenever Netcode reports a change
        // to the synchronized Players list.
        //
        // The 'change' parameter describes what changed,
        // but this implementation simply refreshes the UI.
        //
        // The event is invoked locally on each machine
        // that receives the list change.
        OnLobbyChanged?.Invoke();
    }

    private void OnStateChanged(MatchState oldState, MatchState newState)
    {
        // Called when the synchronized match state changes.
        //
        // oldState = previous phase.
        // newState = current phase.
        //
        // Example:
        // Lobby --> Rolling
        //
        // GameManager uses the new State.Value
        // to decide which panel should be visible.
        OnLobbyChanged?.Invoke();
    }

    private void OnWinnerChanged(FixedString64Bytes oldWinner, FixedString64Bytes newWinner)
    {
        // Called when the synchronized winner changes.
        //
        // GameManager can read Winner.Value
        // to display the selected player's username.
        OnLobbyChanged?.Invoke();
    }

    private void PlayerConnected(ulong id)
    {
        // SERVER CALLBACK
        //
        // Registers a connected player in the lobby.
        //
        // The 'id' identifies the network connection.

        // Prevent players from joining an active match.
        //
        // If the game is already rolling or displaying results,
        // disconnect the newly connected client.
        if (State.Value != MatchState.Lobby)
        {
            NetworkManager.DisconnectClient(id);
            return;
        }

        // Check whether this player is already registered.
        //
        // This prevents duplicate entries because
        // OnNetworkSpawn also registers existing connections.
        foreach (var player in Players)
        {
            if (player.ClientId == id) return;
        }

        // Add a new player to the synchronized list
        Players.Add(new LobbyPlayer
        {
            // Associate the entry with this connection
            ClientId = id,

            // Assign a temporary username
            //
            // The client will later send its chosen username
            // through RegisterUsernameRpc()
            Username = new FixedString64Bytes($"Player {id}"),

            // New player begin unready
            Ready = false
        });

        // Because Players is a NetworkList, this addition
        // is synchronized automatically to connected clients.
    }

    private void PlayerDisconnected(ulong id)
    {
        // SERVER CALLBACK
        //
        // Removes a disconnected player from the lobby list.
        //
        // We iterate backwards because removing an element
        // shifts the indices of the remaining elements.
        for (int i = Players.Count - 1; i >= 0; i--)
        {
            // Find entries belonging to the disconnected client.
            if (Players[i].ClientId == id)
            {
                // Remove the player.
                //
                // NetworkList synchronizes this removal
                // to the remaining connected clients.
                Players.RemoveAt(i);
            }
        }
    }

    // SERVER RPC
    //
    // [Rpc(SendTo.Server)] means this method executes
    // on the server when invoked by a client.
    //
    // The client provides its desired username,
    // but the server decides which player entry to update.
    [Rpc(SendTo.Server)]
    private void RegisterUsernameRpc(string username, RpcParams rpcParams = default)
    {
        // SECURITY / AUTHORITY
        //
        // Determine which client actually sent the RPC.
        //
        // This comes from Netcode's RPC metadata,
        // rather than trusting a client-supplied ID.
        ulong id = rpcParams.Receive.SenderClientId;

        // Reject empty or whitespace-only usernames.
        if (string.IsNullOrWhiteSpace(username)) return;

        // Remove unnecessary spaces from the username.
        username = username.Trim();

        // Search for the player's existing lobby entry.
        for (int i = 0; i < Players.Count; i++)
        {
            // Skip entries belonging to other clients
            if (Players[i].ClientId != id)
                continue;

            // LobbyPlayer is a struct (value type)
            //
            // Retrieving Players[i] gives us a vlaue
            // that we can modify locally
            var player = Players[i];

            // Replace the temporary username
            // with the player's chosen username
            player.Username = new FixedString64Bytes(username);

            // IMPORTANT: WRITE THE STRUCT BACK.
            //
            // Changing the local variable alone does not
            // modify the element stored in NetworkList.
            //
            // Assigning it back updates the collection
            // and triggers network synchronization.
            Players[i] = player;

            // Registration is complete
            return;
        }

        // FALLBACK
        //
        // If the player wasn't found in the list,
        // create a new entry using the sender's ClientId.
        //
        // This handles situations where the username RPC
        // arrives before a corresponding entry exists.
        Players.Add(new LobbyPlayer
        {
            ClientId = id,
            Username = new FixedString64Bytes(username),
            Ready = false
        });
    }

    public void ToggleReady()
    {
        // PUBLIC ENTRY POINT
        //
        // Called by GameManager when a player presses
        // the Ready / Unready button.
        //
        // This method does not change readiness directly.

        // Only connected clients can request readiness changes.
        //
        // Readiness can only be changed during the Lobby phase.
        if (!IsClient || State.Value != MatchState.Lobby) return;

        // Send the readines request to the server
        ToggleReadyRpc();
    }

    [Rpc(SendTo.Server)]
    private void ToggleReadyRpc(RpcParams rpcParams = default)
    {
        // SERVER-AUTHORITATIVE READINESS
        //
        // The server receives the client's request
        // and decides whether to allow the change.

        // Reject readiness changes outside the lobby phase.
        //
        // This is checked again on the server because
        // client-side checks are not authoritative.
        if (State.Value != MatchState.Lobby) return;

        // Identify the client who sent the request.
        ulong id = rpcParams.Receive.SenderClientId;

        // Prevent the host/server from toggling readiness.
        //
        // In this design, the host is considered
        // automatically ready.
        if (id == NetworkManager.ServerClientId) return;

        // Find the requesting player's entry
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].ClientId != id)
                continue;

            // Retrieve the player struct
            var player = Players[i];

            // Toggle readiness
            //
            // false -> true
            // true -> false
            player.Ready = !player.Ready;

            // Write the modified struct back to NetworkList.
            //
            // The server's change is then synchronized
            // to connected clients.
            Players[i] = player;

            return;
        }
    }

    public bool CanStartMatch()
    {
        // SERVER VALIDATION
        //
        // Determines whether the match is allowed to start.
        //
        // All requirements must be satisfied.

        // Requirement 1:
        // Only the server can authorize starting the match.
        //
        // Requirement 2:
        // The game must currently be in the Lobby phase.
        if (!IsServer || State.Value != MatchState.Lobby)
            return false;

        // Requirement 3:
        // At least two players must be connected.
        //
        // This includes the host.
        if (Players.Count < 2)
            return false;

        // Requirement 4:
        // Every non-host player must be ready.
        foreach (var player in Players)
        {
            // Skip the host because the host
            // is considered automatically ready.
            if (player.ClientId == NetworkManager.ServerClientId)
                continue;

            // If even one client isn't ready,
            // the match cannot begin.
            if (!player.Ready)
                return false;
        }

        // If you made it this far,
        // all requirements were satisfied
        return true;
    }

    public void StartMatch()
    {
        // Called by GameManager when the host
        // presses the Play Match button.
        //
        // Only the server can pass CanStartMatch().
        if (!CanStartMatch()) return;

        // Transition the match state:
        //
        // Lobby --> Rolling
        //
        // NetworkVariable synchronizes this change,
        // allowing every client's UI to show
        // the rolling/winner-selection screen.
        State.Value = MatchState.Rolling;

        // Begin the winner-selection coroutine.
        StartCoroutine(RollForWinner());
    }

    private IEnumerator RollForWinner()
    {
        // Wait three seconds before selecting a winner.
        //
        // This gives players time to see the rolling animation.
        //
        // WaitForSeconds uses scaled game time.
        yield return new WaitForSeconds(3.0f);

        // Create a temporary list of eligible players.
        //
        // This is a normal C# List, not a NetworkList.
        //
        // It only needs to exist on the server because
        // the server is responsible for selecting the winner.
        var eligible = new System.Collections.Generic.List<LobbyPlayer>();

        // Examine every player currently in the lobby
        foreach (var player in Players)
        {
            // Include the host automatically
            //
            // Include non-host players only if they're ready.
            if (player.ClientId == NetworkManager.ServerClientId || player.Ready)
            {
                eligible.Add(player);
            }
        }

        // SAFETY CHECK
        //
        // If no eligible players remain,
        // return to the lobby instead of selecting
        // an invalid random index.
        if (eligible.Count == 0)
        {
            State.Value = MatchState.Lobby;

            // Stop executing the coroutine
            yield break;
        }

        // Select a random index from the eligible list.
        //
        // For Unity's integer Random.Range:
        // Minimum is inclusive.
        // Maximum is exclusive.
        //
        // Example:
        // eligible.Count = 3
        // Possible indices: 0, 1, 2
        int index = UnityEngine.Random.Range(0, eligible.Count);

        // Store the selected player's username.
        //
        // Because Winner is a NetworkVariable,
        // the result is synchronized to all clients.
        Winner.Value = eligible[index].Username;

        // Transition the game:
        //
        // Rolling --> Results
        //
        // Every connected client receives the state change.
        // GameManager responds by displaying the Results panel.
        State.Value = MatchState.Results;
    }

    public void ReturnToLobby()
    {
        // SERVER-ONLY RESET
        //
        // Called when the host chooses to return
        // to the lobby after displaying the results.

        // Only the server can reset the match.
        //
        // The game must currently be in Results.
        if (!IsServer || State.Value != MatchState.Results) return;

        // Reset every player's readiness.
        for (int i = 0; i < Players.Count; i++)
        {
            // Retrieve the player struct
            var player = Players[i];

            // Set the player to unready
            player.Ready = false;

            // Write the updated struct back
            // so Netcode synchronizes the change
            Players[i] = player;
        }

        // Clear the previous winner.
        //
        // default resets FixedString64Bytes
        // to its default empty value.
        Winner.Value = default;

        // Transition the game:
        //
        // Results --> Lobby
        //
        // Players can now ready up for another match.
        State.Value = MatchState.Lobby;
    }
}
