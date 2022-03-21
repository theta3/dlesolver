// See https://aka.ms/new-console-template for more information
using DleSolver;
using System.Collections.Concurrent;
using System.Text;

public static class Program
{
    private const int NUM_OF_CHARS = 5;
    private const char WRONG = '_';
    private const char WRONG_SPOT = '?';
    private const char RIGHT_SPOT = '+';
    private const char SEPARATOR = ':';
    private const int MAX_CONCURRENCY = 100;
    private static HashSet<string>[] commonWordLists;
    private static HashSet<string>[] uncommonWordLists;
    private static Dictionary<CharKey, HashSet<Word>>[] commonWordMaps;
    private static Dictionary<CharKey, HashSet<Word>>[] uncommonWordMaps;
    private static int[] indices;
    private static ConcurrentBag<string> guessResults = new ConcurrentBag<string>();

    public static void Main(string[] args)
    {
        Console.WriteLine("Welcome to -Dle Solver v0.7");
        Console.Write("Number of puzzles:");
        int pnumber = int.Parse(Console.ReadLine() ?? "1");
        commonWordLists = new HashSet<string>[pnumber];
        uncommonWordLists = new HashSet<string>[pnumber];
        commonWordMaps = new Dictionary<CharKey, HashSet<Word>>[pnumber];
        uncommonWordMaps = new Dictionary<CharKey, HashSet<Word>>[pnumber];
        indices = new int[pnumber];
        for (int i = 0; i < pnumber; i++)
        {
            commonWordLists[i] = new HashSet<string>();
            uncommonWordLists[i] = new HashSet<string>();
            commonWordMaps[i] = new Dictionary<CharKey, HashSet<Word>>();
            uncommonWordMaps[i] = new Dictionary<CharKey, HashSet<Word>>();
            indices[i] = i;
        }
        int option = 0;
        do
        {
            for (int i = 0; i < pnumber; ++i)
            {
                Console.WriteLine("[{2}]: {0} common words - {1} uncommon words", commonWordLists[i].Count, uncommonWordLists[i].Count, i);
            }
            Console.WriteLine("0. Exit");
            Console.WriteLine("1. Read common word list");
            Console.WriteLine("2. Read uncommon word list");
            Console.WriteLine("3. Read excluded word list");
            Console.WriteLine("4. Enter a guess word");
            Console.WriteLine("5. Make a guess");
            Console.WriteLine("6. Clear");
            Console.WriteLine("7. Generate guess result");
            Console.WriteLine("8. Convert retention rate to prune rate");
            int.TryParse(Console.ReadLine(), out option);
            switch(option)
            {
                case 1:
                    ReadWords(commonWordLists, commonWordMaps).Wait();
                    break;
                case 2:
                    ReadWords(uncommonWordLists, uncommonWordMaps).Wait();
                    break;
                case 3:
                    SubtractWords().Wait();
                    break;
                case 4:
                    AddGuessWord();
                    break;
                case 5:
                    Guesses().Wait();
                    break;
                case 6:
                    for (int i = 0; i < pnumber; i++)
                    {
                        commonWordLists[i].Clear();
                        uncommonWordLists[i].Clear();
                        commonWordMaps[i].Clear();
                        uncommonWordMaps[i].Clear();
                    }
                    break;
                case 7:
                    GenerateMatchResult();
                    break;
                case 8:
                    ConvertRetentionToPruneRate().Wait();
                    break;
            }
        }
        while (option > 0);
        // GenerateWordList().Wait();
       // SeparateLists().Wait();
       // Solver().Wait();

        Console.WriteLine("DONE");
    }

