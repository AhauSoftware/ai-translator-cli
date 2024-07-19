using System.Xml.Linq;

public static class ResxTranslator
{
    public static Dictionary<string, string> ParseResx(string filePath)
    {
        var entries = new Dictionary<string, string>();

        var xdoc = XDocument.Load(filePath);

        foreach (var dataElement in xdoc.Descendants("data"))
        {
            var name = dataElement.Attribute("name")?.Value;
            var type = dataElement.Attribute("type")?.Value;
            var value = dataElement.Element("value")?.Value;

            // Skip entries with type="System.Drawing.Bitmap, System.Drawing"
            if (!string.IsNullOrEmpty(type) && type == "System.Drawing.Bitmap, System.Drawing")
            {
                continue;
            }

            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
            {
                entries[name] = value;
            }
        }

        return entries;
    }
}
