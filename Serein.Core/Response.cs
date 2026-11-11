using System.Numerics;

namespace Serein.Core;

public static class Response
{
    public static double[] BandFrequencies(int count) => count == 31
        ? [20,25,31.5,40,50,63,80,100,125,160,200,250,315,400,500,630,800,1000,1250,1600,2000,2500,3150,4000,5000,6300,8000,10000,12500,16000,20000]
        : [31.5,63,125,250,500,1000,2000,4000,8000,16000];
    public static double GraphicAt(IList<EqPoint> points, double hz)
    {
        if (points.Count == 0) return 0;
        if (hz <= points[0].Frequency) return points[0].Gain;
        for (int i = 1; i < points.Count; i++) if (hz <= points[i].Frequency)
        { double t = Math.Log(hz / points[i-1].Frequency) / Math.Log(points[i].Frequency / points[i-1].Frequency); return points[i-1].Gain * (1-t) + points[i].Gain * t; }
        return points[^1].Gain;
    }
    public static double FilterAt(EqFilter f, double hz, double sampleRate = 48000) => PrepareFilter(f, sampleRate)(hz);
    static Func<double, double> PrepareFilter(EqFilter f, double sampleRate)
    {
        if (!f.Enabled) return _ => 0;
        double a = Math.Pow(10, f.Gain / 40), fc = Math.Min(f.Frequency, sampleRate * .499), q = f.Q;
        // APO LS/HS with explicit Q use corner frequency; LSC/HSC use centre frequency.
        if (f.UseQ && f.Type is "LS" or "HS")
        {
            double slope = 1 / ((1 / (q*q) - 2) / (a + 1/a) + 1);
            double factor = Math.Pow(10, Math.Abs(f.Gain) / 80 / slope);
            fc = Math.Min(sampleRate * .499, fc * (f.Type == "LS" ? factor : 1 / factor));
        }
        double w = 2 * Math.PI * fc / sampleRate, c = Math.Cos(w), sn = Math.Sin(w);
        double alpha = sn / (2 * q);
        if (!f.UseQ)
        {
            alpha = f.Type is "LS" or "HS" or "LSC" or "HSC" ? sn / 2 * Math.Sqrt((a + 1/a) * (1/.9 - 1) + 2) : sn / (2 * Math.Sqrt(.5));
            if (f.Type == "NO") alpha = sn / 60;
            if (f.Type is "AP" or "PK") alpha = sn * Math.Sinh(Math.Log(2) / 2 * w / sn);
        }
        double beta = 2 * Math.Sqrt(a) * alpha;
        (double b0, double b1, double b2, double a0, double a1, double a2) = f.Type switch
        {
            "PK" => (1+alpha*a, -2*c, 1-alpha*a, 1+alpha/a, -2*c, 1-alpha/a),
            "LP" or "LPQ" => ((1-c)/2, 1-c, (1-c)/2, 1+alpha, -2*c, 1-alpha),
            "HP" or "HPQ" => ((1+c)/2, -(1+c), (1+c)/2, 1+alpha, -2*c, 1-alpha),
            "BP" => (alpha, 0, -alpha, 1+alpha, -2*c, 1-alpha),
            "NO" => (1, -2*c, 1, 1+alpha, -2*c, 1-alpha),
            "AP" => (1-alpha, -2*c, 1+alpha, 1+alpha, -2*c, 1-alpha),
            "LS" or "LSC" => (a*((a+1)-(a-1)*c+beta), 2*a*((a-1)-(a+1)*c), a*((a+1)-(a-1)*c-beta), (a+1)+(a-1)*c+beta, -2*((a-1)+(a+1)*c), (a+1)+(a-1)*c-beta),
            "HS" or "HSC" => (a*((a+1)+(a-1)*c+beta), -2*a*((a-1)+(a+1)*c), a*((a+1)+(a-1)*c-beta), (a+1)-(a-1)*c+beta, 2*((a-1)-(a+1)*c), (a+1)-(a-1)*c-beta),
            _ => throw new InvalidDataException("Filtertyp unbekannt")
        };
        return hz =>
        {
            var z = Complex.FromPolarCoordinates(1, -2 * Math.PI * hz / sampleRate);
            return Math.Clamp(20 * Math.Log10(Math.Max(1e-10, ((b0 + b1*z + b2*z*z)/(a0 + a1*z + a2*z*z)).Magnitude)), -200, 200);
        };
    }
    public static double RawAt(Profile p, double hz, double sampleRate = 48000) => p.Filters.Sum(f => FilterAt(f, hz, sampleRate)) + GraphicAt(p.Graphic, hz) + GraphicAt(p.Adjustment, hz);
    public static Func<double, double> Compile(Profile p, double sampleRate = 48000)
    {
        var filters = p.Filters.Where(f => f.Enabled).Select(f => PrepareFilter(f, sampleRate)).ToArray();
        return hz => filters.Sum(filter => filter(hz)) + GraphicAt(p.Graphic, hz) + GraphicAt(p.Adjustment, hz);
    }
    public static double Peak(Profile p, double sampleRate = 48000)
    {
        // Dense grid plus exact filter centres catches narrow resonances better than the rendered curve.
        var response = Compile(p, sampleRate);
        return Enumerable.Range(0, 2049).Select(i => 20 * Math.Pow(1000, i/2048.0)).Concat(p.Filters.Select(f => f.Frequency)).Max(response);
    }
    public static double EffectivePreamp(Profile p) => p.AutoHeadroom ? Math.Min(p.Preamp, -Math.Max(0, Peak(p)) - .2) : p.Preamp;
}
