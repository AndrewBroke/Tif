# Copilot Instructions

## 1. General behavior

- Follow the user's request exactly.
- Prefer the smallest change that solves the requested problem.
- Do not refactor, reorganize, rename, optimize, or redesign code unless explicitly requested.
- Preserve existing architecture, naming, public APIs, serialized fields, and behavior that is unrelated to the task.
- Do not create new files unless explicitly requested or absolutely required.
- Do not modify files that were not requested unless they are strictly necessary to complete the task.
- Before making broad changes, determine whether the task can be solved with a local change.

## 2. Context and file scope

### Important rule

When the user explicitly specifies a file, treat that file as the primary and preferred scope of the task.

For example:

> "Fix PlayerController.cs"

means:

1. Open and understand `PlayerController.cs`.
2. Attempt to solve the problem using only that file.
3. Do not scan the entire workspace.
4. Do not inspect unrelated scripts.
5. Do not modify other files.

Only inspect another file when information from that file is genuinely required to solve the problem.

If another file is required:

- Inspect only the necessary file.
- Do not recursively search related files unless necessary.
- Do not modify the additional file unless explicitly required.
- Prefer asking the user for clarification rather than exploring large parts of the project.

Do not perform broad workspace searches merely to understand the project.

## 3. Minimize context

Avoid unnecessary workspace exploration.

Do NOT inspect:

- Library/
- Temp/
- Logs/
- obj/
- Build/
- Builds/
- UserSettings/
- .vs/
- .git/

These directories contain generated or irrelevant data and should not be used as coding context.

Prefer:

- the current file
- files explicitly mentioned by the user
- directly referenced scripts
- directly required configuration files

Do not search for every possible reference to a class, method, variable, or component unless the task requires it.

## 4. Unity project rules

This is a Unity project.

Assume:

- `Assets/` contains project source files and game content.
- `Packages/` contains package dependencies.
- `ProjectSettings/` contains Unity project configuration.
- `Library/`, `Temp/`, `Logs/`, `obj/`, `Build/`, and `Builds/` are generated data.

Do not inspect generated Unity directories.

When modifying Unity C# scripts:

- Preserve existing `MonoBehaviour` structure.
- Preserve serialized fields and Inspector compatibility.
- Do not rename serialized fields unless explicitly requested.
- Do not change public methods or fields unnecessarily.
- Do not change Unity lifecycle methods unnecessarily.
- Preserve existing references between GameObjects and components.
- Avoid introducing new dependencies or packages unless explicitly requested.
- Prefer Unity APIs already used by the project.
- Match the existing coding style.

## 5. C# rules

- Prefer clear and simple C# over unnecessarily clever solutions.
- Do not introduce abstractions unless they solve an actual problem.
- Do not create interfaces, managers, services, factories, or utility classes unless requested or genuinely necessary.
- Do not replace working code with a completely different architecture without a clear reason.
- Preserve existing variable names where possible.
- Preserve comments unless they are incorrect or directly affected by the change.
- Avoid unnecessary allocations in frequently executed Unity methods such as `Update`, `FixedUpdate`, and `LateUpdate`.
- Do not add LINQ or other abstractions merely to shorten code.
- Do not change access modifiers unless necessary.
- Do not change `public` APIs without explicit permission.

## 6. Unity physics and movement

When working with Rigidbody-based movement:

- Respect Unity's physics update cycle.
- Rigidbody movement should generally be handled in `FixedUpdate`.
- Do not mix physics movement and Transform movement without a specific reason.
- Preserve the existing Rigidbody configuration unless the user asks to change it.
- Do not replace Rigidbody movement with CharacterController movement unless explicitly requested.
- When changing movement code, preserve existing gravity, jumping, crouching, sliding, and state-machine behavior unless the task specifically concerns those systems.

## 7. Input

