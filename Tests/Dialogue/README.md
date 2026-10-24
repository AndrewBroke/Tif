# Dialogue regression checks

Run from the project root with .NET SDK 10:

```powershell
dotnet run --project Tests/Dialogue/DialogueChecks.csproj
```

The checks compile and execute the actual `ConversationManager`, `UIConversationButton`,
`DialogueController`, `SequenceController`, graph model and `PlayerControlLocks` sources.
Unity services, object lifecycle, localization and action components have lightweight stand-ins.
The checks cover rapid mouse/keyboard selection, branch preservation, reentrant events,
cancellation/replacement, completion ordering, source deactivation and the log sequence.

Rendering, native destruction, raycasts and serialized references still need a Unity Play Mode
check. No Inspector changes are needed for this fix. In particular, existing node events that
call `DisableDialogueMode()` and then deactivate their source remain supported.

In Play Mode, rapidly click answers, Continue and End in Acts 1/2/3, including the log's
"Ударить" branch. Each accepted choice should invoke its event once. The following speech
must be reached, and controls should return after the dialogue or complete sequence closes.
Also check cancellation and the options that deactivate their own dialogue object.
