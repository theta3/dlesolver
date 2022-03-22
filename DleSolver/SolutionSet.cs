namespace DleSolver
{
    internal class SolutionSet
    {
        public int PuzzleIndex { get; set; }
        public HashSet<string> CommonWordList { get; set; } = new HashSet<string>();
        public HashSet<string> UncommonWordList { get; set; } = new HashSet<string>();
        public Dictionary<CharKey, HashSet<Word>> CommonWordMap { get; private set; } = new Dictionary<CharKey, HashSet<Word>>();
        public Dictionary<CharKey, HashSet<Word>> UncommonWordMap { get; private set; } = new Dictionary<CharKey, HashSet<Word>>();
        /// <summary>
        /// Positions that are wrong chars in the latest guess
        /// </summary>
       // public HashSet<int> WrongCharIndices { get; private set; } = new HashSet<int>();

        public int Count()
        {
            return CommonWordList.Count + UncommonWordList.Count;
        }

        public string FirstWord
        {
            get
            {
                if (CommonWordList.Count > 0)
                {
                    return CommonWordList.First();
                }

                if (UncommonWordList.Count > 0)
                {
                    return UncommonWordList.First();
                }

                return string.Empty;
            }
        }

        public SolutionSet Copy()
        {
            var result = new SolutionSet();
            result.PuzzleIndex = PuzzleIndex;
            result.CommonWordList = new HashSet<string>(CommonWordList);
            result.UncommonWordList = new HashSet<string>(UncommonWordList);
            foreach (var kv in CommonWordMap)
            {
                result.CommonWordMap.Add(kv.Key, new HashSet<Word>(kv.Value));
            }
            foreach (var kv in UncommonWordMap)
            {
                result.UncommonWordMap.Add(kv.Key, new HashSet<Word>(kv.Value));
            }
            return result;
        }

        public void Clear()
        {
            CommonWordList.Clear();
            UncommonWordList.Clear();
            CommonWordMap.Clear();
            UncommonWordMap.Clear();
        }

        public void RecalculateWordMaps()
        {
            CommonWordMap.Clear();
            UncommonWordMap.Clear();
            foreach (var word in CommonWordList)
            {
                AddWordToWordMap(word, CommonWordMap);
            }
            foreach (var word in UncommonWordList)
            {
                AddWordToWordMap(word, UncommonWordMap);
            }
        }

        /*public void CaptureWrongCharIndices(string guessResult)
        {
            WrongCharIndices.Clear();
            int idx = 0;
            foreach (char c in guessResult)
            {
                if (c == Program.WRONG)
                {
                    WrongCharIndices.Add(idx);
                }
                ++idx;
            }
        }*/

        public void SubtractWord(string line)
        {
            if (CommonWordList.Remove(line))
            {
                int idx = 0;
                foreach (char c in line)
                {
                    var key = new CharKey()
                    {
                        Char = c,
                        Index = idx
                    };
                    CommonWordMap[key].Remove(ConstructWordObject(key, line));
                    ++idx;
                }
            }
            else if (UncommonWordList.Remove(line))
            {
                int idx = 0;
                foreach (char c in line)
                {
                    var key = new CharKey()
                    {
                        Char = c,
                        Index = idx
                    };
                    UncommonWordMap[key].Remove(ConstructWordObject(key, line));
                    ++idx;
                }
            }
        }

        private static void AddWordToWordMap(string line, Dictionary<CharKey, HashSet<Word>> map)
        {
            int idx = 0;
            foreach (char c in line)
            {
                var key = new CharKey()
                {
                    Char = c,
                    Index = idx
                };
                if (!map.TryGetValue(key, out var value))
                {
                    map.Add(key, value = new HashSet<Word>());
                }
                map[key].Add(ConstructWordObject(key, line));
                ++idx;
            }
        }

        private static Word ConstructWordObject(CharKey sourceKey, string line)
        {
            var word = new Word()
            {
                WordStr = line
            };
            int idx2 = 0;
            foreach (char exclc in line)
            {
                if (idx2 != sourceKey.Index)
                {
                    word.CharKeysExcludeSourceKey.Add(new CharKey()
                    {
                        Char = exclc,
                        Index = idx2
                    });
                }
                ++idx2;
            }
            return word;
        }
    }
}
