namespace DleSolver
{
    internal class GuessResult
    {
        public int? PuzzleIndex { get; set; } = null;
        public string Guess { get; set; } = string.Empty;
        public double PruneRate { get; set; }
        public double PruneCount { get; set; }

        public override string ToString()
        {
            return PuzzleIndex == null ?
                string.Format("Final selectedGuessMaxAvg {0}; prune rate: {1} ({2})", Guess, PruneRate, PruneCount) :
                string.Format("[3] Final selectedGuessMaxAvg {0}; prune rate: {1} ({2})", Guess, PruneRate, PruneCount, PuzzleIndex);
        }
    }
}
