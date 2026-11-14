using AssettoServer.Server.OpenSlotFilters;
using AssettoServer.Server.Plugin;
using Autofac;
using Microsoft.Extensions.Hosting;

namespace CarSwapPlugin;

public class CarSwapModule : AssettoServerModule<CarSwapConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<SlotReservations>().AsSelf().SingleInstance();
        builder.RegisterType<CarSwapSlotFilter>().As<IOpenSlotFilter>().SingleInstance();
        builder.RegisterType<CarSwapManager>().AsSelf().As<IHostedService>().SingleInstance();
    }

    public override CarSwapConfiguration ReferenceConfiguration => new()
    {
        ReservationSeconds = 20,
        CooldownSeconds = 5,
        ReconnectDelaySeconds = 1,
        AllowedModels = []
    };
}
