using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zorro.Core;


namespace FogColdControl;

// FogClimb subsystem: Hud
public sealed partial class Plugin
{


	private static void NormalizeShootZombiesFogUiDefaults()
	{
		if (FogUiX != null && FogUiY != null && FogUiScale != null)
		{
			bool num = Approximately(FogUiX.Value, 70f) && Approximately(FogUiY.Value, 4f) && Approximately(FogUiScale.Value, 1.1f);
			bool matchesLegacyPresetB = Approximately(FogUiX.Value, 10f) && Approximately(FogUiY.Value, 10f) && Approximately(FogUiScale.Value, 1.2f);
			bool matchesLegacyPresetC = Approximately(FogUiX.Value, 4f) && Approximately(FogUiY.Value, 4f) && Approximately(FogUiScale.Value, 1.1f);
			bool matchesLegacyPresetD = Approximately(FogUiX.Value, 60f) && Approximately(FogUiY.Value, -200f) && Approximately(FogUiScale.Value, 1.1f);
			bool matchesPreviousFogClimbDefault = Approximately(FogUiX.Value, 60f) && Approximately(FogUiY.Value, -200f) && Approximately(FogUiScale.Value, 1f);
			bool matchesOlderFogClimbDefault = Approximately(FogUiX.Value, 60f) && Approximately(FogUiY.Value, 16f) && Approximately(FogUiScale.Value, 1.2f);
			bool matchesCurrentFogClimbDefault = Approximately(FogUiX.Value, 60f) && Approximately(FogUiY.Value, 0f) && Approximately(FogUiScale.Value, 1.2f);
			bool matchesPreviousCompactDefault = Approximately(FogUiX.Value, 60f) && Approximately(FogUiY.Value, 0f) && Approximately(FogUiScale.Value, 0.9f);
			bool matchesRaisedCompactDefault = Approximately(FogUiX.Value, 60f) && Approximately(FogUiY.Value, 22f) && Approximately(FogUiScale.Value, 0.9f);
			if (num || matchesLegacyPresetB || matchesLegacyPresetC || matchesLegacyPresetD || matchesPreviousFogClimbDefault || matchesOlderFogClimbDefault || matchesCurrentFogClimbDefault || matchesPreviousCompactDefault || matchesRaisedCompactDefault)
			{
				FogUiX.Value = 60f;
				FogUiY.Value = 0f;
				FogUiScale.Value = 0.9f;
			}
		}
	}


	private static bool TryGetVanillaProgressStartThresholds(OrbFogHandler fogHandler, out float requiredHeight, out float requiredForward)
	{
		requiredHeight = 0f;
		requiredForward = 0f;
		if (fogHandler == null)
		{
			return false;
		}
		requiredHeight = fogHandler.currentStartHeight;
		requiredForward = fogHandler.currentStartForward;
		if (!float.IsNaN(requiredHeight) && !float.IsInfinity(requiredHeight) && !float.IsNaN(requiredForward))
		{
			return !float.IsInfinity(requiredForward);
		}
		return false;
	}


	private static bool TryGetVanillaProgressStartProgress(OrbFogHandler fogHandler, out int passedCount, out int totalCount, out float requiredHeight, out float requiredForward)
	{
		passedCount = 0;
		totalCount = 0;
		requiredHeight = 0f;
		requiredForward = 0f;
		if (Ascents.currentAscent < 0 || !TryGetVanillaProgressStartThresholds(fogHandler, out requiredHeight, out requiredForward))
		{
			return false;
		}
		foreach (Character character in Character.AllCharacters)
		{
			if (IsVanillaProgressStartRelevantCharacter(character))
			{
				totalCount++;
				if (character.Center.y >= requiredHeight && character.Center.z >= requiredForward)
				{
					passedCount++;
				}
			}
		}
		return totalCount > 0;
	}


	private static bool ShouldHideFogRelatedUiForCurrentSegment()
	{
		if (!TryGetCurrentGameplaySegment(out var segment))
		{
			return false;
		}
		if ((int)segment >= 4)
		{
			return true;
		}
		try
		{
			MapHandler mapHandler = Singleton<MapHandler>.Instance;
			if (mapHandler != null)
			{
				Biome.BiomeType biome = mapHandler.GetCurrentBiome();
				if (biome == Biome.BiomeType.Swamp || biome == Biome.BiomeType.Volcano)
				{
					return true;
				}
			}
		}
		catch
		{
		}
		return false;
	}


	private static bool ShouldShowLavaApproachUiForCurrentSegment()
	{
		return TryResolveActiveSceneLavaRising(out _);
	}


	private bool TryBuildSyntheticFogStage(Segment syntheticSegment, out Vector3 fogPoint, out float fogSize, out string anchorDescription)
	{
		fogPoint = Vector3.zero;
		fogSize = 0f;
		anchorDescription = string.Empty;
		if (!TryResolveSyntheticFogAnchor(syntheticSegment, out fogPoint, out anchorDescription))
		{
			return false;
		}
		fogSize = ComputeSyntheticFogStartSize(syntheticSegment, fogPoint);
		return fogSize > 0f;
	}


	private bool TryBuildPeakVerticalFogStage(Segment syntheticSegment, out Vector3 fogPoint, out float fogSize, out string anchorDescription)
	{
		fogPoint = Vector3.zero;
		fogSize = 0f;
		anchorDescription = string.Empty;
		if ((int)syntheticSegment < 5)
		{
			return false;
		}
		if (!TryResolvePeakVerticalFogTargetAnchor(syntheticSegment, out var targetAnchor, out var targetDescription))
		{
			return false;
		}
		fogPoint = new Vector3(targetAnchor.x, 1830f, targetAnchor.z);
		fogSize = ComputePeakVerticalFogStartSize(fogPoint, targetAnchor);
		if (fogSize <= 0f)
		{
			return false;
		}
		anchorDescription = $"{targetDescription} vertical-up stop@{1800f:F0}";
		return true;
	}


	private static bool TryResolveFogUiSubjectPosition(out Vector3 subjectPosition)
	{
		subjectPosition = Vector3.zero;
		if (TryResolveFogUiSubjectCharacter(out var subjectCharacter))
		{
			subjectPosition = subjectCharacter.Center;
			return true;
		}
		return false;
	}


	private static bool TryResolveFogUiSubjectCharacter(out Character subjectCharacter)
	{
		subjectCharacter = null;
		Character localCharacter = Character.localCharacter;
		if (IsUsableFogUiSubject(localCharacter))
		{
			subjectCharacter = localCharacter;
			return true;
		}
		Character observedCharacter = TryGetObservedCharacter();
		if (observedCharacter == localCharacter || !IsUsableFogUiSubject(observedCharacter))
		{
			return false;
		}
		subjectCharacter = observedCharacter;
		return true;
	}


	private static bool IsUsableFogUiSubject(Character character)
	{
		if (character != null && character.data != null && !character.data.dead && !character.isBot)
		{
			return !character.isZombie;
		}
		return false;
	}


	private static IEnumerable<Transform> EnumerateCandidateTransforms(object menuInstance, Type menuType)
	{
		if (menuInstance is Component component)
		{
			yield return component.transform;
		}
		if (menuType?.GetProperty("Content", InstanceBindingFlags)?.GetValue(menuInstance) is Transform content)
		{
			yield return content;
		}
	}


	private bool ShouldShowFogUi(Canvas targetCanvas = null)
	{
		if (IsModFeatureEnabled())
		{
			ConfigEntry<bool> fogUiEnabled = FogUiEnabled;
			if (fogUiEnabled == null || fogUiEnabled.Value)
			{
				if (LoadingScreenHandler.loading || IsFogUiBlockedByOverlay() || !IsFogUiSceneAllowed())
				{
					return false;
				}
				return IsCanvasUsable(targetCanvas ?? ResolveHudCanvas());
			}
		}
		return false;
	}


	private static bool IsFogUiBlockedByOverlay()
	{
		GUIManager instance = GUIManager.instance;
		if (instance != null && instance.endScreen != null)
		{
			return instance.endScreen.isOpen;
		}
		return false;
	}


	private static bool IsFogUiSceneAllowed()
	{
		Scene activeScene = SceneManager.GetActiveScene();
		if (!activeScene.IsValid())
		{
			return false;
		}
		if (!IsAirportScene(activeScene))
		{
			return IsGameplayFogScene(activeScene);
		}
		return true;
	}


	private Canvas ResolveHudCanvas()
	{
		if (!IsFogUiSceneAllowed())
		{
			return null;
		}
		GUIManager instance = GUIManager.instance;
		if (instance != null)
		{
			if (IsCanvasUsable(instance.hudCanvas))
			{
				return instance.hudCanvas;
			}
			TextMeshProUGUI source = ((instance.itemPromptMain != null) ? instance.itemPromptMain : instance.interactNameText);
			if (source != null)
			{
				Canvas canvas = source.GetComponentInParent<Canvas>();
				if (canvas != null)
				{
					return canvas;
				}
			}
		}
		Canvas[] array = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
		Canvas bestCanvas = null;
		int bestSortingOrder = int.MinValue;
		float bestArea = -1f;
		Canvas[] array2 = array;
		foreach (Canvas canvas2 in array2)
		{
			if (!(canvas2 == null) && canvas2.isRootCanvas && canvas2.isActiveAndEnabled && canvas2.gameObject.activeInHierarchy && canvas2.renderMode != RenderMode.WorldSpace)
			{
				RectTransform rectTransform = canvas2.GetComponent<RectTransform>();
				float area = ((rectTransform != null) ? Mathf.Abs(rectTransform.rect.width * rectTransform.rect.height) : 0f);
				if (bestCanvas == null || canvas2.sortingOrder > bestSortingOrder || (canvas2.sortingOrder == bestSortingOrder && area > bestArea))
				{
					bestCanvas = canvas2;
					bestSortingOrder = canvas2.sortingOrder;
					bestArea = area;
				}
			}
		}
		return bestCanvas;
	}


	private static bool IsCanvasUsable(Canvas canvas)
	{
		if (canvas != null && canvas.isRootCanvas && canvas.isActiveAndEnabled && canvas.gameObject.activeInHierarchy)
		{
			return canvas.renderMode != RenderMode.WorldSpace;
		}
		return false;
	}


	private bool NeedsFogUiRebuild(Canvas targetCanvas)
	{
		if (_fogUiRect == null || _fogUiText == null || _fogUiEntriesRect == null || !IsCanvasUsable(targetCanvas))
		{
			return true;
		}
		if (IsCanvasUsable(_fogUiRect.GetComponentInParent<Canvas>()))
		{
			return _fogUiRect.parent != targetCanvas.transform;
		}
		return true;
	}


	private TextMeshProUGUI ResolveFogStyleSource()
	{
		bool isChineseLanguage = DetectChineseLanguage();
		GUIManager instance = GUIManager.instance;
		if (instance != null)
		{
			TextMeshProUGUI[] candidates = new TextMeshProUGUI[3] { instance.itemPromptMain, instance.interactNameText, instance.interactPromptText };
			TextMeshProUGUI[] array = candidates;
			foreach (TextMeshProUGUI candidate in array)
			{
				if (IsTextSourceSuitable(candidate, isChineseLanguage))
				{
					return candidate;
				}
			}
			array = candidates;
			foreach (TextMeshProUGUI candidate2 in array)
			{
				if (candidate2 != null)
				{
					return candidate2;
				}
			}
		}
		TextMeshProUGUI localizedSource = FindLocalizedTextSource(isChineseLanguage);
		if (localizedSource != null)
		{
			return localizedSource;
		}
		return null;
	}


	private static TextMeshProUGUI FindLocalizedTextSource(bool isChineseLanguage)
	{
		TextMeshProUGUI bestSource = null;
		int bestScore = int.MinValue;
		TextMeshProUGUI[] array = UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
		foreach (TextMeshProUGUI text in array)
		{
			if (!(text == null) && !(text.font == null) && text.isActiveAndEnabled && text.gameObject.activeInHierarchy)
			{
				int score = 0;
				if (ContainsLocalizedCharacters(text.text, isChineseLanguage))
				{
					score += 100;
				}
				if (text.GetComponentInParent<Canvas>() != null)
				{
					score += 20;
				}
				if (text.fontSharedMaterial != null)
				{
					score += 5;
				}
				if (score > bestScore)
				{
					bestScore = score;
					bestSource = text;
				}
			}
		}
		if (bestScore <= 0)
		{
			return null;
		}
		return bestSource;
	}


	private static bool IsTextSourceSuitable(TextMeshProUGUI source, bool isChineseLanguage)
	{
		if (source == null || source.font == null)
		{
			return false;
		}
		if (isChineseLanguage)
		{
			return ContainsLocalizedCharacters(source.text, isChineseLanguage: true);
		}
		return true;
	}


	private void ApplyGameTextStyle(TextMeshProUGUI target, Color color, float sizeMultiplier = 1f)
	{
		if (!(target == null))
		{
			TextMeshProUGUI source = ResolveFogStyleSource();
			if (source != null)
			{
				target.font = source.font;
				target.fontSharedMaterial = source.fontSharedMaterial;
				target.fontStyle = source.fontStyle;
				target.characterSpacing = source.characterSpacing;
				target.wordSpacing = source.wordSpacing;
				target.lineSpacing = source.lineSpacing;
			}
			else if (TMP_Settings.defaultFontAsset != null)
			{
				target.font = TMP_Settings.defaultFontAsset;
			}
			target.textWrappingMode = TextWrappingModes.NoWrap;
			target.fontSize = 18f * sizeMultiplier;
			target.color = color;
			target.outlineColor = new Color32(0, 0, 0, 220);
			target.outlineWidth = Mathf.Max(target.outlineWidth, 0.045f);
			target.alignment = TextAlignmentOptions.BottomLeft;
			target.fontWeight = FontWeight.Bold;
		}
	}


	private bool ShouldShowCampfireLocatorUi(Canvas targetCanvas = null)
	{
		if (IsModFeatureEnabled())
		{
			ConfigEntry<bool> fogUiEnabled = FogUiEnabled;
			if (fogUiEnabled == null || fogUiEnabled.Value)
			{
				ConfigEntry<bool> campfireLocatorUiEnabled = CampfireLocatorUiEnabled;
				if (campfireLocatorUiEnabled == null || campfireLocatorUiEnabled.Value)
				{
					if (LoadingScreenHandler.loading || IsFogUiBlockedByOverlay() || !IsGameplayFogScene(SceneManager.GetActiveScene()))
					{
						return false;
					}
					Vector3 targetPosition;
					if (IsCanvasUsable(targetCanvas ?? ResolveHudCanvas()))
					{
						return TryResolveNextCampfireTarget(out targetPosition);
					}
					return false;
				}
			}
		}
		return false;
	}


	private bool NeedsCampfireLocatorUiRebuild(Canvas targetCanvas)
	{
		if (_campfireLocatorUiRect == null || _campfireLocatorDotRect == null || !IsCanvasUsable(targetCanvas))
		{
			return true;
		}
		if (IsCanvasUsable(_campfireLocatorUiRect.GetComponentInParent<Canvas>()))
		{
			return _campfireLocatorUiRect.parent != targetCanvas.transform;
		}
		return true;
	}


