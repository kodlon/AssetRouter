using System;
using NUnit.Framework;
using Kodlon.AssetRouter.Actions;

namespace Kodlon.AssetRouter.Tests
{
    public class TrimAudioSilenceActionTests
    {
        // ── WAV builder ───────────────────────────────────────────────────────────

        private static byte[] BuildWav(short[] samples, int channels = 1)
        {
            var dataSize = samples.Length * 2;
            var bytes    = new byte[44 + dataSize];

            WriteStr(bytes, 0,  "RIFF");
            WriteI32(bytes, 4,  36 + dataSize);
            WriteStr(bytes, 8,  "WAVE");
            WriteStr(bytes, 12, "fmt ");
            WriteI32(bytes, 16, 16);
            WriteI16(bytes, 20, 1);                           // PCM
            WriteI16(bytes, 22, channels);
            WriteI32(bytes, 24, 44100);
            WriteI32(bytes, 28, 44100 * channels * 2);
            WriteI16(bytes, 32, channels * 2);                // block align
            WriteI16(bytes, 34, 16);                          // bits per sample
            WriteStr(bytes, 36, "data");
            WriteI32(bytes, 40, dataSize);

            for (var i = 0; i < samples.Length; i++)
            {
                var b = BitConverter.GetBytes(samples[i]);
                bytes[44 + i * 2]     = b[0];
                bytes[44 + i * 2 + 1] = b[1];
            }

            return bytes;
        }

        private static short[] ReadSamples(byte[] wav)
        {
            var dataSize = BitConverter.ToInt32(wav, 40);
            var result   = new short[dataSize / 2];

            for (var i = 0; i < result.Length; i++)
                result[i] = BitConverter.ToInt16(wav, 44 + i * 2);

            return result;
        }

        private static void WriteStr(byte[] buf, int at, string s)
        {
            for (var i = 0; i < s.Length; i++) buf[at + i] = (byte)s[i];
        }

        private static void WriteI32(byte[] buf, int at, int v) =>
            Array.Copy(BitConverter.GetBytes(v), 0, buf, at, 4);

        private static void WriteI16(byte[] buf, int at, int v) =>
            Array.Copy(BitConverter.GetBytes((short)v), 0, buf, at, 2);

        // ── Trimming behaviour ───────────────────────────────────────────────────

        [Test]
        public void TryTrim_LeadingSilence_IsTrimmed()
        {
            var samples = new short[15];
            for (var i = 5; i < 15; i++) samples[i] = 10000;

            var trimmed = (byte[])null;
            Assert.IsTrue(TrimAudioSilenceAction.TryTrim(BuildWav(samples), 0.01f, out trimmed));
            Assert.AreEqual(44 + 10 * 2, trimmed.Length);
        }

        [Test]
        public void TryTrim_TrailingSilence_IsTrimmed()
        {
            var samples = new short[15];
            for (var i = 0; i < 10; i++) samples[i] = 10000;

            Assert.IsTrue(TrimAudioSilenceAction.TryTrim(BuildWav(samples), 0.01f, out var trimmed));
            Assert.AreEqual(44 + 10 * 2, trimmed.Length);
        }

        [Test]
        public void TryTrim_SilenceOnBothEnds_IsTrimmed()
        {
            var samples = new short[16];
            for (var i = 3; i < 13; i++) samples[i] = 10000;

            Assert.IsTrue(TrimAudioSilenceAction.TryTrim(BuildWav(samples), 0.01f, out var trimmed));
            Assert.AreEqual(44 + 10 * 2, trimmed.Length);
        }

        [Test]
        public void TryTrim_SampleContent_MatchesOriginalSignal()
        {
            var samples = new short[] { 0, 0, 1000, 2000, 3000, 0, 0 };

            TrimAudioSilenceAction.TryTrim(BuildWav(samples), 0.01f, out var trimmed);

            var result = ReadSamples(trimmed);
            Assert.AreEqual(new short[] { 1000, 2000, 3000 }, result);
        }

        [Test]
        public void TryTrim_NoSilence_ReturnsFalse()
        {
            var samples = new short[10];
            for (var i = 0; i < 10; i++) samples[i] = 10000;

            Assert.IsFalse(TrimAudioSilenceAction.TryTrim(BuildWav(samples), 0.01f, out _));
        }

