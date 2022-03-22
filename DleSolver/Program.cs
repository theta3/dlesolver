// See https://aka.ms/new-console-template for more information
using DleSolver;
using System.Collections.Concurrent;
using System.Text;

public static class Program
{
    private const int NUM_OF_CHARS = 5;
    internal const char WRONG = '_';
    private const char WRONG_SPOT = '?';
    private const char RIGHT_SPOT = '+';
    private const char SEPARATOR = ':';
    private const int MAX_CONCURRENCY = 100;
    private static SolutionSet[] solutionSets = new SolutionSet[1];
    private static SolutionSet originalSolutionSet = new SolutionSet();
    private static string pruneRatePath = string.Empty;

    public static void Main(string[] args)
    {
        Console.WriteLine("Welcome to -Dle Solver v1.0");
        solutionSets[0] = new SolutionSet();
        int option = 0;
        do
        {
            Console.WriteLine();
            for (int i = 0; i < solutionSets.Length; ++i)
            {
                Console.WriteLine("[{2}]: {0} common words - {1} uncommon words", solutionSets[i].CommonWordList.Count, solutionSets[i].UncommonWordList.Count, solutionSets[i].PuzzleIndex);
            }
            Console.WriteLine("[ORIG]: {0} common words - {1} uncommon words", originalSolutionSet.CommonWordList.Count, originalSolutionSet.UncommonWordList.Count);
            Console.WriteLine();
            Console.WriteLine("0. Exit");
            Console.WriteLine("1. Read common word list");
            Console.WriteLine("2. Read uncommon word list");
            Console.WriteLine("3. Read excluded word list");
            Console.WriteLine("4. Enter a guess word");
            Console.WriteLine("5. Make a guess");
            Console.WriteLine("6. Clear all");
            Console.WriteLine("7. Generate guess result");
            Console.WriteLine("8. Convert retention rate to prune rate");
            Console.WriteLine("9. Set number of puzzle (this will reset)");
            Console.WriteLine("10. Reset");
            Console.WriteLine("11. Print solution sets");
            int.TryParse(Console.ReadLine(), out option);
            switch(option)
            {
                case 1:
                    ReadWords(solutionSets.Select(s => s.CommonWordList)).Wait();
                    foreach (var s in solutionSets)
                    {
                        s.RecalculateWordMaps();
                    }
                    originalSolutionSet = solutionSets[0].Copy();
                    break;
                case 2:
                    ReadWords(solutionSets.Select(s => s.UncommonWordList)).Wait();
                    foreach (var s in solutionSets)
                    {
                        s.RecalculateWordMaps();
                    }
                    originalSolutionSet = solutionSets[0].Copy();
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
                    foreach (var s in solutionSets)
                    {
                        s.Clear();
                    }
                    originalSolutionSet.Clear();
                    break;
                case 7:
                    GenerateMatchResult();
                    break;
                case 8:
                    ConvertRetentionToPruneRate().Wait();
                    break;
                case 9:
                    Console.Write("Number of puzzles:");
                    int pnumber = int.Parse(Console.ReadLine() ?? "1");
                    if (pnumber > 0)
                    {
                        solutionSets = new SolutionSet[pnumber];
                        for (int i = 0; i < solutionSets.Length; i++)
                        {
                            solutionSets[i] = originalSolutionSet.Copy();
                            solutionSets[i].PuzzleIndex = i;
                        }
                    }
                    break;
                case 10:
                    for (int i = 0; i < solutionSets.Length; i++)
                    {
                        solutionSets[i] = originalSolutionSet.Copy();
                        solutionSets[i].PuzzleIndex = i;
                    }
                    break;
                case 11:
                    foreach (var ss in solutionSets)
                    {
                        Console.WriteLine("PUZZLE [{0}]", ss.PuzzleIndex);
                        Console.WriteLine("Commons:");
                        foreach (var s in ss.CommonWordList)
                        {
                            Console.WriteLine(s);
                        }
                        Console.WriteLine("Uncommons:");
                        foreach (var s in ss.UncommonWordList)
                        {
                            Console.WriteLine(s);
                        }
                        Console.WriteLine();
                    }
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
        ConcurrentBag<GuessResult> guessResults = new ConcurrentBag<GuessResult>();
        Console.Write("Saved prune rate file path (ENTER to skip):");
        pruneRatePath = Console.ReadLine() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(pruneRatePath))
        {
            List<string> guesses = ConstructGuessSet();
            List<SolutionSet> moreThan1Sets = new List<SolutionSet>();
            int originalSize = solutionSets.Sum(s => s.Count());
            foreach (var s in solutionSets)
            {
                if (s.Count() == 0)
                {
                    continue;
                }

                if (s.Count() == 1)
                {
                    guessResults.Add(new GuessResult()
                    {
                        PuzzleIndex = s.PuzzleIndex,
                        Guess = s.FirstWord,
                        PruneRate = 1,
                        PruneCount = originalSize
                    });
                }
                else
                {
                    moreThan1Sets.Add(s);
                }
            }
            Console.WriteLine("Consider {0} guesses. {1} > 1 sets", guesses.Count(), moreThan1Sets.Count());
            Parallel.ForEach(guesses, (guess) =>
            {
                EvaluateGuessWordAcrossSolutionSets(guess, moreThan1Sets, guessResults);
            });

            Console.WriteLine();
            bool found = false;
            foreach (var r in guessResults.OrderByDescending(r => r.PruneCount))
            {
                if (found)
                {
                    break;
                }

                Console.WriteLine(r);
                if (r.PuzzleIndex == null && r.PruneRate < 1)
                {
                    found = true;
                }
            }
        }
        else
        {
            await PersistentStartingGuess();
        }
    }

    private static List<string> ConstructGuessSet()
    {
        HashSet<string> result = new HashSet<string>();
        foreach (var s in solutionSets)
        {
            if (s.Count() == 1)
            {
                continue;
            }
            if (s.Count() > 2 && s.Count() < 10)
            {
                Console.WriteLine("Guess set expansion for PUZZLE [{0}]", s.PuzzleIndex);
                foreach (var w in s.CommonWordList)
                {
                    foreach (char c in w)
                    {
                        for (int i = 0; i < NUM_OF_CHARS; i++)
                        {
                            CharKey other = new CharKey()
                            {
                                Char = c,
                                Index = i
                            };
                            if (originalSolutionSet.CommonWordMap.TryGetValue(other, out HashSet<Word> ws))
                            {
                                foreach (var otherWords in ws)
                                {
                                    result.Add(otherWords.WordStr);
                                }
                            }
                        }
                    }
                }
                foreach (var w in s.UncommonWordList)
                {
                    foreach (char c in w)
                    {
                        for (int i = 0; i < NUM_OF_CHARS; i++)
                        {
                            CharKey other = new CharKey()
                            {
                                Char = c,
                                Index = i
                            };
                            if (originalSolutionSet.UncommonWordMap.TryGetValue(other, out HashSet<Word> ws))
                            {
                                foreach (var otherWords in ws)
                                {
                                    result.Add(otherWords.WordStr);
                                }
                            }
                        }
                    }
                }
            }

            foreach (var w in s.CommonWordList)
            {
                result.Add(w);
            }
            foreach (var w in s.UncommonWordList)
            {
                result.Add(w);
            }
        }
        return result.ToList();
    }

    private static async Task PersistentStartingGuess()
    {
        SolutionSet first = solutionSets[0];
        int originalSize = first.Count();
        double maxAvgPruneRate = 0;
        string selectedGuessMaxAvg = string.Empty;
        string[] commonWordArray = first.CommonWordList.ToArray();
        string[] uncommonWordArray = first.UncommonWordList.ToArray();
        // Arrays of min prune rate. Arrays of avg prune rate
        double[] avgCommonPruneRates = new double[commonWordArray.Length];
        double[] avgUncommonPruneRates = new double[uncommonWordArray.Length];
        Dictionary<string, double> savedPruneRates = new Dictionary<string, double>();
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
                EvaluateStartingGuessWord(first, word, (int)(startIndex + index), commonWordArray, uncommonWordArray, originalSize, avgCommonPruneRates, savedPruneRates, newLines)
            );
            await File.AppendAllLinesAsync(pruneRatePath, newLines);
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
                EvaluateStartingGuessWord(first, word, (int)(startIndex + index), uncommonWordArray, commonWordArray, originalSize, avgUncommonPruneRates, savedPruneRates, newLines)
            );
            await File.AppendAllLinesAsync(pruneRatePath, newLines);
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
        Console.WriteLine("Best starting word is \"{0}\", avg prune rate {1}", selectedGuessMaxAvg, maxAvgPruneRate);
    }

    private static void EvaluateGuessWordAcrossSolutionSets(string guess, List<SolutionSet> moreThan1Sets, ConcurrentBag<GuessResult> guessResults)
    {
        var result = new GuessResult();
        int originalSize = moreThan1Sets.Sum(s => s.Count());
        ConcurrentBag<double> pruneCountPerSet = new ConcurrentBag<double>();
        Parallel.ForEach(moreThan1Sets, ss =>
        {
            EvaluateGuessWordOneSolutionSet(guess, ss, pruneCountPerSet);
        });
        result.PruneCount = pruneCountPerSet.Sum(); 
        result.Guess = guess;
        result.PruneRate = result.PruneCount / originalSize;
        Console.WriteLine("{0}: {1}({2})", result.Guess, result.PruneRate, result.PruneCount);
        guessResults.Add(result);
    }

    private static void EvaluateGuessWordOneSolutionSet(string guess, SolutionSet ss, ConcurrentBag<double> pruneCountPerSet /* to store result */)
    {
        double sumPruneRate = 0;
        int rateCount = 0;
        foreach (var resultWord in ss.CommonWordList)
        {
            if (!string.Equals(guess, resultWord, StringComparison.Ordinal))
            {
                var pruneRate = EvaluatePruneRate(ss, guess, resultWord, ss.Count());
                sumPruneRate += pruneRate;
                ++rateCount;
            }
        }

        foreach (string resultWord in ss.UncommonWordList)
        {
            if (!string.Equals(guess, resultWord, StringComparison.Ordinal))
            {
                var pruneRate = EvaluatePruneRate(ss, guess, resultWord, ss.Count());
                sumPruneRate += pruneRate;
                ++rateCount;
            }
        }
        pruneCountPerSet .Add(sumPruneRate * ss.Count() / rateCount);
    }

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
        if (string.IsNullOrEmpty(w))
        {
            return;
        }
        foreach (var s in solutionSets)
        {
            Console.WriteLine("PUZZLE " + s.PuzzleIndex);
            Console.Write("Enter guess result:[{0}{1}{2}](5) where {0}: char not in result; {1}: char wrong spot; {2}: char correct spot; ENTER to skip:", WRONG, WRONG_SPOT, RIGHT_SPOT);
            string r = Console.ReadLine() ?? string.Empty;
            if (r.Length == NUM_OF_CHARS)
            {
                AddGuessWord(s, w + ":" + r);
            }
        }
    }

    private static void AddGuessWord(SolutionSet s, string guessWord)
    {
        var newLists = EvaluateGuessWord(s, guessWord);
        if (newLists.IsSubtract)
        {
            foreach (var word in newLists.CommonWordList)
            {
                s.CommonWordList.Remove(word);
            }

            foreach (var word in newLists.UncommonWordList)
            {
                s.UncommonWordList.Remove(word);
            }
        }
        else
        {
            s.CommonWordList = newLists.CommonWordList;
            s.UncommonWordList = newLists.UncommonWordList;
        }
        s.RecalculateWordMaps();
    }

    private static void EvaluateStartingGuessWord(
        SolutionSet first,
        string guessWord,
        int index, 
        string[] myWordArray,
        string[] otherWordArray,
        int originalSize,
        double[] avgPruneRates,
        Dictionary<string, double> savedPruneRates,
        List<string> newLines)
    {
        if (savedPruneRates.TryGetValue(guessWord, out double val))
        {
            avgPruneRates[index] = val;
            Console.Write("{0}:{1}/ ", guessWord, val);
            return;
        }
        double sumPruneRate = 0;
        for (int i = 0; i < myWordArray.Length; i++)
        {
            if (index != i)
            {
                string resultWord = myWordArray[i];
                var pruneRate = EvaluatePruneRate(first, guessWord, resultWord, originalSize);
                sumPruneRate += pruneRate;
            }
        }

        foreach (string resultWord in otherWordArray)
        {
            var pruneRate = EvaluatePruneRate(first, guessWord, resultWord, originalSize);
            sumPruneRate += pruneRate;
        }
        double avgPruneRate = sumPruneRate / (originalSize - 1);
        avgPruneRates[index] = avgPruneRate;
        savedPruneRates[guessWord] = avgPruneRate;
        newLines.Add(guessWord + SEPARATOR + avgPruneRate);
        Console.Write("{0}:{1}; ", guessWord, avgPruneRate);
    }

    private static double EvaluatePruneRate(SolutionSet ss, string guessWord, string resultWord, int originalSize)
    {
        string guessResult = GenerateMatchResult(guessWord, resultWord);
        var newLists = EvaluateGuessWord(ss, guessWord + SEPARATOR + guessResult);
        return newLists.IsSubtract ?
            ((double)(newLists.CommonWordList.Count + newLists.UncommonWordList.Count))/originalSize :
            1.0 - ((double)(newLists.CommonWordList.Count + newLists.UncommonWordList.Count)) / originalSize;
    }
    private static EvalResult EvaluateGuessWord(SolutionSet ss, string guessWord)
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
                HashSet<string> newList1 = new HashSet<string>();
                if (ss.CommonWordMap.TryGetValue(intersectKey, out var keepList1))
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
                            newList1.Add(word.WordStr);
                        }
                    }
                }
                intersectCommonSets.Add(newList1);

                HashSet<string> newList2 = new HashSet<string>();
                if (ss.UncommonWordMap.TryGetValue(intersectKey, out var keepList2))
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
                            newList2.Add(word.WordStr);
                        }
                    }
                }
                intersectUncommonSets.Add(newList2);
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
                    if (ss.CommonWordMap.TryGetValue(unionKeepKey, out var keepList1))
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
                    if (ss.UncommonWordMap.TryGetValue(unionKeepKey, out var keepList2))
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

                intersectCommonSets.Add(commonWordList);
                intersectUncommonSets.Add(uncommonWordList);
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
                if (ss.CommonWordMap.TryGetValue(removeKey, out var list1))
                {
                    foreach (var word in list1)
                    {
                        result.CommonWordList.Add(word.WordStr);
                    }
                }

                if (ss.UncommonWordMap.TryGetValue(removeKey, out var list2))
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

    private static async Task ReadWords(IEnumerable<HashSet<string>> lists)
    {
        Console.Write("Word list file path:");
        string path = Console.ReadLine() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        var lines = await File.ReadAllLinesAsync(path);
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                foreach (var list in lists)
                {
                    list.Add(line);
                }
            }
        }
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
                foreach (var ss in solutionSets)
                {
                    ss.SubtractWord(line);
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

