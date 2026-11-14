using AssettoServer.Server;
using AssettoServer.Server.OpenSlotFilters;

namespace CarSwapPlugin;

public class CarSwapSlotFilter : OpenSlotFilterBase
{
    private readonly SlotReservations _reservations;

    public CarSwapSlotFilter(SlotReservations reservations)
    {
        _reservations = reservations;
    }

    public override async ValueTask<bool> IsSlotOpen(EntryCar entryCar, ulong guid)
    {
        if (_reservations.IsReservedForOther(entryCar, guid))
            return false;

        return await base.IsSlotOpen(entryCar, guid);
    }
}
