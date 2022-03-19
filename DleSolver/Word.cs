namespace DleSolver
{
    internal class Word
    {
        public string WordStr { get; set; } = string.Empty;
        public HashSet<CharKey> CharKeysExcludeSourceKey { get; set; } = new HashSet<CharKey>();

        public override bool Equals(object? obj)
        {
            var other = obj as Word;
            if (other == null) return false;
            return WordStr.Equals(other.WordStr) && CharKeysExcludeSourceKey.SetEquals(other.CharKeysExcludeSourceKey);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 23 + WordStr.GetHashCode();
            hash = hash * 23 + CharKeysExcludeSourceKey.GetHashCode();
            return hash;
        }
    }
}
