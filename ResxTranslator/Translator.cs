using System.Text;
using Newtonsoft.Json;
using System.Configuration;

public static class Translator
{
    public static async Task<List<Dictionary<string, string>>> TranslateBatches(List<string> batches, string apiKey, string languageCode, string model)
    {
        var translatedBatches = new List<Dictionary<string, string>>();

        // Read maxOutputTokens from App.config
        int maxOutputTokens = int.TryParse(ConfigurationManager.AppSettings["MaxOutputTokens"], out var mot) ? mot : 2731;

        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            foreach (var batch in batches)
            {
                var requestBody = new
                {
                    model = model,
                    messages = new[]
                    {
                        new { role = "system", content = $"You are a professional translator for a project management system. Translate each value in the following JSON array of key-value pairs to the language specified by the code '{languageCode}'. Ensure that each key remains unchanged, and that any HTML entities and special characters in the values are preserved exactly as they are in the translation. Do not modify or remove these entities." },
                        new { role = "user", content = batch }
                    },
                    max_tokens = maxOutputTokens
                };

                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                HttpResponseMessage? response = null;

                // Retry mechanism
                for (int i = 0; i < 5; i++)
                {
                    response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);

                    if (response.IsSuccessStatusCode)
                    {
                        break;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
                }

                if (response == null || !response.IsSuccessStatusCode)
                {
                    throw new Exception("Invalid response from OpenAI. Check your OpenAI API Key.");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonResponse = JsonConvert.DeserializeObject<dynamic>(responseContent);

                if (jsonResponse?.choices != null && jsonResponse?.choices.Count > 0)
                {
                    var messageContent = jsonResponse?.choices[0]?.message?.content?.ToString();
                    if (messageContent != null)
                    {
                        try
                        {
                            var translatedBatch = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(messageContent);
                            translatedBatches.AddRange(translatedBatch);
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"Error deserializing batch: {ex.Message}");
                        }
                    }
                    else
                    {
                        throw new Exception("Invalid response structure from OpenAI");
                    }
                }
                else
                {
                    throw new Exception("Invalid response structure from OpenAI");
                }
            }
        }

        return translatedBatches;
    }
}
