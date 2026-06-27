# TrimAudioSilenceAction

Trims leading and trailing silence from a WAV file. Works only on 16-bit PCM WAV (RIFF little-endian).
Replaces the file on disk atomically and triggers a re-import.

**Applies to:** WAV files only. The extension check is case-insensitive (`.WAV` is accepted).

**Tier:** A — modifying the asset file directly and triggering a re-import.

## Configuration

| Field | Type | What it controls | Default |
|-------|------|-----------------|---------|
| Silence Threshold | float (0..0.1) | Fraction of `short.MaxValue` (32767) below which a sample is considered silent. A value of 0.01 means samples with absolute amplitude below 327 are treated as silence. | 0.01 |

## How it works

`CanRunOn` checks the file extension. Only `.wav` files pass.

`Execute` reads the raw file bytes and parses the RIFF header to locate the `fmt ` and `data` chunks.
It reads the format (must be PCM = 1), channel count, and bits per sample (must be 16).
Then it scans frames from the start to find the first non-silent frame, and from the end to find
the last non-silent frame. If there is nothing to trim (no silence at start or end), the file
is left untouched. If silence is found, it builds a new byte array with only the audio in between,
writes it to a `.tmp` file, and uses `File.Replace` to atomically swap the original.
Finally, it calls `AssetDatabase.ImportAsset(path, ForceUpdate)` to update Unity's cache.

A static `HashSet` tracks paths currently being processed. When the re-import fires and hits this
action again, the path is already in the set, so the action exits immediately. The path is removed
in the `finally` block.

## Idempotency

Yes. If no silence is present on the second run, `TryTrim` returns false and the file is not touched.

## Requirements

The WAV file must be:
- RIFF format (little-endian). RIFX (big-endian) is detected and rejected without error.
- Audio format PCM (format tag = 1). MP3-in-WAV, ADPCM, and other formats are rejected.
- 16-bit samples. 8-bit, 24-bit, and 32-bit files are rejected.

Files that do not meet these requirements are skipped silently (no warning, no file change).

## Edge cases

**All silence:** when all samples are below the threshold, the action returns false and leaves the file untouched.

**Stereo:** the action checks all channels per frame. A frame is trimmed only when all channels are silent.

**Very short files:** if the data chunk has zero frames, the action returns false.

**Undo:** History tab undo moves the file back to its pre-move path. It does not restore the original WAV content.
After undo, the file contains the trimmed audio at the original location.

## Example

A rule matches `SFX_*` and routes to `Assets/Audio/SFX/`. Adding `TrimAudioSilenceAction` with
threshold 0.01 automatically strips the 200 ms of dead air that comes from the DAW export.
Sound designers export and forget; the plugin handles cleanup on every import.
