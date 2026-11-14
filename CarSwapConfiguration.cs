using AssettoServer.Server.Configuration;
using FluentValidation;
using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace CarSwapPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class CarSwapConfiguration : IValidateConfiguration<CarSwapConfigurationValidator>
{
    [YamlMember(Description = "Seconds a slot stays reserved for a player after a swap was approved")]
    public int ReservationSeconds { get; init; } = 20;

    [YamlMember(Description = "Minimum seconds between two swaps of the same player")]
    public int CooldownSeconds { get; init; } = 5;

    [YamlMember(Description = "Seconds the client waits before it reconnects after a swap was approved")]
    public ushort ReconnectDelaySeconds { get; init; } = 1;

    [YamlMember(Description = "Car models players can swap to. Empty = every model that has player slots")]
    public List<string> AllowedModels { get; init; } = [];
}

public class CarSwapConfigurationValidator : AbstractValidator<CarSwapConfiguration>
{
    public CarSwapConfigurationValidator()
    {
        RuleFor(cfg => cfg.ReservationSeconds).InclusiveBetween(5, 120);
        RuleFor(cfg => cfg.CooldownSeconds).GreaterThanOrEqualTo(0);
    }
}
