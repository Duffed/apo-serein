namespace Serein.Core;

public sealed record FilterSetting(string Type, double Frequency, double Gain, double Q, bool Enabled, bool UseQ);
public sealed record PointSetting(double Frequency, double Gain);
public sealed record EqSettings(double Preamp, bool AutoHeadroom, FilterSetting[] Filters, PointSetting[] Graphic, PointSetting[] Adjustment)
{
    public static EqSettings Capture(Profile p) => new(p.Preamp, p.AutoHeadroom,
        p.Filters.Select(f => new FilterSetting(f.Type, f.Frequency, f.Gain, f.Q, f.Enabled, f.UseQ)).ToArray(),
        p.Graphic.Select(f => new PointSetting(f.Frequency, f.Gain)).ToArray(), p.Adjustment.Select(f => new PointSetting(f.Frequency, f.Gain)).ToArray());
    public bool Same(EqSettings other, bool ignoreNeutralAdjustment = false) => Preamp == other.Preamp && AutoHeadroom == other.AutoHeadroom
        && Filters.SequenceEqual(other.Filters) && Graphic.SequenceEqual(other.Graphic)
        && (Adjustment.SequenceEqual(other.Adjustment) || ignoreNeutralAdjustment && Adjustment.All(p => p.Gain == 0) && other.Adjustment.All(p => p.Gain == 0));
    public void Restore(Profile p)
    {
        p.Preamp = Preamp; p.AutoHeadroom = AutoHeadroom;
        p.Filters.Clear(); foreach (var f in Filters) p.Filters.Add(new() { Type = f.Type, Frequency = f.Frequency, Gain = f.Gain, Q = f.Q, Enabled = f.Enabled, UseQ = f.UseQ });
        p.Graphic.Clear(); foreach (var f in Graphic) p.Graphic.Add(new() { Frequency = f.Frequency, Gain = f.Gain });
        p.Adjustment.Clear(); foreach (var f in Adjustment) p.Adjustment.Add(new() { Frequency = f.Frequency, Gain = f.Gain });
    }
    public void Validate() { var probe = new Profile(); Restore(probe); ApoFormat.Validate(probe); }
}
