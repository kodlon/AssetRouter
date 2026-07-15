# Pattern Cookbook

Every rule matches files with a pattern in one of two modes: **Glob** or **Regex**. This page
explains both, side by side, through recipes you can copy into the **Pattern** field. For the
formal reference see [Pattern syntax](DOCUMENTATION_EN.md#pattern-syntax); for `{tokens}` in the
target folder see [Path Templating](api/path-templating.md).

Three facts apply to both modes:

- Matching is case-insensitive. `t_rock.png` matches `T_*`.
- By default the pattern is compared against the **file name only** (`T_Rock.png`). Enable
  **Match Full Path** on the rule to compare against the full path (`Assets/Raw/T_Rock.png`).
  Any pattern that contains `/` needs it.
- The first matching rule from the top of the list wins.

---

## Glob in one minute

Glob is "the file name with holes in it". Three wildcards, everything else is literal:

| Wildcard | Matches | Example |
|----------|---------|---------|
| `*` | Any characters, but not `/` | `T_*` matches `T_Rock.png` |
| `?` | Exactly one character, not `/` | `Icon_??.png` matches `Icon_01.png` |
| `**` | Any characters including `/` | `Assets/Raw/**` matches everything under `Assets/Raw/` |

A Glob always matches the whole name: `T_*` does not match `MyT_file.png`, because the name must
start with `T_`. Each `*` and `**` also saves what it matched as a numbered capture (`{1}`,
`{2}`, ...) for the target folder. `?` saves nothing.

## Regex in five minutes

A regular expression (regex) is a pattern language built into .NET. You need a small subset:

| Piece | Meaning |
|-------|---------|
| `^` | Start of the name. Without it the pattern may match in the middle. |
| `$` | End of the name. |
| `.` | Any single character. A real dot must be escaped: `\.` |
| `\d` | One digit. `\w` is one letter, digit, or underscore. |
| `+` | One or more of the previous piece. `*` is zero or more. |
| `[abc]` | One character from the set. `[0-9]` is a range. |
| `(png\|tga)` | Alternatives: `png` or `tga` (written without the backslash). |
| `(?<name>...)` | Capture what `...` matched into a group called `name`. |

One trap to remember: **Regex mode does not anchor the pattern**. The pattern `T_` matches any
name that contains `T_` anywhere, including `NoT_Really.png`. Glob mode anchors automatically;
in Regex mode write `^` and `$` yourself when you mean the whole name.

---

## Recipes

Each recipe shows the Glob and the Regex version. When one column is missing, that mode cannot
express the task.

**Starts with a prefix.** The most common convention.
Glob: `T_*`
Regex: `^T_`

**Ends with a suffix.** Diffuse maps, LODs, and similar.
Glob: `*_D.png`
Regex: `_D\.png$`

**Prefix and suffix at once.**
Glob: `T_*_N.png`
Regex: `^T_.*_N\.png$`

**One of several extensions.** Glob has no "or", Regex does.
Regex: `^UI_.*\.(png|tga|psd)$`
Glob workaround: two rules, `UI_*.png` and `UI_*.tga`, with the same target folder.

**A numbered sequence.** Frames, takes, variants.
Glob: `Walk_??.png` (exactly two characters)
Regex: `^Walk_\d+\.png$` (one or more digits, and nothing but digits)

**Capture a name segment for the target folder.** The file name encodes the destination.
Glob: `T_Char_*_*` with target `Assets/Art/Characters/{1}/`
Regex: `^T_Char_(?<char>\w+)_.*` with target `Assets/Art/Characters/{char}/`
Both route `T_Char_Hero_D.png` to `Assets/Art/Characters/Hero/`. The Regex version keeps
matching if the convention grows, because `\w+` stops at the next underscore while Glob's `*`
is greedy. Check the live preview to see which segment each capture grabs.

**Everything inside a folder, any name.** Vendor drops, scanner output.
Glob: `Assets/External/**` with **Match Full Path** enabled.
Alternative without a path pattern: pattern `*` plus **Scope Folder** `Assets/External/`.

**A folder anywhere in the path.**
Glob: `**/Raw/*` with **Match Full Path** enabled: any file directly inside any `Raw/` folder.

**Strict version tag.** Only files ending in `_v` plus a number.
Regex: `_v\d+\.png$` matches `Hero_v2.png` but not `Hero_vFinal.png`.

---

## Common mistakes

| Symptom | Cause | Fix |
|---------|-------|-----|
| Regex `T_*.png` matches almost everything | In Regex, `*` means "zero or more of the previous piece" and `.` means "any character". This reads as "`T`, any number of `_`, one char, `png`". | You wanted Glob. In Regex the same intent is `^T_.*\.png$`. |
| Regex matches names it should not | Missing `^` and `$`. Unanchored patterns match anywhere in the name. | Anchor: `^SFX_` instead of `SFX_`. |
| Pattern with `/` never matches | Path patterns are compared against the file name by default. | Enable **Match Full Path** on the rule. |
| `Assets/**` grabs files from every subfolder including direct children | `**` crosses folder boundaries and matches empty too. | Expected behavior. Narrow the prefix (`Assets/Raw/**`) or use **Scope Folder**. |
| `{loc}` stays literal in the target folder | Named groups exist only in Regex mode. Glob produces `{1}`, `{2}` only. | Switch the rule to Regex and use `(?<loc>\w+)`. |
| Console warning about a pattern timeout | The regex is pathological and hit the 50 ms guard. The rule is treated as not matching. | Rewrite the pattern with fewer nested `*` and `+`. |
| Rule matches `T_rock.png` but you wanted only capitals | Matching is always case-insensitive. | Rely on folder or naming discipline; case cannot be enforced by the pattern. |

---

## How to test a pattern

1. **Live preview.** The line under the Pattern field shows up to 3 matching files from your
   project, and the resolved target path when the target folder contains tokens. Red text means
   the regex is invalid. The preview updates 300 ms after you stop typing.
2. **Dry Run tab.** Click **Scan Project** to see what every rule would do to every monitored
   file, without moving anything.
3. **Validate tab.** Lists monitored files that no rule matches. If your new pattern was meant
   to catch them, this is where you check.
4. **regex101.com** for hard regex cases. Set the flavor to **.NET** so the behavior matches
   what Asset Router runs.

The full .NET regex language, far beyond what routing needs, is documented in the
[.NET Regular Expressions quick reference](https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-language-quick-reference).