        [Test]
        public void TryTrim_AllSilence_ReturnsFalse()
        {
            Assert.IsFalse(TrimAudioSilenceAction.TryTrim(BuildWav(new short[10]), 0.01f, out _));
        }

        // ── short.MinValue overflow guard ────────────────────────────────────────

        [Test]
        public void TryTrim_ShortMinValue_IsDetectedAsNonSilent()
        {
            // The old code: (short)(threshold * short.MaxValue) then Math.Abs(sample) for
            // short.MinValue = -32768 would overflow back to -32768, failing the > threshold check.
            // The fix casts to int first: Math.Abs((int)(-32768)) = 32768 > threshold.
            var samples = new short[] { 0, 0, 0, short.MinValue, 0, 0, 0 };

            Assert.IsTrue(TrimAudioSilenceAction.TryTrim(BuildWav(samples), 0.01f, out var trimmed),
                "short.MinValue must be detected as non-silent after the overflow fix");

            var result = ReadSamples(trimmed);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(short.MinValue, result[0]);
        }

        // ── Malformed input ──────────────────────────────────────────────────────

        [Test]
        public void TryTrim_TooShort_ReturnsFalse()
        {
            Assert.IsFalse(TrimAudioSilenceAction.TryTrim(new byte[43], 0.01f, out _));
        }

        [Test]
        public void TryTrim_CorruptRiffFourCC_ReturnsFalse()
        {
            var wav = BuildWav(new short[] { 1000 });
            wav[0] = (byte)'X'; // "RIFF" → "XIFF"

            Assert.IsFalse(TrimAudioSilenceAction.TryTrim(wav, 0.01f, out _));
        }

        [Test]
        public void TryTrim_BigEndianRifx_ReturnsFalse()
        {
            // RIFX is the big-endian variant; the parser only handles little-endian RIFF.
            var wav = BuildWav(new short[] { 1000 });
            wav[3] = (byte)'X'; // "RIFF" → "RIFX"

            Assert.IsFalse(TrimAudioSilenceAction.TryTrim(wav, 0.01f, out _));
        }

        [Test]
        public void TryTrim_NonPcmAudioFormat_ReturnsFalse()
        {
            var wav = BuildWav(new short[] { 1000 });
            WriteI16(wav, 20, 3); // IEEE float, not PCM

            Assert.IsFalse(TrimAudioSilenceAction.TryTrim(wav, 0.01f, out _));
        }

        [Test]
        public void TryTrim_Non16BitDepth_ReturnsFalse()
        {
            var wav = BuildWav(new short[] { 1000 });
            WriteI16(wav, 34, 8); // 8-bit, not 16-bit

            Assert.IsFalse(TrimAudioSilenceAction.TryTrim(wav, 0.01f, out _));
        }

        // ── Output WAV integrity ─────────────────────────────────────────────────

        [Test]
        public void TryTrim_Output_HasValidRiffHeader()
        {
            var samples = new short[] { 0, 10000, 0 };
            TrimAudioSilenceAction.TryTrim(BuildWav(samples), 0.01f, out var trimmed);

            Assert.AreEqual((byte)'R', trimmed[0]);
            Assert.AreEqual((byte)'I', trimmed[1]);
            Assert.AreEqual((byte)'F', trimmed[2]);
            Assert.AreEqual((byte)'F', trimmed[3]);
            Assert.AreEqual((byte)'W', trimmed[8]);
            Assert.AreEqual((byte)'A', trimmed[9]);
            Assert.AreEqual((byte)'V', trimmed[10]);
            Assert.AreEqual((byte)'E', trimmed[11]);
        }

        [Test]
        public void TryTrim_Output_RiffSizeFieldConsistentWithLength()
        {
            var samples = new short[] { 0, 10000, 0 };
            TrimAudioSilenceAction.TryTrim(BuildWav(samples), 0.01f, out var trimmed);

            Assert.AreEqual(trimmed.Length - 8, BitConverter.ToInt32(trimmed, 4));
        }

        [Test]
        public void TryTrim_Output_DataSizeFieldConsistentWithSampleCount()
        {
            var samples = new short[] { 0, 0, 10000, 20000, 0 };
            TrimAudioSilenceAction.TryTrim(BuildWav(samples), 0.01f, out var trimmed);

            var reportedDataSize = BitConverter.ToInt32(trimmed, 40);
            Assert.AreEqual(trimmed.Length - 44, reportedDataSize);
        }
    }
}
