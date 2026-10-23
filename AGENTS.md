# TIF — Unity Project

## Context efficiency
- Inspect only files relevant to the task.
- Avoid full-project scans unless necessary.
- Skip Library, Temp, Logs and generated files.
- Search C# symbols before reading full files.
- Read scenes, prefabs and .meta only when relevant.
- Avoid dumping large files into context.

## Unity workflow
- Preserve serialized fields and prefab references.
- Use only relevant Unity Skills.
- Prefer focused checks over full Unity builds.

## Changes and responses
- Make minimal, targeted changes.
- Don't print complete source files unnecessarily.
- Report modified files and Unity Inspector steps.
- Keep responses concise.