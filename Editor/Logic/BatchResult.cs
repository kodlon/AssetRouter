namespace Kodlon.AssetRouter.Logic
{
    internal readonly struct BatchResult
    {
        public readonly int Moved;
        public readonly int Reimported;
        public readonly int Skipped;
        public readonly int Errored;

        public BatchResult(int moved, int reimported, int skipped, int errored)
        {
            Moved      = moved;
            Reimported = reimported;
            Skipped    = skipped;
            Errored    = errored;
        }

        public override string ToString() =>
            $"Moved: {Moved}, Reimported: {Reimported}, Skipped: {Skipped}, Errored: {Errored}";
    }

    internal readonly struct UndoResult
    {
        public readonly int Reverted;
        public readonly int Failed;

        public UndoResult(int reverted, int failed)
        {
            Reverted = reverted;
            Failed   = failed;
        }
    }
}
