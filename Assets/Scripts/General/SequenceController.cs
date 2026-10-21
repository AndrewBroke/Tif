using System;
using DialogueEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

/// <summary>Optional orchestration layer. Existing standalone actions keep their own locks.</summary>
public class SequenceController : MonoBehaviour
{
    public enum StepType
    {
        Event = 0,
        Dialogue = 1,
        LogSwing = 2,
        PushReaction = 3,
        Timeline = 4,
        Delay = 5,
        WaitForDialogueClose = 6
    }

    [Serializable]
    public class Step
    {
        public string name;
        public StepType type;
        public UnityEvent onStart = new UnityEvent();
        public DialogueController dialogue;
        public NPCConversation conversation;
        public LogSwingController logSwing;
        public PlayerPushReaction pushReaction;
        public PlayableDirector director;
        public PlayableAsset timeline;
        [Min(0f)] public float delay;
        [Tooltip("Optional timeout in real seconds. Zero allows unlimited time (e.g. reading dialogue).")]
        [Min(0f)] public float timeout;
    }

    [SerializeField] private bool blockPlayerControls = true;
    [SerializeField] private Step[] steps = Array.Empty<Step>();
    [SerializeField] private UnityEvent onCompleted = new UnityEvent();
    [SerializeField] private UnityEvent onCancelled = new UnityEvent();

    public bool IsRunning { get; private set; }
    public int CurrentStepIndex { get; private set; } = -1;

    private Step currentStep;
    private float stepStartedAt;
    private bool stepFinished;
    private bool stepSucceeded;
    private Action unsubscribe;
    private Action stopAction;
    private int generation;

    // Safe to call from a dialogue option: acquire before that dialogue releases its lock.
    public void StartSequence()
    {
        if (!isActiveAndEnabled || IsRunning) return;
        if (steps == null || steps.Length == 0)
        {
            Debug.LogWarning("SequenceController: no steps configured.", this);
            return;
        }

        IsRunning = true;
        CurrentStepIndex = -1;
        generation++;
        if (blockPlayerControls) PlayerControlLocks.Acquire(this);
        StartNextStep();
    }

