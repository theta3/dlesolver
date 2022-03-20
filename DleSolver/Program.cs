﻿// See https://aka.ms/new-console-template for more information
using DleSolver;
using System.Text;

public static class Program
{
    private const int NUM_OF_CHARS = 5;
    private const char WRONG = '_';
    private const char WRONG_SPOT = '?';
    private const char RIGHT_SPOT = '+';
    private const char SEPARATOR = ':';
    private const int MAX_CONCURRENCY = 100;
    private static HashSet<string> commonWordList;
    private static HashSet<string> uncommonWordList;
    private static Dictionary<CharKey, HashSet<Word>> commonWordMap;
    private static Dictionary<CharKey, HashSet<Word>> uncommonWordMap;

    public static void Main(string[] args)
    {
        Console.WriteLine("Welcome to -Dle Solver v0.4");
        commonWordList = new HashSet<string>();
        uncommonWordList = new HashSet<string>();
        commonWordMap = new Dictionary<CharKey, HashSet<Word>>();
        uncommonWordMap = new Dictionary<CharKey, HashSet<Word>>();
        int option = 0;
        do
        {
            Console.WriteLine("{0} common words - {1} uncommon words", commonWordList.Count, uncommonWordList.Count);
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
                    ReadWords(commonWordList, commonWordMap).Wait();
                    break;
                case 2:
                    ReadWords(uncommonWordList, uncommonWordMap).Wait();
                    break;
                case 3:
                    SubtractWords().Wait();
                    break;
                case 4:
                    AddGuessWord();
                    break;
                case 5:
                    Guess().Wait();
                    break;
                case 6:
                    commonWordList = new HashSet<string>();
                    uncommonWordList = new HashSet<string>();
                    commonWordMap = new Dictionary<CharKey, HashSet<Word>>();
                    uncommonWordMap = new Dictionary<CharKey, HashSet<Word>>();
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

        private static async Task Guess()
    {
        int originalSize = commonWordList.Count + uncommonWordList.Count;
        double maxAvgPruneRate = 0;
        string selectedGuessMaxAvg = string.Empty;
        string[] commonWordArray = commonWordList.ToArray();
        string[] uncommonWordArray = uncommonWordList.ToArray();
        // Arrays of min prune rate. Arrays of avg prune rate
        double[] avgCommonPruneRates = new double[commonWordArray.Length];
        double[] avgUncommonPruneRates = new double[uncommonWordArray.Length];
        Console.Write("Saved prune rate file path (ENTER to skip):");
        string pruneRatePath = Console.ReadLine() ?? string.Empty;
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
                EvaluateGuessWord(word, (int)(startIndex + index), commonWordArray, uncommonWordArray, originalSize, avgCommonPruneRates, savedPruneRates, newLines)
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
                EvaluateGuessWord(word, (int)(startIndex + index), uncommonWordArray, commonWordArray, originalSize, avgUncommonPruneRates, savedPruneRates, newLines)
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
        Console.WriteLine("FinalselectedGuessMaxAvg {0}; prune rate: {1}", selectedGuessMaxAvg, maxAvgPruneRate);
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
        Console.Write("Enter word in format <word>:[{0}{1}{2}](5) where {0}: char not in result; {1}: char wrong spot; {2}: char correct spot:", WRONG, WRONG_SPOT, RIGHT_SPOT);
        string guessWord = Console.ReadLine() ?? string.Empty;
        if (guessWord.Length == NUM_OF_CHARS * 2 + 1)
        {
            AddGuessWord(guessWord);
        }
    }

    private static void AddGuessWord(string guessWord)
    {
        var newLists = EvaluateGuessWord(guessWord);
        if (newLists.IsSubtract)
        {
            foreach (var word in newLists.CommonWordList)
            {
                commonWordList.Remove(word);
            }

            foreach (var word in newLists.UncommonWordList)
            {
                uncommonWordList.Remove(word);
            }
        }
        else
        {
            commonWordList = newLists.CommonWordList;
            uncommonWordList = newLists.UncommonWordList;
        }
        RecalculateWordMaps();
    }

    private static void EvaluateGuessWord(string guessWord,
        int index, string[] myWordArray, string[] otherWordArray, int originalSize, double[] avgPruneRates, Dictionary<string, double> savedPruneRates, List<string> newLines)
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
                var pruneRate = EvaluatePruneRate(guessWord, resultWord, originalSize);
                sumPruneRate += pruneRate;
            }
        }

