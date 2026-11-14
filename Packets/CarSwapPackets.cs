using System.Numerics;
using AssettoServer.Network.ClientMessages;

namespace CarSwapPlugin.Packets;

// DON'T change field names/types of a packet without changing the key, the Lua struct must match.

/// <summary>Client script is ready and wants the full slot state.</summary>
[OnlineEvent(Key = "CarSwap_Hello")]
public class CarSwapHelloPacket : OnlineEvent<CarSwapHelloPacket>
{
    [OnlineEventField(Name = "dummy")]
    public byte Dummy;
}

/// <summary>Client asks to swap to the model at this index of the model list.</summary>
[OnlineEvent(Key = "CarSwap_Request")]
public class CarSwapRequestPacket : OnlineEvent<CarSwapRequestPacket>
{
    [OnlineEventField(Name = "modelIndex")]
    public byte ModelIndex;

    /// <summary>Where the player is right now, restored after the reconnect.</summary>
    [OnlineEventField(Name = "position")]
    public Vector3 Position;

    [OnlineEventField(Name = "direction")]
    public Vector3 Direction;
}

/// <summary>Slot usage of one model.</summary>
[OnlineEvent(Key = "CarSwap_Slots")]
public class CarSwapSlotsPacket : OnlineEvent<CarSwapSlotsPacket>
{
    [OnlineEventField(Name = "modelIndex")]
    public byte ModelIndex;

    [OnlineEventField(Name = "used")]
    public byte Used;

    [OnlineEventField(Name = "total")]
    public byte Total;
}

/// <summary>Swap approved: client should reconnect with the model at this index.</summary>
[OnlineEvent(Key = "CarSwap_Go")]
public class CarSwapGoPacket : OnlineEvent<CarSwapGoPacket>
{
    [OnlineEventField(Name = "modelIndex")]
    public byte ModelIndex;

    /// <summary>Seconds to wait before reconnecting.</summary>
    [OnlineEventField(Name = "delay")]
    public ushort Delay;
}

/// <summary>Swap denied. 1 = invalid model, 2 = already in this car, 3 = no free slot, 4 = cooldown.</summary>
[OnlineEvent(Key = "CarSwap_Deny")]
public class CarSwapDenyPacket : OnlineEvent<CarSwapDenyPacket>
{
    [OnlineEventField(Name = "reason")]
    public byte Reason;
}

/// <summary>Sent after the reconnect: put the new car where the old one was.</summary>
[OnlineEvent(Key = "CarSwap_Restore")]
public class CarSwapRestorePacket : OnlineEvent<CarSwapRestorePacket>
{
    [OnlineEventField(Name = "position")]
    public Vector3 Position;

    [OnlineEventField(Name = "direction")]
    public Vector3 Direction;
}
