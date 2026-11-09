using FogColdControl;

int checks = 0;
void Check(bool condition, string scenario)
{
    if (!condition) throw new Exception(scenario);
    checks++;
}
void Expect(FogSpeedSamples samples, int origin, double time, float? expected, string scenario, float tolerance = 0.001f)
{
    bool found = samples.TryGetSpeed(origin, time, out float actual);
    Check(found == expected.HasValue && (!found || Math.Abs(actual - expected.Value) <= tolerance),
        $"{scenario}: expected {expected?.ToString() ?? "hidden"}, got {(found ? actual.ToString() : "hidden")}");
}

foreach (float value in new[] { 0f, 0.001f, 0.3f, 0.4f, 10f, float.MaxValue })
    Check(FogSpeedSamples.IsValidSpeed(value), $"valid host speed {value}");
foreach (float value in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
    Check(!FogSpeedSamples.IsValidSpeed(value), $"invalid host speed {value}");

var samples = new FogSpeedSamples();
Expect(samples, 0, 0, null, "no snapshot means no invented local default");
samples.Observe(0, 800, true, 0);
Expect(samples, 0, 0, null, "one snapshot cannot establish speed");
samples.Observe(0, 799, true, 2.5);
Expect(samples, 0, 2.5, null, "short window is not reliable");
samples.Observe(0, 798, true, 5);
Expect(samples, 0, 5, 0.4f, "actual synchronized radius change");
Expect(samples, 0, 10, 0.4f, "local interpolation between packets does not alter samples");
Expect(samples, 0, 18, null, "stale network data is hidden");
Expect(samples, 1, 5, null, "different origin cannot reuse old rate");
Expect(samples, 0, 4, null, "clock moved behind latest snapshot");
Expect(samples, 0, double.NaN, null, "invalid read time");

samples.Observe(0, 797, false, 6);
Expect(samples, 0, 6, null, "stopped fog resets samples");
samples.Observe(0, 797, true, 6);
samples.Observe(0, 795.5f, true, 11);
Expect(samples, 0, 11, 0.3f, "restart obtains a fresh rate");
samples.Observe(1, 900, true, 12);
Expect(samples, 1, 12, null, "origin transition starts a new window");
samples.Observe(1, 898.5f, true, 17);
Expect(samples, 1, 17, 0.3f, "new origin rate");
samples.Observe(1, 910, true, 18);
Expect(samples, 1, 18, null, "upward radius correction is not negative speed");
samples.Observe(1, 908, true, 23);
Expect(samples, 1, 23, 0.4f, "resumes after correction");
samples.Observe(1, 900, true, 50);
Expect(samples, 1, 50, null, "long packet gap starts a new measurement");
samples.Observe(1, 898, true, 55);
Expect(samples, 1, 55, 0.4f, "recovery after packet gap");
samples.Observe(1, 897, true, 54);
Expect(samples, 1, 54, null, "backward clock resets measurement");
samples.Reset();
Expect(samples, 1, 55, null, "room or master change resets measurement");

// Vanilla sends radius snapshots every five seconds. The guest's own default
// speed and player movement have no input into this calculation.
samples.Observe(0, 800, true, 0);
samples.Observe(0, 798.5f, true, 5);
Expect(samples, 0, 5, 0.3f, "vanilla host sample cadence");
samples.Observe(0, 798.5f, true, 5);
Expect(samples, 0, 5, 0.3f, "same-frame duplicate avoids zero-time division");
samples.Observe(0, 796.5f, true, 10);
Expect(samples, 0, 10, 0.4f, "changed rate replaces old window");

// Many small updates with alternating receipt jitter should not turn a single
// delayed packet into a spike in the displayed speed.
samples.Reset();
for (int i = 0; i <= 80; i++)
{
    double received = i * 0.2 + (i % 2 == 0 ? 0 : 0.015);
    samples.Observe(0, 800f - i * 0.08f, true, received);
}
Expect(samples, 0, 16, 0.4f, "window smooths packet jitter", 0.01f);
samples.Observe(0, float.NaN, true, 17);
Expect(samples, 0, 17, null, "invalid radius cannot corrupt next rate");
samples.Observe(0, 800, true, double.PositiveInfinity);
Expect(samples, 0, 18, null, "invalid observation timestamp");
samples.Observe(0, 800, true, 20);
samples.Observe(0, 800, true, 25);
Expect(samples, 0, 25, null, "stationary snapshots do not claim moving speed");

Console.WriteLine($"PASS: {checks} guest fog speed regression checks.");
