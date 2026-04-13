using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Voice.Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using Zorro.Core;

public class PeakHandler : Singleton<PeakHandler>
{
	public bool summonedHelicopter;

	public GameObject peakSequence;

	public GameObject endCutscene;

	public Animator endCutsceneAnimator;

	public float secondsUntilEndscreen = 13f;

	public CustomizationRefs firstCutsceneScout;

	public CustomizationRefs[] cutsceneScoutRefs;

	public EndCutsceneScoutHelper[] cutsceneScoutAnims;

	private List<AnimatedMouth> localMouths = new List<AnimatedMouth>();

	public bool isPlayingCinematic;

	private bool endScreenComplete;

	public void SummonHelicopter()
	{
		peakSequence.SetActive(value: true);
		summonedHelicopter = true;
	}

	public void EndCutscene()
	{
		isPlayingCinematic = true;
		List<Character> allCharacters = Character.AllCharacters;
		foreach (Character item in allCharacters)
		{
			item.refs.animator.gameObject.SetActive(value: false);
		}
		MainCamera.instance.gameObject.SetActive(value: false);
		MenuWindow.CloseAllWindows();
		peakSequence.SetActive(value: false);
		GUIManager.instance.letterboxCanvas.gameObject.SetActive(value: true);
		GUIManager.instance.hudCanvas.enabled = false;
		endCutscene.SetActive(value: true);
		SetCosmetics(allCharacters);
		StartCoroutine(OpenEndscreen());
		IEnumerator OpenEndscreen()
		{
			yield return new WaitForSeconds(secondsUntilEndscreen);
			GUIManager.instance.endScreen.Open();
			while (!endScreenComplete)
			{
				yield return null;
			}
			endCutsceneAnimator.SetBool("Next", value: true);
			GUIManager.instance.endScreen.Close();
		}
	}

	private void SetCosmetics(List<Character> characters)
	{
		Singleton<MicrophoneRelay>.Instance.RegisterMicListener(OnGetLocalMic);
		characters = characters.Where((Character character) => character.refs.stats.won).ToList();
		characters.Sort((Character c1, Character c2) => c1.photonView.ViewID.CompareTo(c2.photonView.ViewID));
		characters[0].refs.customization.SetCustomizationForRef(firstCutsceneScout);
		if (characters[0].data.isSkeleton)
		{
			firstCutsceneScout.SetSkeleton(active: true, isLocal: false);
		}
		firstCutsceneScout.GetComponent<AnimatedMouth>().audioSource = characters[0].GetComponent<AnimatedMouth>().audioSource;
		localMouths.Add(firstCutsceneScout.GetComponent<AnimatedMouth>());
		int num = 0;
		for (int num2 = 0; num2 < 4; num2++)
		{
			if (num2 >= characters.Count)
			{
				cutsceneScoutRefs[num2].gameObject.SetActive(value: false);
				continue;
			}
			characters[num2].refs.customization.SetCustomizationForRef(cutsceneScoutRefs[num]);
			if (characters[num2].data.isSkeleton)
			{
				cutsceneScoutRefs[num2].SetSkeleton(active: true, isLocal: false);
			}
			BadgeUnlocker.SetBadges(characters[num2], cutsceneScoutRefs[num].sashRenderer);
			cutsceneScoutRefs[num].GetComponent<AnimatedMouth>().audioSource = characters[num2].GetComponent<AnimatedMouth>().audioSource;
			if (characters[num2].IsLocal)
			{
				localMouths.Add(cutsceneScoutRefs[num].GetComponent<AnimatedMouth>());
			}
			num++;
		}
		if (characters.Count <= 1)
		{
			cutsceneScoutAnims[0].alone = true;
		}
		if (characters.Count <= 2)
		{
			cutsceneScoutAnims[1].alone = true;
		}
	}

	private void OnGetLocalMic(float[] buffer)
	{
		foreach (AnimatedMouth localMouth in localMouths)
		{
			localMouth.OnGetMic(buffer);
		}
	}

	public void EndScreenComplete()
	{
		Singleton<GameOverHandler>.Instance.ForceEveryPlayerDoneWithEndScreen();
		endScreenComplete = true;
		StartCoroutine(CreditsLogic());
		static IEnumerator CreditsLogic()
		{
			yield return new WaitForSecondsRealtime(20f);
			InputAction anyKeyAction = InputSystem.actions.FindAction("AnyKey");
			float timeCreditsStarted = Time.unscaledTime;
			float t = 0f;
			float timeAnyKeyLastPressed = float.NegativeInfinity;
			bool anyKeyPressed = false;
			while (Time.unscaledTime - timeCreditsStarted < 60f)
			{
				if (anyKeyAction != null && anyKeyAction.WasPerformedThisFrame() && !anyKeyPressed)
				{
					anyKeyPressed = true;
					timeAnyKeyLastPressed = Time.unscaledTime;
				}
				if (anyKeyPressed)
				{
					if (GUIManager.TimeLastPaused + 0.5f > timeAnyKeyLastPressed)
					{
						Debug.Log("'Any key' consumed by pause menu");
						anyKeyPressed = false;
					}
					else if (Time.unscaledTime - timeAnyKeyLastPressed > 0.5f)
					{
						break;
					}
				}
				t += Time.unscaledDeltaTime;
				yield return null;
			}
			Debug.Log("Local player is done with credits!");
			Singleton<GameOverHandler>.Instance.LoadAirport();
		}
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		if (isPlayingCinematic && (bool)Singleton<MicrophoneRelay>.Instance)
		{
			Singleton<MicrophoneRelay>.Instance.UnregisterMicListener(OnGetLocalMic);
		}
	}
}
