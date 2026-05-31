using System;
using System.Linq.Dynamic.Core.CustomTypeProviders;
using System.Numerics;
using ReAgent.State;

namespace ReAgent.SideEffects;

[DynamicLinqType]
[Api]
[method: Api]
public record ChronomancerPendulumSideEffect(
    Vector2 Position,
    Vector2 Size,
    float PendulumT,
    int CastPercent,
    int AoePercent,
    bool DrawLabels = true,
    string TrackColor = "DimGray",
    string MarkerColor = "Gold",
    string CastLabelColor = "LightSkyBlue",
    string AoeLabelColor = "Orange") : ISideEffect
{
    public SideEffectApplicationResult Apply(RuleState state)
    {
        state.InternalState.ChronomancerPendulumsToDisplay.Add((
            Position,
            Size,
            Math.Clamp(PendulumT, 0f, 1f),
            CastPercent,
            AoePercent,
            DrawLabels,
            TrackColor,
            MarkerColor,
            CastLabelColor,
            AoeLabelColor));
        return SideEffectApplicationResult.AppliedDuplicate;
    }

    public override string ToString() =>
        $"Chronomancer pendulum t={PendulumT:0.###} (cast {CastPercent}, AoE {AoePercent}) at {Position}";
}
