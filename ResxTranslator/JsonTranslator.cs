using Newtonsoft.Json;

public static class JsonTranslator
{
    public static Dictionary<string, string> ParseJson(string filePath)
    {
        var jsonContent = File.ReadAllText(filePath);
        var entries = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent);
        return entries ?? new Dictionary<string, string>();
    }

    public static void ReconstructJson(string originalFilePath, Dictionary<string, string> originalEntries, List<Dictionary<string, string>> translatedBatches, string outputPath)
    {
        var translatedEntries = new Dictionary<string, string>();
        var originalKeys = new List<string>(originalEntries.Keys);

        int currentIndex = 0;

        foreach (var batch in translatedBatches)
        {
            foreach (var entry in batch)
            {
                if (currentIndex < originalKeys.Count)
                {
                    translatedEntries[originalKeys[currentIndex]] = entry.Value.Trim();
                    currentIndex++;
                }
            }
        }

        var jsonString = JsonConvert.SerializeObject(translatedEntries, Formatting.Indented);
        File.WriteAllText(outputPath, jsonString);

        Console.WriteLine($"Saved translated JSON file to {outputPath}");
    }
}
