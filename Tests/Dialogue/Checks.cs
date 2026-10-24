using System;
using System.Collections.Generic;
using System.Reflection;
using DialogueEditor;
using UnityEngine;
using UnityEngine.Events;
using Step = SequenceController.Step;
using Kind = SequenceController.StepType;

static class Checks
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static ConversationManager manager;
    static int passed;
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
    static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Fields).GetValue(target);
    static void Invoke(object target, string name) => target.GetType().GetMethod(name, Fields).Invoke(target, null);
    static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    static string State => Get<object>(manager, "m_state").ToString();
    static List<UIConversationButton> Buttons => Get<List<UIConversationButton>>(manager, "m_uiOptions");
    static UnityEvent Event(Action action)
    {
        var result = new UnityEvent();
        result.AddListener(action);
        return result;
    }
    static NPCConversation Graph(SpeechNode root) => new() { Data = new Conversation { Root = root } };
    static UIConversationButton NewButton()
    {
        var button = new UIConversationButton();
        Set(button, "TextMesh", new TMPro.TextMeshProUGUI());
        Set(button, "OptionBackgroundImage", new UnityEngine.UI.Image());
        Invoke(button, "Awake");
        return button;
    }
    static void Tick(float elapsed = 0.21f)
    {
        Time.frameCount++;
        Time.unscaledTime += elapsed;
        Time.deltaTime = elapsed * Time.timeScale;
        if (manager.isActiveAndEnabled) Invoke(manager, "Update");
    }
    static void Ready(NPCConversation graph)
    {
        manager.StartConversation(graph);
        Tick();
        Tick();
        Assert(State == "Idle", "Graph must be ready for selection");
    }
    static DialogueController ReadyController(NPCConversation graph)
    {
        var controller = new DialogueController();
        controller.StartDialogue(graph);
        Coroutine.Tick();
        Tick();
        Tick();
        Assert(State == "Idle", "Controller must start its graph");
        return controller;
    }
    static void Burst(UIConversationButton button)
    {
        for (int i = 0; i < 100; i++)
        {
            button.OnClick();
            button.OnButtonPressed();
            manager.PressSelectedOption();
        }
    }
    static void Run(string name, Action check)
    {
        DialogueController.ActiveDialogue?.InterruptDialogue();
        ConversationManager.OnConversationEnded = null;
        ConversationManager.OnConversationStarted = null;
        typeof(ConversationManager).GetProperty("Instance").SetValue(null, null);
        typeof(PlayerControlLocks).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        Coroutine.Running.Clear();
        Time.timeScale = 1f;
        Debug.Exceptions = 0;
        UnityEngine.Object.InstantiateHandler = _ => NewButton();
        manager = new ConversationManager
        {
            DialoguePanel = new RectTransform(), OptionsPanel = new RectTransform(),
            DialogueText = new TMPro.TextMeshProUGUI(), NameText = new TMPro.TextMeshProUGUI(),
            DialogueBackground = new UnityEngine.UI.Image(), AudioPlayer = new AudioSource(),
            ButtonPrefab = NewButton(), AllowMouseInteraction = true
        };
        Invoke(manager, "Awake");
        check();
        passed++;
        Console.WriteLine("PASS " + name);
    }

    static int Main()
    {
        try { RunAll(); return 0; }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    static void RunAll()
    {
        Run("mouse and keyboard bursts preserve the selected branch and invoke each node once", () =>
        {
            int choices = 0, speeches = 0, ended = 0;
            var next = new SpeechNode { Text = "next", Event = Event(() => speeches++) };
            var option = new OptionNode { Event = Event(() => choices++) };
            option.Connections.Add(new SpeechConnection(next));
            var root = new SpeechNode();
            root.Connections.Add(new OptionConnection(option));
            ConversationManager.OnConversationEnded += () => ended++;
            manager.StartConversation(Graph(root));
            Tick();
            var button = Buttons[0];
            Burst(button);
            Assert(choices == 0 && State == "TransitioningOptionsOn", "Fade-in must reject input");
            Tick();
            Burst(button);
            Assert(choices == 1 && Get<OptionNode>(manager, "m_selectedOption") == option,
                "Repeated presses must not erase or repeat the accepted choice");
            manager.EndButtonSelected();
            manager.SpeechSelected(root);
            Tick(0.11f);
            Burst(button);
            Tick(0.11f);
            Assert(speeches == 1 && ended == 0 && manager.IsConversationActive,
                "Fade-out clicks must neither reset the timer nor skip the next speech");
            Assert(Get<SpeechNode>(manager, "m_currentSpeech") == next, "Wrong branch");
            Tick();
            button.gameObject.SetActive(true);
            Burst(button);
            Assert(choices == 1, "A stale button must not affect the new node");
        });
        Run("the first of two answers wins, including reentrant selection inside Event", () =>
        {
            int firstCalls = 0, secondCalls = 0;
            var second = new OptionNode { Event = Event(() => secondCalls++) };
            var first = new OptionNode { Event = Event(() =>
            {
                firstCalls++;
                manager.OptionSelected(second);
                manager.PressSelectedOption();
            }) };
            var root = new SpeechNode();
            root.Connections.Add(new OptionConnection(first));
            root.Connections.Add(new OptionConnection(second));
            Ready(Graph(root));
            var other = Buttons[1];
            Burst(Buttons[0]);
            Burst(other);
            Assert(firstCalls == 1 && secondCalls == 0 && Get<OptionNode>(manager, "m_selectedOption") == first,
                "Selection must be committed before callbacks");
        });
        Run("Continue ignores fade-in and stale clicks and cannot skip the following speech", () =>
        {
            int nextCalls = 0, lastCalls = 0;
            var last = new SpeechNode { Event = Event(() => lastCalls++) };
            var next = new SpeechNode { Event = Event(() => nextCalls++) };
            next.Connections.Add(new SpeechConnection(last));
            var root = new SpeechNode();
            root.Connections.Add(new SpeechConnection(next));
            Ready(Graph(root));
            var previous = Buttons[0];
            previous.OnClick();
            Burst(previous);
            Burst(Buttons[0]);
            Assert(nextCalls == 1 && lastCalls == 0, "Continue must wait for the next node's readiness");
            Tick();
            Assert(Get<SpeechNode>(manager, "m_currentSpeech") == next, "The next speech was skipped");
        });
        Run("End spam completes once after UI closes and does not restart its timer", () =>
        {
            int ended = 0, finished = 0;
            var controller = ReadyController(Graph(new SpeechNode()));
            controller.Finished += success => { Assert(success, "Expected normal completion"); finished++; };
            ConversationManager.OnConversationEnded += () =>
            {
                Assert(!manager.IsConversationActive && !manager.DialoguePanel.gameObject.activeSelf &&
                    !manager.OptionsPanel.gameObject.activeSelf, "Completion preceded UI cleanup");
                ended++;
            };
            var button = Buttons[0];
            Burst(button);
            Tick();
            Assert(ended == 0 && finished == 0 && PlayerControlLocks.IsBlocked, "Closed too early");
            Tick(0.11f);
            manager.EndConversation();
            Burst(button);
            Tick(0.11f);
            Tick();
            manager.EndConversation();
            Assert(ended == 1 && finished == 1 && !PlayerControlLocks.IsBlocked, "Completion must be idempotent");
        });
        Run("legacy DisableDialogueMode plus source deactivation completes after closing", () =>
        {
            int finished = 0;
            var option = new OptionNode();
            var root = new SpeechNode();
            root.Connections.Add(new OptionConnection(option));
            var controller = ReadyController(Graph(root));
            var interaction = new Interactable();
            Set(controller, "pendingInteraction", interaction);
            controller.Finished += success => { Assert(success, "Source deactivation cancelled a normal ending"); finished++; };
            option.Event = Event(() =>
            {
                controller.DisableDialogueMode();
                controller.gameObject.SetActive(false);
            });
            Burst(Buttons[0]);
            Assert(State == "TransitioningOptionsOff" && finished == 0 && !interaction.Completed &&
                PlayerControlLocks.IsBlocked, "The legacy event completed early");
            Tick();
            Assert(finished == 0 && PlayerControlLocks.IsBlocked, "Source completed before closing UI");
            Tick();
            Assert(finished == 1 && interaction.Completed && !interaction.Cancelled &&
                DialogueController.ActiveDialogue == null && !PlayerControlLocks.IsBlocked, "Normal ending was lost");
        });
        Run("DisableDialogueMode in a final speech does not skip reading that speech", () =>
        {
            int finished = 0;
            var controller = new DialogueController();
            controller.Finished += success => { Assert(success, "Final speech was cancelled"); finished++; };
            controller.StartDialogue(Graph(new SpeechNode
            {
                Text = "Keep the final line readable",
                Event = Event(controller.DisableDialogueMode)
            }));
            Coroutine.Tick();
            Tick();
            Tick();
            Assert(State == "Idle" && finished == 0 && manager.DialogueText.text == "Keep the final line readable",
                "Early mode completion skipped the final line");
            Burst(Buttons[0]);
            Tick();
            Tick();
            Assert(finished == 1 && !PlayerControlLocks.IsBlocked, "Final speech failed to complete");
        });
        Run("an option can cancel without the old transition overwriting cancellation", () =>
        {
            int ended = 0;
            var option = new OptionNode { Event = Event(() => manager.CancelConversation()) };
            var root = new SpeechNode();
            root.Connections.Add(new OptionConnection(option));
            Ready(Graph(root));
            ConversationManager.OnConversationEnded += () => ended++;
            Burst(Buttons[0]);
            Assert(State == "TransitioningDialogueOff", "Old option transition resumed after cancellation");
            Tick();
            Assert(ended == 0 && !manager.IsConversationActive && !PlayerControlLocks.IsBlocked, "Cancel emitted success");
        });
        Run("speech Event can cancel without applying old parameters or playing old audio", () =>
        {
            var root = new SpeechNode { Audio = new AudioClip(), Event = Event(() => manager.CancelConversation()) };
            root.ParamActions.Add(new SetIntParamAction { ParameterName = "value", Value = 7 });
            var graph = Graph(root);
            var parameter = new IntParameter("value", 0);
            graph.Data.Parameters.Add(parameter);
            manager.StartConversation(graph);
            Tick();
            Assert(State == "TransitioningDialogueOff" && Buttons.Count == 0 && parameter.IntValue == 0 &&
                manager.AudioPlayer.Plays == 0, "Cancelled speech continued executing");
            Tick();
            Assert(!manager.IsConversationActive && !PlayerControlLocks.IsBlocked, "Cancel leaked controls");
        });
        Run("manager cancellation from a node also interrupts its owning controller once", () =>
        {
            int interrupted = 0;
            var option = new OptionNode { Event = Event(manager.CancelConversation) };
            var root = new SpeechNode();
            root.Connections.Add(new OptionConnection(option));
            var controller = ReadyController(Graph(root));
            controller.Finished += success => { Assert(!success, "Cancellation reported success"); interrupted++; };
            Burst(Buttons[0]);
            Tick();
            Assert(interrupted == 1 && DialogueController.ActiveDialogue == null && !PlayerControlLocks.IsBlocked,
                "Manager cancellation left its owner or sequence waiting");
        });
        Run("speech Event can replace the conversation without old code modifying the new graph", () =>
        {
            var replacement = Graph(new SpeechNode { Name = "replacement", Text = "new graph" });
            var old = new SpeechNode { Audio = new AudioClip(), Event = Event(() => manager.StartConversation(replacement)) };
            old.ParamActions.Add(new SetIntParamAction { ParameterName = "value", Value = 7 });
            var parameter = new IntParameter("value", 0);
            replacement.Data.Parameters.Add(parameter);
            manager.StartConversation(Graph(old));
            Tick();
            Assert(State == "TransitioningDialogueBoxOn" && Get<Conversation>(manager, "m_conversation") == replacement.Data &&
                parameter.IntValue == 0 && manager.AudioPlayer.Plays == 0, "Old speech overwrote its replacement");
            Tick();
            Tick();
            Assert(manager.DialogueText.text == "new graph" && State == "Idle", "Replacement failed to become ready");
        });
        Run("restarting during opening resets the fade and root UI", () =>
        {
            manager.StartConversation(Graph(new SpeechNode { Name = "old" }));
            Tick(0.15f);
            manager.StartConversation(Graph(new SpeechNode { Name = "new" }));
            Tick(0.1f);
            Assert(State == "TransitioningDialogueBoxOn" && manager.NameText.text == "new", "Opening reused old state");
        });
        Run("a started callback can cancel without resurrecting the conversation", () =>
        {
            ConversationManager.OnConversationStarted += manager.CancelConversation;
            manager.StartConversation(Graph(new SpeechNode()));
            Tick();
            Assert(!manager.IsConversationActive && !PlayerControlLocks.IsBlocked, "Start callback cancellation was overwritten");
        });
        Run("node exceptions interrupt the controller without success or leaked locks", () =>
        {
            var root = new SpeechNode { Event = Event(() => throw new InvalidOperationException("Expected test exception")) };
            var controller = new DialogueController();
            int failed = 0;
            controller.Finished += success => { Assert(!success, "An exception must not complete successfully"); failed++; };
            controller.StartDialogue(Graph(root));
            Coroutine.Tick();
            Tick();
            Tick();
            Assert(Debug.Exceptions == 1 && failed == 1 && DialogueController.ActiveDialogue == null &&
                !manager.IsConversationActive && !PlayerControlLocks.IsBlocked, "Exception left the dialogue locked");
        });
        Run("disabling the manager during a speech Event cannot recreate options", () =>
        {
            manager.StartConversation(Graph(new SpeechNode { Event = Event(() => manager.gameObject.SetActive(false)) }));
            Tick();
            Assert(!manager.IsConversationActive && Buttons.Count == 0 && !PlayerControlLocks.IsBlocked,
                "Disabled manager was resurrected by its old speech");
        });
        Run("paused dialogue rejects both mouse and keyboard", () =>
        {
            int calls = 0;
            var root = new SpeechNode();
            root.Connections.Add(new OptionConnection(new OptionNode { Event = Event(() => calls++) }));
            Ready(Graph(root));
            Time.timeScale = 0f;
            Burst(Buttons[0]);
            Assert(calls == 0 && State == "Idle", "Pause accepted dialogue input");
        });
        Run("Finished can start the next controller after all old cleanup", () =>
        {
            var controller = ReadyController(Graph(new SpeechNode()));
            var next = new DialogueController();
            var graph = Graph(new SpeechNode { Text = "next controller" });
            controller.Finished += success => { if (success) next.StartDialogue(graph); };
            Burst(Buttons[0]);
            Tick();
            Tick();
            Assert(DialogueController.ActiveDialogue == next && PlayerControlLocks.IsBlocked, "Next controller lost ownership");
            Coroutine.Tick();
            Tick();
            Tick();
            Assert(State == "Idle" && manager.DialogueText.text == "next controller", "Old cleanup closed the next dialogue");
        });
        Run("log chain runs once and holds controls until the final dialogue has closed", () =>
        {
            var option = new OptionNode();
            var root = new SpeechNode();
            root.Connections.Add(new OptionConnection(option));
            ReadyController(Graph(root));
            var swing = new LogSwingController();
            var push = new PlayerPushReaction();
            var thoughts = new DialogueController();
            var sequence = new SequenceController();
            Set(sequence, "steps", new[]
            {
                new Step { type = Kind.WaitForDialogueClose },
                new Step { type = Kind.LogSwing, logSwing = swing },
                new Step { type = Kind.PushReaction, pushReaction = push },
                new Step { type = Kind.Dialogue, dialogue = thoughts, conversation = Graph(new SpeechNode()) }
            });
            option.Event = Event(sequence.StartSequence);
            Burst(Buttons[0]);
            Tick();
            Invoke(sequence, "Update");
            Assert(!swing.IsPlaying && PlayerControlLocks.IsBlocked, "Swing started before closing");
            Tick();
            Invoke(sequence, "Update");
            sequence.StartSequence();
            Assert(swing.Starts == 1 && PlayerControlLocks.IsBlocked, "Swing duplicated or unlocked controls");
            swing.Complete();
            Invoke(sequence, "Update");
            Assert(push.IsReacting && PlayerControlLocks.IsBlocked, "Push did not start");
            push.Complete();
            Invoke(sequence, "Update");
            Coroutine.Tick();
            Tick();
            Tick();
            Assert(DialogueController.ActiveDialogue == thoughts && PlayerControlLocks.IsBlocked, "Thoughts failed to start");
            Burst(Buttons[0]);
            Tick();
            Invoke(sequence, "Update");
            Assert(sequence.IsRunning && PlayerControlLocks.IsBlocked, "Final dialogue unlocked too early");
            Tick();
            Invoke(sequence, "Update");
            Assert(!sequence.IsRunning && !PlayerControlLocks.IsBlocked, "Completed chain left controls locked");
        });
        Console.WriteLine($"{passed} dialogue regression checks passed.");
    }
}
