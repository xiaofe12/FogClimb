using FogColdControl;

// Exercise the production resolver with data-only doubles. Unity lifecycle and
// HUD rendering still require verification inside PEAK.
int checks = 0;
void Expect(MapHandler map, Segment current, Biome.BiomeType? expected, string scenario)
{
    bool found = NextBiomeResolver.TryGetNextBiome(map, current, out var actual);
    if (found != expected.HasValue || (found && actual != expected.Value))
        throw new Exception($"{scenario}: expected {expected?.ToString() ?? "hidden"}, got {(found ? actual.ToString() : "hidden")}");
    checks++;
}

MapHandler MakeMap(params Biome.BiomeType[] biomes) => new MapHandler
{
    segments = biomes.Select(b => new MapHandler.MapSegment { value = b }).ToArray()
};

foreach (Biome.BiomeType next in Enum.GetValues<Biome.BiomeType>().Append((Biome.BiomeType)250))
{
    var map = MakeMap(Biome.BiomeType.Shore, next);
    Expect(map, Segment.Beach, next, $"custom next biome {next}");
}

var custom = MakeMap(Biome.BiomeType.Swamp, Biome.BiomeType.Temple, Biome.BiomeType.Shore,
    Biome.BiomeType.Roots, Biome.BiomeType.Mesa);
Expect(custom, Segment.Beach, Biome.BiomeType.Temple, "swamp at first stage");
Expect(custom, Segment.Tropics, Biome.BiomeType.Shore, "reordered shore");
Expect(custom, Segment.Alpine, Biome.BiomeType.Roots, "third transition");
Expect(custom, Segment.Caldera, Biome.BiomeType.Mesa, "custom fourth transition");
var normal = MakeMap(Biome.BiomeType.Shore, Biome.BiomeType.Tropics, Biome.BiomeType.Alpine,
    Biome.BiomeType.Volcano, Biome.BiomeType.Volcano);
Expect(normal, Segment.Beach, Biome.BiomeType.Tropics, "normal first transition");
Expect(normal, Segment.Tropics, Biome.BiomeType.Alpine, "normal second transition");
Expect(normal, Segment.Alpine, Biome.BiomeType.Volcano, "normal final biome");
Expect(normal, Segment.Caldera, null, "kiln repeats the final biome without a new transition");
custom.segments[1].value = Biome.BiomeType.Alpine;
Expect(custom, Segment.Beach, Biome.BiomeType.Alpine, "room changes are read without stale cache");
custom.segments[1].value = Biome.BiomeType.Swamp;
Expect(custom, Segment.Beach, Biome.BiomeType.Swamp, "repeated biomes are valid");

var variant = MakeMap(Biome.BiomeType.Shore, Biome.BiomeType.Tropics);
variant.segments[1].hasVariant = true;
variant.variantSegments = new[] { new MapHandler.MapSegment { value = Biome.BiomeType.Temple } };
Expect(variant, Segment.Beach, Biome.BiomeType.Tropics, "unselected variant uses base biome");
variant.biomes.Add(Biome.BiomeType.Temple);
Expect(variant, Segment.Beach, Biome.BiomeType.Temple, "loaded variant overrides base biome");
variant.segments[1].variantBiomeIndex = -1;
Expect(variant, Segment.Beach, null, "negative variant index");
variant.segments[1].variantBiomeIndex = 1;
Expect(variant, Segment.Beach, null, "variant index beyond array");
variant.segments[1].variantBiomeIndex = 0;
variant.variantSegments[0] = null;
Expect(variant, Segment.Beach, null, "missing variant entry");
variant.variantSegments = null;
Expect(variant, Segment.Beach, null, "variant array not loaded");
variant.variantSegments = new[] { new MapHandler.MapSegment { value = Biome.BiomeType.Mesa } };
variant.biomes = null;
Expect(variant, Segment.Beach, null, "biome selection not loaded");
variant.biomes = new();
variant.variantSegments[0].hasVariant = true;
Expect(variant, Segment.Beach, null, "variant cycle is rejected before invoking recursive getter");
variant.variantSegments = new[]
{
    new MapHandler.MapSegment { hasVariant = true, variantBiomeIndex = 1 },
    new MapHandler.MapSegment { hasVariant = true, variantBiomeIndex = 0 }
};
Expect(variant, Segment.Beach, null, "multiple variant cycle");
variant.variantSegments[1].hasVariant = false;
variant.variantSegments[1].value = Biome.BiomeType.Mesa;
variant.biomes.Add(Biome.BiomeType.Mesa);
Expect(variant, Segment.Beach, Biome.BiomeType.Mesa, "valid nested variant");

Expect(null, Segment.Beach, null, "map not loaded");
Expect(new MapHandler(), Segment.Beach, null, "segments not loaded");
Expect(MakeMap(), Segment.Beach, null, "empty map");
Expect(MakeMap(Biome.BiomeType.Shore), Segment.Beach, null, "last segment");
var incomplete = MakeMap(Biome.BiomeType.Shore, Biome.BiomeType.Roots);
incomplete.segments[1] = null;
Expect(incomplete, Segment.Beach, null, "next segment missing");
incomplete.segments[0] = null;
Expect(incomplete, Segment.Beach, null, "current segment missing");
Expect(custom, (Segment)250, null, "invalid current segment");
var terminal = MakeMap(Enumerable.Repeat(Biome.BiomeType.Volcano, 8).ToArray());
foreach (var current in new[] { Segment.TheKiln, Segment.Peak, Segment.Void })
    Expect(terminal, current, null, $"no forward route from {current}");
var unavailable = MakeMap(Biome.BiomeType.Shore, Biome.BiomeType.Roots);
unavailable.segments[1].throwOnRead = true;
Expect(unavailable, Segment.Beach, null, "getter failure does not escape into HUD");
unavailable.segments[1].throwOnRead = false;
Expect(unavailable, Segment.Beach, Biome.BiomeType.Roots, "data recovery retries successfully");
Console.WriteLine($"PASS: {checks} next-biome resolver regression checks.");

// Minimal surface used by the resolver. The getter follows the game's
// MapSegment.biome variant-selection semantics, including its recursive call.
public class MapHandler
{
    private static MapHandler instance;
    public MapHandler() => instance = this;
    public MapSegment[] segments;
    public MapSegment[] variantSegments;
    public List<Biome.BiomeType> biomes = new();
    public class MapSegment
    {
        public bool hasVariant;
        public int variantBiomeIndex;
        public Biome.BiomeType value;
        public bool throwOnRead;
        public Biome.BiomeType biome
        {
            get
            {
                if (throwOnRead) throw new InvalidOperationException("Map is changing");
                if (hasVariant && instance.biomes.Contains(instance.variantSegments[variantBiomeIndex].biome))
                    return instance.variantSegments[variantBiomeIndex].biome;
                return value;
            }
        }
    }
}

public enum Segment : byte { Beach, Tropics, Alpine, Caldera, TheKiln, Peak, Void }
public class Biome
{
    public enum BiomeType
    {
        Shore = 0, Tropics = 1, Alpine = 2, Volcano = 3, Peak = 5, Mesa = 6,
        Roots = 7, Swamp = 8, Temple = 9, Grasslands = 10, Ocean = 11,
        Skyblock = 12, Hell = 13, Heaven = 14, Mars = 15, Wisconsin = 16, Void = 17
    }
}
