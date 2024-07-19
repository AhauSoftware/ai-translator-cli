using System.Configuration;
using Newtonsoft.Json;

public static class Batching
{
    public static List<string> BatchEntries(Dictionary<string, string> entries)
    {
        var batches = new List<string>();
        var currentBatch = new List<Dictionary<string, string>>();
        int currentTokenCount = 0;

        // Read maxTokens from App.config
        int maxTokens = int.TryParse(ConfigurationManager.AppSettings["MaxTokens"], out var mt) ? mt : 1365;

        // Approximate conversion factor: 1 token ~ 4 characters
        int maxLength = maxTokens * 4;

        foreach (var entry in entries)
        {
            var jsonEntry = new Dictionary<string, string> { { entry.Key, entry.Value } };
            var jsonString = JsonConvert.SerializeObject(jsonEntry) + "\n";

            if (currentTokenCount + jsonString.Length > maxLength)
            {
                batches.Add(JsonConvert.SerializeObject(currentBatch));
                currentBatch.Clear();
                currentTokenCount = 0;
            }

            currentBatch.Add(jsonEntry);
            currentTokenCount += jsonString.Length;
        }

        if (currentBatch.Count > 0)
        {
            batches.Add(JsonConvert.SerializeObject(currentBatch));
        }

        return batches;
    }
}
