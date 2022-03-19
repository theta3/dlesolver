// See https://aka.ms/new-console-template for more information
using DleSolver;
using System.Text;

public static class Program
{
    private const int NUM_OF_CHARS = 5;
    private const char WRONG = '_';
    private const char WRONG_SPOT = '?';
    private const char RIGHT_SPOT = '+';
    private static HashSet<string> commonWordList;
    private static HashSet<string> uncommonWordList;
    private static Dictionary<CharKey, HashSet<Word>> commonWordMap;
    private static Dictionary<CharKey, HashSet<Word>> uncommonWordMap;

    public static void Main(string[] args)
    {
        Console.WriteLine("Welcome to -Dle Solver v0.3");
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
                    Guess();
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
            }
        }
        while (option > 0);
        // GenerateWordList().Wait();
       // SeparateLists().Wait();
       // Solver().Wait();

        Console.WriteLine("DONE");
    }

    private static void Guess()
    {
        int originalSize = commonWordList.Count + uncommonWordList.Count;
        double maxMinPruneRate = 0;
        double maxAvgPruneRate = 0;
        string selectedGuessMaxMin = string.Empty;
        string selectedGuessMaxAvg = string.Empty;
        string[] commonWordArray = commonWordList.ToArray();
        string[] uncommonWordArray = uncommonWordList.ToArray();
        // Arrays of min prune rate. Arrays of avg prune rate
        double[] minCommonPruneRates = new double[commonWordArray.Length];
        double[] minUncommonPruneRates = new double[uncommonWordArray.Length];
        double[] avgCommonPruneRates = new double[commonWordArray.Length];
        double[] avgUncommonPruneRates = new double[uncommonWordArray.Length];


        // Parallel.ForEach https://stackoverflow.com/questions/39713258/c-sharp-parallel-foreach-loop-finding-index
        Parallel.ForEach(commonWordArray, (word, state, index) =>
            EvaluateGuessWord(word, (int)index, commonWordArray, uncommonWordArray, originalSize, minCommonPruneRates, avgCommonPruneRates)
        );
        Parallel.ForEach(uncommonWordArray, (word, state, index) =>
            EvaluateGuessWord(word, (int)index, uncommonWordArray, commonWordArray, originalSize, minUncommonPruneRates, avgUncommonPruneRates)
        );
        // find max min and max avg
        for (int i = 0; i < commonWordArray.Length; i++)
        {
            if (minCommonPruneRates[i] > maxMinPruneRate)
            {
                maxMinPruneRate = minCommonPruneRates[i];
                selectedGuessMaxMin = commonWordArray[i];
            }

            if (avgCommonPruneRates[i] > maxAvgPruneRate)
            {
                maxAvgPruneRate = avgCommonPruneRates[i];
                selectedGuessMaxAvg = commonWordArray[i];
            }
        }

        for (int i = 0; i < uncommonWordArray.Length; ++i)
        {
            if (minUncommonPruneRates[i] > maxMinPruneRate)
            {
                maxMinPruneRate = minUncommonPruneRates[i];
                selectedGuessMaxMin = uncommonWordArray[i];
            }

            if (avgUncommonPruneRates[i] > maxAvgPruneRate)
            {
                maxAvgPruneRate = avgUncommonPruneRates[i];
                selectedGuessMaxAvg = uncommonWordArray[i];
            }
        }
        Console.WriteLine("Final selectedGuessMaxMin {0}; prune rate: {1}", selectedGuessMaxMin, maxMinPruneRate);
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
        Console.Write("Enter word in format <word>:[{0}{1}{2}](5) where {0}: char not in result; {1}: char wrong spot; {2}: char correct spot", WRONG, WRONG_SPOT, RIGHT_SPOT);
        string guessWord = Console.ReadLine() ?? string.Empty;
        if (guessWord.Length == NUM_OF_CHARS * 2 + 1)
        {
            AddGuessWord(guessWord);
        }
    }

    private static void AddGuessWord(string guessWord)
    {
        var newLists = EvaluateGuessWord(guessWord);
        commonWordList = newLists.Item1;
        uncommonWordList = newLists.Item2;
        RecalculateWordMaps();
    }

    private static void EvaluateGuessWord(string guessWord, int index, string[] myWordArray, string[] otherWordArray, int originalSize, double[] minPruneRates, double[] avgPruneRates)
    {
        double minPruneRate = 1;
        double sumPruneRate = 0;
        for (int i = 0; i < myWordArray.Length; i++)
        {
            if (index != i)
            {
                string resultWord = myWordArray[i];
                var pruneRate = EvaluatePruneRate(guessWord, resultWord, originalSize);
                sumPruneRate += pruneRate;
                if (minPruneRate > pruneRate)
                {
                    minPruneRate = pruneRate;
                }
            }
        }

        foreach (string resultWord in otherWordArray)
        {
            var pruneRate = EvaluatePruneRate(guessWord, resultWord, originalSize);
            sumPruneRate += pruneRate;
            if (minPruneRate > pruneRate)
            {
                minPruneRate = pruneRate;
            }
        }
        double avgPruneRate = sumPruneRate / (originalSize - 1);
        minPruneRates[index] = minPruneRate;
        avgPruneRates[index] = avgPruneRate;
        Console.Write("{0}:{1}-{2}; ", guessWord, minPruneRate, avgPruneRate);
    }

    private static double EvaluatePruneRate(string guessWord, string resultWord, int originalSize)
    {
        string guessResult = GenerateMatchResult(guessWord, resultWord);
        var newLists = EvaluateGuessWord(guessWord + ":" + guessResult);
        return ((double)(newLists.Item1.Count + newLists.Item2.Count))/originalSize;
    }
    private static Tuple<HashSet<string>, HashSet<string>> EvaluateGuessWord(string guessWord)
    {
        string[] guessParts = guessWord.Split(':');
        int idx = 0;
        HashSet<CharKey> toRemoveKeys = new HashSet<CharKey>();
        HashSet<CharKey> toKeepKeys = new HashSet<CharKey>();
        HashSet<int> rightSpotIndices = new HashSet<int>();
        foreach (char guessState in guessParts[1])
        {
            if (guessState == RIGHT_SPOT)
            {
                rightSpotIndices.Add(idx);
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
                            toKeepKeys.Add(key2);
                        }
                    }
                    break;
                case RIGHT_SPOT:
                    var key3 = new CharKey()
                    {
                        Char = guessChar,
                        Index = idx
                    };
                    toKeepKeys.Add(key3);
                    break;
            }
            ++idx;
        }

        var newCommonWordList = new HashSet<string>();
        var newUncommonWordList = new HashSet<string>();
        foreach (var keepKey in toKeepKeys)
        {
            if (commonWordMap.TryGetValue(keepKey, out var keepList1))
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
                        newCommonWordList.Add(word.WordStr);
                    }
                }
            }
            if (uncommonWordMap.TryGetValue(keepKey, out var keepList2))
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
                        newUncommonWordList.Add(word.WordStr);
                    }
                }
            }
        }

        return Tuple.Create(newCommonWordList, newUncommonWordList);
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

