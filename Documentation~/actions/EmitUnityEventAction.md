# EmitUnityEventAction

Fires a serialized `UnityEvent<Object>` when an asset is imported.
Lets non-programmers wire up callbacks entirely in the Inspector without writing any code.

**Applies to:** Any asset type.

**Tier:** D — no-code Inspector hook using `UnityEvent`.

## Configuration

| Field | Type | What it controls | Default |
|-------|------|-----------------|---------|
| On Import | ImportedAssetEvent (UnityEvent) | Listeners to call with the imported asset as the argument. | No listeners |

## How it works

`CanRunOn` returns false when the serialized event is null or has zero persistent listeners
(`_onImport != null && _onImport.GetPersistentEventCount() > 0`). In either case the action is
silently skipped with no log entry.

`Execute` calls `_onImport?.Invoke(importedAsset)`. The imported asset is passed as the `Object`
argument to every listener.

Listeners are configured in the Inspector on the action asset, the same way you configure
a Button `onClick` in a UI. Drag a target object into the listener slot, pick a method, and save.

## Idempotency

The action does not track state. It fires the event on every import. Whether the result is
idempotent depends on what the listeners do.

## Edge cases

**Listener target must be an asset, not a scene object:** this action is itself a ScriptableObject
asset, so persistent listener references are resolved against other assets (a ScriptableObject, a
prefab's components, etc.). A listener pointing at a GameObject in a scene serializes to `null` and
silently does nothing when invoked — there is no error or warning.

**Listener calls a method that triggers another import:** if the listener method imports or
modifies assets, the postprocessor may run again. Make sure listener methods do not create
a feedback loop.

**Runtime listeners are not supported:** `UnityEvent` persistent listeners are resolved at edit
time. Listeners added at runtime via `AddListener` are not persistent and are not present during
the import pipeline.

## Example

An art lead wants a Slack notification (via a custom Editor tool) every time a new character
texture arrives. They create an `EmitUnityEventAction`, wire the `On Import` event to the
notification method on an Editor ScriptableObject, and add the action to the character texture rule.
No custom action code needed.

## See also

[Writing Your Own Action](../api/extension-points.md) for cases where the Inspector alone is not enough.
