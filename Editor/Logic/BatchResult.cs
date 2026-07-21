namespace Kodlon.AssetRouter.Logic
{
    internal readonly struct BatchResult
    {
        public readonly int Errored;
        public readonly int Moved;
        public readonly int Reimported;
        public readonly int Skipped;

        public BatchResult(int moved, int reimported, int skipped, int errored)
        {
            Moved = moved;
            Reimported = reimported;
            Skipped = skipped;
            Errored = errored;
        }

        public override string ToString() => $"Moved: {Moved}, Reimported: {Reimported}, Skipped: {Skipped}, Errored: {Errored}";
    }

    internal readonly struct UndoResult
    {
        public readonly int Failed;
        public readonly int Reverted;

        public UndoResult(int reverted, int failed)
        {
            Reverted = reverted;
            Failed = failed;
        }
    }
}