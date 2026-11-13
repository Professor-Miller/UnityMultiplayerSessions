
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
    public string Username { get; private set; } = "";

    public event Action OnSessionJoined;
    public event Action OnSessionLeft;

    private bool _busy;
    private Task _initializationTask;
    private Task<List<ISessionInfo>> _browseTask;

    private void Awake()
    {
        // SINGLETON VALIDATION
        //
        // If another SessionManager already exists,
        // destroy this duplicate and stop executing Awake().
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Register this object as the global SessionManager.
        Instance = this;
    }

    private void Start()
    {
        // Begin initializing Unity Services when the game starts.
        //
        // The underscore discards the returned Task because
        // we aren't waiting for initialization here.
        //
        // Other methods will await InitializeAsync()
        // before attempting network operations.
        _ = InitializeAsync();
    }

    public Task InitializeAsync()
    {
        // Return the existing initialization Task if one exists.
        //
        // The ??= operator means:
        // "If _initializationTask is null, assign a new Task."
        //
        // This prevents multiple callers from starting
        // Unity Services initialization simultaneously.
        return _initializationTask ??= InitializeServicesAsync();
    }

    private async Task InitializeServicesAsync()
    {
        try
        {
            // UNITY SERVICES INITIALIZATION
            //
            // Unity Multiplayer Services must be initialized
            // before we can use authentication or sessions.
            //
            // Only initialize if services aren't already ready.
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                // await pauses this method until initialization finishes.
                //
                // It does NOT freeze the Unity game loop.
                // Execution resumes after the Task completes.
                await UnityServices.InitializeAsync();
            }

            // AUTHENTICATION
            //
            // Unity Multiplayer Services require players
            // to have an authenticated Unity player identity.
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                // Sign in anonymously.
                //
                // This allows players to connect without
                // manually creating an account or entering credentials.
                //
                // Unity Authentication assigns the player an identity.
                // This is different from their chosen display username.
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        catch (Exception e)
        {
            // Log initialization failtures for debugging
            Debug.LogError($"[NETWORK] Initialization failed: {e}");

            // Clear the stored Task so a future call
            // can attempt initialization again.
            _initializationTask = null;

            // Rethrow the exception to the caller.
            //
            // This allows whichever method awaited initialization
            // to know that initialization failed.
            throw;
        }
    }

    public void SetUsername(string username)
    {
        // Store the username without leading/trailing spaces.
        //
        // ?. safely handles a null string.
        // ?? provides an empty string if the result is null.
        //
        // Example: "  Justin  " becomes "Justin".
        Username = username?.Trim() ?? "";
    }

    public async Task CreateRoom(string roomName)
    {
        // Prevent conflicting operations.
        //
        // Don't create a room if another session operation
        // is running OR the player is already in a session.
        if (_busy || CurrentSession != null) return;

        // Lock session operations until this one finishes.
        _busy = true;

        try
        {
            // Ensure Unity Services and Authentication are ready
            await InitializeAsync();

            // SESSION CONFIGURATION
            //
            // These options describe the session being created.
            var options = new SessionOptions
            {
                // Remove unncecessary whitespace from the room name
                Name = roomName.Trim(),

                // Maximum number of players, including the host
                MaxPlayers = 4,

                // False means the session can appear
                // in public session discoery queries
                IsPrivate = false
            }

            // Configure the session to use Unity Relay networking.
            //
            // Basically ignore all the IP stuff in the Transport
            .WithRelayNetwork();

            // Ask Unity Multiplayer Services to create the session.
            //
            // await waits for the asynchronous operation to finish.
            //
            // If successful, store the resulting active session.
            CurrentSession = await MultiplayerService.Instance.CreateSessionAsync(options);

            // Listen for events such as the session being deleted
            // or this player being removed
            SubscribeToSession();

            // Notify other scripts that we successfully joined
            // a session (as its host)
            OnSessionJoined?.Invoke();
        }

        catch (Exception e)
        {
            // Log errors such as failed authentication,
            // service failures, or unsuccessful room creation
            Debug.LogError($"Create failed: {e}");
        }

        finally
        {
            // ALWAYS release the busy flag.
            //
            // finally runs whether the operation succeeds
            // or an exception occurs.
            //
            // Without this, an error could permanently
            // prevent additional session operations.
            _busy = false;
        }
    }

    public Task<List<ISessionInfo>> BrowseRooms()
    {
        // If a browse request is already running,
        // return that same Task instead of starting another query.
        //
        // This prevents duplicate requests when players
        // repeatedly press the Refresh button.
        if (_browseTask != null && !_browseTask.IsCompleted) return _browseTask;

        // Start a new room query and remember its Task
        _browseTask = BrowseRoomsInternal();

        // Return the Task so callers can await the results
        return _browseTask;
    }

    private async Task<List<ISessionInfo>> BrowseRoomsInternal()
    {
        // Session discovery requires initialized Unity Services.
        await InitializeAsync();

        try
        {
            // Request a list of discoverable sessions.
            //
            // QuerySessionsOptions can be configured with filters,
            // sorting, and other query settings.
            //
            // Here, we're using the default options.
            var results = await MultiplayerService.Instance.QuerySessionsAsync(new QuerySessionsOptions());

            // Convert the returned session information
            // into a standard C# List.
            //
            // ISessionInfo contains room information such as
            // its name, ID, player capacity, and available slots.
            return new List<ISessionInfo>(results.Sessions);
        }

        catch (Exception e)
        {
            // Log the failure
            Debug.LogError($"[NETWORK] Browse failed: {e}");

            // Allow the caller to handle the error as well.
            throw;
        }
    }

    public async Task JoinRoom(string sessionId)
    {
        // Prevent joining while another session operation
        // is running or while already connected to a session
        if (_busy || CurrentSession != null) return;

        _busy = true;

        try
        {
            // Make sure services and authentication are ready
            await InitializeAsync();

            // Ask Unity Multiplayer Services to join
            // the session identified by sessionId
            //
            // This ID comes from the room browsing results.
            //
            // The session's configured netowrking integration
            // handles establishing the multiplayer connection
            CurrentSession = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId);

            // Subscribe to session lifecycle events.
            SubscribeToSession();

            // Notify listeners that the player joined a session
            //
            // GameManager uses this event to isplay the room UI
            OnSessionJoined?.Invoke();
        }

        catch (Exception e)
        {
            // Possible failures include a full room,
            // deleted session, or connection problem
            Debug.LogError($"Join failed: {e}");
        }

        finally
        {
            // Allow future session operations.
            _busy = false;
        }
    }

    public async Task LeaveRoom()
    {
        // Don't leave if another session operation is running
        // or if the player isn't currently in a session
        if (_busy || CurrentSession == null) return;

        _busy = true;

        try
        {
            // HOST VS CLIENT
            //
            // The host has different responsibilities
            // from an ordinary connected player
            if (CurrentSession.IsHost)
            {
                //HOST:
                // Delete the session itself
                //
                // This ends the hosted session rather than
                // removing the host as a participant
                await CurrentSession.AsHost().DeleteAsync();
            }
            else
            {
                // CLIENT:
                // Leave the session without deleting it
                //
                // The host and remaining playes can continue
                await CurrentSession.LeaveAsync();
            }
        }

        catch (Exception e)
        {
            // Log any errors encountered while leaving
            Debug.LogError($"[NETWORK] Leave failed: {e}");
        }

        finally
        {
            // Clear the local session reference and notify listeners.
            ClearSession();

            // Unlock future session operations
            _busy = false;
        }
    }

    private void SubscribeToSession()
    {
        // Listen for the session being deleted
        //
        // This can hapen when the host closes the session
        CurrentSession.Deleted += HandleSessionDeleted;

        // Listen for this player being removed from the session
        //
        // This can happen independently of the player
        // pressing the Leave Button
        CurrentSession.RemovedFromSession += HandleSessionDeleted;
    }

    private void HandleSessionDeleted()
    {
        // Both session deletion and removal require
        // the same local cleanup
        ClearSession();
    }

    private void ClearSession()
    {
        // Nothing to clean up if no active session exists
        if (CurrentSession == null) return;

        // EVENT UNSUBSCRIPTION
        //
        // Remove listeners from the old session before
        // discarding our reference to it
        //
        // This helps prevent stale callbacks and
        // unwanted event handling after leaving
        CurrentSession.Deleted -= HandleSessionDeleted;
        CurrentSession.RemovedFromSession -= HandleSessionDeleted;

        // Clear the active session reference
        //
        // Other scripts can now recognize that
        // this player is no longer in a session
        CurrentSession = null;

        // Notify all subscribers that the session ended
        //
        // GameManager listns to this event and
        // returns the player to the browsing screen
        OnSessionLeft?.Invoke();
    }

    private void OnDestroy()
    {
        // If this object is the registered singleton,
        // clear the global reference when it's destroyed.
        //
        // This prevents Instance from continuing to reference
        // a SessionManager that no longer exists.
        if (Instance == this) Instance = null;
    }
}
