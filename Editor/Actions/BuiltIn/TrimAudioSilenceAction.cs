using System;
using System.IO;
using Kodlon.AssetRouter.Logic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kodlon.AssetRouter.Actions
{
    /// <summary>
    /// Trims leading and trailing silence from WAV files (16-bit PCM only).
    /// Writes the trimmed file back to disk atomically and reimports.
    /// Idempotent — returns without writing if no silence is found.
    /// </summary>
    [CreateAssetMenu(menuName = "Asset Router/Actions/Trim Audio Silence", fileName = "TrimAudioSilenceAction")]
    public sealed class TrimAudioSilenceAction : AssetImportActionAsset
    {
        [Range(0f, 0.1f), Tooltip("Samples with absolute amplitude below this fraction of max are considered silent.")]
        public float silenceThreshold = 0.01f;

        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx)
            => string.Equals(Path.GetExtension(ctx.AssetPath), ".wav", StringComparison.OrdinalIgnoreCase);

        public override void Execute(Object importedAsset, AssetImportContext ctx)
        {
            var absolutePath = PathUtility.ToAbsolute(ctx.AssetPath);

            if (!File.Exists(absolutePath))
                return;

            var bytes = File.ReadAllBytes(absolutePath);

            if (!TryTrim(bytes, silenceThreshold, out var trimmed))
                return;

            var tmp = absolutePath + ".tmp";
            File.WriteAllBytes(tmp, trimmed);
            File.Delete(absolutePath);
            File.Move(tmp, absolutePath);

            AssetDatabase.ImportAsset(ctx.AssetPath, ImportAssetOptions.ForceUpdate);
            ctx.Logger.Log($"[AssetRouter] TrimAudioSilence → {ctx.AssetPath}");
        }

        // ── WAV / RIFF parsing (16-bit PCM) ──────────────────────────────────────

        internal static bool TryTrim(byte[] wav, float threshold, out byte[] trimmed)
        {
            trimmed = null;

            if (wav.Length < 44
                || wav[0] != 'R' || wav[1] != 'I' || wav[2] != 'F' || wav[3] != 'F'
                || wav[8] != 'W' || wav[9] != 'A' || wav[10] != 'V' || wav[11] != 'E')
                return false;

            if (!FindChunk(wav, 12, "fmt ", out var fmtDataOffset, out _))
                return false;

            if (fmtDataOffset + 16 > wav.Length)
                return false;

            var audioFormat  = BitConverter.ToInt16(wav, fmtDataOffset);
            var channels     = BitConverter.ToInt16(wav, fmtDataOffset + 2);
            var bitsPerSample = BitConverter.ToInt16(wav, fmtDataOffset + 14);

            if (audioFormat != 1 || bitsPerSample != 16)
                return false;

            if (!FindChunk(wav, 12, "data", out var dataDataOffset, out var dataSize))
                return false;

            var frameSize  = channels * 2;
            var frameCount = dataSize / frameSize;

            if (frameCount == 0)
                return false;

            var thresholdSample = (short)(threshold * short.MaxValue);

            var startFrame = 0;
            var endFrame   = frameCount - 1;

            for (var i = 0; i < frameCount; i++)
            {
                if (HasNonSilentSample(wav, dataDataOffset + i * frameSize, channels, thresholdSample))
                {
                    startFrame = i;
                    break;
                }
            }

            for (var i = frameCount - 1; i >= startFrame; i--)
            {
                if (HasNonSilentSample(wav, dataDataOffset + i * frameSize, channels, thresholdSample))
                {
                    endFrame = i;
                    break;
                }
            }

            if (startFrame == 0 && endFrame == frameCount - 1)
                return false;

            var newFrameCount = endFrame - startFrame + 1;
            var newDataSize   = newFrameCount * frameSize;

            // dataChunkHeaderOffset is where the "data" ID starts in the original file
            var dataChunkHeaderOffset = dataDataOffset - 8;
            var outputSize = dataChunkHeaderOffset + 8 + newDataSize;

            trimmed = new byte[outputSize];

            // Copy everything before the "data" chunk (RIFF header + fmt + other chunks)
            Array.Copy(wav, 0, trimmed, 0, dataChunkHeaderOffset);

            // Write new "data" chunk header
            trimmed[dataChunkHeaderOffset]     = (byte)'d';
            trimmed[dataChunkHeaderOffset + 1] = (byte)'a';
            trimmed[dataChunkHeaderOffset + 2] = (byte)'t';
            trimmed[dataChunkHeaderOffset + 3] = (byte)'a';
            var newDataSizeBytes = BitConverter.GetBytes(newDataSize);
            Array.Copy(newDataSizeBytes, 0, trimmed, dataChunkHeaderOffset + 4, 4);

            // Copy trimmed samples
            Array.Copy(wav, dataDataOffset + startFrame * frameSize, trimmed, dataChunkHeaderOffset + 8, newDataSize);

            // Update RIFF total size (outputSize − 8)
            var riffSizeBytes = BitConverter.GetBytes(outputSize - 8);
            Array.Copy(riffSizeBytes, 0, trimmed, 4, 4);

            return true;
        }

        private static bool FindChunk(byte[] data, int searchFrom, string id, out int chunkDataOffset, out int chunkSize)
        {
            chunkDataOffset = 0;
            chunkSize = 0;
            var pos = searchFrom;

            while (pos + 8 <= data.Length)
            {
                if (data[pos]     == id[0] && data[pos + 1] == id[1]
                    && data[pos + 2] == id[2] && data[pos + 3] == id[3])
                {
                    chunkSize       = BitConverter.ToInt32(data, pos + 4);
                    chunkDataOffset = pos + 8;
                    return true;
                }

                var size = BitConverter.ToInt32(data, pos + 4);
                pos += 8 + size;
                if ((size & 1) != 0) pos++; // RIFF chunks are word-aligned
            }

            return false;
        }

        private static bool HasNonSilentSample(byte[] data, int frameOffset, int channels, short threshold)
        {
            for (var ch = 0; ch < channels; ch++)
            {
                var sample = BitConverter.ToInt16(data, frameOffset + ch * 2);
                if (Math.Abs(sample) > threshold)
                    return true;
            }

            return false;
        }
    }
}
