using Serein.Core;

namespace Serein.App;

public enum CurveLayer { Filters, Adjustment, ImportedGraphic }
public sealed record CurveLayerChoice(CurveLayer Layer, string Label);

public static class CurveEditing
{
    public static bool HasGain(EqFilter filter) => filter.Type is "PK" or "LS" or "HS" or "LSC" or "HSC";

    // Handles sit on the combined response, so dragging must subtract the other stages.
    public static void MoveFilter(Profile profile, EqFilter filter, double hz, double totalDb, bool moveFrequency = true)
    {
        if (moveFrequency) filter.Frequency = Math.Clamp(hz, 20, 20000);
        if (!HasGain(filter)) return;
        double ownTarget = totalDb - Response.EffectivePreamp(profile) - (Response.RawAt(profile, filter.Frequency) - Response.FilterAt(filter, filter.Frequency));
        var probe = new EqFilter { Type = filter.Type, Frequency = filter.Frequency, Q = filter.Q, UseQ = filter.UseQ };
        double low = -100, high = 100;
        for (int i = 0; i < 24; i++)
        {
            probe.Gain = (low + high) / 2;
            if (Response.FilterAt(probe, probe.Frequency) < ownTarget) low = probe.Gain; else high = probe.Gain;
        }
        filter.Gain = Math.Round((low + high) / 2, 2);
    }

    public static bool ChangeQ(EqFilter filter, int wheelDelta)
    {
        double next = Math.Clamp(Math.Round(filter.Q * Math.Pow(2, wheelDelta / 120.0 / 8), 3), .01, 100);
        if (next == filter.Q) return false;
        filter.Type = filter.Type switch { "LP" => "LPQ", "HP" => "HPQ", _ => filter.Type };
        filter.UseQ = true; filter.Q = next; return true;
    }

    public static void MoveGraphic(Profile profile, EqPoint point, bool moveFrequency, double hz, double totalDb)
    {
        if (moveFrequency)
        {
            int index = profile.Graphic.IndexOf(point);
            if (index < 0) return;
            double low = index == 0 ? 20 : Math.Max(20, profile.Graphic[index - 1].Frequency * 1.000001);
            double high = index == profile.Graphic.Count - 1 ? 20000 : Math.Min(20000, profile.Graphic[index + 1].Frequency / 1.000001);
            if (low <= high) point.Frequency = Math.Clamp(hz, low, high);
        }
        double others = Response.RawAt(profile, point.Frequency) - point.Gain;
        point.Gain = Math.Round(Math.Clamp(totalDb - Response.EffectivePreamp(profile) - others, -100, 100), 2);
    }
}
