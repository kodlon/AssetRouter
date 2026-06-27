# Solo developer setup

This page covers a minimal Asset Router configuration for a solo developer on a 2D mobile game
with around 500 assets.

## When to set it up

Do not configure Asset Router on day one of a project. Set it up when you notice you are doing
the same manual step repeatedly: moving the same type of file to the same folder after every
export, or setting the same importer option every time.

Asset Router removes repetition. Identify the repetition first.

## Minimal setup

Change only the target folders in the four default rules to match your project structure.
Leave patterns, presets, and everything else at their defaults.

Example: if your textures go in `Assets/Sprites/UI/` instead of `Assets/Art/UI/`, change only
the target folder on the "UI Textures" rule.

That is enough for most solo projects.

## When to add actions

Add an action when you notice you do the same thing manually after import more than a few times
per week:

- Always set pivot to center on every UI sprite: add `SetPivotAction`.
- Always trim silence from WAV exports: add `TrimAudioSilenceAction`.
- Always create a material from each texture for a specific rule: add `CreateMaterialFromTextureAction`.

Do not add actions speculatively. One useful action beats five unused ones.

## JSON export

For a solo developer with a single machine, the database `.asset` file in git is enough.
You do not need JSON export unless you want readable diffs in pull requests or plan to share
rules with someone else.

## Diagnostic Window

When a file does not move and you cannot figure out why, open
**Tools > Asset Router > Diagnostic Window**. It shows exactly what the postprocessor saw:
whether the file's extension is monitored, whether a rule matched, and what outcome resulted.

## Validate tab

Open the Validate tab after a large asset import to find any files that did not match a rule.
Either rename them to fit a rule, add a new rule, or leave them in place.

The Validate tab does not move anything. It is a read-only report.

## History and Undo

If you apply the wrong rule or mistype a target folder and 50 files end up in the wrong place,
open the History tab and undo the session. The undo reverses the moves without touching import
settings.

Check the result in the History tab before closing Unity. The undo log is stored in `Library/`,
which is not committed to git. If you need to recover after a fresh clone, use the Dry Run tab
to re-apply the rules from scratch.

## Scope folder

Skip `scopeFolder` until you have multiple "input" folders for the same file type. For a solo
developer with one asset drop location, it is not needed.
