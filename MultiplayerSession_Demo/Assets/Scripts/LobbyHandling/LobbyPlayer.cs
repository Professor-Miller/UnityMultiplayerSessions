using System;
using Unity.Collections;
using Unity.Netcode;

public struct LobbyPlayer : INetworkSerializable, IEquatable<LobbyPlayer>
{
    public ulong ClientId;
    public FixedString64Bytes Username;
    public bool Ready;

    // NETWORK SERIALIZATION
    //
    // Serialization converts data into a representation
    // that can be transmitted over the network.
    //
    // Deserialization reconstructs that data when received.
    //
    // Netcode calls this method when it needs to
    // read or write a LobbyPlayer's network data.
    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        // SERIALIZE CLIENT ID
        //
        // Writes or reads the player's network connection ID.
        //
        // 'ref' allows the serializer to access the field
        // directly and update it when deserializing.
        serializer.SerializeValue(ref ClientId);

        // SERIALIZE USERNAME
        //
        // Writes or reads the player's display name.
        //
        // FixedString64Bytes is supported by Netcode's
        // serialization system.
        serializer.SerializeValue(ref Username);

        // SERIALIZE READY STATUS
        //
        // Writes or reads the player's readiness value.
        serializer.SerializeValue(ref Ready);

        // IMPORTANT:
        // The order of serialization must be consistent
        // between the sender and receiver.
        //
        // Both sides must interpret the transmitted data
        // using the same field order and data types.
    }

    public bool Equals(LobbyPlayer other)
    {
        return ClientId == other.ClientId &&
            Username.Equals(other.Username) &&
            Ready == other.Ready;
    }
}