    private void Update()
    {
        if (!IsRunning || currentStep == null) return;
        try
        {
            if (currentStep.timeout > 0f && Time.unscaledTime - stepStartedAt >= currentStep.timeout)
                throw new InvalidOperationException("Step timed out.");

            if (currentStep.type == StepType.Delay)
                MarkFinished(Time.unscaledTime - stepStartedAt >= currentStep.delay);
            else if (currentStep.type == StepType.WaitForDialogueClose)
            {
                if (!ConversationIsActive()) MarkFinished(true);
            }
            else if (currentStep.type == StepType.Timeline && currentStep.director != null &&
                     currentStep.director.extrapolationMode == DirectorWrapMode.Hold &&
                     currentStep.director.time >= currentStep.director.duration)
                MarkFinished(true);

            if (!stepFinished) return;
            if (!stepSucceeded)
            {
                CancelSequence();
                return;
            }

            // OnConversationEnded precedes the closing UI transition. Do not start a new
            // conversation inside that callback or let the old transition close the new UI.
            if ((currentStep.type == StepType.Dialogue ||
                 currentStep.type == StepType.WaitForDialogueClose) && ConversationIsActive())
                return;

            StartNextStep();
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static bool ConversationIsActive() =>
        ConversationManager.Instance != null && ConversationManager.Instance.IsConversationActive;

    private void StartNextStep()
    {
        ClearSubscriptions();
        stopAction = null;
        CurrentStepIndex++;
        if (CurrentStepIndex >= steps.Length)
        {
            IsRunning = false;
            currentStep = null;
            PlayerControlLocks.Release(this);
            onCompleted?.Invoke();
            return;
        }

        currentStep = steps[CurrentStepIndex];
        stepStartedAt = Time.unscaledTime;
        stepFinished = false;
        stepSucceeded = false;
        try
        {
            if (currentStep == null) throw new InvalidOperationException("Missing step.");
            int run = generation;
            int index = CurrentStepIndex;
            Action<bool> finished = success =>
            {
                if (IsRunning && generation == run && CurrentStepIndex == index)
                {
                    stepFinished = true;
                    stepSucceeded = success;
                }
            };

            currentStep.onStart?.Invoke();
            if (!IsRunning || generation != run || CurrentStepIndex != index) return;

            switch (currentStep.type)
            {
                case StepType.Event:
                    finished(true);
                    break;
                case StepType.Dialogue:
                {
                    var target = currentStep.dialogue;
                    RequireEnabled(target);
                    if (currentStep.conversation == null || ConversationManager.Instance == null ||
                        ConversationIsActive() || DialogueController.ActiveDialogue != null)
                        throw new InvalidOperationException("Dialogue is missing or another conversation is active.");
                    target.Finished += finished;
                    unsubscribe = () => target.Finished -= finished;
                    stopAction = () => { if (target != null) target.InterruptDialogue(); };
                    target.StartDialogue(currentStep.conversation);
                    if (DialogueController.ActiveDialogue != target && !stepFinished)
                        throw new InvalidOperationException("Dialogue did not start.");
                    break;
                }
                case StepType.LogSwing:
                {
                    var target = currentStep.logSwing;
                    RequireEnabled(target);
                    if (target.IsPlaying) throw new InvalidOperationException("Log swing is already playing.");
                    target.Finished += finished;
                    unsubscribe = () => target.Finished -= finished;
                    stopAction = () => { if (target != null) target.StopSwing(); };
                    target.PlaySwing();
                    if (!target.IsPlaying && !stepFinished)
                        throw new InvalidOperationException("Log swing did not start.");
                    break;
                }
                case StepType.PushReaction:
                {
                    var target = currentStep.pushReaction;
                    RequireEnabled(target);
                    if (target.IsReacting) throw new InvalidOperationException("Push reaction is already playing.");
                    target.Finished += finished;
                    unsubscribe = () => target.Finished -= finished;
                    stopAction = () => { if (target != null) target.StopReaction(); };
                    target.Push();
                    if (!target.IsReacting && !stepFinished)
                        throw new InvalidOperationException("Push reaction did not start.");
                    break;
                }
                case StepType.Timeline:
                {
                    var target = currentStep.director;
                    RequireEnabled(target);
                    if (target.state == PlayState.Playing || target.extrapolationMode == DirectorWrapMode.Loop)
                        throw new InvalidOperationException("Timeline must be idle and must not loop.");
                    if (currentStep.timeline != null) target.playableAsset = currentStep.timeline;
                    if (target.playableAsset == null) throw new InvalidOperationException("Missing Timeline asset.");
                    // DirectorWrapMode.None resets time when stopping. Treat stopped as the
                    // end signal; explicit interruption goes through CancelSequence().
                    Action<PlayableDirector> stopped = source => finished(true);
                    target.stopped += stopped;
                    unsubscribe = () => target.stopped -= stopped;
                    stopAction = () => { if (target != null) target.Stop(); };
                    target.Play();
                    break;
                }
                case StepType.WaitForDialogueClose:
                {
                    // Observe an existing conversation without taking ownership or cancelling it.
                    var target = DialogueController.ActiveDialogue;
                    if (target != null)
                    {
                        target.Finished += finished;
                        unsubscribe = () => target.Finished -= finished;
                    }
                    if (!ConversationIsActive()) finished(true);
                    break;
                }
                case StepType.Delay:
                    break;
                default:
                    throw new InvalidOperationException("Unsupported step type.");
            }
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private void MarkFinished(bool complete)
    {
        if (!complete || stepFinished) return;
        stepFinished = true;
        stepSucceeded = true;
    }

    private static void RequireEnabled(Behaviour target)
    {
        if (target == null || !target.isActiveAndEnabled)
            throw new InvalidOperationException("Step target is missing or disabled.");
    }

    private void ClearSubscriptions()
    {
        var cleanup = unsubscribe;
        unsubscribe = null;
        cleanup?.Invoke();
    }

    public void CancelSequence()
    {
        if (!IsRunning) return;
        IsRunning = false;
        generation++;
        ClearSubscriptions();
        var stop = stopAction;
        stopAction = null;
        currentStep = null;
        try
        {
            stop?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
        finally
        {
            PlayerControlLocks.Release(this);
        }
        onCancelled?.Invoke();
    }

    private void Fail(Exception exception)
    {
        Debug.LogError($"SequenceController: step {CurrentStepIndex + 1} ({currentStep?.name}) failed: {exception.Message}", this);
        CancelSequence();
    }

    private void OnDisable()
    {
        CancelSequence();
        PlayerControlLocks.Release(this);
    }
}