        foreach (string resultWord in otherWordArray)
        {
            var pruneRate = EvaluatePruneRate(guessWord, resultWord, originalSize);
            sumPruneRate += pruneRate;
        }
        double avgPruneRate = sumPruneRate / (originalSize - 1);
        avgPruneRates[index] = avgPruneRate;
        savedPruneRates[guessWord] = avgPruneRate;
        newLines.Add(guessWord + SEPARATOR + avgPruneRate);
        Console.Write("{0}:{1}; ", guessWord, avgPruneRate);
    }

    private static double EvaluatePruneRate(string guessWord, string resultWord, int originalSize)
    {
        string guessResult = GenerateMatchResult(guessWord, resultWord);
        var newLists = EvaluateGuessWord(guessWord + SEPARATOR + guessResult);
        return newLists.IsSubtract ?
            ((double)(newLists.CommonWordList.Count + newLists.UncommonWordList.Count))/originalSize :
            1.0 - ((double)(newLists.CommonWordList.Count + newLists.UncommonWordList.Count)) / originalSize;
    }
    private static EvalResult EvaluateGuessWord(string guessWord)
    {
        string[] guessParts = guessWord.Split(':');
        int idx = 0;
        HashSet<CharKey> intersectKeepKeys = new HashSet<CharKey>();
        HashSet<CharKey> unionKeepKeys = new HashSet<CharKey>();
        HashSet<CharKey> toRemoveKeys = new HashSet<CharKey>();
        HashSet<int> rightSpotIndices = new HashSet<int>();
        foreach (char guessState in guessParts[1])
        {
            if (guessState == RIGHT_SPOT)
            {
                rightSpotIndices.Add(idx);
                char guessChar = guessParts[0][idx];
                intersectKeepKeys.Add(new CharKey()
                {
                    Char = guessChar,
                    Index = idx
                });
            }
            ++idx;
        }
        idx = 0;
        foreach (char guessChar in guessParts[0])
        {
            char guessState = guessParts[1][idx];
            switch (guessState)
            {
                case WRONG:
                    for (int i = 0; i < NUM_OF_CHARS; ++i)
                    {
                        var key1 = new CharKey()
                        {
                            Char = guessChar,
                            Index = i
                        };
                        toRemoveKeys.Add(key1);
                    }
                    break;
                case WRONG_SPOT:
                    for (int i = 0; i < NUM_OF_CHARS; ++i)
                    {
                        var key2 = new CharKey()
                        {
                            Char = guessChar,
                            Index = i
                        };
                        if (i == idx)
                        {
                            toRemoveKeys.Add(key2);
                        }
                        else if (!rightSpotIndices.Contains(i))
                        {
                            unionKeepKeys.Add(key2);
                        }
                    }
                    break;
            }
            ++idx;
        }

        EvalResult result = new EvalResult();
        if (intersectKeepKeys.Count > 0 ||
            unionKeepKeys.Count > 0)
        {
            var newCommonWordList = new HashSet<string>();
            var newUncommonWordList = new HashSet<string>();
            if (intersectKeepKeys.Count > 0)
            {
                foreach (var intersectKey in intersectKeepKeys)
                {
                    if (commonWordMap.TryGetValue(intersectKey, out var keepList1))
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
                        if (newCommonWordList.Count == 0)
                        {
                            newCommonWordList = new HashSet<string>(newList);
                        }
                        else
                        {
                            newCommonWordList.IntersectWith(newList);
                        }
                    }
                    if (uncommonWordMap.TryGetValue(intersectKey, out var keepList2))
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
                        if (newUncommonWordList.Count == 0)
                        {
                            newUncommonWordList = new HashSet<string>(newList);
                        }
                        else
                        {
                            newUncommonWordList.IntersectWith(newList);
                        }
                    }
                }
            }
            if (unionKeepKeys.Count > 0)
            {
                var newCommonUnionList = new HashSet<string>();
                var newUncommonUnionList = new HashSet<string>();
                foreach (var unionKey in unionKeepKeys)
                {
                    if (commonWordMap.TryGetValue(unionKey, out var keepList1))
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
                                newCommonUnionList.Add(word.WordStr);
                            }
                        }
                    }
                    if (uncommonWordMap.TryGetValue(unionKey, out var keepList2))
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
                                newUncommonUnionList.Add(word.WordStr);
                            }
                        }
                    }
                }

                if (newCommonWordList.Count > 0)
                {
                    newCommonWordList.IntersectWith(newCommonUnionList);
                }
                else
                {
                    newCommonWordList = newCommonUnionList;
                }

                if (newUncommonWordList.Count > 0)
                {
                    newUncommonWordList.IntersectWith(newUncommonUnionList);
                }
                else
                {
                    newUncommonWordList = newUncommonUnionList;
                }
            }

            result.CommonWordList = newCommonWordList;
            result.UncommonWordList = newUncommonWordList;
        }
        else
        {
            result.IsSubtract = true;
            foreach (var removeKey in toRemoveKeys)
            {
                if (commonWordMap.TryGetValue(removeKey, out var list1))
                {
                    foreach (var word in list1)
                    {
                        result.CommonWordList.Add(word.WordStr);
                    }
                }

                if (uncommonWordMap.TryGetValue(removeKey, out var list2))
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

    private static void RecalculateWordMaps()
    {
        commonWordMap.Clear();
        uncommonWordMap.Clear();
        foreach (var word in commonWordList)
        {
            AddWordToWordMap(word, commonWordMap);
        }
        foreach (var word in uncommonWordList)
        {
            AddWordToWordMap(word, uncommonWordMap);
        }
    }

    private static async Task ReadWords(HashSet<string> list, Dictionary<CharKey, HashSet<Word>> map)
    {
        Console.Write("Word list file path:");
        string path = Console.ReadLine() ?? string.Empty;
        var lines = await File.ReadAllLinesAsync(path);
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                list.Add(line);
                AddWordToWordMap(line, map);
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
                if (commonWordList.Remove(line))
                {
                    int idx = 0;
                    foreach (char c in line)
                    {
                        var key = new CharKey()
                        {
                            Char = c,
                            Index = idx
                        };
                        commonWordMap[key].Remove(ConstructWordObject(key, line));
                        ++idx;
                    }
                }
                else if (uncommonWordList.Remove(line))
                {
                    int idx = 0;
                    foreach (char c in line)
                    {
                        var key = new CharKey()
                        {
                            Char = c,
                            Index = idx
                        };
                        uncommonWordMap[key].Remove(ConstructWordObject(key, line));
                        ++idx;
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

