using System;
using System.Collections.Generic;

namespace FogColdControl;

// Only network snapshots belong here. Locally simulated fog movement does not
// establish the host's speed, and player movement must not affect this value.
internal sealed class FogSpeedSamples
{
	private struct Sample
	{
		internal float Size;
		internal double Time;
	}

	private readonly List<Sample> _samples = new List<Sample>();
	private int _origin = -1;
	private const double WindowSeconds = 5;
	private const double TimeoutSeconds = 12;

	internal static bool IsValidSpeed(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

	internal void Reset()
	{
		_samples.Clear();
		_origin = -1;
	}

	internal void Observe(int origin, float size, bool moving, double gameTime)
	{
		if (!moving || !IsValidSpeed(size) || double.IsNaN(gameTime) || double.IsInfinity(gameTime))
		{
			Reset();
			return;
		}
		if (_samples.Count > 0)
		{
			Sample last = _samples[_samples.Count - 1];
			if (_origin != origin || size > last.Size + 0.01f || gameTime < last.Time || gameTime - last.Time > TimeoutSeconds)
			{
				Reset();
			}
			else if (gameTime == last.Time)
			{
				return;
			}
		}
		_origin = origin;
		_samples.Add(new Sample { Size = size, Time = gameTime });
		while (_samples.Count > 2 && _samples[1].Time <= gameTime - WindowSeconds)
		{
			_samples.RemoveAt(0);
		}
	}

	internal bool TryGetSpeed(int origin, double gameTime, out float speed)
	{
		speed = 0f;
		if (_origin != origin || _samples.Count < 2 || double.IsNaN(gameTime) || double.IsInfinity(gameTime))
		{
			return false;
		}
		Sample first = _samples[0];
		Sample last = _samples[_samples.Count - 1];
		double elapsed = last.Time - first.Time;
		if (elapsed < WindowSeconds || gameTime < last.Time || gameTime - last.Time > TimeoutSeconds)
		{
			return false;
		}
		speed = (float)((first.Size - last.Size) / elapsed);
		return IsValidSpeed(speed) && speed >= 0.02f;
	}
}