	private void CreateCampfireLocatorUi(Canvas targetCanvas = null)
	{
		CleanupCampfireLocatorUi();
		Canvas canvas = targetCanvas ?? ResolveHudCanvas();
		if (!ShouldShowCampfireLocatorUi(canvas))
		{
			return;
		}
		try
		{
			GameObject container = new GameObject("FogClimbCampfireLocatorUI", typeof(RectTransform));
			_campfireLocatorUiRect = container.GetComponent<RectTransform>();
			_campfireLocatorUiRect.SetParent(canvas.transform, worldPositionStays: false);
			_campfireLocatorUiRect.SetAsLastSibling();
			_campfireLocatorUiRect.sizeDelta = new Vector2(CampfireLocatorUiWidth, CampfireLocatorUiHeight);
			float lineY = 0f;
			GameObject lineObj = new GameObject("Line", typeof(RectTransform), typeof(Image));
			RectTransform lineRect = lineObj.GetComponent<RectTransform>();
			lineRect.SetParent(_campfireLocatorUiRect, worldPositionStays: false);
			lineRect.anchorMin = new Vector2(0.5f, 0.5f);
			lineRect.anchorMax = new Vector2(0.5f, 0.5f);
			lineRect.pivot = new Vector2(0.5f, 0.5f);
			lineRect.anchoredPosition = new Vector2(0f, lineY);
			lineRect.sizeDelta = new Vector2(CampfireLocatorLineWidth, CampfireLocatorLineHeight);
			Image lineImage = lineObj.GetComponent<Image>();
			lineImage.raycastTarget = false;
			lineImage.sprite = GetCampfireLocatorLineSprite();
			lineImage.type = Image.Type.Simple;
			lineImage.color = CampfireLocatorLineColor;
			float halfLine = CampfireLocatorLineWidth * 0.5f;
			int tickCount = 9;
			float tickSpacing = CampfireLocatorLineWidth / (float)(tickCount - 1);
			for (int i = 0; i < tickCount; i++)
			{
				bool isCenter = i == tickCount / 2;
				float tickH = isCenter ? CampfireLocatorCenterTickHeight : CampfireLocatorTickHeight;
				float tickW = isCenter ? CampfireLocatorCenterTickWidth : CampfireLocatorTickWidth;
				float tickX = -halfLine + (float)i * tickSpacing;
				GameObject tickObj = new GameObject($"Tick{i}", typeof(RectTransform), typeof(Image));
				RectTransform tickRect = tickObj.GetComponent<RectTransform>();
				tickRect.SetParent(_campfireLocatorUiRect, worldPositionStays: false);
				tickRect.anchorMin = new Vector2(0.5f, 0.5f);
				tickRect.anchorMax = new Vector2(0.5f, 0.5f);
				tickRect.pivot = new Vector2(0.5f, 0.5f);
				tickRect.anchoredPosition = new Vector2(tickX, lineY + CampfireLocatorLineHeight * 0.5f + tickH * 0.5f);
				tickRect.sizeDelta = new Vector2(tickW, tickH);
				Image tickImage = tickObj.GetComponent<Image>();
				tickImage.raycastTarget = false;
				tickImage.color = isCenter ? new Color(1f, 0.85f, 0.4f, 0.95f) : new Color(1f, 1f, 1f, 0.5f);
			}
			GameObject dotObject = new GameObject("Dot", typeof(RectTransform), typeof(Image));
			_campfireLocatorDotRect = dotObject.GetComponent<RectTransform>();
			_campfireLocatorDotRect.SetParent(_campfireLocatorUiRect, worldPositionStays: false);
			_campfireLocatorDotRect.anchorMin = new Vector2(0.5f, 0.5f);
			_campfireLocatorDotRect.anchorMax = new Vector2(0.5f, 0.5f);
			_campfireLocatorDotRect.pivot = new Vector2(0.5f, 0.5f);
			_campfireLocatorDotRect.anchoredPosition = new Vector2(0f, lineY);
			_campfireLocatorDotRect.sizeDelta = new Vector2(CampfireLocatorDotSize, CampfireLocatorDotSize);
			Image dotImage = dotObject.GetComponent<Image>();
			dotImage.raycastTarget = false;
			dotImage.preserveAspect = true;
			dotImage.sprite = GetCampfireLocatorDotSprite();
			dotImage.color = Color.white;
			_campfireLocatorDotImage = dotImage;
			GameObject distObj = new GameObject("Distance", typeof(RectTransform), typeof(TextMeshProUGUI));
			RectTransform distRect = distObj.GetComponent<RectTransform>();
			distRect.SetParent(_campfireLocatorUiRect, worldPositionStays: false);
			distRect.anchorMin = new Vector2(0.5f, 0.5f);
			distRect.anchorMax = new Vector2(0.5f, 0.5f);
			distRect.pivot = new Vector2(0.5f, 1f);
			distRect.anchoredPosition = new Vector2(0f, lineY - CampfireLocatorDotSize * 0.5f - 2f);
			distRect.sizeDelta = new Vector2(120f, 20f);
			_campfireLocatorDistanceText = distObj.GetComponent<TextMeshProUGUI>();
			_campfireLocatorDistanceText.alignment = TextAlignmentOptions.Center;
			_campfireLocatorDistanceText.textWrappingMode = TextWrappingModes.NoWrap;
			_campfireLocatorDistanceText.fontSize = CampfireLocatorDistanceFontSize;
			_campfireLocatorDistanceText.color = new Color(1f, 1f, 1f, 0.9f);
			_campfireLocatorDistanceText.text = string.Empty;
			_campfireLocatorCurrentDotX = 0f;
			ClampCampfireLocatorUiToCanvas(canvas);
			SetCampfireLocatorUiVisible(visible: true);
		}
		catch
		{
			CleanupCampfireLocatorUi();
		}
	}


	private void UpdateCampfireLocatorUi()
	{
		if ((FogUiEnabled != null && !FogUiEnabled.Value) || (CampfireLocatorUiEnabled != null && !CampfireLocatorUiEnabled.Value))
		{
			SetCampfireLocatorUiVisible(visible: false);
			return;
		}
		Canvas targetCanvas = ResolveHudCanvas();
		if (!ShouldShowCampfireLocatorUi(targetCanvas))
		{
			SetCampfireLocatorUiVisible(visible: false);
			return;
		}
		if (NeedsCampfireLocatorUiRebuild(targetCanvas))
		{
			CreateCampfireLocatorUi(targetCanvas);
			if (_campfireLocatorUiRect == null || _campfireLocatorDotRect == null)
			{
				return;
			}
		}
		if (!TryResolveNextCampfireTarget(out var targetPosition))
		{
			SetCampfireLocatorUiVisible(visible: false);
			return;
		}
		SetCampfireLocatorUiVisible(visible: true);
		ClampCampfireLocatorUiToCanvas(targetCanvas);
		UpdateCampfireLocatorDot(targetPosition);
	}


	private void UpdateCampfireLocatorDot(Vector3 targetPosition)
	{
		if (_campfireLocatorDotRect == null)
		{
			return;
		}
		if (_campfireLocatorDotImage != null)
		{
			Sprite resolvedDotSprite = GetCampfireLocatorDotSprite();
			if (_campfireLocatorDotImage.sprite != resolvedDotSprite)
			{
				_campfireLocatorDotImage.sprite = resolvedDotSprite;
			}
		}
		float halfTravelDistance = Mathf.Max(0f, 171f);
		float targetDotX = 0f;
		Camera viewCamera = ResolveActiveViewCamera();
		if (viewCamera != null)
		{
			Vector3 viewportPosition = viewCamera.WorldToViewportPoint(targetPosition);
			if (viewportPosition.z > 0.05f)
			{
				targetDotX = Mathf.Lerp(0f - halfTravelDistance, halfTravelDistance, Mathf.Clamp01(viewportPosition.x));
			}
			else
			{
				Vector3 toTarget = Vector3.ProjectOnPlane(targetPosition - viewCamera.transform.position, Vector3.up);
				Vector3 right = Vector3.ProjectOnPlane(viewCamera.transform.right, Vector3.up);
				float side = 0f;
				if (toTarget.sqrMagnitude >= 0.01f && right.sqrMagnitude >= 0.01f)
				{
					side = Vector3.Dot(toTarget.normalized, right.normalized);
				}
				targetDotX = ((side >= 0f) ? halfTravelDistance : (0f - halfTravelDistance));
			}
		}
		float smoothing = 1f - Mathf.Exp(0f - 12f * Mathf.Max(Time.unscaledDeltaTime, 0f));
		_campfireLocatorCurrentDotX = Mathf.Lerp(_campfireLocatorCurrentDotX, targetDotX, smoothing);
		_campfireLocatorDotRect.anchoredPosition = new Vector2(_campfireLocatorCurrentDotX, 0f);
		if (_campfireLocatorDistanceText != null)
		{
			_campfireLocatorDistanceText.rectTransform.anchoredPosition = new Vector2(_campfireLocatorCurrentDotX, 0f - CampfireLocatorDotSize * 0.5f - 2f);
		}
		UpdateCampfireLocatorDistanceText(targetPosition);
	}


	private void UpdateCampfireLocatorDistanceText(Vector3 targetPosition)
	{
		if (_campfireLocatorDistanceText == null)
		{
			return;
		}
		if (!TryResolveFogUiSubjectPosition(out var subjectPosition))
		{
			_campfireLocatorDistanceText.text = string.Empty;
			return;
		}
		float distance = Vector3.Distance(subjectPosition, targetPosition);
		string text = FormatFogDistanceValue(distance);
		if (_campfireLocatorDistanceText.text != text)
		{
			_campfireLocatorDistanceText.text = text;
		}
	}


	private void CreateFogUi(Canvas targetCanvas = null)
	{
		CleanupFogUi();
		Canvas canvas = targetCanvas ?? ResolveHudCanvas();
		if (!ShouldShowFogUi(canvas))
		{
			return;
		}
		try
		{
			bool verticalLayout = IsFogUiVerticalLayout();
			float containerWidth = verticalLayout ? FogUiVerticalContainerWidth : FogUiHorizontalWidth;
			float containerHeight = verticalLayout ? FogUiVerticalMinHeight : FogUiHorizontalHeight;
			GameObject container = new GameObject("FogClimbUI", typeof(RectTransform));
			_fogUiRect = container.GetComponent<RectTransform>();
			_fogUiRect.SetParent(canvas.transform, worldPositionStays: false);
			_fogUiRect.anchorMin = Vector2.zero;
			_fogUiRect.anchorMax = Vector2.zero;
			_fogUiRect.pivot = Vector2.zero;
			_fogUiRect.sizeDelta = new Vector2(containerWidth, containerHeight);
			_fogUiRect.SetAsLastSibling();
			GameObject backgroundObject = new GameObject("FogClimbUIBackground", typeof(RectTransform), typeof(Image));
			RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
			backgroundRect.SetParent(_fogUiRect, worldPositionStays: false);
			backgroundRect.anchorMin = Vector2.zero;
			backgroundRect.anchorMax = Vector2.one;
			backgroundRect.offsetMin = Vector2.zero;
			backgroundRect.offsetMax = Vector2.zero;
			_fogUiBackgroundImage = backgroundObject.GetComponent<Image>();
			_fogUiBackgroundImage.sprite = GetFogUiBackgroundSprite();
			_fogUiBackgroundImage.type = Image.Type.Sliced;
			_fogUiBackgroundImage.raycastTarget = false;
			_fogUiBackgroundImage.color = new Color(0f, 0f, 0f, ResolveFogUiBackgroundOpacity());
			GameObject labelObject = new GameObject("FogClimbUILabel", typeof(RectTransform));
			RectTransform component = labelObject.GetComponent<RectTransform>();
			component.SetParent(_fogUiRect, worldPositionStays: false);
			component.anchorMin = Vector2.zero;
			component.anchorMax = Vector2.zero;
			component.pivot = Vector2.zero;
			component.anchoredPosition = Vector2.zero;
			component.sizeDelta = new Vector2(containerWidth, containerHeight);
			_fogUiText = labelObject.AddComponent<TextMeshProUGUI>();
			ApplyGameTextStyle(_fogUiText, Color.white);
			_fogUiText.richText = true;
			_fogUiText.alignment = TextAlignmentOptions.MidlineLeft;
			_fogUiText.margin = new Vector4(10f, 0f, 10f, 0f);
			_fogUiText.text = string.Empty;
			_fogUiText.enabled = false;
			GameObject entriesObject = new GameObject("FogClimbUIEntries", typeof(RectTransform), verticalLayout ? typeof(VerticalLayoutGroup) : typeof(HorizontalLayoutGroup));
			_fogUiEntriesRect = entriesObject.GetComponent<RectTransform>();
			_fogUiEntriesRect.SetParent(_fogUiRect, worldPositionStays: false);
			_fogUiEntriesRect.anchorMin = Vector2.zero;
			_fogUiEntriesRect.anchorMax = Vector2.zero;
			_fogUiEntriesRect.pivot = Vector2.zero;
			_fogUiEntriesRect.anchoredPosition = Vector2.zero;
			_fogUiEntriesRect.sizeDelta = new Vector2(containerWidth, containerHeight);
			if (verticalLayout)
			{
				VerticalLayoutGroup columnLayout = entriesObject.GetComponent<VerticalLayoutGroup>();
				columnLayout.childControlWidth = true;
				columnLayout.childControlHeight = true;
				columnLayout.childForceExpandWidth = false;
				columnLayout.childForceExpandHeight = false;
				columnLayout.childAlignment = TextAnchor.UpperLeft;
				columnLayout.spacing = ResolveFogUiEntrySpacing(compact: false);
				columnLayout.padding = ResolveFogUiUniformPadding(compact: false);
			}
			else
			{
				HorizontalLayoutGroup component2 = entriesObject.GetComponent<HorizontalLayoutGroup>();
				component2.childControlWidth = true;
				component2.childControlHeight = true;
				component2.childForceExpandWidth = false;
				component2.childForceExpandHeight = false;
				component2.childAlignment = TextAnchor.MiddleLeft;
				component2.spacing = ResolveFogUiEntrySpacing(compact: false);
				component2.padding = ResolveFogUiUniformPadding(compact: false);
			}
			_lastFogUiRenderedText = string.Empty;
			_fogUiLayoutDirty = true;
			_lastFogUiEntryLayoutCount = -1;
			_lastFogUiResolvedWidth = -1f;
			ClampFogUiToCanvas(canvas);
			SetFogUiVisible(visible: true);
		}
		catch
		{
			CleanupFogUi();
		}
	}


	private void UpdateFogUi()
	{
		if (FogUiEnabled != null && !FogUiEnabled.Value)
		{
			SetFogUiVisible(visible: false);
			return;
		}
		Canvas targetCanvas = ResolveHudCanvas();
		if (!ShouldShowFogUi(targetCanvas))
		{
			SetFogUiVisible(visible: false);
			return;
		}
		if (IsAirportScene(SceneManager.GetActiveScene()) && IsReadOnlyFogUiViewer())
		{
			SetFogUiVisible(visible: false);
			return;
		}
		if (NeedsFogUiRebuild(targetCanvas))
		{
			CreateFogUi(targetCanvas);
			if (_fogUiRect == null || _fogUiText == null)
			{
				return;
			}
		}
		SetFogUiVisible(visible: true);
		ClampFogUiToCanvas(targetCanvas);
		UpdateFogUiTheme();
		if (_fogUiBackgroundImage != null)
		{
			float backgroundOpacity = ResolveFogUiBackgroundOpacity();
			if (!Mathf.Approximately(_fogUiBackgroundImage.color.a, backgroundOpacity))
			{
				_fogUiBackgroundImage.color = new Color(0f, 0f, 0f, backgroundOpacity);
			}
		}
		bool isChineseLanguage = DetectChineseLanguage();
		bool isLobbyScene = IsAirportScene(SceneManager.GetActiveScene());
		bool showFogRelatedUi = isLobbyScene || !ShouldHideFogRelatedUiForCurrentSegment();
		if (_fogUiEntriesRect != null)
		{
			PopulateFogUiEntries(isChineseLanguage, isLobbyScene, showFogRelatedUi);
			return;
		}
		if (IsReadOnlyFogUiViewer())
		{
			SetFogUiText(BuildReadOnlyFogUiText(isChineseLanguage, showFogRelatedUi));
			return;
		}
		float compactSpeed = (_fogPaused ? 0f : ((_orbFogHandler != null) ? _orbFogHandler.speed : (FogSpeed?.Value ?? 0.4f)));
		string stateBadge = BuildFogUiBadge("ST", "#A8E0A0", GetFogRuntimeStateLabel(isChineseLanguage));
		string speedBadge = BuildFogUiBadge("SPD", "#8EC5FF", Colorize(FormatFogSpeedValue(compactSpeed), "#D6F1FF"));
		if (isLobbyScene)
		{
			float lobbySpeed = FogSpeed?.Value ?? 0.4f;
			string lobbySpeedBadge = BuildFogUiBadge("SPD", "#8EC5FF", Colorize(FormatFogSpeedValue(lobbySpeed), "#D6F1FF"));
			SetFogUiText(BuildFogUiLine(GetCompactFogHandlingBadge(), GetCompactNightBadge(), lobbySpeedBadge, GetCompactLobbyFogDelayBadge(isChineseLanguage)));
			return;
		}
		string timerBadge = GetCompactRunTimerBadge();
		string dayNightBadge = GetCompactDayNightBadge(isChineseLanguage);
		string timingBadge = GetCompactTimingBadge();
		string nextBiomeBadge = GetCompactNextBiomeBadge(isChineseLanguage);
		string campfireBadge = GetCompactNextCampfireDistanceBadge();
		string fogApproachBadge = GetCompactFogApproachBadge(isChineseLanguage);
		string lavaApproachBadge = GetCompactLavaApproachBadge(isChineseLanguage);
		string directBadge = GetCompactDirectStartBadge(isChineseLanguage);
		if (!showFogRelatedUi)
		{
			SetFogUiText(BuildFogUiLine(timerBadge, dayNightBadge, campfireBadge, lavaApproachBadge, nextBiomeBadge));
			return;
		}
		if (!string.IsNullOrWhiteSpace(timingBadge))
		{
			SetFogUiText(BuildFogUiLine(stateBadge, timerBadge, dayNightBadge, speedBadge, campfireBadge, directBadge, timingBadge, nextBiomeBadge));
			return;
		}
		SetFogUiText(BuildFogUiLine(stateBadge, timerBadge, dayNightBadge, speedBadge, campfireBadge, directBadge, fogApproachBadge, nextBiomeBadge));
		if (!ShouldUseLegacyFogUiLayout())
		{
			return;
		}
		if (IsReadOnlyFogUiViewer())
		{
			SetFogUiText(BuildReadOnlyFogUiText(isChineseLanguage, showFogRelatedUi));
			return;
		}
		float speed = (_fogPaused ? 0f : ((_orbFogHandler != null) ? _orbFogHandler.speed : (FogSpeed?.Value ?? 0.4f)));
		string text = (isChineseLanguage ? "毒雾速度:" : "FogSpeed:");
		string stateText = (isChineseLanguage ? "当前状态:" : "State:");
		string speedLabel = Colorize(text, "#8EC5FF") + " " + Colorize(FormatFogSpeedValue(speed), "#D6F1FF");
		string stateLabel = Colorize(stateText, "#A8E0A0") + " " + GetFogRuntimeStateLabel(isChineseLanguage);
		string timingLabel = GetFogTimingLabel(isChineseLanguage);
		string distanceLabel = GetResolvedFogDistanceLabel(isChineseLanguage);
		string arrivalLabel = GetResolvedFogArrivalEtaLabel(isChineseLanguage);
		string primaryLine = BuildFogUiLine(speedLabel, stateLabel, timingLabel, distanceLabel, arrivalLabel);
		string directStartHint = GetVanillaProgressStartUiLabel(isChineseLanguage);
		if (!string.IsNullOrWhiteSpace(directStartHint))
		{
			primaryLine = BuildFogUiLine(primaryLine, directStartHint);
		}
		if (!isLobbyScene)
		{
			SetFogUiText(primaryLine);
			return;
		}
		string fogHandlingHint = GetFogHandlingUiLabel(isChineseLanguage);
		string nightHint = GetNightColdUiLabel(isChineseLanguage);
		SetFogUiText(BuildFogUiLine(primaryLine, fogHandlingHint, nightHint));
	}