- Preserve the input system currently used by the project.
- Do not migrate from the legacy Input Manager to the New Input System unless explicitly requested.
- Do not introduce a new input abstraction for a small local change.
- Preserve existing key bindings and input behavior unless the user asks to change them.

## 8. UI and localization

When working with UI:

- Preserve the existing UI system.
- If the project uses TextMeshPro, use TextMeshPro.
- Do not replace TextMeshPro with legacy UI or vice versa unless requested.
- Preserve existing localization architecture.
- Do not hard-code user-visible strings when the project already uses localization.
- When adding localized text, follow the existing localization system and naming conventions.

## 9. Scene and prefab safety

Be careful when modifying Unity scenes, prefabs, and serialized data.

Do not:

- rewrite `.unity` files unnecessarily
- rewrite `.prefab` files unnecessarily
- modify unrelated serialized properties
- remove components without explicit instruction
- change GameObject hierarchy without explicit instruction

If a C# change can solve the problem, prefer changing the C# script rather than modifying scene or prefab data.

## 10. Debugging

When debugging:

1. Identify the actual symptom.
2. Inspect the relevant code.
3. Find the smallest likely cause.
4. Make the smallest reasonable fix.
5. Avoid unrelated cleanup.

Do not assume that a common Unity bug is necessarily the cause.

Use the information provided by the user, including:

- error messages
- console output
- logs
- debugger information
- Inspector values
- observed behavior

If the evidence contradicts the initial hypothesis, reconsider the hypothesis instead of repeatedly applying similar fixes.

## 11. Existing code

Treat existing code as intentional unless there is evidence that it is wrong.

Do not:

- rewrite entire scripts unnecessarily
- rename variables for style reasons
- reorder large sections of code
- remove apparently unused fields without checking Unity serialization
- remove code merely because it appears unnecessary
- perform "cleanup" unrelated to the requested task

When possible, provide a focused patch rather than replacing the entire file.

## 12. When the user asks for a complete script

If the user explicitly asks for a complete script:

- Provide the complete resulting script.
- Preserve all existing functionality unless the user requests otherwise.
- Include the requested modification.
- Do not silently remove existing features.
- Do not introduce unrelated refactoring.

## 13. Agent behavior

Do not autonomously expand the scope of the task.

If the user asks:

> "Fix this script"

do not interpret that as:

> "Review and improve the entire project."

If the user asks:

> "Add a dialogue option"

do not redesign the dialogue system.

If the user asks:

> "Fix localization"

do not migrate the entire localization architecture.

Solve the requested problem first.

## 14. Searching the workspace

Before using workspace-wide search, ask:

> Is workspace-wide search actually necessary?

If the answer is no, do not use it.

Prefer targeted searches such as:

- exact filename
- exact class name
- exact method name
- exact error message
- directly referenced component

Avoid broad searches such as searching the entire project for common terms like:

- `Player`
- `GameObject`
- `Update`
- `Start`
- `Manager`
- `UI`

unless the task explicitly requires it.

## 15. Changes to multiple files

If a task genuinely requires multiple files:

1. Identify the minimum required files.
2. Inspect only those files.
3. Explain briefly why multiple files are required.
4. Modify only those files.
5. Do not expand the change to unrelated files.

## 16. Code quality

Prefer code that is:

- readable
- maintainable
- explicit
- compatible with the existing project
- easy for a beginner/intermediate Unity developer to understand

Do not optimize prematurely.

Do not introduce complexity merely because a more sophisticated solution exists.

## 17. Response format

After making changes:

- Briefly state what was changed.
- Mention which files were modified.
- Mention important behavior or assumptions if relevant.
- If there is a potential issue that cannot be verified from the available context, state it clearly.

Do not provide a long explanation of unrelated code.

## 18. Priority

When instructions conflict, use this priority:

1. Explicit user request.
2. Existing project architecture and code.
3. These instructions.
4. General coding preferences.

Never use these instructions to override an explicit user requirement.