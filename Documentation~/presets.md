# Presets

A Unity **Preset** is an asset (a `.preset` file) that stores a snapshot of import settings:
texture type, compression, sprite mode, audio load type, anything the importer's Inspector shows.
Asset Router applies the preset of the matched rule to every imported file in `OnPreprocessAsset`,
before Unity finishes the import. The preset type must match the importer type: a
`TextureImporter` preset does nothing on a WAV file, and a warning is logged.

## Using the bundled presets

The package ships with 10 presets covering common texture, audio, and model setups. They live in
`Packages/com.kodlon.assetrouter/Presets/`. Assign one via the **Import Preset** field in the rule details
and click **Save / Apply**. The full table is in
[Bundled presets](DOCUMENTATION_EN.md#bundled-presets).

## Making your own

1. Select any asset of the target type (for example a texture) in the Project window.
2. In the Inspector, set the import settings the way you want every matched file to import.
3. Click the preset icon in the top-right corner of the Inspector (the small slider icon) and
   choose **Save current to...** in the window that opens.
4. Save the `.preset` file anywhere inside `Assets/`.
5. Assign it to a rule via the **Import Preset** field and click **Save / Apply**.

From now on every file matched by that rule imports with those settings. To refresh files that
were already imported, use **Force Re-import In-Place** on the Dry Run tab.

## Further reading

Presets are a stock Unity feature and go deeper than routing needs: the Preset Manager, default
presets per importer type, applying presets by hand. The official manual explains all of it:
[Unity Manual: Presets](https://docs.unity3d.com/Manual/Presets.html).