	private string BuildReadOnlyFogUiText(bool isChineseLanguage, bool showFogRelatedUi)
	{
		if (!showFogRelatedUi)
		{
			return BuildFogUiLine(GetCompactRunTimerBadge(), GetCompactDayNightBadge(isChineseLanguage), GetCompactNextCampfireDistanceBadge(), GetCompactLavaApproachBadge(isChineseLanguage), GetCompactNextBiomeBadge(isChineseLanguage));
		}
		if (IsGuestUsingVanillaHostFog())
		{
			return BuildFogUiLine(GetCompactRunTimerBadge(), GetCompactDayNightBadge(isChineseLanguage), GetCompactGuestSpeedBadge(), GetCompactNextCampfireDistanceBadge(), GetCompactFogApproachBadge(isChineseLanguage), GetCompactNextBiomeBadge(isChineseLanguage));
		}
		string stateBadge = BuildFogUiBadge("ST", "#A8E0A0", GetGuestFogRuntimeStateLabel(isChineseLanguage));
		return BuildFogUiLine(stateBadge, GetCompactRunTimerBadge(), GetCompactDayNightBadge(isChineseLanguage), GetCompactGuestSpeedBadge(), GetCompactNextCampfireDistanceBadge(), GetCompactFogApproachBadge(isChineseLanguage), GetCompactNextBiomeBadge(isChineseLanguage));
	}


	private static string BuildFogUiLine(params string[] parts)
	{
		if (parts == null || parts.Length == 0)
		{
			return string.Empty;
		}
		return string.Join("  |  ", from part in parts
			where !string.IsNullOrWhiteSpace(part)
			select part.Trim());
	}


	private static bool ShouldUseLegacyFogUiLayout()
	{
		return false;
	}


