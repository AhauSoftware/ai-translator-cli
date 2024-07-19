using System.Xml.Linq;

public static class ResxReconstructor
{
    public static void ReconstructResx(string originalFilePath, Dictionary<string, string> originalEntries, List<Dictionary<string, string>> translatedBatches, string outputPath)
    {
        var translatedEntries = new Dictionary<string, string>();
        var originalKeys = originalEntries.Keys.ToList();

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

        var xdoc = XDocument.Load(originalFilePath);

        foreach (var dataElement in xdoc.Descendants("data"))
        {
            var name = dataElement.Attribute("name")?.Value;
            if (name != null && translatedEntries.ContainsKey(name))
            {
                var valueElement = dataElement.Element("value");
                if (valueElement != null)
                {
                    valueElement.Value = translatedEntries[name];
                }
            }
        }

        xdoc.Save(outputPath);

        Console.WriteLine($"Saved translated .resx file to {outputPath}");
    }
}
