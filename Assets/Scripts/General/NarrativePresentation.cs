using DialogueEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;

/// <summary>Switches the gameplay HUD and narrative grading together for this scene.</summary>
[DefaultExecutionOrder(1000)]
public sealed class NarrativePresentation : MonoBehaviour
{
    [SerializeField] private GameObject visibleUI;
    [SerializeField] private Volume narrativeVolume;

    private PlayableDirector[] directors;
    private SequenceController[] sequences;
    private MedicFinishCutscene[] medicCutscenes;
    private PlayerWakeUp[] wakeUps;
    private LogSwingController[] logSwings;
    private PlayerPushReaction[] pushReactions;

    private void Awake()
    {
        directors = FindInScene<PlayableDirector>();
        sequences = FindInScene<SequenceController>();
        medicCutscenes = FindInScene<MedicFinishCutscene>();
        wakeUps = FindInScene<PlayerWakeUp>();
        logSwings = FindInScene<LogSwingController>();
        pushReactions = FindInScene<PlayerPushReaction>();
    }

    private T[] FindInScene<T>() where T : Component
    {
        var results = new System.Collections.Generic.List<T>();
        foreach (var root in gameObject.scene.GetRootGameObjects())
            results.AddRange(root.GetComponentsInChildren<T>(true));
        return results.ToArray();
    }

    private void LateUpdate()
    {
        bool active = DialogueController.ActiveDialogue != null ||
            (ConversationManager.Instance != null && ConversationManager.Instance.IsConversationActive);

        foreach (var director in directors)
        {
            if (director == null || !director.isActiveAndEnabled || director.state != PlayState.Playing)
                continue;
            // Hold may keep the last frame without raising the stopped event.
            if (director.extrapolationMode != DirectorWrapMode.Hold || director.time < director.duration)
                active = true;
        }

        foreach (var sequence in sequences)
            active |= sequence != null && sequence.isActiveAndEnabled && sequence.IsRunning;
        foreach (var cutscene in medicCutscenes)
            active |= cutscene != null && cutscene.isActiveAndEnabled && cutscene.IsPlaying;
        foreach (var wakeUp in wakeUps)
            active |= wakeUp != null && wakeUp.isActiveAndEnabled && wakeUp.IsPlaying;
        foreach (var swing in logSwings)
            active |= swing != null && swing.isActiveAndEnabled && swing.IsPlaying;
        foreach (var reaction in pushReactions)
            active |= reaction != null && reaction.isActiveAndEnabled && reaction.IsReacting;

        Apply(active);
    }

    private void Apply(bool active)
    {
        if (visibleUI != null && visibleUI.activeSelf == active)
            visibleUI.SetActive(!active);
        if (narrativeVolume != null)
        {
            narrativeVolume.weight = active ? 1f : 0f;
            if (active)
            {
                narrativeVolume.gameObject.SetActive(true);
                narrativeVolume.enabled = true;
            }
        }
    }

    private void OnDisable() => Apply(false);
}
