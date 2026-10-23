using System;
using System.Reflection;
using DialogueEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using Step = SequenceController.Step;
using Kind = SequenceController.StepType;

static class Checks
{
    static void Set(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    static void Invoke(object target, string method) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    static void Assert(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }
    static void Tick(SequenceController sequence)
    {
        Time.frameCount++;
        Time.unscaledTime += 0.02f;
        Invoke(sequence, "Update");
    }
    static SequenceController Sequence(params Step[] steps)
    {
        var sequence = new SequenceController();
        Set(sequence, "steps", steps);
        return sequence;
    }
    static void Reset()
    {
        typeof(PlayerControlLocks).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, null);
        DialogueController.ActiveDialogue?.InterruptDialogue();
        ConversationManager.OnConversationEnded = null;
        ConversationManager.Instance = new ConversationManager();
        Time.unscaledTime = 0;
    }
    static void Run(string name, Action check)
    {
        Reset();
        check();
        Console.WriteLine("PASS " + name);
    }
    static void Main()
    {
        Run("complete log chain holds controls through both dialogue closing transitions", () =>
        {
            var initialDialogue = new DialogueController();
            var thoughts = new DialogueController();
            var swing = new LogSwingController();
            var push = new PlayerPushReaction();
            initialDialogue.StartDialogue(new NPCConversation());
            var sequence = Sequence(
                new Step { type = Kind.WaitForDialogueClose },
                new Step { type = Kind.LogSwing, logSwing = swing },
                new Step { type = Kind.PushReaction, pushReaction = push },
                new Step { type = Kind.Dialogue, dialogue = thoughts, conversation = new NPCConversation() });
            sequence.StartSequence();
            ConversationManager.Instance.EndConversation();
            Tick(sequence);
            Assert(!swing.IsPlaying && PlayerControlLocks.IsBlocked, "Must wait for old UI to close");
            ConversationManager.Instance.CloseUI();
            Tick(sequence);
            Assert(swing.IsPlaying && PlayerControlLocks.IsBlocked, "Swing must hold controls");
            sequence.StartSequence();
            Assert(swing.Starts == 1, "Duplicate sequence start must be ignored");
            swing.Complete();
            Tick(sequence);
            Assert(push.IsReacting && PlayerControlLocks.IsBlocked, "Push must hold controls");
            push.Complete();
            Tick(sequence);
            Assert(DialogueController.ActiveDialogue == thoughts && PlayerControlLocks.IsBlocked,
                "Thoughts must start after push completion");
            ConversationManager.Instance.EndConversation();
            Tick(sequence);
            Assert(sequence.IsRunning && PlayerControlLocks.IsBlocked, "Wait for thoughts UI to close");
            ConversationManager.Instance.CloseUI();
            Tick(sequence);
            Assert(!sequence.IsRunning && !PlayerControlLocks.IsBlocked, "Release after final closing transition");
        });
        Run("other owners retain their locks after a sequence completes", () =>
        {
            var pause = new MonoBehaviour();
            PlayerControlLocks.Acquire(pause, true);
            var sequence = Sequence(new Step { type = Kind.Event });
            sequence.StartSequence();
            Tick(sequence);
            Assert(!sequence.IsRunning && PlayerControlLocks.IsBlocked && Cursor.visible,
                "Sequence must not release pause lock or hide its cursor");
            PlayerControlLocks.Release(pause);
            Assert(!PlayerControlLocks.IsBlocked, "No sequence lock should remain");
        });
        Run("ordinary standalone dialogue retains its existing lock behavior", () =>
        {
            var dialogue = new DialogueController();
            dialogue.StartDialogue(new NPCConversation());
            Assert(PlayerControlLocks.IsBlocked && Cursor.visible, "Dialogue must lock controls");
            ConversationManager.Instance.EndConversation();
            Assert(PlayerControlLocks.IsBlocked, "Manager lock remains through UI fade");
            ConversationManager.Instance.CloseUI();
            Assert(!PlayerControlLocks.IsBlocked && !Cursor.visible, "Dialogue must release controls");
        });
        Run("cancelling a step stops only the owned action and supports restarting", () =>
        {
            var swing = new LogSwingController();
            var sequence = Sequence(new Step { type = Kind.LogSwing, logSwing = swing });
            sequence.StartSequence();
            sequence.CancelSequence();
            Assert(!swing.IsPlaying && !PlayerControlLocks.IsBlocked, "Cancel must stop action and unlock");
            swing.Complete();
            sequence.StartSequence();
            Tick(sequence);
            Assert(sequence.IsRunning && swing.IsPlaying, "Old completion must not advance new run");
            swing.Complete();
            Tick(sequence);
            Assert(!sequence.IsRunning && !PlayerControlLocks.IsBlocked, "Restart must complete cleanly");
        });
        Run("interrupted dialogue aborts instead of advancing", () =>
        {
            var dialogue = new DialogueController();
            bool nextRan = false;
            var after = new Step { type = Kind.Event };
            after.onStart.AddListener(() => nextRan = true);
            var sequence = Sequence(new Step { type = Kind.Dialogue, dialogue = dialogue,
                conversation = new NPCConversation() }, after);
            sequence.StartSequence();
            dialogue.InterruptDialogue();
            Tick(sequence);
            Assert(!nextRan && !sequence.IsRunning && !PlayerControlLocks.IsBlocked,
                "Interruption must cancel and release sequence lock");
        });
        Run("missing or disabled targets fail without leaking a lock", () =>
        {
            var sequence = Sequence(new Step { type = Kind.LogSwing });
            sequence.StartSequence();
            Assert(!sequence.IsRunning && !PlayerControlLocks.IsBlocked, "Missing target must abort");
            sequence = Sequence(new Step { type = Kind.PushReaction,
                pushReaction = new PlayerPushReaction { isActiveAndEnabled = false } });
            sequence.StartSequence();
            Assert(!sequence.IsRunning && !PlayerControlLocks.IsBlocked, "Disabled target must abort");
        });
        Run("disable and timeout clean up their running action", () =>
        {
            var swing = new LogSwingController();
            var sequence = Sequence(new Step { type = Kind.LogSwing, logSwing = swing, timeout = 0.01f });
            sequence.StartSequence();
            Tick(sequence);
            Assert(!sequence.IsRunning && !swing.IsPlaying && !PlayerControlLocks.IsBlocked, "Timeout cleanup");
            sequence.StartSequence();
            Invoke(sequence, "OnDisable");
            Assert(!sequence.IsRunning && !swing.IsPlaying && !PlayerControlLocks.IsBlocked, "Disable cleanup");
        });
        Run("cancellation from onStart cannot start the action afterwards", () =>
        {
            var swing = new LogSwingController();
            var step = new Step { type = Kind.LogSwing, logSwing = swing };
            var sequence = Sequence(step);
            step.onStart.AddListener(sequence.CancelSequence);
            sequence.StartSequence();
            Assert(!swing.IsPlaying && !sequence.IsRunning && !PlayerControlLocks.IsBlocked,
                "Reentrant cancellation must be respected");
        });
        Run("Timeline Hold completes without requiring a stopped event", () =>
        {
            var director = new PlayableDirector { playableAsset = new PlayableAsset(),
                extrapolationMode = DirectorWrapMode.Hold };
            var sequence = Sequence(new Step { type = Kind.Timeline, director = director });
            sequence.StartSequence();
            director.time = director.duration;
            Tick(sequence);
            Assert(!sequence.IsRunning && !PlayerControlLocks.IsBlocked, "Hold completion must unlock");
        });
        Run("step exceptions cancel without leaving controls blocked", () =>
        {
            var step = new Step { type = Kind.Event };
            step.onStart.AddListener(() => throw new InvalidOperationException("Test failure"));
            var sequence = Sequence(step);
            sequence.StartSequence();
            Assert(!sequence.IsRunning && !PlayerControlLocks.IsBlocked, "Exception cleanup");
        });
    }
}
