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
        Players = new NetworkList<LobbyPlayer>();
    }

    public override void OnNetworkSpawn()
    {
        Players.OnListChanged += OnPlayersChanged;
        State.OnValueChanged += OnStateChanged;
        Winner.OnValueChanged += OnWinnerChanged;

        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback += PlayerConnected;
            NetworkManager.OnClientDisconnectCallback += PlayerDisconnected;

            foreach (ulong id in NetworkManager.ConnectedClientsIds)
            {
                PlayerConnected(id);
            }
        }

        if (IsClient)
        {
            RegisterUsernameRpc(SessionManager.Instance.Username);
        }

        OnLobbyChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        Players.OnListChanged -= OnPlayersChanged;
        State.OnValueChanged -= OnStateChanged;
        Winner.OnValueChanged -= OnWinnerChanged;

        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback -= PlayerConnected;
            NetworkManager.OnClientDisconnectCallback -= PlayerDisconnected;
        }
    }

    private void OnPlayersChanged(NetworkListEvent<LobbyPlayer> change)
    {
        OnLobbyChanged?.Invoke();
    }

    private void OnStateChanged(MatchState oldState, MatchState newState)
    {
        OnLobbyChanged?.Invoke();
    }

    private void OnWinnerChanged(FixedString64Bytes oldWinner, FixedString64Bytes newWinner)
    {
        OnLobbyChanged?.Invoke();
    }

    private void PlayerConnected(ulong id)
    {
        if (State.Value != MatchState.Lobby)
        {
            NetworkManager.DisconnectClient(id);
            return;
        }

        foreach (var player in Players)
        {
            if (player.ClientId == id) return;
        }

        Players.Add(new LobbyPlayer
        {
            ClientId = id,
            Username = new FixedString64Bytes($"Player {id}"),
            Ready = false
        });
    }

    private void PlayerDisconnected(ulong id)
    {
        for (int i = Players.Count - 1; i >= 0; i--)
        {
            if (Players[i].ClientId == id)
            {
                Players.RemoveAt(i);
            }
        }
    }

    [Rpc(SendTo.Server)]
    private void RegisterUsernameRpc(string username, RpcParams rpcParams = default)
    {
        ulong id = rpcParams.Receive.SenderClientId;

        if (string.IsNullOrWhiteSpace(username))
            return;

        username = username.Trim();

        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].ClientId != id)
                continue;

            var player = Players[i];
            player.Username = new FixedString64Bytes(username);
            Players[i] = player;
            return;
        }

        Players.Add(new LobbyPlayer
        {
            ClientId = id,
            Username = new FixedString64Bytes(username),
            Ready = false
        });
    }

    public void ToggleReady()
    {
        if (!IsClient || State.Value != MatchState.Lobby) return;

        ToggleReadyRpc();
    }

    [Rpc(SendTo.Server)]
    private void ToggleReadyRpc(RpcParams rpcParams = default)
    {
        if (State.Value != MatchState.Lobby) return;

        ulong id = rpcParams.Receive.SenderClientId;

        if (id == NetworkManager.ServerClientId) return;

        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].ClientId != id)
                continue;

            var player = Players[i];
            player.Ready = !player.Ready;
            Players[i] = player;

            return;
        }
    }

    public bool CanStartMatch()
    {
        if (!IsServer || State.Value != MatchState.Lobby)
            return false;

        if (Players.Count < 2)
            return false;

        foreach (var player in Players)
        {
            if (player.ClientId == NetworkManager.ServerClientId)
                continue;

            if (!player.Ready)
                return false;
        }

        return true;
    }

    public void StartMatch()
    {
        if (!CanStartMatch()) return;

        State.Value = MatchState.Rolling;
        StartCoroutine(RollForWinner());
    }

    private IEnumerator RollForWinner()
    {
        yield return new WaitForSeconds(3.0f);

        var eligible = new System.Collections.Generic.List<LobbyPlayer>();

        foreach (var player in Players)
        {
            if (player.ClientId == NetworkManager.ServerClientId || player.Ready)
            {
                eligible.Add(player);
            }
        }

        if (eligible.Count == 0)
        {
            State.Value = MatchState.Lobby;
            yield break;
        }

        int index = UnityEngine.Random.Range(0, eligible.Count);

        Winner.Value = eligible[index].Username;
        State.Value = MatchState.Results;
    }

    public void ReturnToLobby()
    {
        if (!IsServer || State.Value != MatchState.Results)
            return;

        for (int i = 0; i < Players.Count; i++)
        {
            var player = Players[i];
            player.Ready = false;
            Players[i] = player;
        }

        Winner.Value = default;
        State.Value = MatchState.Lobby;
    }

    private int FindPlayer(ulong id)
    {
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].ClientId == id) return i;
        }

        return -1;
    }
}
