# Cleaning up a legacy project

This page describes how to use Asset Router to sort a project where assets have accumulated in
`Assets/` over years without a consistent folder structure.

## The situation

A project has 3000 assets spread across `Assets/`, `Assets/Temp/`, `Assets/Old/`, and a handful
of art folders. Names follow a loose convention that most (but not all) of the team knows.
The project compiles and ships, but nobody can find anything quickly.

## Step 1: Understand what names exist

Before setting up rules, open **Tools > Asset Router Settings**, go to the **Validate** tab, and
click **Scan Project**. This lists all monitored assets that currently match no rule. The list is
probably long; that is expected. Use **Copy to Clipboard** to export it and look at the naming
patterns.

Identify groups: textures that start with `T_`, UI images that start with `UI_`, audio files
that start with `SFX_`, etc. These become your rules.

## Step 2: Set up rules for known patterns

Add rules for the naming patterns you identified. Start with a few, not all at once.

Example starting set:
- `T_*` → `Assets/Art/Textures/`
- `UI_*` → `Assets/Art/UI/`
- `SFX_*` → `Assets/Audio/SFX/`

Leave `enableAutoImport` on to route new imports automatically. You will handle existing assets
in the next step.

## Step 3: Preview with Dry Run before moving anything

Open the **Dry Run** tab and click **Scan Project**. Review the table:

- Check that the "target folder" column shows where you actually want each file to go.
- Uncheck rows for files you are not sure about.
- Check that "Validate" and "already in place" are correct.

Do not click Apply Selected yet. If a pattern is too broad and catching wrong files, adjust it
in Settings, then scan again.

## Step 4: Apply in small batches

When you are confident in a subset, select only those rows in the Dry Run table and click
**Apply Selected**. Start with 20-30 files.

After each batch, open the **History** tab and verify the session looks correct. If something
went wrong, click **Undo Selected Session** to move the files back.

## Step 5: Handle files that match no rule

Assets that match no rule stay in place (no popup, no move). After each Dry Run batch, open
Validate again to see how many unmatched files remain. Decide whether to:

- Add a new rule for them.
- Rename them to fit an existing convention.
- Leave them where they are.

There is no requirement to route every file. Only the assets that fit the convention benefit from
the automation.

## Step 6: Repeat until done

Run Dry Run, apply a batch, check History, repeat. With 3000 assets, expect 5-10 sessions.
Each session is reversible via History until you are satisfied.

## What to watch for

**Extensions:** only files with extensions in the Monitored Extensions list appear in Dry Run.
If `.psd` files are not in the list but you want to route them, add `.psd` to the list first.

**Ignored folders:** files under `Assets/Plugins/` and `Assets/Editor/` are always ignored.
If you have legacy assets in a custom "ignored" folder you added, remove it from Ignored Folders
before scanning, or move those assets out first.

**References:** Asset Router uses `AssetDatabase.MoveAsset`, which updates `.meta` file GUIDs
and retains all asset references in scenes and prefabs. Moving assets does not break references.

## After the cleanup

Once the project is sorted, the rules continue working for new imports. The on-import routing
runs automatically; new artists drop files in the project and they land in the right folder.

Keep the database JSON in version control so the rules are shared across the team.
