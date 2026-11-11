using System;
using Unity.Collections;
using Unity.Netcode;

public struct LobbyPlayer : INetworkSerializable, IEquatable<LobbyPlayer>
{
    public ulong ClientId;
    public FixedString64Bytes Username;
    public bool Ready;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref Username);
        serializer.SerializeValue(ref Ready);
    }

    public bool Equals(LobbyPlayer other)
    {
        return ClientId == other.ClientId &&
            Username.Equals(other.Username) &&
            Ready == other.Ready;
    }
}
