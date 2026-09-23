# Interaction prompt and event plan

## Context
- `Assets/Scripts/InteractionSystem/PlayerInteraction.cs` currently only declares some radius/distance fields and draws a gizmo; no detection, UI, or event logic yet.
- Project already uses the new Input System. `Assets/InputSystem_Actions.inputactions` defines a `Player/Interact` button action (bound to `<Keyboard>/e` plus other devices) that the user confirmed should drive the interaction flow.
- `FireExtinguish` and other gameplay scripts are scripted responses (via public methods) that we need to hook to from interactable objects via UnityEvent instead of hard coding.

## Requirements
1. When the player looks at a nearby interactable object, show a bottom-screen prompt reading `Нажмите <binding> чтобы взаимодействовать` (binding string reflects the current `Interact` action binding, per Input System settings) and hide it when the player looks away or leaves range.
2. Pressing the `Interact` action while the prompt is visible should invoke a UnityEvent exposed by the object being looked at, so designers can hook up any existing script (e.g., `FireExtinguish.ExtinguishFire`).
3. Detection should respect a configurable range and a small cone/radius to tolerate minor aim errors.

## Key decisions
1. **Interactable definition** – introduce an `[RequireComponent(typeof(Collider))]` `Interactable` component that stores an optional prompt alias (defaulting to the GameObject name) and a `UnityEvent OnInteract`. This makes it easy to connect pressing `Interact` to arbitrary behavior without further scripting.
2. **Detection logic** – extend `PlayerInteraction` so it raycasts or sphere-casts from the main camera, using serialized `interactDistance`/`interactRadius` and a `LayerMask` for interactables, and caches the currently looked-at `Interactable` to drive the prompt/UI state.
3. **Input binding awareness** – the interaction prompt will rely on an `InputActionReference` to `Player/Interact`; `PlayerInteraction` subscribes to the action’s `performed` callback (taking into account the current `Hold` interaction) and asks the action for a binding display string every time the prompt is shown.
4. **Prompt UI** – create `InteractionPromptUI` that owns a `CanvasGroup` + `TMP_Text` and exposes `Show(string bindingText, string objectLabel)` / `Hide()` so `PlayerInteraction` can control visibility without duplicating UI logic.

## Implementation steps
1. **Player interaction controller** (`Assets/Scripts/InteractionSystem/PlayerInteraction.cs`)
   - Cache references to the main camera, the prompt UI, the `Interact` `InputActionReference`, and a new `LayerMask interactableMask`.
   - In `Update`, perform a `Physics.SphereCast` (or `Physics.Raycast` with radius) from the camera along its forward vector up to `interactDistance`, filter by `interactableMask`, and pick the closest `Interactable` hit. Track state changes so the prompt only updates when the target changes.
   - When a target is found, call `promptUI.Show(bindingDisplayString, target.GetPromptLabel())`; when the target is lost, call `promptUI.Hide()`.
   - Subscribe to `interactActionReference.action.performed`/`cancelled` in `OnEnable`/`OnDisable` to react to the input. When the action fires and there is a valid `currentInteractable`, invoke its UnityEvent.
2. **Prompt UI helper** (`Assets/Scripts/InteractionSystem/InteractionPromptUI.cs`)
   - Reference a `TMP_Text promptText` and optionally a `CanvasGroup` to fade/show the prompt.
   - Expose a serialized format string (`string format = "Нажмите {0} чтобы взаимодействовать"`) and a method to build the runtime message (maybe allow appending target label if provided).
   - Provide `Show(...)`, `Hide()`, and ensure it starts hidden (alpha 0/interactable false) so designers can place the UI anywhere.
3. **Interactable component** (`Assets/Scripts/InteractionSystem/Interactable.cs`)
   - Serializable fields: `string promptLabel`, `UnityEvent onInteract`, `bool requiresLookFacing` (if needed later). Provide `GetPromptLabel()` returning either the custom label or `gameObject.name`.
   - `public void Interact()` method that invokes `onInteract` (optionally checks `enabled` and `gameObject.activeInHierarchy`).
4. **Scene/prefab wiring**
   - Create/bind a UI Canvas (probably overlay) with `TMP_Text` anchored at the bottom. Attach the new `InteractionPromptUI` and point its `TMP_Text` reference to the label.
   - On the player prefab (e.g., `StarterAssets/FirstPersonController/Prefabs/PlayerCapsule.prefab`), add `PlayerInteraction` and assign `interactDistance`, `interactRadius`, `interactableMask` (use new layer for interactables), link the prompt UI, and assign the `Interact` `InputActionReference` from `Assets/InputSystem_Actions.inputactions` (the `Player/Interact` action).
   - For each object that should be interactable (starting with existing fire), add the `Interactable` component, set a user-friendly prompt label if needed (e.g., `"Тушить огонь"`), and wire its `OnInteract` UnityEvent to call `FireExtinguish.ExtinguishFire` (or other handlers as required).
   - Ensure interactable objects live on the designated layer so the spherecast can filter them.
5. **Documentation/notes**
   - Note in the developer documentation (or inline comments) how to add new interactables: add component, set prompt string, wire UnityEvent.
   - Mention to designers that the prompt format uses the Input System binding string and respects remapping.

## Validation
- Run the scene in the editor, look at an interactable, and confirm the prompt text appears with the proper binding string and optional object label.
- Press `E` (or remapped binding) while the prompt is active and confirm the linked UnityEvent runs (e.g., fire extinguishes). Ensure the message stays until the player moves away.
- Verify that leaving the interactable’s range or angling away hides the prompt immediately.
- Check that disabling/unassigning the `InputActionReference` does not throw null-reference exceptions (log warnings if not assigned).

- `Player/Interact` is currently configured with a `Hold` interaction. Confirm whether the system should interact on `performed` (after hold completes) or switch to `Press` so a tap triggers immediately. In the plan we assume the existing `Hold` is deliberate; if not, the action asset needs adjusting.
- Need to assign a dedicated layer mask for interactables; ensure existing objects are moved to that layer (or let the detection include `Default` if no custom layer is available).
