namespace Kodlon.AssetRouter.Data
{
    /// <summary>Determines how the pattern field on a rule is interpreted.</summary>
    public enum PatternMode
    {
        /// <summary>
        /// Glob-style pattern. Supports <c>*</c> (any chars except /), <c>?</c> (one char
        /// except /),
        /// and <c>**</c> (any path segment including /). Case-insensitive.
        /// </summary>
        Glob = 0,

        /// <summary>
        /// Full .NET regular expression. Case-insensitive, 50 ms timeout per match to
        /// guard against ReDoS.
        /// </summary>
        Regex = 1
    }
}