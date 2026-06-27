# Contributing

## Running the tests

1. Open the project in Unity 2022.3 LTS or later.
2. Open **Window > General > Test Runner**.
3. Select **EditMode**.
4. Click **Run All**.

All tests must be green before submitting a pull request. The CI workflow runs the same tests on
every push and pull request via GitHub Actions.

## Pull request requirements

Every pull request must satisfy all of the following before it is merged:

**Code**
- All existing tests pass.
- New logic has tests. Aim for 80% coverage on new code; 100% on critical paths (pattern matching,
  migration, file writes).
- No `internal` class promoted to `public` without a documented reason.

**Documentation**
- A PR that adds a built-in action must include `Documentation~/actions/<ActionName>.md`.
- A PR that changes a public API (adds or renames a field, changes a method signature) must update
  the XMLDoc comment on that member.
- A PR that changes behavior visible to the user must update `Documentation~/DOCUMENTATION_EN.md`
  in the relevant section.

**Changelog**
- Add an entry to `CHANGELOG.md` under the version being developed. Follow the
  [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) format (Added / Changed / Fixed / Removed).

## Code style

- `internal sealed` where possible. Do not expose types externally unless the extension use case
  requires it.
- No comments unless the why is non-obvious. Names explain the what.
- No trailing whitespace, no unused `using` directives.
- Paths passed to `AssetDatabase.*` use forward slashes. Paths passed to `System.IO.*` for
  absolute paths use `Path.Combine` with `Path.DirectorySeparatorChar` or `PathUtility.ToAbsolute`.

## Serialization rules

- Any `[Serializable]` class stored in a `[SerializeReference]` list must have
  `[MovedFrom(true, oldNamespace, null, oldClassName)]` added when renamed or moved.
  Skipping this causes silent data loss in existing user databases.
- Any serialized field rename must use `[FormerlySerializedAs("oldName")]`.

## File writes

Any action or system that writes files to disk must use the atomic pattern:
write to `.tmp`, then `File.Replace(tmp, target, backup)`. A plain `File.WriteAllText` on the
target is not acceptable.
