using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance { get; private set; }

    public ISession CurrentSession { get; private set; }
    public string Username { get; private set; }

    public event Action OnSessionJoined;
    public event Action OnSessionLeft;

    private bool _busy;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;
    }

    public async Task InitializeAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    public void SetUsername(string username)
    {
        Username = username.Trim();
    }

    public async Task CreateRoom(string roomName)
    {
        if (_busy || CurrentSession != null) return;
        _busy = true;

        try
        {
            await InitializeAsync();

            var options = new SessionOptions
            {
                Name = roomName.Trim(),
                MaxPlayers = 4,
                IsPrivate = false
            }
            .WithRelayNetwork();

            CurrentSession =
                await MultiplayerService.Instance.CreateSessionAsync(options);

            SubscribeToSession();

            Debug.Log($"Created: {CurrentSession.Name}");
            Debug.Log($"Join Code: {CurrentSession.Code}");

            OnSessionJoined?.Invoke();
        }

        catch (Exception e)
        {
            Debug.LogError($"Create failed: {e}");
        }

        finally
        {
            _busy = false;
        }
    }

    public async Task<List<ISessionInfo>> BrowseRooms()
    {
        await InitializeAsync();

        var results = await MultiplayerService.Instance
            .QuerySessionsAsync(new QuerySessionsOptions());

        return new List<ISessionInfo>(results.Sessions);
    }

    public async Task JoinRoom(string sessionId)
    {
        if (_busy || CurrentSession != null) return;
        _busy = true;

        try
        {
            await InitializeAsync();

            CurrentSession = await MultiplayerService.Instance
                .JoinSessionByIdAsync(sessionId);

            SubscribeToSession();
            OnSessionJoined?.Invoke();
        }

        catch (Exception e)
        {
            Debug.LogError($"Join failed: {e}");
        }

        finally
        {
            _busy = false;
        }
    }

    public async Task LeaveRoom()
    {
        if (_busy || CurrentSession == null) return;
        _busy = true;

        try
        {
            if (CurrentSession.IsHost)
                await CurrentSession.AsHost().DeleteAsync();
            else
                await CurrentSession.LeaveAsync();
        }

        catch (Exception e)
        {
            Debug.LogError($"Leave failed: {e}");
        }

        finally
        {
            ClearSession();
            _busy = false;
        }
    }

    private void SubscribeToSession()
    {
        CurrentSession.Deleted += HandleSessionDeleted;
        CurrentSession.RemovedFromSession += HandleSessionDeleted;
    }

    private void HandleSessionDeleted()
    {
        ClearSession();
    }

    private void ClearSession()
    {
        if (CurrentSession == null) return;

        CurrentSession.Deleted -= HandleSessionDeleted;
        CurrentSession.RemovedFromSession -= HandleSessionDeleted;

        CurrentSession = null;
        OnSessionLeft?.Invoke();
    }
}
