# Schema migration: v1 to v2

This migration happened between Asset Router v0.1.0 and v0.2.0. If you installed the package
at v0.2.0 or later, your database was already created with v2 schema and this page does not apply.

## What changed

**v1 schema** stored matching conditions as three separate fields on each rule:

| Field | Example |
|-------|---------|
| `prefix` | `"T_"` |
| `suffix` | `""` |
| `extensionFilter` | `".png"` |

**v2 schema** replaced all three with a single `pattern` field and a `patternMode` enum:

| Field | Example |
|-------|---------|
| `patternMode` | `Glob` |
| `pattern` | `"T_*.png"` |

The migration combines the three v1 fields into one glob pattern:
`prefix + "*" + suffix + extensionFilter`

Examples:
- `prefix="T_"`, `suffix=""`, `extensionFilter=""` becomes `pattern="T_*"`
- `prefix="T_"`, `suffix="_D"`, `extensionFilter=".png"` becomes `pattern="T_*_D.png"`
- `prefix=""`, `suffix=""`, `extensionFilter=".wav"` becomes `pattern="*.wav"`

## How migration runs

When Asset Router loads a database with `schemaVersion < 2`, `RuleMigrator.MigrateIfNeeded` runs
automatically. It converts each rule, sets `schemaVersion = 2`, and saves the database.

The migration runs once. After `schemaVersion` is set to 2, it never runs again on that database.

The three legacy fields (`_legacyPrefix`, `_legacySuffix`, `_legacyExtensionFilter`) are kept in
the class with `[FormerlySerializedAs]` attributes so existing `.asset` files can be read.
They are hidden in the Inspector.

## The migration is automatic and irreversible

No user action is needed. On the first Editor load after upgrading, the database updates itself.

There is no downgrade path. Once saved as v2, the database cannot be opened by a v1 installation
of the plugin. This is only relevant if you are using the plugin in a team where different members
have different plugin versions, which is not recommended.

## Verifying the migration

After migration, open the Settings window. Each rule should show a `Pattern` field with a glob
string instead of the three separate prefix/suffix/extension fields. The `schemaVersion` field
on the database ScriptableObject should read `2`.
