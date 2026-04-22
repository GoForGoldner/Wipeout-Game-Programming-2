using System;
using Unity.Netcode;

[Serializable]
public struct PlayerPerkSelection : INetworkSerializable, IEquatable<PlayerPerkSelection>
{
    public ulong clientId;
    public int perkIndex;
    public bool isReady;

    public PlayerPerkSelection(ulong clientId, int perkIndex, bool isReady)
    {
        this.clientId = clientId;
        this.perkIndex = perkIndex;
        this.isReady = isReady;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref perkIndex);
        serializer.SerializeValue(ref isReady);
    }

    public bool Equals(PlayerPerkSelection other)
    {
        return clientId == other.clientId
            && perkIndex == other.perkIndex
            && isReady == other.isReady;
    }
}