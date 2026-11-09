using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zorro.Core;

namespace FogColdControl;

public sealed partial class Plugin
{
	private static readonly FieldInfo MovingLavaStartedField = AccessTools.Field(typeof(MovingLava), "timeToMove");
	private MovingLava[] _cachedKilnLava;
	private float _lastKilnLavaScan = -999f;

	private bool TryGetLavaApproach(out float distance, out float etaSeconds, out bool hasEta)
	{
		distance = 0f;
		etaSeconds = 0f;
		hasEta = false;
		if (LavaApproachUiEnabled?.Value == false || !ShouldShowLavaApproachUiForCurrentSegment()
			|| LoadingScreenHandler.loading || !TryResolveFogUiSubjectPosition(out var subject))
		{
			return false;
		}
		// The kiln uses MovingLava. The volcano's LavaRising object may still
		// exist in the loaded scene and must not supply the kiln's height/timer.
		if (_cachedKilnLava == null || Time.unscaledTime - _lastKilnLavaScan >= 1f)
		{
			_lastKilnLavaScan = Time.unscaledTime;
			_cachedKilnLava = UnityEngine.Object.FindObjectsByType<MovingLava>(FindObjectsSortMode.None);
		}
		MapHandler map = Singleton<MapHandler>.Instance;
		Scene mapScene = map != null ? map.gameObject.scene : SceneManager.GetActiveScene();
		MovingLava lava = _cachedKilnLava.Where(candidate => candidate != null && candidate.isActiveAndEnabled
			&& candidate.gameObject.scene == mapScene)
			.OrderByDescending(candidate => candidate.transform.position.y).FirstOrDefault();
		if (lava == null)
		{
			return false;
		}
		float height = lava.transform.position.y;
		if (float.IsNaN(height) || float.IsInfinity(height) || float.IsNaN(subject.y) || float.IsInfinity(subject.y))
		{
			return false;
		}
		distance = Mathf.Max(subject.y - height, 0f);
		if (distance <= 0.05f)
		{
			hasEta = true;
			return true;
		}
		// This is MovingLava's actual stopping height in the current game.
		// Above it, there is no arrival time to predict.
		if (height <= 1150f && subject.y <= 1150f && !float.IsNaN(lava.speed) && !float.IsInfinity(lava.speed) && lava.speed > 0f
			&& TryReadBooleanField(MovingLavaStartedField, lava, out bool started) && started)
		{
			etaSeconds = distance / lava.speed;
			hasEta = !float.IsNaN(etaSeconds) && !float.IsInfinity(etaSeconds);
		}
		return true;
	}
}
