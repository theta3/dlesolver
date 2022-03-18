// See https://aka.ms/new-console-template for more information
public static class Program
{
    private const int NUM_OF_CHARS = 5;
    private static HashSet<string> previousWords;
    private static HashSet<string> commonWordList;
    private static List<string> unCommonWordList;
    private static HashSet<char>[] NoneChars;

    public static void Main(string[] args)
    {
        Console.WriteLine("Welcome to -Dle Solver v0.2");
        // GenerateWordList().Wait();
        SeparateLists().Wait();
       // Solver().Wait();

        Console.WriteLine("DONE");
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

    private static async Task Solver()
    {
        Console.Write("Previous word list (ENTER if none):");
        string? previousPath = Console.ReadLine();
        previousWords = new HashSet<string>();
        if (!string.IsNullOrWhiteSpace(previousPath))
        {
            var lines = await File.ReadAllLinesAsync(previousPath);
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    previousWords.Add(line);
                }
            }
        }

        Console.Write("Word list file:");
        string path = Console.ReadLine() ?? string.Empty;
        

    }
}

