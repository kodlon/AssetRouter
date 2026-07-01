# Path Templating

Path Templating lets a single rule route assets to different subdirectories based on values captured
from the pattern. Tokens in the `targetFolder` field are replaced at import time with the captured
values from the matched file name.

## Quick example

Pattern `T_Char_*_*` (Glob), target `Assets/Art/Characters/{1}/`:

| Imported file | Resolved target |
|---------------|-----------------|
| `T_Char_Hero_D.png` | `Assets/Art/Characters/Hero/` |
| `T_Char_Enemy_N.png` | `Assets/Art/Characters/Enemy/` |
| `T_Char_Boss_D.png` | `Assets/Art/Characters/Boss/` |

One rule replaces one rule per character.

---

## Token grammar

| Token | Meaning |
|-------|---------|
| `{1}`, `{2}`, … | Positional capture group (1-indexed). |
| `{name}` | Named capture group. Regex mode only, via `(?<name>...)`. |
| `{{` | Literal `{`. |
| `}}` | Literal `}`. |

Group 0 (the entire match) is available as `{0}`, but rarely useful since it equals the full file name.

---

## Capture groups by pattern mode

### Glob mode

Each `*` and `**` wildcard produces one positional capture group, numbered left-to-right:

| Wildcard | Regex equivalent | Group |
|----------|-----------------|-------|
| `*` | `([^/]*)` | Positional |
| `**` | `(.*)` | Positional |
| `?` | `[^/]` | None — no capture |

Example: pattern `T_*_*`, file `T_Hero_Diffuse.png`:
- `{1}` → `Hero`
- `{2}` → `Diffuse.png`

### Regex mode

Groups are numbered by their opening parenthesis, left-to-right. Named groups `(?<name>...)` are
referenced by name with `{name}` or by index with `{1}`, `{2}`, etc.

Example: pattern `^T_Loc_(?<loc>\w+)_.*`, file `T_Loc_Forest_Rock.png`:
- `{1}` → `Forest`
- `{loc}` → `Forest`

---

## Token resolution flow

1. If the template contains no `{`, it is returned unchanged (fast path, zero overhead).
2. The template is scanned left-to-right one character at a time.
3. `{{` emits a literal `{` and advances past both characters.
4. `}}` emits a literal `}` and advances past both characters.
5. `{` starts a token. The scanner reads until the closing `}` to extract the token name.
   - If the name is a non-negative integer, the corresponding group is looked up by index.
   - Otherwise, the group is looked up by name.
   - If the group exists and participated in the match, its value is sanitized and emitted.
   - If the group does not exist or did not participate, the token is emitted literally (`{name}`).
6. A lone `}` (not preceded by another `}`) is emitted literally.

---

## Path sanitization

Captured values pass through a sanitizer before substitution:

- Backslashes (`\`) are converted to forward slashes (`/`).
- The value is split on `/` into path segments.
- Any segment equal to `..` or `.` is rejected: the token is kept literally and a warning is logged.

This prevents a captured value like `../../Secret` from producing a path traversal.

---

## Missing or unmatched tokens

A token whose group does not exist in the pattern, or whose group did not participate in the match
(e.g. an optional group that was skipped), is kept in the output literally.

Template `Assets/{3}/` with a pattern that has only two capture groups produces `Assets/{3}/`
unchanged in the resolved path.

---

## Backward compatibility

Any `targetFolder` that contains no `{` character is handled by a fast path and is unaffected by
this feature. Existing rules continue to work with zero overhead.

---

## Troubleshooting

**The file lands in a folder named `{1}` literally.**
The pattern has no capture groups at that index, or the group index is out of range. In Glob mode,
`?` does not produce a capture group — only `*` and `**` do. Check the live preview in the Settings
window: when the target folder contains tokens, the preview shows `filename → resolved_path`.

**The file lands in an unexpected subfolder.**
Open the rule detail panel and look at the live preview. It shows the resolved path for each
matching file. Verify which capture group holds the value you expect.

**Warning in the Console: captured value rejected (path traversal).**
A captured value contained `..` or `.` as a path segment. The token was kept literally.
Adjust the pattern to exclude such values, or use a more restrictive wildcard.

**Named token `{loc}` is not substituted.**
Named groups require Regex mode. In Glob mode, `{loc}` is always kept literally because Glob
produces only positional groups. Switch the rule to Regex and use `(?<loc>\w+)` syntax.
