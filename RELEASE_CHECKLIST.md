# Release checklist

Run this checklist before tagging a release. No step is optional.

## Code and tests

- [ ] All tests pass in CI (check the GitHub Actions run for the release commit).
- [ ] `TEST.md` manual checklist completed on a clean Unity project. No blocking issues.
- [ ] No new `TODO` or `FIXME` comments introduced in this release.

## Documentation

- [ ] Every new public class and method has an XMLDoc comment.
- [ ] Every new built-in action has a page in `Documentation~/actions/`.
- [ ] `Documentation~/DOCUMENTATION_EN.md` reflects all changes visible to users.
- [ ] `Documentation~/index.md` links are valid (no broken references).
- [ ] `README.md` in the package root is accurate for this version.
- [ ] `Samples~/QuickStart/README.md` is accurate.

## Version and changelog

- [ ] `package.json` version matches the intended git tag.
- [ ] `CHANGELOG.md` has an entry for this version with date and full list of changes.
- [ ] The changelog entry follows Keep a Changelog format (Added / Changed / Fixed / Removed).

## Release

- [ ] Create a git tag matching the version in `package.json` (e.g. `v0.9.0`).
- [ ] Create a GitHub Release from the tag. Paste the changelog entry as the release notes.
- [ ] Verify the package installs correctly on a fresh Unity 2022.3 project via git URL.
