using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class LobbyPlayer : NetworkBehaviour
{
    [SerializeField] private Image readyStatus;

    public NetworkVariable<bool> IsReady = new(false);

    private Color _readyColor = Color.green;
    private Color _waitingColor = new Color(0.3f, 0.3f, 0.3f, 1.0f);

    void Start()
    {
        readyStatus.color = _waitingColor;
    }

    public void ToggleReady()
    {
        if (!IsOwner) return;

        SetReadyRpc(!IsReady.Value);
    }

    [Rpc(SendTo.Server)]
    private void SetReadyRpc(bool ready)
    {
        IsReady.Value = ready;
    }
}
