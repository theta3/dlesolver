namespace DleSolver
{
    internal class CharKey
    {
        public char Char { get; set; }
        public int Index { get; set; }
        public override bool Equals(object? obj)
        {
            var other = obj as CharKey;
            if (other == null) return false;
            return Char == other.Char && Index == other.Index;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 23 + Char.GetHashCode();
            hash = hash * 23 + Index.GetHashCode();
            return hash;
        }
    }
}
