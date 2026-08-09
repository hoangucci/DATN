using System;
using Unity.Netcode;

namespace MidnightChaos.Inventory
{
    public enum VerticalSliceItemId : byte
    {
        None = 0,
        Rock = 1,
        Wood = 2,
        Ore = 3,
        Workbench = 4,
        ChaosShard = 5
    }

    [Serializable]
    public struct VerticalSliceInventorySlot : INetworkSerializable,
        IEquatable<VerticalSliceInventorySlot>
    {
        public VerticalSliceItemId Item;
        public ushort Amount;

        public VerticalSliceInventorySlot(
            VerticalSliceItemId item,
            int amount)
        {
            Item = item;
            Amount = (ushort)System.Math.Clamp(amount, 0, ushort.MaxValue);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref Item);
            serializer.SerializeValue(ref Amount);
        }

        public bool Equals(VerticalSliceInventorySlot other)
        {
            return Item == other.Item && Amount == other.Amount;
        }
    }
}
