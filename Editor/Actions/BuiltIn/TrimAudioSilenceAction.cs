using System;
using System.Collections.Generic;
using System.IO;
using Kodlon.AssetRouter.Logic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kodlon.AssetRouter.Actions
{
    /// <summary>
    /// Trims leading and trailing silence from WAV files. Works only on 16-bit PCM WAV (RIFF little-endian).
    /// Replaces the file atomically via <c>File.Replace</c> and triggers a re-import.
    /// </summary>
    /// <remarks>
    /// Files in RIFX (big-endian), non-PCM, or non-16-bit format are left untouched.
    /// A re-entry guard prevents the re-import from triggering the action a second time.
    /// </remarks>
    [CreateAssetMenu(menuName = "Asset Router/Actions/Trim Audio Silence", fileName = "TrimAudioSilenceAction")]
    public sealed class TrimAudioSilenceAction : AssetImportActionAsset
    {
        /// <summary>
        /// Fraction of <c>short.MaxValue</c> below which a sample is considered silent. Range: 0 to 0.1.
        /// A value of 0.01 means samples with amplitude below 327 (out of 32767) are treated as silence.
        /// </summary>
        [Range(0f, 0.1f), Tooltip("Samples with absolute amplitude below this fraction of max are considered silent.")]
        public float silenceThreshold = 0.01f;

        private static readonly HashSet<string> _processing = new(StringComparer.OrdinalIgnoreCase);

        public override bool CanRunOn(Object importedAsset, AssetImportContext ctx)
            => string.Equals(Path.GetExtension(ctx.AssetPath), ".wav", StringComparison.OrdinalIgnoreCase);

        public override void Execute(Object importedAsset, AssetImportContext ctx)
        {
            if (!_processing.Add(ctx.AssetPath))
                return;

            try
            {
                var absolutePath = PathUtility.ToAbsolute(ctx.AssetPath);

                if (!File.Exists(absolutePath))
                    return;

                var bytes = File.ReadAllBytes(absolutePath);

                if (!TryTrim(bytes, silenceThreshold, out var trimmed))
                    return;

                var tmp = absolutePath + ".tmp";
                File.WriteAllBytes(tmp, trimmed);

                if (File.Exists(absolutePath))
                    File.Replace(tmp, absolutePath, null);
                else
                    File.Move(tmp, absolutePath);

                AssetDatabase.ImportAsset(ctx.AssetPath, ImportAssetOptions.ForceUpdate);
                ctx.Logger.Log($"[AssetRouter] TrimAudioSilence → {ctx.AssetPath}");
            }
            finally
            {
                _processing.Remove(ctx.AssetPath);
            }
        }

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

            var audioFormat   = BitConverter.ToInt16(wav, fmtDataOffset);
            var channels      = BitConverter.ToInt16(wav, fmtDataOffset + 2);
            var bitsPerSample = BitConverter.ToInt16(wav, fmtDataOffset + 14);

            if (audioFormat != 1 || bitsPerSample != 16)
                return false;

            if (!FindChunk(wav, 12, "data", out var dataDataOffset, out var dataSize))
                return false;

            if (dataSize < 0 || (long)dataDataOffset + dataSize > wav.Length)
                return false;

            if (channels <= 0)
                return false;

            var frameSize  = channels * 2;
            var frameCount = dataSize / frameSize;

            if (frameCount == 0)
                return false;

            var thresholdSample = (int)(threshold * short.MaxValue);

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

            var dataChunkHeaderOffset = dataDataOffset - 8;
            var newPad = (newDataSize & 1) != 0 ? 1 : 0;

            // Chunks after "data" (smpl loop points, cue, LIST/INFO, ...) must survive the trim —
            // account for the original chunk's word-alignment pad byte when locating them.
            var originalPad     = (dataSize & 1) != 0 ? 1 : 0;
            var trailingOffset  = dataDataOffset + dataSize + originalPad;
            var trailingLength  = wav.Length - trailingOffset;

            var outputSize = dataChunkHeaderOffset + 8 + newDataSize + newPad + trailingLength;

            trimmed = new byte[outputSize];

            Array.Copy(wav, 0, trimmed, 0, dataChunkHeaderOffset);

            trimmed[dataChunkHeaderOffset]     = (byte)'d';
            trimmed[dataChunkHeaderOffset + 1] = (byte)'a';
            trimmed[dataChunkHeaderOffset + 2] = (byte)'t';
            trimmed[dataChunkHeaderOffset + 3] = (byte)'a';
            var newDataSizeBytes = BitConverter.GetBytes(newDataSize);
            Array.Copy(newDataSizeBytes, 0, trimmed, dataChunkHeaderOffset + 4, 4);

            Array.Copy(wav, dataDataOffset + startFrame * frameSize, trimmed, dataChunkHeaderOffset + 8, newDataSize);

            if (trailingLength > 0)
                Array.Copy(wav, trailingOffset, trimmed, dataChunkHeaderOffset + 8 + newDataSize + newPad, trailingLength);

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

                if (size < 0 || pos + 8 + size > data.Length)
                    return false;

                pos += 8 + size;
                if ((size & 1) != 0) pos++; // RIFF chunks are word-aligned
            }

            return false;
        }

        private static bool HasNonSilentSample(byte[] data, int frameOffset, int channels, int threshold)
        {
            for (var ch = 0; ch < channels; ch++)
            {
                var sample = BitConverter.ToInt16(data, frameOffset + ch * 2);
                if (Math.Abs((int)sample) > threshold)
                    return true;
            }

            return false;
        }
    }
}