	private void PopulateFogUiEntries(bool isChineseLanguage, bool isLobbyScene, bool showFogRelatedUi)
	{
		_fogUiDisplayEntries.Clear();
		if (IsReadOnlyFogUiViewer())
		{
			bool showHostManagedFogUi = showFogRelatedUi && !IsGuestUsingVanillaHostFog();
			FogUiIconKind guestStateIconKind = GetGuestFogRuntimeStateIconKind();
			if (showHostManagedFogUi && FogStateUiEnabled?.Value != false && guestStateIconKind != FogUiIconKind.StateWaiting && guestStateIconKind != FogUiIconKind.StateRunning)
			{
				_fogUiDisplayEntries.Add(new FogUiDisplayEntry(guestStateIconKind, ParseColorOrDefault("#A8E0A0"), GetGuestFogStateUiEntryText(isChineseLanguage)));
			}
			if (!isLobbyScene)
			{
				TryAppendRunTimerEntry(_fogUiDisplayEntries);
				TryAppendDayNightTimeEntry(_fogUiDisplayEntries, isChineseLanguage);
				TryAppendAltitudeEntry(_fogUiDisplayEntries);
				if (showFogRelatedUi)
				{
					TryAppendGuestFogSpeedEntry(_fogUiDisplayEntries);
				}
				TryAppendNextCampfireDistanceEntry(_fogUiDisplayEntries);
				TryAppendWeatherHazardEntries(_fogUiDisplayEntries);
			}
			if (showFogRelatedUi)
			{
				TryAppendFogApproachEntry(_fogUiDisplayEntries, isChineseLanguage);
			}
			else
			{
				TryAppendLavaApproachEntry(_fogUiDisplayEntries, isChineseLanguage);
			}
			if (!isLobbyScene)
			{
				TryAppendNextBiomeEntry(_fogUiDisplayEntries, isChineseLanguage);
			}
			TryLogGuestFogUiDiagnostics(isLobbyScene, showFogRelatedUi);
			ApplyFogUiEntries();
			return;
		}
		FogUiIconKind stateIconKind = GetFogRuntimeStateIconKind();
		if (!isLobbyScene && showFogRelatedUi && FogStateUiEnabled?.Value != false && stateIconKind != FogUiIconKind.StateWaiting && stateIconKind != FogUiIconKind.StateRunning)
		{
			_fogUiDisplayEntries.Add(new FogUiDisplayEntry(stateIconKind, ParseColorOrDefault("#A8E0A0"), GetFogStateUiEntryText(isChineseLanguage)));
		}
		if (isLobbyScene)
		{
			bool fogColdSuppression = IsFogColdSuppressionEnabled();
			bool nightColdEnabled = IsNightColdFeatureEnabled();
			_fogUiDisplayEntries.Add(new FogUiDisplayEntry(FogUiIconKind.FogHandling, ParseColorOrDefault(fogColdSuppression ? "#FFA64D" : "#9FFFA8"), GetFogHandlingUiEntryText(isChineseLanguage)));
			_fogUiDisplayEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Night, ParseColorOrDefault(nightColdEnabled ? "#9FFFA8" : "#FFB3B3"), GetNightColdUiEntryText(isChineseLanguage)));
			if (FogSpeedUiEnabled?.Value != false)
			{
				_fogUiDisplayEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Speed, ParseColorOrDefault("#8EC5FF"), GetLobbyFogSpeedUiEntryText(isChineseLanguage)));
			}
			_fogUiDisplayEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Delay, ParseColorOrDefault("#F2C75C"), GetLobbyFogDelayUiEntryText(isChineseLanguage)));
			ApplyFogUiEntries();
			return;
		}
		if (!isLobbyScene)
		{
			TryAppendRunTimerEntry(_fogUiDisplayEntries);
			TryAppendDayNightTimeEntry(_fogUiDisplayEntries, isChineseLanguage);
			TryAppendAltitudeEntry(_fogUiDisplayEntries);
			if (showFogRelatedUi && FogSpeedUiEnabled?.Value != false)
			{
				_fogUiDisplayEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Speed, ParseColorOrDefault("#8EC5FF"), GetFogSpeedUiEntryText(isChineseLanguage)));
			}
			TryAppendNextCampfireDistanceEntry(_fogUiDisplayEntries);
			TryAppendWeatherHazardEntries(_fogUiDisplayEntries);
		}
		if (showFogRelatedUi && TryAppendFogTimingEntry(_fogUiDisplayEntries, isChineseLanguage))
		{
			TryAppendDirectStartEntry(_fogUiDisplayEntries, isChineseLanguage);
			MoveLastFogUiEntryOfKindToEnd(FogUiIconKind.Buffer);
			MoveLastFogUiEntryOfKindToEnd(FogUiIconKind.Delay);
			ApplyFogUiEntries();
			return;
		}
		if (showFogRelatedUi)
		{
			TryAppendDirectStartEntry(_fogUiDisplayEntries, isChineseLanguage);
			TryAppendFogApproachEntry(_fogUiDisplayEntries, isChineseLanguage);
		}
		else
		{
			TryAppendLavaApproachEntry(_fogUiDisplayEntries, isChineseLanguage);
		}
		if (!isLobbyScene)
		{
			TryAppendNextBiomeEntry(_fogUiDisplayEntries, isChineseLanguage);
		}
		ApplyFogUiEntries();
	}


	private void TryLogGuestFogUiDiagnostics(bool isLobbyScene, bool showFogRelatedUi)
	{
		if (!EnableDebugDiagnostics || !IsReadOnlyFogUiViewer())
		{
			return;
		}
		float now = Time.unscaledTime;
		if (now - _lastGuestFogDiagLogTime < 3f)
		{
			return;
		}
		_lastGuestFogDiagLogTime = now;
		bool readOk = TryReadHostInstallState(out bool hostModEnabled);
		bool guestUsingVanilla = IsGuestUsingVanillaHostFog();
		string handlerInfo = ((_orbFogHandler == null) ? "null" : string.Format("origin={0},size={1:F1},moving={2},arrived={3},speed={4:F2},paused={5}", _orbFogHandler.currentID, _orbFogHandler.currentSize, _orbFogHandler.isMoving, _orbFogHandler.hasArrived, _orbFogHandler.speed, _fogPaused));
		bool hasRemaining = TryGetFogArrivalRemainingDistance(out float remainingDistance);
		bool hasEta = TryGetFogArrivalEtaSeconds(out float etaSeconds);
		bool hasSpeed = TryGetGuestEstimatedFogSpeed(out float estimatedSpeed);
		System.Text.StringBuilder entryBuilder = new System.Text.StringBuilder();
		foreach (FogUiDisplayEntry fogUiDisplayEntry in _fogUiDisplayEntries)
		{
			entryBuilder.Append(" ").Append(fogUiDisplayEntry.Kind);
		}
		base.Logger.LogInfo(string.Format("[Fog&ColdControl] [DIAG-GUEST] hostProp={0}(readOk={1}), guestUsingVanilla={2}, showFog={3}, lobby={4}, handler=[{5}], distance={6}({7:F1}), eta={8}({9:F1}s), speed={10}({11:F2}), entries=[{12}]", hostModEnabled, readOk, guestUsingVanilla, showFogRelatedUi, isLobbyScene, handlerInfo, hasRemaining, remainingDistance, hasEta, etaSeconds, hasSpeed, estimatedSpeed, entryBuilder.ToString()));
	}


	private void TryAppendGuestFogSpeedEntry(List<FogUiDisplayEntry> targetEntries)
	{
		if (targetEntries != null && FogSpeedUiEnabled?.Value != false && TryGetGuestEstimatedFogSpeed(out var estimatedSpeed))
		{
			targetEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Speed, ParseColorOrDefault("#8EC5FF"), GetGuestFogSpeedUiEntryText(estimatedSpeed)));
		}
	}


	private void TryAppendRunTimerEntry(List<FogUiDisplayEntry> targetEntries)
	{
		if (targetEntries != null && RunTimerUiEnabled?.Value != false && TryGetRunElapsedSeconds(out var elapsedSeconds))
		{
			targetEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Timer, ParseColorOrDefault("#B7C0CC"), GetRunTimerUiEntryText(elapsedSeconds)));
		}
	}


	private void MoveLastFogUiEntryOfKindToEnd(FogUiIconKind kind)
	{
		for (int index = _fogUiDisplayEntries.Count - 1; index >= 0; index--)
		{
			FogUiDisplayEntry entry = _fogUiDisplayEntries[index];
			if (entry.Kind == kind)
			{
				_fogUiDisplayEntries.RemoveAt(index);
				_fogUiDisplayEntries.Add(entry);
				break;
			}
		}
	}


	private bool TryAppendFogTimingEntry(List<FogUiDisplayEntry> targetEntries, bool isChineseLanguage)
	{
		float configuredDelay = Mathf.Max(FogDelay?.Value ?? 900f, 0f);
		if (FogTimingUiEnabled?.Value == false || _orbFogHandler == null || IsFogRemovedInCurrentScene() || _fogPaused || _orbFogHandler.isMoving || _orbFogHandler.hasArrived || _initialDelayCompleted)
		{
			return false;
		}
		if (_fogHiddenBufferTimer < 10f)
		{
			return false;
		}
		float delayRemaining = Mathf.Max(configuredDelay - _fogDelayTimer, 0f);
		if (delayRemaining <= 0.05f)
		{
			return false;
		}
		float danger = ((configuredDelay <= 0.01f) ? 1f : Mathf.Clamp01(1f - delayRemaining / configuredDelay));
		string delayLabelColor = LerpHexColor(FogUiDelayCountdownStartLabelColor, FogUiDelayCountdownEndLabelColor, danger);
		targetEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Delay, ParseColorOrDefault(delayLabelColor), GetFogTimingUiEntryText(isChineseLanguage)));
		return true;
	}


	private void TryAppendDayNightTimeEntry(List<FogUiDisplayEntry> targetEntries, bool isChineseLanguage)
	{
		if (DayNightTimeUiEnabled?.Value != false && TryGetDayNightDisplay(out var isDay, out var phaseProgress, out var _, out var dayCount))
		{
			string iconColor = (isDay ? "#FFE38A" : "#AFC6FF");
			targetEntries.Add(new FogUiDisplayEntry(isDay ? FogUiIconKind.DayTime : FogUiIconKind.NightTime, ParseColorOrDefault(iconColor), GetDayNightProgressUiEntryText(isDay, phaseProgress, dayCount)));
		}
	}


	private void TryAppendNextBiomeEntry(List<FogUiDisplayEntry> targetEntries, bool isChineseLanguage)
	{
		if (targetEntries != null && NextBiomeUiEnabled?.Value != false && TryGetNextBiomeDisplayName(isChineseLanguage, out var biomeName))
		{
			targetEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Biome, ParseColorOrDefault("#9FD7A6"), GetNextBiomeUiEntryText(biomeName)));
		}
	}


	private void TryAppendNextCampfireDistanceEntry(List<FogUiDisplayEntry> targetEntries)
	{
		if (targetEntries == null || (CampfireLocatorUiEnabled != null && CampfireLocatorUiEnabled.Value) || CampfireDistanceUiEnabled?.Value == false)
		{
			return;
		}
		if (TryGetNextCampfireDistance(out var distance))
		{
			targetEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Campfire, ParseColorOrDefault("#FFB864"), GetNextCampfireDistanceUiEntryText(distance)));
		}
	}


	private void TryAppendAltitudeEntry(List<FogUiDisplayEntry> targetEntries)
	{
		if (targetEntries == null || AltitudeUiEnabled?.Value == false || !TryResolveFogUiSubjectPosition(out var subjectPosition))
		{
			return;
		}
		// 有下一个未点燃篝火时显示"当前 / 篝火目标高度"；无篝火时不显示顶峰常量目标（1920m），仅显示当前高度。
		if (TryResolveNextCampfireTarget(out var campfirePosition))
		{
			targetEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Altitude, ParseColorOrDefault("#7ED6C5"), GetAltitudeUiEntryText(subjectPosition.y, campfirePosition.y)));
		}
		else
		{
			targetEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Altitude, ParseColorOrDefault("#7ED6C5"), GetAltitudeUiEntryText(subjectPosition.y)));
		}
	}


	private void TryAppendWeatherHazardEntries(List<FogUiDisplayEntry> targetEntries)
	{
		if (targetEntries == null || WeatherHazardUiEnabled?.Value == false || LoadingScreenHandler.loading)
		{
			return;
		}
		StormVisual[] visuals = ResolveCachedStormVisuals();
		if (visuals == null || visuals.Length == 0)
		{
			return;
		}
		if (!TryResolveFogUiSubjectPosition(out var subjectPosition))
		{
			return;
		}
		WindChillZone selectedZone = null;
		FogUiIconKind selectedKind = FogUiIconKind.Wind;
		string selectedColorHex = "#A8D8F0";
		float selectedRemaining = 0f;
		bool selectedIsWarning = false;
		foreach (StormVisual visual in visuals)
		{
			if (visual == null)
			{
				continue;
			}
			FogUiIconKind iconKind;
			string colorHex;
			if (!TryMapWeatherStormType(visual.stormType, out iconKind, out colorHex))
			{
				continue;
			}
			WindChillZone zone = ReadStormVisualZone(visual);
			if (zone == null || !zone.windZoneBounds.Contains(subjectPosition))
			{
				continue;
			}
			LogWindChillZoneFields(zone);
			float untilSwitch = ReadWindChillUntilSwitch(zone);
			if (zone.windActive)
			{
				float remaining = Mathf.Max(untilSwitch, 0f);
				if (remaining <= 0.05f)
				{
					continue;
				}
				selectedZone = zone;
				selectedKind = iconKind;
				selectedColorHex = colorHex;
				selectedRemaining = remaining;
				selectedIsWarning = false;
				break;
			}
			if (selectedZone != null)
			{
				continue;
			}
			if (untilSwitch > 0.05f && untilSwitch <= WeatherIncomingWarningSeconds)
			{
				selectedZone = zone;
				selectedKind = iconKind;
				selectedColorHex = colorHex;
				selectedRemaining = Mathf.Max(untilSwitch, 0f);
				selectedIsWarning = true;
			}
		}
		if (selectedZone != null)
		{
			_weatherUiShownZone = selectedZone;
			_weatherUiShownKind = selectedKind;
			_weatherUiShownColorHex = selectedColorHex;
			_weatherUiShownRemaining = selectedRemaining;
			_weatherUiShownIsWarning = selectedIsWarning;
			_weatherUiGraceUntil = -1f;
			AppendWeatherHazardEntry(targetEntries);
			return;
		}
		if (_weatherUiShownZone == null)
		{
			return;
		}
		float now = Time.unscaledTime;
		if (_weatherUiGraceUntil < 0f)
		{
			_weatherUiGraceUntil = now + FogUiWeatherGraceSeconds;
		}
		if (now >= _weatherUiGraceUntil)
		{
			ResetWeatherHazardUiState();
			return;
		}
		_weatherUiShownRemaining = Mathf.Max(_weatherUiShownRemaining - Time.unscaledDeltaTime, 0f);
		if (_weatherUiShownRemaining <= 0.05f)
		{
			ResetWeatherHazardUiState();
			return;
		}
		AppendWeatherHazardEntry(targetEntries);
	}


	private void AppendWeatherHazardEntry(List<FogUiDisplayEntry> targetEntries)
	{
		if (_weatherUiShownRemaining <= 0.05f)
		{
			ResetWeatherHazardUiState();
			return;
		}
		string countdownText = FormatFogEtaSeconds(_weatherUiShownRemaining);
		Color iconColor;
		Color gradientLeft;
		Color gradientRight;
		if (_weatherUiShownIsWarning)
		{
			iconColor = ParseColorOrDefault("#FFC859");
			gradientLeft = ParseColorOrDefault("#FFC859");
			gradientRight = ParseColorOrDefault("#FF7A45");
		}
		else
		{
			iconColor = ParseColorOrDefault(_weatherUiShownColorHex);
			gradientLeft = ParseColorOrDefault(_weatherUiShownColorHex);
			gradientRight = Color.white;
		}
		targetEntries.Add(new FogUiDisplayEntry(_weatherUiShownKind, iconColor, countdownText, gradientLeft, gradientRight));
	}


	private void ResetWeatherHazardUiState()
	{
		_weatherUiShownZone = null;
		_weatherUiShownIsWarning = false;
		_weatherUiGraceUntil = -1f;
	}


	private static bool TryMapWeatherStormType(StormVisual.StormType stormType, out FogUiIconKind iconKind, out string colorHex)
	{
		switch (stormType)
		{
		case StormVisual.StormType.Wind:
			iconKind = FogUiIconKind.Wind;
			colorHex = "#A8D8F0";
			return true;
		case StormVisual.StormType.Snow:
			iconKind = FogUiIconKind.Snowstorm;
			colorHex = "#B8CCFF";
			return true;
		case StormVisual.StormType.Rain:
			iconKind = FogUiIconKind.Rain;
			colorHex = "#9FB8E8";
			return true;
		default:
			iconKind = FogUiIconKind.Wind;
			colorHex = "#A8D8F0";
			return false;
		}
	}


	private void TryAppendFogApproachEntry(List<FogUiDisplayEntry> targetEntries, bool isChineseLanguage)
	{
		if (targetEntries != null && FogApproachUiEnabled?.Value != false && TryGetFogArrivalRemainingDistance(out var remainingDistance) && !(remainingDistance <= 0.05f))
		{
			GetFogApproachColors(remainingDistance, out var labelColor, out var _);
			targetEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Distance, ParseColorOrDefault(labelColor), GetFogApproachUiEntryText(isChineseLanguage, remainingDistance)));
		}
	}


	private void TryAppendLavaApproachEntry(List<FogUiDisplayEntry> targetEntries, bool isChineseLanguage)
	{
		if (targetEntries == null || LavaApproachUiEnabled?.Value == false || !TryGetLavaApproach(out var distance, out var etaSeconds, out var hasEta))
		{
			return;
		}
		if (IsVoidGhostsHazardCurrent())
		{
			targetEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Petrify, ParseColorOrDefault("#A9B7C6"), GetPetrifyApproachUiEntryText(distance, etaSeconds, hasEta)));
			return;
		}
		targetEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Lava, ParseColorOrDefault("#FF7A3D"), GetLavaApproachUiEntryText(distance, etaSeconds, hasEta)));
	}


	private void TryAppendFogDistanceEntry(List<FogUiDisplayEntry> targetEntries, bool isChineseLanguage)
	{
		if (TryGetFogArrivalRemainingDistance(out var remainingDistance) && !(remainingDistance <= 0.05f))
		{
			GetFogDistanceColors(remainingDistance, out var labelColor, out var _);
			targetEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Distance, ParseColorOrDefault(labelColor), GetFogDistanceUiEntryText(isChineseLanguage)));
		}
	}


	private void TryAppendDirectStartEntry(List<FogUiDisplayEntry> targetEntries, bool isChineseLanguage)
	{
		if (DirectStartUiEnabled?.Value == false || _orbFogHandler == null || IsAirportScene(SceneManager.GetActiveScene()) || IsFogRemovedInCurrentScene() || _fogPaused || _orbFogHandler.isMoving || _orbFogHandler.hasArrived || _initialDelayCompleted || !ShouldEnforceConfiguredDelay(_orbFogHandler))
		{
			return;
		}
		switch (GetVanillaProgressStartUiState())
		{
		case VanillaProgressStartUiState.Unavailable:
			targetEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Direct, ParseColorOrDefault("#B7C0CC"), GetDirectStartUiEntryText(isChineseLanguage)));
			break;
		case VanillaProgressStartUiState.Tracking:
		{
			if (!TryGetVanillaProgressStartProgress(_orbFogHandler, out var passedCount, out var totalCount, out var _, out var _))
			{
				targetEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Direct, ParseColorOrDefault("#B7C0CC"), GetDirectStartUiEntryText(isChineseLanguage)));
				break;
			}
			float progress = ((totalCount <= 0) ? 0f : Mathf.Clamp01((float)passedCount / (float)totalCount));
			string valueColor = LerpHexColor("#E2EAF3", "#B5FFB8", progress);
			targetEntries.Add(new FogUiDisplayEntry(FogUiIconKind.Direct, ParseColorOrDefault(valueColor), GetDirectStartUiEntryText(isChineseLanguage)));
			break;
		}
		}
	}


	private void ApplyFogUiEntries()
	{
		if (_fogUiEntriesRect == null)
		{
			return;
		}
		UpdateFogUiTheme();
		EnsureFogUiEntryViewCount(_fogUiDisplayEntries.Count);
		bool layoutDirty = _fogUiLayoutDirty || _lastFogUiEntryLayoutCount != _fogUiDisplayEntries.Count;
		_fogUiLayoutDirty = false;
		_lastFogUiEntryLayoutCount = _fogUiDisplayEntries.Count;
		_fogUiStateAnimTime += Time.deltaTime;
		for (int i = 0; i < _fogUiEntryViews.Count; i++)
		{
			FogUiEntryView view = _fogUiEntryViews[i];
			bool shouldBeVisible = i < _fogUiDisplayEntries.Count;
			if (view?.Root == null)
			{
				continue;
			}
			if (view.Root.gameObject.activeSelf != shouldBeVisible)
			{
				view.Root.gameObject.SetActive(shouldBeVisible);
				layoutDirty = true;
			}
			if (shouldBeVisible)
			{
				FogUiDisplayEntry entry = _fogUiDisplayEntries[i];
				Color themedIconColor = ApplyFogUiThemeToIconColor(entry.IconColor);
				float animAlpha = GetStateIconAnimAlpha(entry.Kind);
				if (animAlpha < 1f)
				{
					themedIconColor.a *= animAlpha;
				}
				bool kindChanged = view.LastIconKind != entry.Kind;
				bool textChanged = view.LastText == null || !string.Equals(view.LastText, entry.Text, StringComparison.Ordinal);
				bool progressBarChanged = view.LastHasProgressBar != entry.HasProgressBar;
				bool needsResize = view.LastContentWidth < 0f || kindChanged || textChanged || progressBarChanged;
				if (view.Icon != null)
				{
					Sprite resolvedIconSprite = GetFogUiIconSprite(entry.Kind);
					if (kindChanged || view.LastIconColor != themedIconColor || view.Icon.sprite != resolvedIconSprite)
					{
						view.Icon.sprite = resolvedIconSprite;
						view.Icon.color = themedIconColor;
						view.LastIconKind = entry.Kind;
						view.LastIconColor = themedIconColor;
					}
				}
				if (view.Text != null && textChanged)
				{
					view.LastText = entry.Text;
					view.Text.text = entry.Text;
				}
				if (view.Text != null)
				{
					if (entry.HasTextGradient)
					{
						if (!view.LastHasTextGradient || view.LastGradientLeft != entry.TextGradientLeft || view.LastGradientRight != entry.TextGradientRight)
						{
							view.Text.colorGradient = new VertexGradient(entry.TextGradientLeft, entry.TextGradientRight, entry.TextGradientLeft, entry.TextGradientRight);
							view.Text.color = Color.white;
							view.LastHasTextGradient = true;
							view.LastGradientLeft = entry.TextGradientLeft;
							view.LastGradientRight = entry.TextGradientRight;
						}
					}
					else
					{
						if (view.LastHasTextGradient)
						{
							view.Text.colorGradient = new VertexGradient(Color.white);
							view.LastHasTextGradient = false;
							view.LastGradientLeft = Color.clear;
							view.LastGradientRight = Color.clear;
						}
						Color baseTextColor = GetFogUiBaseTextColor();
						if (view.LastTextColor != baseTextColor)
						{
							view.Text.color = baseTextColor;
							view.LastTextColor = baseTextColor;
						}
					}
				}
				ApplyFogUiEntryProgressBar(view, entry);
				if (needsResize)
				{
					float previousContentWidth = view.LastContentWidth;
					float rawContentWidth = ResolveFogUiEntryPreferredContentWidth(view, entry) + 2f;
					float measuredWidth = Mathf.Ceil(rawContentWidth / FogUiEntryWidthQuantum) * FogUiEntryWidthQuantum;
					// 宽度贴合实测文字宽度；仅当明显变窄（超过一个量化步长）时才允许收缩，避免数字字形差异带来的抖动。
					if (kindChanged || progressBarChanged || previousContentWidth < 0f || measuredWidth + FogUiEntryWidthQuantum < previousContentWidth)
					{
						view.LastContentWidth = measuredWidth;
					}
					else
					{
						view.LastContentWidth = Mathf.Max(previousContentWidth, measuredWidth);
					}
					view.LastHasProgressBar = entry.HasProgressBar;
					if (!Approximately(previousContentWidth, view.LastContentWidth))
					{
						layoutDirty = true;
					}
				}
				float contentWidth = view.LastContentWidth;
				float entryWidth = FogUiIconSize + FogUiEntryIconTextSpacing + contentWidth;
				if (view.EntryLayoutElement != null)
				{
					if (!Approximately(view.EntryLayoutElement.minWidth, entryWidth) || !Approximately(view.EntryLayoutElement.preferredWidth, entryWidth))
					{
						view.EntryLayoutElement.minWidth = entryWidth;
						view.EntryLayoutElement.preferredWidth = entryWidth;
						layoutDirty = true;
					}
				}
				if (view.TextLayoutElement != null)
				{
					if (!Approximately(view.TextLayoutElement.minWidth, contentWidth) || !Approximately(view.TextLayoutElement.preferredWidth, contentWidth))
					{
						view.TextLayoutElement.minWidth = contentWidth;
						view.TextLayoutElement.preferredWidth = contentWidth;
						layoutDirty = true;
					}
				}
			}
		}
		if (layoutDirty)
		{
			RebuildFogUiLayout();
		}
		UpdateFogUiBackgroundSize();
	}


	private void UpdateFogUiBackgroundSize()
	{
		if (_fogUiBackgroundImage == null || _fogUiEntriesRect == null || _fogUiRect == null)
		{
			return;
		}
		if (IsFogUiVerticalLayout())
		{
			return;
		}
		float contentWidth = 0f;
		int visibleCount = 0;
		for (int i = 0; i < _fogUiEntryViews.Count; i++)
		{
			FogUiEntryView view = _fogUiEntryViews[i];
			if (view?.Root == null || !view.Root.gameObject.activeSelf)
			{
				continue;
			}
			contentWidth += FogUiIconSize + FogUiEntryIconTextSpacing + Mathf.Max(view.LastContentWidth, 0f);
			visibleCount++;
		}
		if (visibleCount <= 0)
		{
			// 所有条目开关均关闭时收缩背景，避免残留空白背景。
			RectTransform emptyBackgroundRect = _fogUiBackgroundImage.rectTransform;
			emptyBackgroundRect.anchorMin = Vector2.zero;
			emptyBackgroundRect.anchorMax = Vector2.zero;
			emptyBackgroundRect.pivot = Vector2.zero;
			emptyBackgroundRect.anchoredPosition = Vector2.zero;
			emptyBackgroundRect.sizeDelta = Vector2.zero;
			return;
		}
		bool compact = _fogUiRect != null && _fogUiRect.sizeDelta.x < FogUiCompactWidthThreshold;
		float spacing = ResolveFogUiEntrySpacing(compact);
		contentWidth += spacing * Mathf.Max(visibleCount - 1, 0) + spacing * 2f;
		RectTransform backgroundRect = _fogUiBackgroundImage.rectTransform;
		backgroundRect.anchorMin = Vector2.zero;
		backgroundRect.anchorMax = Vector2.zero;
		backgroundRect.pivot = Vector2.zero;
		backgroundRect.anchoredPosition = Vector2.zero;
		backgroundRect.sizeDelta = new Vector2(contentWidth, _fogUiRect.sizeDelta.y);
	}


	private void RebuildFogUiLayout()
	{
		if (_fogUiEntriesRect == null)
		{
			return;
		}
		if (IsFogUiVerticalLayout())
		{
			UpdateFogUiVerticalContainerSize();
		}
		LayoutRebuilder.MarkLayoutForRebuild(_fogUiEntriesRect);
		LayoutRebuilder.ForceRebuildLayoutImmediate(_fogUiEntriesRect);
		if (_fogUiRect != null)
		{
			LayoutRebuilder.MarkLayoutForRebuild(_fogUiRect);
		}
	}


	private void UpdateFogUiVerticalContainerSize()
	{
		if (_fogUiRect == null || _fogUiEntriesRect == null)
		{
			return;
		}
		float maxEntryWidth = 0f;
		int visibleCount = 0;
		for (int i = 0; i < _fogUiEntryViews.Count; i++)
		{
			FogUiEntryView view = _fogUiEntryViews[i];
			if (view?.Root == null || !view.Root.gameObject.activeSelf)
			{
				continue;
			}
			visibleCount++;
			if (view.EntryLayoutElement != null)
			{
				maxEntryWidth = Mathf.Max(maxEntryWidth, view.EntryLayoutElement.preferredWidth);
			}
		}
		float spacing = ResolveFogUiEntrySpacing(compact: false);
		float targetWidth = Mathf.Max(FogUiVerticalContainerWidth, maxEntryWidth + spacing * 2f);
		float targetHeight = Mathf.Max(FogUiVerticalMinHeight, visibleCount * FogUiEntryHeight + Mathf.Max(visibleCount - 1, 0) * spacing + FogUiBackgroundVerticalPadding * 2f);
		if (!Approximately(_fogUiEntriesRect.sizeDelta.x, targetWidth) || !Approximately(_fogUiEntriesRect.sizeDelta.y, targetHeight))
		{
			_fogUiEntriesRect.sizeDelta = new Vector2(targetWidth, targetHeight);
			_fogUiRect.sizeDelta = new Vector2(targetWidth, targetHeight);
		}
	}


	private static float ResolveFogUiEntryPreferredContentWidth(FogUiEntryView view, FogUiDisplayEntry entry)
	{
		if (entry.HasProgressBar || view?.Text == null)
		{
			return 128f;
		}
		float preferredWidth = 0f;
		try
		{
			preferredWidth = view.Text.GetPreferredValues(entry.Text ?? string.Empty).x;
		}
		catch
		{
			preferredWidth = view.Text.preferredWidth;
		}
		if (preferredWidth <= 0f)
		{
			preferredWidth = ResolveStableFogUiEntryContentWidth(entry.Kind);
		}
		return Mathf.Clamp(preferredWidth, 0f, FogUiEntryMaxWidth);
	}


	private static float ResolveStableFogUiEntryContentWidth(FogUiIconKind kind)
	{
		switch (kind)
		{
		case FogUiIconKind.Speed:
			return 36f;
		case FogUiIconKind.Timer:
		case FogUiIconKind.Eta:
			return 56f;
		case FogUiIconKind.Buffer:
		case FogUiIconKind.Delay:
			return 56f;
		case FogUiIconKind.Campfire:
			return 44f;
		case FogUiIconKind.Altitude:
		return 108f;
		case FogUiIconKind.Lava:
		case FogUiIconKind.Petrify:
		case FogUiIconKind.Distance:
			return 96f;
		case FogUiIconKind.Wind:
		case FogUiIconKind.Snowstorm:
		case FogUiIconKind.Rain:
			return 56f;
		case FogUiIconKind.DayTime:
		case FogUiIconKind.NightTime:
			return 76f;
		default:
			return 0f;
		}
	}


	private float GetStateIconAnimAlpha(FogUiIconKind iconKind)
	{
		switch (iconKind)
		{
		case FogUiIconKind.StateRunning:
			return 0.75f + Mathf.PingPong(_fogUiStateAnimTime * 0.8f, 0.25f);
		case FogUiIconKind.StateWaiting:
			return 0.6f + Mathf.PingPong(_fogUiStateAnimTime * 1.2f, 0.4f);
		case FogUiIconKind.StatePaused:
			if (!(Mathf.Repeat(_fogUiStateAnimTime, 2f) < 0.2f))
			{
				return 1f;
			}
			return 0.3f;
		default:
			return 1f;
		}
	}


	private static void ApplyFogUiEntryProgressBar(FogUiEntryView view, FogUiDisplayEntry entry)
	{
		if (view == null)
		{
			return;
		}
		if (view.Text != null && view.Text.gameObject.activeSelf == entry.HasProgressBar)
		{
			view.Text.gameObject.SetActive(!entry.HasProgressBar);
		}
		if (view.BarRoot != null && view.BarRoot.gameObject.activeSelf != entry.HasProgressBar)
		{
			view.BarRoot.gameObject.SetActive(entry.HasProgressBar);
		}
		if (!entry.HasProgressBar)
		{
			view.LastProgress = -1f;
			return;
		}
		if (view.BarBackground != null && view.LastBarBackgroundColor != entry.BarBackgroundColor)
		{
			view.BarBackground.color = entry.BarBackgroundColor;
			view.LastBarBackgroundColor = entry.BarBackgroundColor;
		}
		if (view.BarFill != null)
		{
			if (Mathf.Abs(view.LastProgress - entry.Progress) > 0.001f)
			{
				view.BarFill.fillAmount = entry.Progress;
				view.LastProgress = entry.Progress;
			}
			if (view.LastBarFillColor != entry.BarFillColor)
			{
				view.BarFill.color = entry.BarFillColor;
				view.LastBarFillColor = entry.BarFillColor;
			}
		}
		if (view.BarText != null && !string.Equals(view.LastBarText, entry.BarText, StringComparison.Ordinal))
		{
			view.LastBarText = entry.BarText;
			view.BarText.text = entry.BarText;
		}
		if (view.BarText != null)
		{
			Color baseBarTextColor = GetFogUiBaseTextColor();
			if (view.LastTextColor != baseBarTextColor)
			{
				view.BarText.color = baseBarTextColor;
				view.LastTextColor = baseBarTextColor;
			}
		}
	}


	private static string BuildFogUiBadge(string tag, string tagColor, string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}
		string normalizedTag = (string.IsNullOrWhiteSpace(tag) ? string.Empty : ("[" + tag.Trim().ToUpperInvariant() + "]"));
		if (string.IsNullOrEmpty(normalizedTag))
		{
			return value.Trim();
		}
		return Colorize(normalizedTag, tagColor) + " " + value.Trim();
	}


	private void UpdateFogUiTheme()
	{
		FogUiThemeKind themeKind = ResolveFogUiThemeKind();
		if (!_fogUiThemeInitialized || _fogUiThemeKind != themeKind)
		{
			_fogUiThemeKind = themeKind;
			_fogUiThemeInitialized = true;
		}
	}


	private FogUiThemeKind ResolveFogUiThemeKind()
	{
		if (IsFogUiSubjectInsideFog())
		{
			return FogUiThemeKind.Fog;
		}
		if (TryGetDayNightDisplay(out var isDay, out var _, out var _, out var _))
		{
			if (!isDay)
			{
				return FogUiThemeKind.Night;
			}
			return FogUiThemeKind.Day;
		}
		return FogUiThemeKind.Day;
	}


	private bool IsFogUiSubjectInsideFog()
	{
		if (TryResolveFogUiSubjectCharacter(out var subjectCharacter) && IsCharacterInsideAnyFog(subjectCharacter))
		{
			return true;
		}
		if (TryGetFogArrivalRemainingDistance(out var remainingDistance))
		{
			return remainingDistance <= 0.05f;
		}
		return false;
	}


	private static void GetFogUiThemeColors(FogUiThemeKind themeKind, out Color textTint, out Color iconTint)
	{
		switch (themeKind)
		{
		case FogUiThemeKind.Fog:
			textTint = new Color(1f, 0.62f, 0.56f, 1f);
			iconTint = new Color(1f, 0.24f, 0.18f, 1f);
			break;
		case FogUiThemeKind.Night:
			textTint = new Color(0.82f, 0.74f, 1f, 1f);
			iconTint = new Color(0.77f, 0.47f, 0.93f, 1f);
			break;
		default:
			textTint = new Color(1f, 0.92f, 0.68f, 1f);
			iconTint = new Color(1f, 0.78f, 0.24f, 1f);
			break;
		}
	}


	private static string PickMoreUrgentFogUiColor(string distanceColor, string etaColor)
	{
		if (!TryParseHtmlColor(distanceColor, out var parsedDistanceColor))
		{
			return etaColor;
		}
		if (!TryParseHtmlColor(etaColor, out var parsedEtaColor))
		{
			return distanceColor;
		}
		float distanceUrgency = parsedDistanceColor.r - parsedDistanceColor.g * 0.35f;
		if (!(parsedEtaColor.r - parsedEtaColor.g * 0.35f > distanceUrgency))
		{
			return distanceColor;
		}
		return etaColor;
	}


	private static string FormatSmallUiUnit(string unit)
	{
		if (string.IsNullOrEmpty(unit))
		{
			return string.Empty;
		}
		return $"<size={78}%>{unit}</size>";
	}


	private string GetVanillaProgressStartUiLabel(bool isChineseLanguage)
	{
		if (_orbFogHandler == null || IsAirportScene(SceneManager.GetActiveScene()) || IsFogRemovedInCurrentScene() || _fogPaused)
		{
			return string.Empty;
		}
		if (_orbFogHandler.isMoving || _orbFogHandler.hasArrived || _initialDelayCompleted || !ShouldEnforceConfiguredDelay(_orbFogHandler))
		{
			return string.Empty;
		}
		string label = (isChineseLanguage ? "切入:" : "DirectStart:");
		switch (GetVanillaProgressStartUiState())
		{
		case VanillaProgressStartUiState.Unavailable:
			return Colorize(label, "#B7C0CC") + " " + Colorize(isChineseLanguage ? "当前段无原版阈值" : "N/A", "#FFB3B3");
		default:
			return string.Empty;
		case VanillaProgressStartUiState.Tracking:
		{
			if (!TryGetVanillaProgressStartProgress(_orbFogHandler, out var passedCount, out var totalCount, out var _, out var _))
			{
				return Colorize(label, "#B7C0CC") + " " + Colorize(isChineseLanguage ? "进度触发" : "Progress", "#E2EAF3");
			}
			float progress = ((totalCount <= 0) ? 0f : Mathf.Clamp01((float)passedCount / (float)totalCount));
			string valueColor = LerpHexColor("#E2EAF3", "#B5FFB8", progress);
			string valueText = (isChineseLanguage ? $"推进 {passedCount}/{totalCount}" : $"{passedCount}/{totalCount}");
			return Colorize(label, "#B7C0CC") + " " + Colorize(valueText, valueColor);
		}
		}
	}


	private string GetFogStateUiEntryText(bool isChineseLanguage)
	{
		return GetFogRuntimeStateLabel(isChineseLanguage);
	}


	private string GetGuestFogStateUiEntryText(bool isChineseLanguage)
	{
		return GetGuestFogRuntimeStateLabel(isChineseLanguage);
	}


	private FogUiIconKind GetFogRuntimeStateIconKind()
	{
		if (!IsModFeatureEnabled())
		{
			return FogUiIconKind.StateOff;
		}
		if (IsAirportScene(SceneManager.GetActiveScene()))
		{
			return FogUiIconKind.StateLobby;
		}
		if (IsFogRemovedInCurrentScene())
		{
			return FogUiIconKind.StateOff;
		}
		if (_fogPaused)
		{
			return FogUiIconKind.StatePaused;
		}
		if (_orbFogHandler == null)
		{
			return FogUiIconKind.StateSyncing;
		}
		if (!_initialDelayCompleted || ShouldEnforceConfiguredDelay(_orbFogHandler))
		{
			return FogUiIconKind.StateWaiting;
		}
		if (_orbFogHandler.isMoving)
		{
			return FogUiIconKind.StateRunning;
		}
		if (_orbFogHandler.hasArrived)
		{
			return FogUiIconKind.StateArrived;
		}
		return FogUiIconKind.StateIdle;
	}


	private FogUiIconKind GetGuestFogRuntimeStateIconKind()
	{
		if (!IsModFeatureEnabled())
		{
			return FogUiIconKind.StateOff;
		}
		if (IsAirportScene(SceneManager.GetActiveScene()))
		{
			return FogUiIconKind.StateLobby;
		}
		if (IsFogRemovedInCurrentScene())
		{
			return FogUiIconKind.StateOff;
		}
		if (IsGuestUsingVanillaHostFog())
		{
			return FogUiIconKind.StateOff;
		}
		if (_orbFogHandler == null)
		{
			return FogUiIconKind.StateSyncing;
		}
		if (_orbFogHandler.isMoving)
		{
			return FogUiIconKind.StateRunning;
		}
		if (_orbFogHandler.hasArrived)
		{
			return FogUiIconKind.StateArrived;
		}
		if (!_initialDelayCompleted || ShouldEnforceConfiguredDelay(_orbFogHandler))
		{
			return FogUiIconKind.StateWaiting;
		}
		if (!_orbFogHandler.isMoving && !_orbFogHandler.hasArrived)
		{
			return FogUiIconKind.StateWaiting;
		}
		return FogUiIconKind.StateIdle;
	}


	private string GetFogSpeedUiEntryText(bool isChineseLanguage)
	{
		return Colorize(FormatFogSpeedValue(_fogPaused ? 0f : ((_orbFogHandler != null) ? _orbFogHandler.speed : (FogSpeed?.Value ?? 0.4f))), "#D6F1FF");
	}


	private static string GetGuestFogSpeedUiEntryText(float estimatedSpeed)
	{
		return Colorize(FormatGuestFogSpeedDisplayValue(estimatedSpeed), "#D6F1FF");
	}


	private static string GetRunTimerUiEntryText(float elapsedSeconds)
	{
		return Colorize(FormatRunElapsedTime(elapsedSeconds), "#E2EAF3");
	}


	private static string GetDayNightProgressUiEntryText(bool isDay, float phaseProgress, int dayCount)
	{
		string color = (isDay ? "#FFE38A" : "#AFC6FF");
		string progressText = Colorize(string.Format("{0}{1}", Mathf.RoundToInt(Mathf.Clamp01(phaseProgress) * 100f), FormatSmallUiUnit("%")), color);
		string separatorText = Colorize("/", color);
		string dayText = Colorize(string.Format("{0} {1}", dayCount, FormatDayCountUnit()), color);
		return string.Format("{0} {1} {2}", progressText, separatorText, dayText);
	}


	private static string GetNextBiomeUiEntryText(string biomeName)
	{
		return Colorize(biomeName, "#E0F5E2");
	}


	private static string GetNextCampfireDistanceUiEntryText(float distance)
	{
		return Colorize(FormatFogDistanceValue(distance), "#FFE6BF");
	}


	private static string GetAltitudeUiEntryText(float altitudeUnits)
	{
		// 与游戏 CharacterStats.heightInMeters 相同的 units→meters 换算（×1.6）。
		float metersPerUnit = CharacterStats.unitsToMeters;
		return Colorize(FormatFogDistanceValue(altitudeUnits * metersPerUnit), "#A5EBDF");
	}


	private static string GetAltitudeUiEntryText(float altitudeUnits, float targetHeightUnits)
	{
		float metersPerUnit = CharacterStats.unitsToMeters;
		string currentText = FormatFogDistanceValue(altitudeUnits * metersPerUnit);
		string targetText = FormatFogDistanceValue(targetHeightUnits * metersPerUnit);
		return Colorize(currentText, "#A5EBDF") + " " + Colorize("/", "#B7C0CC") + " " + Colorize(targetText, "#8FC9BC");
	}


	private string GetFogApproachUiEntryText(bool isChineseLanguage, float remainingDistance)
	{
		GetFogApproachColors(remainingDistance, out var labelColor, out var valueColor);
		string distanceText = Colorize(FormatFogDistanceValue(remainingDistance), valueColor);
		if (_orbFogHandler == null || IsFogRemovedInCurrentScene() || _fogPaused || !_orbFogHandler.isMoving || _orbFogHandler.hasArrived)
		{
			return distanceText;
		}
		if (!TryGetFogArrivalEtaSeconds(out var etaSeconds))
		{
			return distanceText + " " + Colorize("/", "#B7C0CC") + " " + Colorize("--:--", "#E2EAF3");
		}
		if (etaSeconds <= 0.05f)
		{
			return distanceText;
		}
		float displayedEtaSeconds = QuantizeFogEtaSeconds(etaSeconds);
		GetFogEtaColors(displayedEtaSeconds, out labelColor, out var etaValueColor);
		return distanceText + " " + Colorize("/", "#B7C0CC") + " " + Colorize(FormatFogEtaTime(displayedEtaSeconds), etaValueColor);
	}


	private string GetLavaApproachUiEntryText(float distance, float etaSeconds, bool hasEta)
	{
		string distanceText = Colorize(FormatFogDistanceValue(distance), "#FFD1A6");
		string timeText = (hasEta ? FormatFogEtaTime(etaSeconds) : "--:--");
		return distanceText + " " + Colorize("/", "#B7C0CC") + " " + Colorize(timeText, "#FFD1A6");
	}


	private string GetPetrifyApproachUiEntryText(float distance, float etaSeconds, bool hasEta)
	{
		string distanceText = Colorize(FormatFogDistanceValue(distance), "#D5DEE8");
		string timeText = (hasEta ? FormatFogEtaTime(etaSeconds) : "--:--");
		return distanceText + " " + Colorize("/", "#B7C0CC") + " " + Colorize(timeText, "#D5DEE8");
	}


	private string GetLobbyPauseUiEntryText(bool isChineseLanguage)
	{
		return Colorize(GetFogPauseHotkeyLabel(), "#E2EAF3");
	}


	private string GetLobbyFogSpeedUiEntryText(bool isChineseLanguage)
	{
		float speed = FogSpeed?.Value ?? 0.4f;
		return Colorize(FormatFogSpeedValue(speed), "#D6F1FF");
	}


	private string GetLobbyFogDelayUiEntryText(bool isChineseLanguage)
	{
		float delay = Mathf.Max(FogDelay?.Value ?? 900f, 0f);
		return Colorize(FormatFogDelaySeconds(delay), "#FFE8A3");
	}


	private string GetFogHandlingUiEntryText(bool isChineseLanguage)
	{
		bool blocked = IsFogColdSuppressionEnabled();
		string label = isChineseLanguage ? "毒雾寒冷" : "Fog Cold";
		string state = blocked ? "OFF" : "ON";
		string stateColor = blocked ? "#FFA64D" : "#9FFFA8";
		return Colorize(label, "#E2EAF3") + " " + Colorize(state, stateColor);
	}


	private string GetFogTimingUiEntryText(bool isChineseLanguage)
	{
		float configuredDelay = Mathf.Max(FogDelay?.Value ?? 900f, 0f);
		if (_orbFogHandler == null)
		{
			return Colorize(FormatFogDelaySeconds(configuredDelay), "#FFE8A3");
		}
		if (IsFogRemovedInCurrentScene() || _fogPaused || _orbFogHandler.isMoving || _orbFogHandler.hasArrived || _initialDelayCompleted)
		{
			return string.Empty;
		}
		if (_fogHiddenBufferTimer < 10f)
		{
			return string.Empty;
		}
		float delayRemaining = Mathf.Max(configuredDelay - _fogDelayTimer, 0f);
		if (delayRemaining <= 0.05f)
		{
			return string.Empty;
		}
		float danger = ((configuredDelay <= 0.01f) ? 1f : Mathf.Clamp01(1f - delayRemaining / configuredDelay));
		string fogDelayValueColor = LerpHexColor(FogUiDelayCountdownStartValueColor, FogUiDelayCountdownEndValueColor, danger);
		return Colorize(FormatFogDelaySeconds(delayRemaining), fogDelayValueColor);
	}


	private string GetFogDistanceUiEntryText(bool isChineseLanguage)
	{
		if (!TryGetFogArrivalRemainingDistance(out var remainingDistance) || remainingDistance <= 0.05f)
		{
			return string.Empty;
		}
		GetFogDistanceColors(remainingDistance, out var _, out var valueColor);
		return Colorize(FormatFogDistanceValue(remainingDistance), valueColor);
	}


	private string GetDirectStartUiEntryText(bool isChineseLanguage)
	{
		if (_orbFogHandler == null || IsAirportScene(SceneManager.GetActiveScene()) || IsFogRemovedInCurrentScene() || _fogPaused)
		{
			return string.Empty;
		}
		if (_orbFogHandler.isMoving || _orbFogHandler.hasArrived || _initialDelayCompleted || !ShouldEnforceConfiguredDelay(_orbFogHandler))
		{
			return string.Empty;
		}
		switch (GetVanillaProgressStartUiState())
		{
		case VanillaProgressStartUiState.Unavailable:
			return Colorize("N/A", "#FFB3B3");
		default:
			return string.Empty;
		case VanillaProgressStartUiState.Tracking:
		{
			if (!TryGetVanillaProgressStartProgress(_orbFogHandler, out var passedCount, out var totalCount, out var _, out var _))
			{
				return Colorize(isChineseLanguage ? "进度触发" : "Progress", "#E2EAF3");
			}
			float progress = ((totalCount <= 0) ? 0f : Mathf.Clamp01((float)passedCount / (float)totalCount));
			string valueColor = LerpHexColor("#E2EAF3", "#B5FFB8", progress);
			return Colorize($"{passedCount}/{totalCount}", valueColor);
		}
		}
	}


	private VanillaProgressStartUiState GetVanillaProgressStartUiState()
	{
		if (_orbFogHandler == null || _orbFogHandler.isMoving || _orbFogHandler.hasArrived || _initialDelayCompleted || !ShouldEnforceConfiguredDelay(_orbFogHandler))
		{
			return VanillaProgressStartUiState.Hidden;
		}
		if (ShouldPauseVanillaProgressStartForJoiningPlayers())
		{
			return VanillaProgressStartUiState.Hidden;
		}
		if (!TryGetVanillaProgressStartThresholds(_orbFogHandler, out var _, out var _) || Ascents.currentAscent < 0)
		{
			return VanillaProgressStartUiState.Unavailable;
		}
		return VanillaProgressStartUiState.Tracking;
	}


	private static string GetFogHandlingUiLabel(bool isChineseLanguage)
	{
		string label = (isChineseLanguage ? "毒雾寒冷:" : "Fog Cold:");
		if (IsFogColdSuppressionEnabled())
		{
			if (!isChineseLanguage)
			{
				return Colorize(label, "#B7C0CC") + " " + Colorize("Block Fog Cold", "#FFA64D");
			}
			return Colorize(label, "#B7C0CC") + " " + Colorize("阻止毒雾寒冷", "#FFA64D");
		}
		if (!isChineseLanguage)
		{
			return Colorize(label, "#B7C0CC") + " " + Colorize("Fog Cold", "#9FFFA8");
		}
		return Colorize(label, "#B7C0CC") + " " + Colorize("毒雾寒冷", "#9FFFA8");
	}


	private static string ApplyFogUiThemeToHexColor(string hexColor)
	{
		if (!TryParseHtmlColor(hexColor, out var color))
		{
			return hexColor;
		}
		return "#" + ColorUtility.ToHtmlStringRGB(ApplyFogUiThemeToColor(color));
	}


	private static Color ApplyFogUiThemeToColor(Color color)
	{
		Plugin instance = Instance;
		if (instance == null)
		{
			return color;
		}
		return ApplyFogUiThemeToColor(color, instance._fogUiThemeInitialized ? instance._fogUiThemeKind : instance.ResolveFogUiThemeKind());
	}


	private static Color GetFogUiBaseTextColor()
	{
		return ApplyFogUiThemeToColor(new Color(0.92f, 0.96f, 1f, 1f));
	}


	private static Color ApplyFogUiThemeToColor(Color color, FogUiThemeKind themeKind)
	{
		GetFogUiThemeColors(themeKind, out var textTint, out var _);
		float blend = themeKind switch
		{
			FogUiThemeKind.Night => 0.1f, 
			FogUiThemeKind.Day => 0.05f, 
			_ => 0.16f, 
		};
		float alpha = color.a;
		Color themed = Color.Lerp(color, textTint, blend);
		themed = EnsureReadableFogUiTextColor(themed, themeKind);
		themed.a = alpha;
		return themed;
	}


	private static Color ApplyFogUiThemeToIconColor(Color color)
	{
		Plugin instance = Instance;
		if (instance == null)
		{
			return color;
		}
		FogUiThemeKind themeKind = (instance._fogUiThemeInitialized ? instance._fogUiThemeKind : instance.ResolveFogUiThemeKind());
		GetFogUiThemeColors(themeKind, out var _, out var iconTint);
		float blend = themeKind switch
		{
			FogUiThemeKind.Night => 0.22f, 
			FogUiThemeKind.Day => 0.1f, 
			_ => 0.32f, 
		};
		float alpha = color.a;
		Color themed = Color.Lerp(color, iconTint, blend);
		themed.a = alpha;
		return themed;
	}


	private static Color EnsureReadableFogUiTextColor(Color color, FogUiThemeKind themeKind)
	{
		float minimumLuminance = themeKind switch
		{
			FogUiThemeKind.Fog => 0.55f, 
			FogUiThemeKind.Night => 0.58f, 
			_ => 0.5f, 
		};
		float luminance = GetRelativeLuminance(color);
		if (luminance >= minimumLuminance)
		{
			return color;
		}
		float lift = Mathf.Clamp01((minimumLuminance - luminance) / minimumLuminance) * 0.65f;
		return Color.Lerp(color, Color.white, lift);
	}


	private float ResolveFogUiEntrySpacing(bool compact)
	{
		float spacing = FogUiEntrySpacing?.Value ?? DefaultFogUiEntrySpacing;
		spacing = Mathf.Clamp(spacing, MinFogUiEntrySpacing, MaxFogUiEntrySpacing);
		if (compact)
		{
			spacing = Mathf.Min(spacing, FogUiCompactEntrySpacing);
		}
		return spacing;
	}


	private RectOffset ResolveFogUiUniformPadding(bool compact)
	{
		int horizontalPadding = Mathf.RoundToInt(ResolveFogUiEntrySpacing(compact));
		int verticalPadding = Mathf.RoundToInt(FogUiBackgroundVerticalPadding);
		return new RectOffset(horizontalPadding, horizontalPadding, verticalPadding, verticalPadding);
	}


	private void ClampFogUiToCanvas(Canvas targetCanvas = null)
	{
		if (_fogUiRect == null)
		{
			return;
		}
		Canvas canvas = targetCanvas ?? ResolveHudCanvas() ?? _fogUiRect.GetComponentInParent<Canvas>();
		if (!IsCanvasUsable(canvas))
		{
			return;
		}
		if (_fogUiRect.parent != canvas.transform)
		{
			_fogUiRect.SetParent(canvas.transform, worldPositionStays: false);
		}
		RectTransform canvasRect = canvas.GetComponent<RectTransform>();
		if (canvasRect == null)
		{
			return;
		}
		float scale = FogUiScale?.Value ?? 0.9f;
		float x = FogUiX?.Value ?? 60f;
		bool verticalLayout = IsFogUiVerticalLayout();
		float resolvedWidth = (verticalLayout ? Mathf.Max(_fogUiRect.sizeDelta.x, FogUiVerticalContainerWidth) : ResolveFogUiWidth(canvasRect, scale, x));
		float targetSpacing = ResolveFogUiEntrySpacing(!verticalLayout && resolvedWidth < FogUiCompactWidthThreshold);
		if (!verticalLayout && !Approximately(_lastFogUiResolvedWidth, resolvedWidth))
		{
			_lastFogUiResolvedWidth = resolvedWidth;
			_fogUiLayoutDirty = true;
		}
		float containerHeight = (verticalLayout ? Mathf.Max(_fogUiRect.sizeDelta.y, FogUiVerticalMinHeight) : (FogUiHorizontalHeight + FogUiBackgroundVerticalPadding * 2f));
		ApplyBottomLeftAnchoredRect(_fogUiRect, canvasRect, resolvedWidth, containerHeight, scale, x, FogUiY?.Value ?? 0f);
		if (_fogUiText != null)
		{
			_fogUiText.rectTransform.sizeDelta = new Vector2(resolvedWidth, containerHeight);
		}
		if (!(_fogUiEntriesRect != null))
		{
			return;
		}
		if (!verticalLayout)
		{
			_fogUiEntriesRect.sizeDelta = new Vector2(resolvedWidth, FogUiHorizontalHeight + FogUiBackgroundVerticalPadding * 2f);
		}
		if (verticalLayout)
		{
			VerticalLayoutGroup columnLayout = _fogUiEntriesRect.GetComponent<VerticalLayoutGroup>();
			if (columnLayout != null)
			{
				if (!Mathf.Approximately(columnLayout.spacing, targetSpacing))
				{
					columnLayout.spacing = targetSpacing;
					_fogUiLayoutDirty = true;
					ResetFogUiEntryLayoutCaches();
				}
				RectOffset targetPadding = ResolveFogUiUniformPadding(compact: false);
				if (columnLayout.padding.left != targetPadding.left || columnLayout.padding.right != targetPadding.right || columnLayout.padding.top != targetPadding.top || columnLayout.padding.bottom != targetPadding.bottom)
				{
					columnLayout.padding = targetPadding;
					_fogUiLayoutDirty = true;
					ResetFogUiEntryLayoutCaches();
				}
			}
			return;
		}
		HorizontalLayoutGroup rowLayout = _fogUiEntriesRect.GetComponent<HorizontalLayoutGroup>();
		if (rowLayout != null)
		{
			if (!Mathf.Approximately(rowLayout.spacing, targetSpacing))
			{
				rowLayout.spacing = targetSpacing;
				_fogUiLayoutDirty = true;
				ResetFogUiEntryLayoutCaches();
			}
			RectOffset targetPadding = ResolveFogUiUniformPadding(compact: resolvedWidth < FogUiCompactWidthThreshold);
			if (rowLayout.padding.left != targetPadding.left || rowLayout.padding.right != targetPadding.right || rowLayout.padding.top != targetPadding.top || rowLayout.padding.bottom != targetPadding.bottom)
			{
				rowLayout.padding = targetPadding;
				_fogUiLayoutDirty = true;
				ResetFogUiEntryLayoutCaches();
			}
		}
	}


	private bool IsFogUiVerticalLayout()
	{
		return FogUiLayout?.Value == FogUiLayoutKind.Vertical;
	}


	private void ResetFogUiEntryLayoutCaches()
	{
		foreach (FogUiEntryView view in _fogUiEntryViews)
		{
			if (view == null)
			{
				continue;
			}
			view.LastContentWidth = -1f;
			view.LastHasProgressBar = false;
		}
	}


	private void ClampCampfireLocatorUiToCanvas(Canvas targetCanvas = null)
	{
		if (_campfireLocatorUiRect == null)
		{
			return;
		}
		Canvas canvas = targetCanvas ?? ResolveHudCanvas() ?? _campfireLocatorUiRect.GetComponentInParent<Canvas>();
		if (IsCanvasUsable(canvas))
		{
			if (_campfireLocatorUiRect.parent != canvas.transform)
			{
				_campfireLocatorUiRect.SetParent(canvas.transform, worldPositionStays: false);
			}
			RectTransform canvasRect = canvas.GetComponent<RectTransform>();
			if (!(canvasRect == null))
			{
				ApplyTopCenterAnchoredRect(_campfireLocatorUiRect, canvasRect, 372f, 24f, 1f, 54f);
			}
		}
	}


	private static void ApplyBottomLeftAnchoredRect(RectTransform target, RectTransform canvasRect, float width, float height, float scale, float x, float y)
	{
		if (!(target == null) && !(canvasRect == null))
		{
			target.localScale = Vector3.one * Mathf.Max(scale, 0.5f);
			target.anchorMin = Vector2.zero;
			target.anchorMax = Vector2.zero;
			target.pivot = Vector2.zero;
			target.sizeDelta = new Vector2(width, height);
			float scaledWidth = width * target.localScale.x;
			float scaledHeight = height * target.localScale.y;
			Rect canvasArea = canvasRect.rect;
			float clampedX = Mathf.Clamp(x, 0f, Mathf.Max(0f, canvasArea.width - scaledWidth));
			float clampedY = Mathf.Clamp(y, 0f, Mathf.Max(0f, canvasArea.height - scaledHeight));
			target.anchoredPosition = new Vector2(clampedX, clampedY);
		}
	}


	private static float ResolveFogUiWidth(RectTransform canvasRect, float scale, float x)
	{
		if (canvasRect == null)
		{
			return 1180f;
		}
		float clampedScale = Mathf.Max(scale, 0.5f);
		float availableWidth = Mathf.Max(320f, (canvasRect.rect.width - Mathf.Max(x, 0f) - 18f) / clampedScale);
		return Mathf.Min(1180f, availableWidth);
	}


	private static void ApplyTopCenterAnchoredRect(RectTransform target, RectTransform canvasRect, float width, float height, float scale, float topOffset)
	{
		if (!(target == null) && !(canvasRect == null))
		{
			target.localScale = Vector3.one * Mathf.Clamp(scale, 0.5f, 2.5f);
			target.anchorMin = new Vector2(0.5f, 1f);
			target.anchorMax = new Vector2(0.5f, 1f);
			target.pivot = new Vector2(0.5f, 1f);
			target.sizeDelta = new Vector2(width, height);
			target.anchoredPosition = new Vector2(0f, 0f - topOffset);
		}
	}


	private void SetFogUiVisible(bool visible)
	{
		if (_fogUiRect != null)
		{
			_fogUiRect.gameObject.SetActive(visible);
		}
	}


	private void SetFogUiText(string text)
	{
		if (!(_fogUiText == null))
		{
			string nextText = text ?? string.Empty;
			if (!string.Equals(_lastFogUiRenderedText, nextText, StringComparison.Ordinal))
			{
				_lastFogUiRenderedText = nextText;
				_fogUiText.text = nextText;
			}
		}
	}


	private void EnsureFogUiEntryViewCount(int targetCount)
	{
		if (!(_fogUiEntriesRect == null))
		{
			while (_fogUiEntryViews.Count < targetCount)
			{
				_fogUiEntryViews.Add(CreateFogUiEntryView(_fogUiEntryViews.Count));
			}
		}
	}


	private FogUiEntryView CreateFogUiEntryView(int index)
	{
		GameObject obj = new GameObject($"Entry{index}", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
		RectTransform entryRect = obj.GetComponent<RectTransform>();
		entryRect.SetParent(_fogUiEntriesRect, worldPositionStays: false);
		HorizontalLayoutGroup component = obj.GetComponent<HorizontalLayoutGroup>();
		component.childControlWidth = false;
		component.childControlHeight = true;
		component.childForceExpandWidth = false;
		component.childForceExpandHeight = false;
		component.childAlignment = TextAnchor.MiddleLeft;
		component.spacing = FogUiEntryIconTextSpacing;
		LayoutElement entryElement = obj.GetComponent<LayoutElement>();
		entryElement.minHeight = FogUiEntryHeight;
		entryElement.preferredHeight = FogUiEntryHeight;
		entryElement.minWidth = 0f;
		entryElement.preferredWidth = 0f;
		entryElement.flexibleWidth = 0f;
		GameObject obj2 = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
		RectTransform component2 = obj2.GetComponent<RectTransform>();
		component2.SetParent(entryRect, worldPositionStays: false);
		component2.anchorMin = new Vector2(0f, 0.5f);
		component2.anchorMax = new Vector2(0f, 0.5f);
		component2.pivot = new Vector2(0f, 0.5f);
		component2.sizeDelta = new Vector2(20f, 20f);
		component2.anchoredPosition = new Vector2(0f, 0f);
		LayoutElement component3 = obj2.GetComponent<LayoutElement>();
		component3.minWidth = 20f;
		component3.preferredWidth = 20f;
		component3.minHeight = 20f;
		component3.preferredHeight = 20f;
		component3.flexibleWidth = 0f;
		Image iconImage = obj2.GetComponent<Image>();
		iconImage.raycastTarget = false;
		iconImage.preserveAspect = true;
		iconImage.color = new Color(1f, 1f, 1f, 0.92f);
		GameObject obj3 = new GameObject("Text", typeof(RectTransform), typeof(LayoutElement));
		RectTransform component4 = obj3.GetComponent<RectTransform>();
		component4.SetParent(entryRect, worldPositionStays: false);
		component4.anchorMin = new Vector2(0f, 0.5f);
		component4.anchorMax = new Vector2(0f, 0.5f);
		component4.pivot = new Vector2(0f, 0.5f);
		LayoutElement textLayout = obj3.GetComponent<LayoutElement>();
		textLayout.minHeight = FogUiEntryHeight;
		textLayout.preferredHeight = FogUiEntryHeight;
		textLayout.minWidth = 0f;
		textLayout.preferredWidth = 0f;
		textLayout.flexibleWidth = 0f;
		TextMeshProUGUI textComponent = obj3.AddComponent<TextMeshProUGUI>();
		ApplyGameTextStyle(textComponent, Color.white, 0.9f);
		textComponent.richText = true;
		textComponent.alignment = TextAlignmentOptions.MidlineLeft;
		textComponent.overflowMode = TextOverflowModes.Overflow;
		textComponent.enableAutoSizing = false;
		textComponent.text = string.Empty;
		GameObject barObject = new GameObject("ProgressBar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
		RectTransform barRect = barObject.GetComponent<RectTransform>();
		barRect.SetParent(entryRect, worldPositionStays: false);
		barRect.anchorMin = new Vector2(0f, 0.5f);
		barRect.anchorMax = new Vector2(0f, 0.5f);
		barRect.pivot = new Vector2(0f, 0.5f);
		barRect.sizeDelta = new Vector2(128f, 17f);
		LayoutElement component5 = barObject.GetComponent<LayoutElement>();
		component5.minWidth = 128f;
		component5.preferredWidth = 128f;
		component5.minHeight = 17f;
		component5.preferredHeight = 17f;
		Image barBackground = barObject.GetComponent<Image>();
		barBackground.raycastTarget = false;
		GameObject obj4 = new GameObject("Fill", typeof(RectTransform), typeof(Image));
		RectTransform component6 = obj4.GetComponent<RectTransform>();
		component6.SetParent(barRect, worldPositionStays: false);
		component6.anchorMin = Vector2.zero;
		component6.anchorMax = Vector2.one;
		component6.offsetMin = Vector2.zero;
		component6.offsetMax = Vector2.zero;
		Image fillImage = obj4.GetComponent<Image>();
		fillImage.raycastTarget = false;
		fillImage.type = Image.Type.Filled;
		fillImage.fillMethod = Image.FillMethod.Horizontal;
		fillImage.fillOrigin = 0;
		fillImage.fillAmount = 0f;
		GameObject obj5 = new GameObject("Label", typeof(RectTransform));
		RectTransform component7 = obj5.GetComponent<RectTransform>();
		component7.SetParent(barRect, worldPositionStays: false);
		component7.anchorMin = Vector2.zero;
		component7.anchorMax = Vector2.one;
		component7.offsetMin = new Vector2(4f, 0f);
		component7.offsetMax = new Vector2(-4f, 0f);
		TextMeshProUGUI barTextComponent = obj5.AddComponent<TextMeshProUGUI>();
		ApplyGameTextStyle(barTextComponent, Color.white, 0.9f);
		barTextComponent.richText = true;
		barTextComponent.alignment = TextAlignmentOptions.Center;
		barTextComponent.overflowMode = TextOverflowModes.Overflow;
		barTextComponent.enableAutoSizing = false;
		barTextComponent.text = string.Empty;
		barObject.SetActive(value: false);
		return new FogUiEntryView
		{
			Root = entryRect,
			Icon = iconImage,
			Text = textComponent,
			BarRoot = barRect,
			BarBackground = barBackground,
			BarFill = fillImage,
			BarText = barTextComponent,
			EntryLayoutElement = entryElement,
			TextLayoutElement = textLayout
		};
	}


	private void RefreshFogUiEntryStyles()
	{
		foreach (FogUiEntryView entryView in _fogUiEntryViews)
		{
			if (!(entryView?.Text == null))
			{
				ApplyGameTextStyle(entryView.Text, Color.white, 0.9f);
				entryView.Text.richText = true;
				entryView.Text.alignment = TextAlignmentOptions.MidlineLeft;
				entryView.Text.overflowMode = TextOverflowModes.Overflow;
				if (entryView.BarText != null)
				{
					ApplyGameTextStyle(entryView.BarText, Color.white, 0.9f);
					entryView.BarText.richText = true;
					entryView.BarText.alignment = TextAlignmentOptions.Center;
					entryView.BarText.overflowMode = TextOverflowModes.Overflow;
				}
			}
		}
	}


	private Sprite GetFogUiIconSprite(FogUiIconKind iconKind)
	{
		if (iconKind == FogUiIconKind.NightTime)
		{
			iconKind = FogUiIconKind.Night;
		}
		switch (iconKind)
		{
		case FogUiIconKind.Altitude:
		{
			Sprite nativeAltitudeIcon = ResolveNativeAltitudeIconSprite();
			if (nativeAltitudeIcon != null)
			{
				return nativeAltitudeIcon;
			}
			break;
		}
		case FogUiIconKind.Campfire:
		{
			Sprite nativeCampfireIcon = ResolveNativeCampfireIconSprite();
			if (nativeCampfireIcon != null)
			{
				return nativeCampfireIcon;
			}
			break;
		}
		case FogUiIconKind.Distance:
		{
			Sprite nativePoisonIcon = ResolveNativeStatusIconSprite(CharacterAfflictions.STATUSTYPE.Poison);
			if (nativePoisonIcon != null)
			{
				return nativePoisonIcon;
			}
			break;
		}
		case FogUiIconKind.Lava:
		{
			if (IsFortressSegmentCurrent())
			{
				Sprite nativeDrowsyIcon = ResolveNativeStatusIconSprite(CharacterAfflictions.STATUSTYPE.Drowsy);
				if (nativeDrowsyIcon != null)
				{
					return nativeDrowsyIcon;
				}
			}
			break;
		}
		case FogUiIconKind.Petrify:
		{
			Sprite nativePetrifyIcon = ResolveNativeStatusIconSprite(CharacterAfflictions.STATUSTYPE.Petrify);
			if (nativePetrifyIcon != null)
			{
				return nativePetrifyIcon;
			}
			break;
		}
		case FogUiIconKind.Snowstorm:
		{
			Sprite nativeColdIcon = ResolveNativeStatusIconSprite(CharacterAfflictions.STATUSTYPE.Cold);
			if (nativeColdIcon != null)
			{
				return nativeColdIcon;
			}
			break;
		}
		}
		if (_fogUiIconSprites.TryGetValue(iconKind, out var sprite) && sprite != null)
		{
			return sprite;
		}
		sprite = CreateFogUiIconSprite(iconKind);
		_fogUiIconSprites[iconKind] = sprite;
		return sprite;
	}


	private Sprite ResolveNativeAltitudeIconSprite()
	{
		if (_nativeAltitudeIconSprite != null)
		{
			return _nativeAltitudeIconSprite;
		}
		try
		{
			GUIManager guiManager = GUIManager.instance;
			if (guiManager != null && guiManager.reticleBoost != null)
			{
				_nativeAltitudeIconSprite = FindDominantImageSprite(guiManager.reticleBoost);
			}
		}
		catch
		{
		}
		return _nativeAltitudeIconSprite;
	}


	private static Sprite FindDominantImageSprite(GameObject root)
	{
		if (root == null)
		{
			return null;
		}
		Sprite best = null;
		float bestArea = -1f;
		Image[] images = root.GetComponentsInChildren<Image>(true);
		for (int i = 0; i < images.Length; i++)
		{
			Image image = images[i];
			if (image == null || image.sprite == null)
			{
				continue;
			}
			float area = image.sprite.rect.width * image.sprite.rect.height;
			if (area > bestArea)
			{
				bestArea = area;
				best = image.sprite;
			}
		}
		return best;
	}


	private Sprite ResolveNativeCampfireIconSprite()
	{
		if (_nativeCampfireIconSprite != null)
		{
			return _nativeCampfireIconSprite;
		}
		StaminaBar staminaBar = TryAcquireStaminaBar();
		if (staminaBar == null)
		{
			return null;
		}
		try
		{
			if (staminaBar.campfire != null)
			{
				Image campfireImage = staminaBar.campfire.GetComponentInChildren<Image>(true) ?? staminaBar.campfire.GetComponent<Image>();
				if (campfireImage != null && campfireImage.sprite != null)
				{
					_nativeCampfireIconSprite = campfireImage.sprite;
				}
			}
		}
		catch
		{
		}
		return _nativeCampfireIconSprite;
	}


	private StaminaBar TryAcquireStaminaBar()
	{
		float now = Time.unscaledTime;
		if (now - _lastNativeIconAttemptTime < 2f)
		{
			return null;
		}
		_lastNativeIconAttemptTime = now;
		try
		{
			return UnityEngine.Object.FindObjectsByType<StaminaBar>(FindObjectsSortMode.None).FirstOrDefault((StaminaBar candidate) => candidate != null);
		}
		catch
		{
			return null;
		}
	}


	private Sprite GetCampfireLocatorDotSprite()
	{
		Sprite nativeCampfireIcon = ResolveNativeCampfireIconSprite();
		if (nativeCampfireIcon != null)
		{
			return nativeCampfireIcon;
		}
		if (_campfireLocatorDotSprite != null)
		{
			return _campfireLocatorDotSprite;
		}
		_campfireLocatorDotSprite = CreateCampfireLocatorDotSprite();
		return _campfireLocatorDotSprite;
	}


	private static Sprite CreateCampfireLocatorDotSprite()
	{
		Texture2D obj = new Texture2D(32, 32, TextureFormat.RGBA32, mipChain: false)
		{
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp
		};
		Color32[] pixels = Enumerable.Repeat(new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, 0), 1024).ToArray();
		DrawCampfireIcon(pixels, 32);
		obj.SetPixels32(pixels);
		obj.Apply(updateMipmaps: false, makeNoLongerReadable: false);
		return Sprite.Create(obj, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 100f);
	}


	private Sprite GetCampfireLocatorLineSprite()
	{
		if (_campfireLocatorLineSprite != null)
		{
			return _campfireLocatorLineSprite;
		}
		_campfireLocatorLineSprite = CreateCampfireLocatorLineSprite();
		return _campfireLocatorLineSprite;
	}


	private static Sprite CreateCampfireLocatorLineSprite()
	{
		int width = 128;
		int height = 4;
		Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
		{
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp
		};
		Color32[] pixels = new Color32[width * height];
		for (int x = 0; x < width; x++)
		{
			float t = (float)x / (float)(width - 1);
			float alpha = Mathf.Clamp01(Mathf.Sin(t * Mathf.PI));
			alpha = alpha * alpha;
			Color32 c = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)(alpha * 255f));
			for (int y = 0; y < height; y++)
			{
				pixels[y * width + x] = c;
			}
		}
		texture.SetPixels32(pixels);
		texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
		return Sprite.Create(texture, new Rect(0f, 0f, (float)width, (float)height), new Vector2(0.5f, 0.5f), 100f);
	}


	private static float ResolveFogUiBackgroundOpacity()
	{
		if (FogUiBackgroundOpacity != null)
		{
			return Mathf.Clamp(FogUiBackgroundOpacity.Value, MinFogUiBackgroundOpacity, MaxFogUiBackgroundOpacity);
		}
		return DefaultFogUiBackgroundOpacity;
	}


	private Sprite GetFogUiBackgroundSprite()
	{
		if (_fogUiBackgroundSprite != null)
		{
			return _fogUiBackgroundSprite;
		}
		_fogUiBackgroundSprite = CreateFogUiBackgroundSprite();
		return _fogUiBackgroundSprite;
	}


	private static Sprite CreateFogUiBackgroundSprite()
	{
		int size = FogUiBackgroundSpriteSize;
		float half = (float)(size - 1) * 0.5f;
		float innerX = half - FogUiBackgroundCornerRadiusX;
		float innerY = half - FogUiBackgroundCornerRadiusY;
		Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
		{
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp
		};
		Color32[] pixels = new Color32[size * size];
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float px = (float)x + 0.5f - half;
				float py = (float)y + 0.5f - half;
				float ex = Mathf.Abs(px) - innerX;
				float ey = Mathf.Abs(py) - innerY;
				byte alpha;
				if (ex <= 0f || ey <= 0f)
				{
					alpha = byte.MaxValue;
				}
				else
				{
					float nx = ex / FogUiBackgroundCornerRadiusX;
					float ny = ey / FogUiBackgroundCornerRadiusY;
					float signedDist = (Mathf.Sqrt(nx * nx + ny * ny) - 1f) * Mathf.Min(FogUiBackgroundCornerRadiusX, FogUiBackgroundCornerRadiusY);
					alpha = (byte)(Mathf.Clamp01(0.5f - signedDist) * 255f);
				}
				pixels[y * size + x] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, alpha);
			}
		}
		texture.SetPixels32(pixels);
		texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
		return Sprite.Create(texture, new Rect(0f, 0f, (float)size, (float)size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(FogUiBackgroundSliceBorderX, FogUiBackgroundSliceBorderY, FogUiBackgroundSliceBorderX, FogUiBackgroundSliceBorderY));
	}


	private static void DrawCampfireIcon(Color32[] pixels, int size)
	{
		float s = (float)size / 24f;
		Color32 white = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
		DrawLine(pixels, size, 4f * s, 4f * s, 20f * s, 4f * s, 2.5f * s);
		DrawLine(pixels, size, 6f * s, 4f * s, 8f * s, 8f * s, 2f * s);
		DrawLine(pixels, size, 18f * s, 4f * s, 16f * s, 8f * s, 2f * s);
		DrawLine(pixels, size, 9f * s, 8f * s, 10f * s, 11f * s, 1.8f * s);
		DrawLine(pixels, size, 15f * s, 8f * s, 14f * s, 11f * s, 1.8f * s);
		DrawLine(pixels, size, 10f * s, 6f * s, 12f * s, 12f * s, 2f * s);
		DrawLine(pixels, size, 14f * s, 6f * s, 12f * s, 12f * s, 2f * s);
		DrawLine(pixels, size, 8f * s, 11f * s, 12f * s, 20f * s, 2.2f * s);
		DrawLine(pixels, size, 16f * s, 11f * s, 12f * s, 20f * s, 2.2f * s);
		FillCircle(pixels, size, 12f * s, 10f * s, 2f * s, white);
	}


	private static Sprite CreateFogUiIconSprite(FogUiIconKind iconKind)
	{
		Texture2D texture = new Texture2D(24, 24, TextureFormat.RGBA32, mipChain: false);
		texture.filterMode = FilterMode.Bilinear;
		texture.wrapMode = TextureWrapMode.Clamp;
		Color32[] pixels = Enumerable.Repeat(new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, 0), 576).ToArray();
		switch (iconKind)
		{
		case FogUiIconKind.State:
			DrawCircleOutline(pixels, 24, 12f, 8f, 4.5f, 2f);
			DrawLine(pixels, 24, 12f, 12f, 12f, 20f, 2f);
			DrawLine(pixels, 24, 9f, 18f, 15f, 18f, 2f);
			break;
		case FogUiIconKind.StateRunning:
			DrawCircleOutline(pixels, 24, 12f, 12f, 9f, 2f);
			FillCircle(pixels, 24, 12f, 12f, 2.5f, new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
			DrawLine(pixels, 24, 16f, 12f, 20f, 12f, 2.5f);
			DrawLine(pixels, 24, 18f, 10f, 20f, 12f, 2f);
			DrawLine(pixels, 24, 18f, 14f, 20f, 12f, 2f);
			break;
		case FogUiIconKind.StateWaiting:
			DrawCircleOutline(pixels, 24, 12f, 12f, 9f, 2f);
			FillCircle(pixels, 24, 12f, 9f, 2f, new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
			FillCircle(pixels, 24, 12f, 15f, 2f, new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
			break;
		case FogUiIconKind.StatePaused:
			DrawCircleOutline(pixels, 24, 12f, 12f, 9f, 2f);
			DrawLine(pixels, 24, 9f, 8f, 9f, 16f, 2.5f);
			DrawLine(pixels, 24, 15f, 8f, 15f, 16f, 2.5f);
			break;
		case FogUiIconKind.StateArrived:
			DrawCircleOutline(pixels, 24, 12f, 12f, 9f, 2f);
			DrawLine(pixels, 24, 7f, 12f, 11f, 16f, 2.5f);
			DrawLine(pixels, 24, 11f, 16f, 17f, 8f, 2.5f);
			break;
		case FogUiIconKind.StateSyncing:
			DrawCircleOutline(pixels, 24, 12f, 12f, 9f, 2f);
			DrawLine(pixels, 24, 12f, 6f, 12f, 10f, 2f);
			DrawLine(pixels, 24, 16f, 12f, 18f, 12f, 2f);
			DrawLine(pixels, 24, 12f, 14f, 12f, 18f, 2f);
			DrawLine(pixels, 24, 6f, 12f, 8f, 12f, 2f);
			break;
		case FogUiIconKind.StateOff:
			DrawCircleOutline(pixels, 24, 12f, 12f, 9f, 2f);
			DrawLine(pixels, 24, 7f, 7f, 17f, 17f, 2.5f);
			DrawLine(pixels, 24, 17f, 7f, 7f, 17f, 2.5f);
			break;
		case FogUiIconKind.StateLobby:
			DrawLine(pixels, 24, 12f, 6f, 6f, 12f, 2f);
			DrawLine(pixels, 24, 12f, 6f, 18f, 12f, 2f);
			DrawLine(pixels, 24, 6f, 12f, 6f, 18f, 2f);
			DrawLine(pixels, 24, 18f, 12f, 18f, 18f, 2f);
			DrawLine(pixels, 24, 4f, 18f, 20f, 18f, 2f);
			break;
		case FogUiIconKind.StateIdle:
			DrawCircleOutline(pixels, 24, 12f, 12f, 9f, 2f);
			FillCircle(pixels, 24, 12f, 12f, 2f, new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
			break;
		case FogUiIconKind.Speed:
			DrawLine(pixels, 24, 6f, 6f, 11f, 12f, 2.2f);
			DrawLine(pixels, 24, 11f, 12f, 6f, 18f, 2.2f);
			DrawLine(pixels, 24, 13f, 6f, 18f, 12f, 2.2f);
			DrawLine(pixels, 24, 18f, 12f, 13f, 18f, 2.2f);
			break;
		case FogUiIconKind.Buffer:
			DrawLine(pixels, 24, 7f, 4f, 17f, 4f, 2f);
			DrawLine(pixels, 24, 7f, 20f, 17f, 20f, 2f);
			DrawLine(pixels, 24, 8f, 6f, 16f, 12f, 2f);
			DrawLine(pixels, 24, 16f, 12f, 8f, 18f, 2f);
			break;
		case FogUiIconKind.Delay:
			DrawCircleOutline(pixels, 24, 12f, 12f, 9f, 2f);
			DrawLine(pixels, 24, 12f, 12f, 12f, 7f, 2.5f);
			DrawLine(pixels, 24, 12f, 12f, 15.5f, 14f, 2.5f);
			FillCircle(pixels, 24, 12f, 12f, 2f, new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
			break;
		case FogUiIconKind.Eta:
			DrawCircleOutline(pixels, 24, 12f, 12f, 8f, 2f);
			DrawLine(pixels, 24, 12f, 12f, 12f, 7f, 2f);
			DrawLine(pixels, 24, 12f, 12f, 16f, 14f, 2f);
			DrawLine(pixels, 24, 17f, 6f, 20f, 6f, 1.8f);
			DrawLine(pixels, 24, 20f, 6f, 20f, 9f, 1.8f);
			break;
		case FogUiIconKind.Timer:
			DrawCircleOutline(pixels, 24, 12f, 13f, 7f, 2f);
			DrawLine(pixels, 24, 10f, 4f, 14f, 4f, 2f);
			DrawLine(pixels, 24, 12f, 4f, 12f, 6f, 2f);
			DrawLine(pixels, 24, 8f, 6.5f, 9.5f, 8f, 1.8f);
			DrawLine(pixels, 24, 12f, 13f, 12f, 9f, 2f);
			DrawLine(pixels, 24, 12f, 13f, 15.5f, 15f, 2f);
			break;
		case FogUiIconKind.Campfire:
			DrawCampfireIcon(pixels, 24);
			break;
		case FogUiIconKind.Biome:
			DrawLine(pixels, 24, 4f, 4f, 8f, 16f, 2.2f);
			DrawLine(pixels, 24, 8f, 16f, 12f, 10f, 2.2f);
			DrawLine(pixels, 24, 12f, 10f, 16f, 18f, 2.2f);
			DrawLine(pixels, 24, 16f, 18f, 20f, 4f, 2.2f);
			DrawLine(pixels, 24, 4f, 4f, 20f, 4f, 2.2f);
			DrawLine(pixels, 24, 6f, 4f, 9f, 13f, 1.6f);
			DrawLine(pixels, 24, 14f, 4f, 17f, 15f, 1.6f);
			FillCircle(pixels, 24, 16f, 18f, 2.2f, new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
			break;
		case FogUiIconKind.Altitude:
			DrawLine(pixels, 24, 4f, 4f, 20f, 4f, 2.2f);
			DrawLine(pixels, 24, 4f, 4f, 12f, 13f, 2.2f);
			DrawLine(pixels, 24, 20f, 4f, 12f, 13f, 2.2f);
			DrawLine(pixels, 24, 12f, 15f, 12f, 21f, 2f);
			DrawLine(pixels, 24, 12f, 21f, 8.5f, 17.5f, 2f);
			DrawLine(pixels, 24, 12f, 21f, 15.5f, 17.5f, 2f);
			break;
		case FogUiIconKind.Lava:
			DrawLine(pixels, 24, 2f, 4.5f, 6f, 8f, 2.6f);
			DrawLine(pixels, 24, 6f, 8f, 10f, 4.5f, 2.6f);
			DrawLine(pixels, 24, 10f, 4.5f, 14f, 8f, 2.6f);
			DrawLine(pixels, 24, 14f, 8f, 18f, 4.5f, 2.6f);
			DrawLine(pixels, 24, 18f, 4.5f, 22f, 8f, 2.6f);
			DrawLine(pixels, 24, 12f, 9f, 12f, 19f, 2.2f);
			DrawLine(pixels, 24, 12f, 19f, 8f, 15f, 2.2f);
			DrawLine(pixels, 24, 12f, 19f, 16f, 15f, 2.2f);
			break;
		case FogUiIconKind.Petrify:
		{
			Color32 white = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
			FillCircle(pixels, 24, 12f, 7f, 3.2f, white);
			DrawLine(pixels, 24, 12f, 10.5f, 12f, 19f, 5f);
			DrawLine(pixels, 24, 12f, 13f, 5.5f, 16f, 2f);
			DrawLine(pixels, 24, 12f, 13f, 18.5f, 16f, 2f);
			DrawLine(pixels, 24, 9.5f, 15f, 14.5f, 17f, 1.5f);
			DrawLine(pixels, 24, 14.5f, 15f, 9.5f, 13f, 1.5f);
			break;
		}
		case FogUiIconKind.Distance:
		{
			DrawLine(pixels, 24, 12f, 12f, 12f, 12f, 2f);
			for (int i = 0; i < 30; i++)
			{
				float angle = (float)i * 0.4f;
				float r = 2f + (float)i * 0.35f;
				float x = 12f + Mathf.Cos(angle) * r;
				float y = 12f + Mathf.Sin(angle) * r;
				if (i > 0)
				{
					float prevAngle = (float)(i - 1) * 0.4f;
					float prevR = 2f + (float)(i - 1) * 0.35f;
					float prevX = 12f + Mathf.Cos(prevAngle) * prevR;
					float prevY = 12f + Mathf.Sin(prevAngle) * prevR;
					DrawLine(pixels, 24, prevX, prevY, x, y, 1.8f);
				}
			}
			break;
		}
		case FogUiIconKind.Direct:
			DrawCircleOutline(pixels, 24, 12f, 12f, 3.2f, 1.8f);
			DrawLine(pixels, 24, 14f, 10f, 20f, 4f, 2f);
			DrawLine(pixels, 24, 17f, 4f, 20f, 4f, 2f);
			DrawLine(pixels, 24, 20f, 4f, 20f, 7f, 2f);
			break;
		case FogUiIconKind.Pause:
			DrawRectOutline(pixels, 24, 5f, 5f, 14f, 14f, 2f);
			DrawLine(pixels, 24, 9f, 12f, 15f, 12f, 2f);
			break;
		case FogUiIconKind.FogHandling:
			DrawLine(pixels, 24, 12f, 4f, 12f, 20f, 2f);
			DrawLine(pixels, 24, 12f, 8f, 9f, 5f, 1.6f);
			DrawLine(pixels, 24, 12f, 8f, 15f, 5f, 1.6f);
			DrawLine(pixels, 24, 12f, 16f, 9f, 19f, 1.6f);
			DrawLine(pixels, 24, 12f, 16f, 15f, 19f, 1.6f);
			DrawLine(pixels, 24, 4f, 12f, 20f, 12f, 2f);
			DrawLine(pixels, 24, 8f, 12f, 5f, 9f, 1.6f);
			DrawLine(pixels, 24, 8f, 12f, 5f, 15f, 1.6f);
			DrawLine(pixels, 24, 16f, 12f, 19f, 9f, 1.6f);
			DrawLine(pixels, 24, 16f, 12f, 19f, 15f, 1.6f);
			break;
		case FogUiIconKind.Night:
			FillCircle(pixels, 24, 16f, 12f, 8f, new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
			FillCircle(pixels, 24, 19.5f, 12f, 8f, new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, 0));
			break;
		case FogUiIconKind.DayTime:
			FillCircle(pixels, 24, 12f, 12f, 5f, new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
			DrawLine(pixels, 24, 12f, 2f, 12f, 5f, 1.8f);
			DrawLine(pixels, 24, 12f, 19f, 12f, 22f, 1.8f);
			DrawLine(pixels, 24, 2f, 12f, 5f, 12f, 1.8f);
			DrawLine(pixels, 24, 19f, 12f, 22f, 12f, 1.8f);
			DrawLine(pixels, 24, 5f, 5f, 7f, 7f, 1.6f);
			DrawLine(pixels, 24, 17f, 17f, 19f, 19f, 1.6f);
			DrawLine(pixels, 24, 19f, 5f, 17f, 7f, 1.6f);
			DrawLine(pixels, 24, 7f, 17f, 5f, 19f, 1.6f);
			break;
		case FogUiIconKind.Wind:
			DrawLine(pixels, 24, 12f, 12f, 12f, 12f, 2f);
			for (int i = 0; i < 30; i++)
			{
				float angle = (float)i * 0.4f;
				float r = 2f + (float)i * 0.35f;
				float x = 12f + Mathf.Cos(angle) * r;
				float y = 12f + Mathf.Sin(angle) * r;
				if (i > 0)
				{
					float prevAngle = (float)(i - 1) * 0.4f;
					float prevR = 2f + (float)(i - 1) * 0.35f;
					float prevX = 12f + Mathf.Cos(prevAngle) * prevR;
					float prevY = 12f + Mathf.Sin(prevAngle) * prevR;
					DrawLine(pixels, 24, prevX, prevY, x, y, 1.8f);
				}
			}
			break;
		case FogUiIconKind.Snowstorm:
			DrawLine(pixels, 24, 12f, 4f, 12f, 20f, 1.8f);
			DrawLine(pixels, 24, 4f, 12f, 20f, 12f, 1.8f);
			DrawLine(pixels, 24, 6.3f, 6.3f, 17.7f, 17.7f, 1.6f);
			DrawLine(pixels, 24, 17.7f, 6.3f, 6.3f, 17.7f, 1.6f);
			DrawLine(pixels, 24, 12f, 4f, 9.5f, 6.5f, 1.2f);
			DrawLine(pixels, 24, 12f, 4f, 14.5f, 6.5f, 1.2f);
			DrawLine(pixels, 24, 12f, 20f, 9.5f, 17.5f, 1.2f);
			DrawLine(pixels, 24, 12f, 20f, 14.5f, 17.5f, 1.2f);
			DrawLine(pixels, 24, 4f, 12f, 6.5f, 9.5f, 1.2f);
			DrawLine(pixels, 24, 4f, 12f, 6.5f, 14.5f, 1.2f);
			DrawLine(pixels, 24, 20f, 12f, 17.5f, 9.5f, 1.2f);
			DrawLine(pixels, 24, 20f, 12f, 17.5f, 14.5f, 1.2f);
			break;
		case FogUiIconKind.Rain:
		{
			Color32 white = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
			FillEllipse(pixels, 24, 12f, 16.5f, 8f, 2.4f, white);
			FillCircle(pixels, 24, 6f, 17.5f, 2.4f, white);
			FillCircle(pixels, 24, 12f, 18.5f, 2.6f, white);
			FillCircle(pixels, 24, 18f, 17.5f, 2.4f, white);
			DrawLine(pixels, 24, 4.5f, 14.5f, 19.5f, 14.5f, 2f, white);
			DrawLine(pixels, 24, 6f, 12f, 4f, 6.5f, 1.8f, white);
			DrawLine(pixels, 24, 12f, 12f, 10f, 6.5f, 1.8f, white);
			DrawLine(pixels, 24, 18f, 12f, 16f, 6.5f, 1.8f, white);
			break;
		}
		case FogUiIconKind.Separator:
			DrawLine(pixels, 24, 12f, 4f, 12f, 20f, 1.6f);
			break;
		}
		texture.SetPixels32(pixels);
		texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
		return Sprite.Create(texture, new Rect(0f, 0f, 24f, 24f), new Vector2(0.5f, 0.5f), 100f);
	}


	private void SetCampfireLocatorUiVisible(bool visible)
	{
		if (_campfireLocatorUiRect != null)
		{
			_campfireLocatorUiRect.gameObject.SetActive(visible);
		}
	}


	private void CleanupFogUi()
	{
		if (_fogUiRect != null)
		{
			UnityEngine.Object.Destroy(_fogUiRect.gameObject);
		}
		_fogUiRect = null;
		_fogUiText = null;
		_fogUiEntriesRect = null;
		_fogUiBackgroundImage = null;
		_fogUiBackgroundSprite = null;
		_fogUiThemeInitialized = false;
		_fogUiThemeKind = FogUiThemeKind.Day;
		_fogUiEntryViews.Clear();
		_fogUiDisplayEntries.Clear();
		_lastFogUiRenderedText = string.Empty;
		_fogUiLayoutDirty = false;
		_lastFogUiEntryLayoutCount = -1;
		_lastFogUiResolvedWidth = -1f;
	}


	private void CleanupCampfireLocatorUi()
	{
		if (_campfireLocatorUiRect != null)
		{
			UnityEngine.Object.Destroy(_campfireLocatorUiRect.gameObject);
		}
		_campfireLocatorUiRect = null;
		_campfireLocatorDotRect = null;
		_campfireLocatorDotImage = null;
		_campfireLocatorDistanceText = null;
		_campfireLocatorCurrentDotX = 0f;
	}


	private static bool IsReadOnlyFogUiViewer()
	{
		if (PhotonNetwork.InRoom)
		{
			return !PhotonNetwork.IsMasterClient;
		}
		return false;
	}
}
