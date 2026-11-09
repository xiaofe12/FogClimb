using System;

namespace FogColdControl;

internal static class NextBiomeResolver
{
	internal static bool TryGetNextBiome(MapHandler mapHandler, Segment currentSegment, out Biome.BiomeType biome)
	{
		biome = default;
		// The kiln and the summit end the route; the void is reached separately,
		// even though its map data is appended to the same segment array.
		if (mapHandler == null || currentSegment == Segment.TheKiln || currentSegment == Segment.Peak || currentSegment == Segment.Void)
		{
			return false;
		}
		MapHandler.MapSegment[] segments = mapHandler.segments;
		int currentIndex = (int)currentSegment;
		if (segments == null || currentIndex < 0 || currentIndex >= segments.Length - 1 || segments[currentIndex] == null)
		{
			return false;
		}
		MapHandler.MapSegment nextSegment = segments[currentIndex + 1];
		if (nextSegment == null || !HasReadableVariantChain(mapHandler, nextSegment))
		{
			return false;
		}
		try
		{
			// Use the game's getter so the loaded room's selected variant wins.
			// Do not infer a biome from a fixed stage order or today's map seed.
			biome = nextSegment.biome;
			// In the normal route the kiln is the final biome's campfire area,
			// not another biome. A custom room may give it a different biome.
			if (currentSegment == Segment.Caldera)
			{
				MapHandler.MapSegment current = segments[currentIndex];
				if (!HasReadableVariantChain(mapHandler, current) || current.biome == biome)
				{
					return false;
				}
			}
			return true;
		}
		catch (Exception)
		{
			// Room/map changes can leave variant data temporarily unavailable.
			// Retry on the next HUD refresh without interrupting the other entries.
			return false;
		}
	}

	private static bool HasReadableVariantChain(MapHandler mapHandler, MapHandler.MapSegment segment)
	{
		MapHandler.MapSegment[] variants = mapHandler.variantSegments;
		int remaining = (variants?.Length ?? 0) + 1;
		while (segment.hasVariant)
		{
			// The vanilla getter falls back to segments[0] for an invalid index,
			// which can recurse indefinitely in a partially initialized custom map.
			int index = segment.variantBiomeIndex;
			if (remaining-- <= 0 || mapHandler.biomes == null || variants == null || index < 0 || index >= variants.Length || variants[index] == null)
			{
				return false;
			}
			segment = variants[index];
		}
		return true;
	}
}
