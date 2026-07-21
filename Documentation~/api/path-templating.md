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
   - If the group exists **and** participated in the match, its value is sanitized and emitted.
   - If the group exists in the pattern but simply did not participate in this match (e.g. an
     optional group that was skipped), it is emitted as an empty string — the token disappears
     from the resolved path, so `A/{opt}/B/` becomes `A/B/` (repeated slashes are collapsed).
   - If the group does not exist at all (index out of range, name not defined), the token is
     emitted literally (`{name}`) and a warning is logged.
6. A lone `}` (not preceded by another `}`) is emitted literally.

---

## Path sanitization

Captured values pass through a sanitizer before substitution:

- Backslashes (`\`) are converted to forward slashes (`/`).
- The value is split on `/` into path segments; each segment is processed independently.
- Leading and trailing whitespace on a segment is trimmed (so `" .. "` cannot bypass the path
  traversal check below).
- Any segment equal to `..` or `.` is rejected: the token is kept literally and a warning is logged.
- Leading and trailing `.` characters on a segment are stripped — Unity ignores folders that begin
  with a dot, and Windows forbids trailing dots in folder names.
- Characters Windows forbids in a file or folder name (`< > : " | ? *`) are removed from each
  segment.
- If, after these removals, a non-empty input segment became empty, the token is rejected: kept
  literally and a warning is logged.
- Finally, any resulting `//` in the template is collapsed to `/`.

The path-traversal rejection prevents a captured value like `../../Secret` from redirecting a move
outside the intended target folder. The Windows-name rules keep folder creation from failing
silently on a mixed-OS team.

---

## Missing or unmatched tokens

**Group does not exist at all** (index out of range, undefined named group): the token is kept
literally and a warning is logged. Template `Assets/{3}/` with a pattern that has only two capture
groups produces `Assets/{3}/` unchanged in the resolved path.

**Group exists but did not participate in the match** (e.g. an optional segment that was skipped):
the token resolves to an empty string, and the surrounding path is collapsed. Template
`Assets/Art/{sub}/{name}/` when `sub` did not participate produces `Assets/Art/{name}/` — not
`Assets/Art//{name}/` or a literal `{sub}` folder.

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

**Warning in the Console: captured value became empty after removing invalid characters.**
The captured segment consisted entirely of Windows-forbidden characters (`< > : " | ? *`) or of
leading/trailing dots. The token is kept literally. Rename the file or narrow the capture group.

**Named token `{loc}` is not substituted.**
Named groups require Regex mode. In Glob mode, `{loc}` is always kept literally because Glob
produces only positional groups. Switch the rule to Regex and use `(?<loc>\w+)` syntax.
