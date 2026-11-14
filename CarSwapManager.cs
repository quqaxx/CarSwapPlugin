using System.Numerics;
using System.Reflection;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Utils;
using CarSwapPlugin.Packets;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace CarSwapPlugin;

public class CarSwapManager : BackgroundService
{
    private const byte DenyInvalid = 1;
    private const byte DenySameCar = 2;
    private const byte DenyFull = 3;
    private const byte DenyCooldown = 4;
    private const int RestoreWindowSeconds = 60;
    private const float MaxRestoreDistanceMeters = 100;

    private sealed record PendingRestore(string Model, Vector3 Position, Vector3 Direction, DateTime At);

    private readonly CarSwapConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    private readonly SlotReservations _reservations;
    private readonly string[] _models;
    private readonly (byte Used, byte Total)[] _lastSent;
    private readonly Dictionary<ulong, DateTime> _lastSwap = new();
    private readonly Dictionary<ulong, PendingRestore> _pendingRestores = new();
    private readonly object _swapLock = new();

    public CarSwapManager(CarSwapConfiguration configuration,
        ACServerConfiguration serverConfiguration,
        EntryCarManager entryCarManager,
        SlotReservations reservations,
        CSPServerScriptProvider scriptProvider,
        CSPClientMessageTypeManager cspClientMessageTypeManager)
    {
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _reservations = reservations;

        if (!serverConfiguration.Extra.EnableClientMessages)
        {
            throw new ConfigurationException("CarSwapPlugin requires ClientMessages to be enabled (EnableClientMessages: true)");
        }

        var aiEnabled = serverConfiguration.Extra.EnableAi;
        _models = serverConfiguration.EntryList.Cars
            .Where(c => !(aiEnabled && c.AiMode == AiMode.Fixed))
            .Select(c => c.Model)
            .Distinct()
            .Where(m => configuration.AllowedModels.Count == 0 || configuration.AllowedModels.Contains(m))
            .Take(255)
            .ToArray();

        _lastSent = new (byte, byte)[_models.Length];
        Array.Fill(_lastSent, ((byte)255, (byte)255));

        var script = File.ReadAllText(Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "lua", "carswap.lua"));
        scriptProvider.AddScript(script, "carswap.lua", new Dictionary<string, object>
        {
            ["models"] = $"\"{string.Join(';', _models)}\""
        });

        cspClientMessageTypeManager.RegisterOnlineEvent<CarSwapHelloPacket>(OnHello);
        cspClientMessageTypeManager.RegisterOnlineEvent<CarSwapRequestPacket>(OnRequest);

        // a reservation is used up as soon as the player has connected again
        _entryCarManager.ClientConnected += (client, _) =>
        {
            _reservations.ReleaseForGuid(client.Guid);

            // log how long approve -> reconnect took (measurement for the swap speed)
            lock (_swapLock)
            {
                if (_lastSwap.TryGetValue(client.Guid, out var approvedAt))
                {
                    var seconds = (DateTime.UtcNow - approvedAt).TotalSeconds;
                    if (seconds < _configuration.ReservationSeconds)
                        client.Logger.Information("{ClientName} swap finished: approved -> reconnected in {Seconds:F1}s", client.Name, seconds);
                }
            }
        };

        Log.Information("CarSwapPlugin: {Count} swappable models", _models.Length);
    }

    private static bool IsFixedAi(EntryCar car) => car.AiMode == AiMode.Fixed;

    private (byte Used, byte Total) GetSlots(string model)
    {
        int used = 0, total = 0;
        foreach (var car in _entryCarManager.EntryCars)
        {
            if (car.Model != model || IsFixedAi(car)) continue;

            total++;
            if (car.Client != null || _reservations.IsReserved(car)) used++;
        }

        return ((byte)Math.Min(used, 255), (byte)Math.Min(total, 255));
    }

    private void OnHello(ACTcpClient client, CarSwapHelloPacket packet)
    {
        for (int i = 0; i < _models.Length; i++)
        {
            var (used, total) = GetSlots(_models[i]);
            client.SendPacket(new CarSwapSlotsPacket { ModelIndex = (byte)i, Used = used, Total = total });
        }

        // after a swap: put the new car where the old one was
        PendingRestore? restore = null;
        lock (_swapLock)
        {
            if (_pendingRestores.TryGetValue(client.Guid, out var pending))
            {
                if ((DateTime.UtcNow - pending.At).TotalSeconds > RestoreWindowSeconds)
                {
                    _pendingRestores.Remove(client.Guid);
                }
                else if (pending.Model == client.EntryCar.Model)
                {
                    _pendingRestores.Remove(client.Guid);
                    restore = pending;
                }
            }
        }

        if (restore != null)
        {
            client.SendPacket(new CarSwapRestorePacket { Position = restore.Position, Direction = restore.Direction });
        }
    }

    private void OnRequest(ACTcpClient client, CarSwapRequestPacket packet)
    {
        if (packet.ModelIndex >= _models.Length)
        {
            Deny(client, DenyInvalid);
            return;
        }

        var model = _models[packet.ModelIndex];
        if (client.EntryCar.Model == model)
        {
            Deny(client, DenySameCar);
            return;
        }

        EntryCar? target;
        lock (_swapLock)
        {
            var now = DateTime.UtcNow;
            if (_lastSwap.TryGetValue(client.Guid, out var last) && (now - last).TotalSeconds < _configuration.CooldownSeconds)
            {
                Deny(client, DenyCooldown);
                return;
            }

            target = _entryCarManager.EntryCars.FirstOrDefault(c =>
                c.Model == model && !IsFixedAi(c) && c.Client == null && !_reservations.IsReserved(c));

            if (target == null || !_reservations.TryReserve(target, client.Guid, TimeSpan.FromSeconds(_configuration.ReservationSeconds)))
            {
                Deny(client, DenyFull);
                return;
            }

            _lastSwap[client.Guid] = now;

            // only trust the claimed position if it matches where the server sees the car
            var claimedOk = Vector3.Distance(packet.Position, client.EntryCar.Status.Position) <= MaxRestoreDistanceMeters;
            if (claimedOk && packet.Direction != Vector3.Zero)
                _pendingRestores[client.Guid] = new PendingRestore(model, packet.Position, packet.Direction, now);
            else
                _pendingRestores.Remove(client.Guid);
        }

        client.Logger.Information("{ClientName} swaps {OldModel} -> {NewModel} (slot {SessionId})",
            client.Name, client.EntryCar.Model, model, target.SessionId);

        client.SendPacket(new CarSwapGoPacket
        {
            ModelIndex = packet.ModelIndex,
            Delay = _configuration.ReconnectDelaySeconds
        });
    }

    private static void Deny(ACTcpClient client, byte reason) =>
        client.SendPacket(new CarSwapDenyPacket { Reason = reason });

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // broadcast slot changes (connects, disconnects, reservations, expiries) once per second
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                for (int i = 0; i < _models.Length; i++)
                {
                    var slots = GetSlots(_models[i]);
                    if (slots == _lastSent[i]) continue;

                    _lastSent[i] = slots;
                    _entryCarManager.BroadcastPacket(new CarSwapSlotsPacket
                    {
                        ModelIndex = (byte)i,
                        Used = slots.Used,
                        Total = slots.Total
                    });
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