    private static async Task ConvertRetentionToPruneRate()
    {
        Console.Write("Input file:");
        string fPath = Console.ReadLine() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(fPath))
        {
            string[] lines = await File.ReadAllLinesAsync(fPath);
            List<string> output = new List<string>();
            foreach (var line in lines)
            {
                var parts = line.Split(SEPARATOR);
                if (parts.Length == 2 && double.TryParse(parts[1], out double rate))
                {
                    output.Add(parts[0] + SEPARATOR + (1.0 - rate));
                }
            }
            string fileName = Path.GetFileNameWithoutExtension(fPath);
            await File.WriteAllLinesAsync(Path.Combine(Path.GetDirectoryName(fPath) ?? string.Empty, fileName + "-pruned.txt"), output);
        }
    }
    private static async Task Guesses()
    {
        guessResults.Clear();
        await Parallel.ForEachAsync(indices, async (idx, token) =>
            await Guess(idx));
        Console.WriteLine();
        foreach (string r in guessResults)
        {
            Console.WriteLine(r);
        }
    }

    private static async Task Guess(int listIndex)
    {
        var commonWordList = commonWordLists[listIndex];
        var uncommonWordList = uncommonWordLists[listIndex];
        int originalSize = commonWordList.Count + uncommonWordList.Count;
        double maxAvgPruneRate = 0;
        string selectedGuessMaxAvg = string.Empty;
        string[] commonWordArray = commonWordList.ToArray();
        string[] uncommonWordArray = uncommonWordList.ToArray();
        // Arrays of min prune rate. Arrays of avg prune rate
        double[] avgCommonPruneRates = new double[commonWordArray.Length];
        double[] avgUncommonPruneRates = new double[uncommonWordArray.Length];
        string pruneRatePath = string.Empty;
        if (listIndex == 0)
        {
            Console.Write("Saved prune rate file path (ENTER to skip):");
            pruneRatePath = Console.ReadLine() ?? string.Empty;
        }
        Dictionary<string, double> savedPruneRates = new Dictionary<string, double>();
        if (!string.IsNullOrWhiteSpace(pruneRatePath))
        {
            string[] lines = await File.ReadAllLinesAsync(pruneRatePath);
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    var parts = line.Split(SEPARATOR);
                    if (parts.Length == 2 && double.TryParse(parts[1], out double rate))
                    {
                        savedPruneRates[parts[0]] = rate;
                    }
                }
            }
        }

        int iterCount = commonWordArray.Length / MAX_CONCURRENCY + 1;
        for (int i = 0; i < iterCount; i++)
        {
            int startIndex = i * MAX_CONCURRENCY;
            int length = i == iterCount - 1 ? commonWordArray.Length - startIndex : MAX_CONCURRENCY;
            string[] smallerArray = new string[length];
            Array.Copy(commonWordArray, startIndex, smallerArray, 0, length);
            List<string> newLines = new List<string>();
            // Parallel.ForEach https://stackoverflow.com/questions/39713258/c-sharp-parallel-foreach-loop-finding-index
            Parallel.ForEach(smallerArray, (word, state, index) =>
                EvaluateGuessWord(listIndex, word, (int)(startIndex + index), commonWordArray, uncommonWordArray, originalSize, avgCommonPruneRates, savedPruneRates, newLines)
            );
            if (!string.IsNullOrWhiteSpace(pruneRatePath))
            {
                await File.AppendAllLinesAsync(pruneRatePath, newLines);
            }
        }
        iterCount = uncommonWordArray.Length / MAX_CONCURRENCY + 1;
        for (int i = 0; i < iterCount; i++)
        {
            int startIndex = i * MAX_CONCURRENCY;
            int length = i == iterCount - 1 ? uncommonWordArray.Length - startIndex : MAX_CONCURRENCY;
            string[] smallerArray = new string[length];
            Array.Copy(uncommonWordArray, startIndex, smallerArray, 0, length);
            List<string> newLines = new List<string>();
            // Parallel.ForEach https://stackoverflow.com/questions/39713258/c-sharp-parallel-foreach-loop-finding-index
            Parallel.ForEach(smallerArray, (word, state, index) =>
                EvaluateGuessWord(listIndex, word, (int)(startIndex + index), uncommonWordArray, commonWordArray, originalSize, avgUncommonPruneRates, savedPruneRates, newLines)
            );
            if (!string.IsNullOrWhiteSpace(pruneRatePath))
            {
                await File.AppendAllLinesAsync(pruneRatePath, newLines);
            }
        }
        // find max avg
        for (int i = 0; i < commonWordArray.Length; i++)
        {
            if (avgCommonPruneRates[i] > maxAvgPruneRate)
            {
                maxAvgPruneRate = avgCommonPruneRates[i];
                selectedGuessMaxAvg = commonWordArray[i];
            }
        }

        for (int i = 0; i < uncommonWordArray.Length; ++i)
        {
            if (avgUncommonPruneRates[i] > maxAvgPruneRate)
            {
                maxAvgPruneRate = avgUncommonPruneRates[i];
                selectedGuessMaxAvg = uncommonWordArray[i];
            }
        }
        guessResults.Add(string.Format("PUZZLE [{2}]: final selectedGuessMaxAvg {0}; prune rate: {1}", selectedGuessMaxAvg, maxAvgPruneRate, listIndex));
    }

    /*private static Dictionary<CharKey, HashSet<string>> DupDict(Dictionary<CharKey, HashSet<string>> input)
    {
        var result = new Dictionary<CharKey, HashSet<string>>();
        foreach (var kv in input)
        {
            result[kv.Key] = new HashSet<string>(kv.Value);
        }
        return result;
    }*/

    private static void GenerateMatchResult()
    {
        Console.Write("Enter guess word:");
        string guessWord = Console.ReadLine() ?? string.Empty;
        Console.Write("Enter result word:");
        string resultWord = Console.ReadLine() ?? string.Empty;
        string result = GenerateMatchResult(guessWord, resultWord);
        Console.WriteLine("Match result is:" + result);
    }

    private static string GenerateMatchResult(string guessWord, string resultWord)
    {
        StringBuilder result = new StringBuilder();
        int idx = 0;
        foreach (char c in guessWord)
        {
            if (c == resultWord[idx])
            {
                result.Append(RIGHT_SPOT);
            }
            else if (resultWord.Contains(c))
            {
                result.Append(WRONG_SPOT);
            }
            else
            {
                result.Append(WRONG);
            }

            ++idx;
        }
        return result.ToString();
    }

    private static void AddGuessWord()
    {
        Console.Write("Enter word:");
        string w = Console.ReadLine() ?? string.Empty;
        foreach (var i in indices)
        {
            Console.WriteLine("PUZZLE " + i);
            Console.Write("Enter guess result:[{0}{1}{2}](5) where {0}: char not in result; {1}: char wrong spot; {2}: char correct spot:", WRONG, WRONG_SPOT, RIGHT_SPOT);
            string r = Console.ReadLine() ?? string.Empty;
            if (r.Length == NUM_OF_CHARS)
            {
                AddGuessWord(i, w + ":" + r);
            }
        }
    }

    private static void AddGuessWord(int pzIdx, string guessWord)
    {
        var newLists = EvaluateGuessWord(pzIdx, guessWord);
        if (newLists.IsSubtract)
        {
            foreach (var word in newLists.CommonWordList)
            {
                commonWordLists[pzIdx].Remove(word);
            }

            foreach (var word in newLists.UncommonWordList)
            {
                uncommonWordLists[pzIdx].Remove(word);
            }
        }
        else
        {
            commonWordLists[pzIdx] = newLists.CommonWordList;
            uncommonWordLists[pzIdx] = newLists.UncommonWordList;
        }
        RecalculateWordMaps(pzIdx);
    }

    private static void EvaluateGuessWord(int pzIdx, string guessWord,
        int index, string[] myWordArray, string[] otherWordArray, int originalSize, double[] avgPruneRates, Dictionary<string, double> savedPruneRates, List<string> newLines)
    {
        if (savedPruneRates.TryGetValue(guessWord, out double val))
        {
            avgPruneRates[index] = val;
            Console.Write("[{2}]:{0}:{1}/ ", guessWord, val, pzIdx);
            return;
        }
        double sumPruneRate = 0;
        for (int i = 0; i < myWordArray.Length; i++)
        {
            if (index != i)
            {
                string resultWord = myWordArray[i];
                var pruneRate = EvaluatePruneRate(pzIdx, guessWord, resultWord, originalSize);
                sumPruneRate += pruneRate;
            }
        }

        foreach (string resultWord in otherWordArray)
        {
            var pruneRate = EvaluatePruneRate(pzIdx, guessWord, resultWord, originalSize);
            sumPruneRate += pruneRate;
        }
        double avgPruneRate = sumPruneRate / (originalSize - 1);
        avgPruneRates[index] = avgPruneRate;
        savedPruneRates[guessWord] = avgPruneRate;
        newLines.Add(guessWord + SEPARATOR + avgPruneRate);
        Console.Write("[{2}]:{0}:{1}; ", guessWord, avgPruneRate, pzIdx);
    }

    private static double EvaluatePruneRate(int pzIdx, string guessWord, string resultWord, int originalSize)
    {
        string guessResult = GenerateMatchResult(guessWord, resultWord);
        var newLists = EvaluateGuessWord(pzIdx, guessWord + SEPARATOR + guessResult);
        return newLists.IsSubtract ?
            ((double)(newLists.CommonWordList.Count + newLists.UncommonWordList.Count))/originalSize :
            1.0 - ((double)(newLists.CommonWordList.Count + newLists.UncommonWordList.Count)) / originalSize;
    }
    private static EvalResult EvaluateGuessWord(int pzIdx, string guessWord)
    {
        string[] guessParts = guessWord.Split(':');
        int idx = 0;
        List<IEnumerable<string>> intersectCommonSets = new List<IEnumerable<string>>();
        List<IEnumerable<string>> intersectUncommonSets = new List<IEnumerable<string>>();
        HashSet<CharKey> toRemoveKeys = new HashSet<CharKey>();
        HashSet<int> rightSpotIndices = new HashSet<int>();
        foreach (char guessState in guessParts[1])
        {
            if (guessState == WRONG)
            {
                for (int i = 0; i < NUM_OF_CHARS; i++)
                {
                    toRemoveKeys.Add(new CharKey()
                    {
                        Char = guessParts[0][idx],
                        Index = i
                    });
                }
            }
            else if (guessState == WRONG_SPOT)
            {
                toRemoveKeys.Add(new CharKey()
                {
                    Char = guessParts[0][idx],
                    Index = idx
                });
            }
            else if (guessState == RIGHT_SPOT)
            {
                rightSpotIndices.Add(idx);
            }
            ++idx;
        }
        idx = 0;
        foreach (char guessState in guessParts[1])
        {
            char guessChar = guessParts[0][idx];
            if (guessState == RIGHT_SPOT)
            {
                var intersectKey = new CharKey()
                {
                    Char = guessChar,
                    Index = idx
                };
                if (commonWordMaps[pzIdx].TryGetValue(intersectKey, out var keepList1))
                {
                    HashSet<string> newList = new HashSet<string>();
                    foreach (var word in keepList1!)
                    {
                        bool toKeep = true;
                        foreach (var otherKey in word.CharKeysExcludeSourceKey)
                        {
                            if (toRemoveKeys.Contains(otherKey))
                            {
                                toKeep = false;
                                break;
                            }
                        }

                        if (toKeep)
                        {
                            newList.Add(word.WordStr);
                        }
                    }
                    if (newList.Count > 0)
                    {
                        intersectCommonSets.Add(newList);
                    }
                }
                if (uncommonWordMaps[pzIdx].TryGetValue(intersectKey, out var keepList2))
                {
                    HashSet<string> newList = new HashSet<string>();
                    foreach (var word in keepList2!)
                    {
                        bool toKeep = true;
                        foreach (var otherKey in word.CharKeysExcludeSourceKey)
                        {
                            if (toRemoveKeys.Contains(otherKey))
                            {
                                toKeep = false;
                                break;
                            }
                        }

                        if (toKeep)
                        {
                            newList.Add(word.WordStr);
                        }
                    }
                    if (newList.Count > 0)
                    {
                        intersectUncommonSets.Add(newList);
                    }
                }
            }
            else if (guessState == WRONG_SPOT)
            {
                List<string> commonWordList = new List<string>();
                List<string> uncommonWordList = new List<string>();
                HashSet<CharKey> unionKeepKeys = new HashSet<CharKey>();
                for (int i = 0; i < NUM_OF_CHARS; ++i)
                {
                    if (i != idx && !rightSpotIndices.Contains(i))
                    {
                        unionKeepKeys.Add(new CharKey()
                        {
                            Char = guessChar,
                            Index = i
                        });
                    }
                }
                foreach (var unionKeepKey in unionKeepKeys)
                {
                    if (commonWordMaps[pzIdx].TryGetValue(unionKeepKey, out var keepList1))
                    {
                        foreach (var word in keepList1!)
                        {
                            bool toKeep = true;
                            foreach (var otherKey in word.CharKeysExcludeSourceKey)
                            {
                                if (toRemoveKeys.Contains(otherKey))
                                {
                                    toKeep = false;
                                    break;
                                }
                            }

                            if (toKeep)
                            {
                                commonWordList.Add(word.WordStr);
                            }
                        }
                    }
                    if (uncommonWordMaps[pzIdx].TryGetValue(unionKeepKey, out var keepList2))
                    {
                        foreach (var word in keepList2!)
                        {
                            bool toKeep = true;
                            foreach (var otherKey in word.CharKeysExcludeSourceKey)
                            {
                                if (toRemoveKeys.Contains(otherKey))
                                {
                                    toKeep = false;
                                    break;
                                }
                            }

                            if (toKeep)
                            {
                                uncommonWordList.Add(word.WordStr);
                            }
                        }
                    }
                }

                if (commonWordList.Count > 0)
                {
                    intersectCommonSets.Add(commonWordList);
                }
                if (uncommonWordList.Count > 0)
                {
                    intersectUncommonSets.Add(uncommonWordList);
                }
            }
            ++idx;
        }

        EvalResult result = new EvalResult();
        if (intersectCommonSets.Count > 0)
        {
            idx = 0;
            foreach (var s in intersectCommonSets)
            {
                if (idx == 0)
                {
                    result.CommonWordList = new HashSet<string>(s);
                }
                else
                {
                    result.CommonWordList.IntersectWith(s);
                }
                ++idx;
            }
        }
        if (intersectUncommonSets.Count > 0)
        {
            idx = 0;
            foreach (var s in intersectUncommonSets)
            {
                if (idx == 0)
                {
                    result.UncommonWordList = new HashSet<string>(s);
                }
                else
                {
                    result.UncommonWordList.IntersectWith(s);
                }
                ++idx;
            }
        }

        if (result.CommonWordList.Count == 0 &&
            result.UncommonWordList.Count == 0)
        {
            result.IsSubtract = true;
            foreach (var removeKey in toRemoveKeys)
            {
                if (commonWordMaps[pzIdx].TryGetValue(removeKey, out var list1))
                {
                    foreach (var word in list1)
                    {
                        result.CommonWordList.Add(word.WordStr);
                    }
                }

                if (uncommonWordMaps[pzIdx].TryGetValue(removeKey, out var list2))
                {
                    foreach (var word in list2)
                    {
                        result.UncommonWordList.Add(word.WordStr);
                    }
                }
            }
        }

        return result;
    }

    private static void RecalculateWordMaps(int pzIdx)
    {
        commonWordMaps[pzIdx].Clear();
        uncommonWordMaps[pzIdx].Clear();
        foreach (var word in commonWordLists[pzIdx])
        {
            AddWordToWordMap(word, commonWordMaps[pzIdx]);
        }
        foreach (var word in uncommonWordLists[pzIdx])
        {
            AddWordToWordMap(word, uncommonWordMaps[pzIdx]);
        }
    }

    private static async Task ReadWords(HashSet<string>[] lists, Dictionary<CharKey, HashSet<Word>>[] maps)
    {
        Console.Write("Word list file path:");
        string path = Console.ReadLine() ?? string.Empty;
        var lines = await File.ReadAllLinesAsync(path);
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                for (int i = 0; i < lists.Length; i++)
                {
                    lists[i].Add(line);
                    AddWordToWordMap(line, maps[i]);
                }
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

    private static async Task SubtractWords()
    {
        Console.Write("Word list file path:");
        string path = Console.ReadLine() ?? string.Empty;
        var lines = await File.ReadAllLinesAsync(path);
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                for (int i = 0; i < commonWordLists.Length; i++)
                {
                    if (commonWordLists[i].Remove(line))
                    {
                        int idx = 0;
                        foreach (char c in line)
                        {
                            var key = new CharKey()
                            {
                                Char = c,
                                Index = idx
                            };
                            commonWordMaps[i][key].Remove(ConstructWordObject(key, line));
                            ++idx;
                        }
                    }
                    else if (uncommonWordLists[i].Remove(line))
                    {
                        int idx = 0;
                        foreach (char c in line)
                        {
                            var key = new CharKey()
                            {
                                Char = c,
                                Index = idx
                            };
                            uncommonWordMaps[i][key].Remove(ConstructWordObject(key, line));
                            ++idx;
                        }
                    }
                }
            }
        }
    }

    private static async Task GenerateWordList()
    {
        Console.Write("Input file:");
        string path = Console.ReadLine() ?? string.Empty;
        var lines = await File.ReadAllLinesAsync(path);
        Console.Write("Target word length:");
        int targetLength = int.Parse(Console.ReadLine() ?? "0");
        List<string> output = new List<string>();
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line) && line.Length == targetLength)
            {
                output.Add(line);
            }
        }

        string fileName = Path.GetFileNameWithoutExtension(path);
        await File.WriteAllLinesAsync(Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, fileName + "-" + targetLength + ".txt"), output);
    }

    private static async Task SeparateLists()
    {
        Console.Write("Left file:");
        string leftPath = Console.ReadLine() ?? string.Empty;
        var lines = await File.ReadAllLinesAsync(leftPath);
        Console.Write("Right file:");
        string rightPath = Console.ReadLine() ?? string.Empty;
        var rlines = await File.ReadAllLinesAsync(rightPath);
        HashSet<string> rights = new HashSet<string>();
        foreach (string l in rlines)
        {
            if (!string.IsNullOrWhiteSpace(l))
            {
                rights.Add(l);
            }
        }
        List<string> result = new List<string>();
        foreach (string l in lines)
        {
            if (!string.IsNullOrWhiteSpace(l) && !rights.Contains(l))
            {
                result.Add(l);
            }
        }
        string fileName = Path.GetFileNameWithoutExtension(leftPath);
        await File.WriteAllLinesAsync(Path.Combine(Path.GetDirectoryName(leftPath) ?? string.Empty, fileName + "-minus.txt"), result);
    }
}

