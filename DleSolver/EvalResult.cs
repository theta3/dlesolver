namespace DleSolver
{
    internal class EvalResult
    {
        /// <summary>
        /// Whether this result set is to be subtracted from the original sets
        /// </summary>
        public bool IsSubtract { get; set; }
        public HashSet<string> CommonWordList { get; set; } = new HashSet<string>();
        public HashSet<string> UncommonWordList { get; set; } = new HashSet<string>();
    }
}
