using CommandLine;
using CommandLine.Text;
using System.Configuration;
using System.Formats.Tar;
using System.Globalization;
using System.Text.RegularExpressions;

class Program
{
    static async Task Main(string[] args)
    {
        var parser = new Parser(with => with.HelpWriter = null);
        var parserResult = parser.ParseArguments<Options>(args);

        await parserResult
            .MapResult(
                async opts => await RunOptionsAndReturnExitCode(opts),
                errs => Task.FromResult(DisplayHelp(parserResult, errs))
            );
    }

    private static int DisplayHelp<T>(ParserResult<T> result, IEnumerable<Error>? errors)
    {
        var helpText = HelpText.AutoBuild(result, h =>
        {
            h.AdditionalNewLineAfterOption = false;
            h.Heading = "Resx Translator 1.0"; // Change this to your application name
            return HelpText.DefaultParsingErrorsHandler(result, h);
        }, e => e);

        Console.WriteLine(helpText);
        return 1; // Non-zero exit code for error
    }

    private static async Task RunOptionsAndReturnExitCode(Options opts)
    {
        try
        {
            if (opts.RetryMode)
            {
                await RetryTranslations(opts.ApiKey ?? ConfigurationManager.AppSettings["OpenAIApiKey"] ?? throw new ArgumentNullException("API key is missing in configuration."),
                                           opts.Model ?? ConfigurationManager.AppSettings["Model"] ?? throw new ArgumentNullException("AI Model is missing in configuration."));
            }
            else
            {
                if (string.IsNullOrEmpty(opts.InputPath))
                {
                    Console.WriteLine("Input path is required unless in validation mode.");
                    DisplayHelp(Parser.Default.ParseArguments<Options>(new string[] { }), null);
                    return;
                }
                if (string.IsNullOrEmpty(opts.LanguageCode))
                {
                    Console.WriteLine("Language code is required unless in validation mode.");
                    DisplayHelp(Parser.Default.ParseArguments<Options>(new string[] { }), null);
                    return;
                }

                string inputPath = opts.InputPath;
                string languageCode = opts.LanguageCode;
                string apiKey = opts.ApiKey ?? ConfigurationManager.AppSettings["OpenAIApiKey"] ?? throw new ArgumentNullException("API key is missing in configuration.");
                string model = opts.Model ?? ConfigurationManager.AppSettings["Model"] ?? throw new ArgumentNullException("AI Model is missing in configuration.");

                if (!IsValidLanguageCode(languageCode))
                {
                    Console.WriteLine("Invalid language code provided.");
                    DisplayHelp(Parser.Default.ParseArguments<Options>(new string[] { }), null);
                    return;
                }

                string fullPath = Path.GetFullPath(inputPath);

                if (Directory.Exists(fullPath))
                {
                    string outputFolderPath = fullPath + $"_{languageCode}";
                    await ProcessDirectory(fullPath, outputFolderPath, languageCode, apiKey, model);
                }
                else if (File.Exists(fullPath))
                {
                    string extension = Path.GetExtension(fullPath).ToLower();
                    string? directoryName = Path.GetDirectoryName(fullPath);
                    if (directoryName == null)
                    {
                        Console.WriteLine("Invalid file path provided.");
                        DisplayHelp(Parser.Default.ParseArguments<Options>(new string[] { }), null);
                        return;
                    }

                    string outputFilePath;
                    if (extension == ".resx")
                    {
                        outputFilePath = Path.Combine(directoryName, $"{Path.GetFileNameWithoutExtension(fullPath)}.{languageCode}.resx");
                        await TranslateAndSaveResxFile(fullPath, outputFilePath, languageCode, apiKey, model);
                    }
                    else if (extension == ".json")
                    {
                        outputFilePath = ReplaceLanguageCodeInFilename(fullPath, languageCode);
                        await TranslateAndSaveJsonFile(fullPath, outputFilePath, languageCode, apiKey, model);
                    }
                    else
                    {
                        Console.WriteLine("Invalid input file provided. Only .resx and .json files are supported.");
                        return;
                    }
                }
                else
                {
                    Console.WriteLine("Invalid input path provided. Please provide a valid directory or .resx/.json file path.");
                    DisplayHelp(Parser.Default.ParseArguments<Options>(new string[] { }), null);
                    return;
                }

                Console.WriteLine("Translation process completed successfully.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
        finally
        {
            // Log all failed translations at the end
            LogAllFailedTranslations();
        }
    }

    private static async Task ProcessDirectory(string inputFolderPath, string outputFolderPath, string languageCode, string apiKey, string model)
    {
        var files = Directory.GetFiles(inputFolderPath, "*.*", SearchOption.AllDirectories)
                             .Where(f => f.EndsWith(".resx", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
        foreach (var filePath in files)
        {
            string relativePath = Path.GetRelativePath(inputFolderPath, filePath);
            string outputFilePath;
            if (filePath.EndsWith(".resx", StringComparison.OrdinalIgnoreCase))
            {
                outputFilePath = Path.Combine(outputFolderPath, Path.ChangeExtension(relativePath, $".{languageCode}.resx"));
            }
            else
            {
                outputFilePath = Path.Combine(outputFolderPath, ReplaceLanguageCodeInFilename(relativePath, languageCode));
            }

            string? outputDirectory = Path.GetDirectoryName(outputFilePath);
            if (outputDirectory != null)
            {
                Directory.CreateDirectory(outputDirectory);
            }

            if (filePath.EndsWith(".resx", StringComparison.OrdinalIgnoreCase))
            {
                await TranslateAndSaveResxFile(filePath, outputFilePath, languageCode, apiKey, model);
            }
            else if (filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                await TranslateAndSaveJsonFile(filePath, outputFilePath, languageCode, apiKey, model);
            }
        }
    }

    private static bool IsValidLanguageCode(string languageCode)
    {        
        var cultures = CultureInfo.GetCultures(CultureTypes.NeutralCultures)
                                  .Select(c => c.TwoLetterISOLanguageName)
                                  .Distinct();
        return cultures.Contains(languageCode);
    }

    private static async Task TranslateAndSaveResxFile(string resxFilePath, string outputFilePath, string languageCode, string apiKey, string model)
    {
        Dictionary<string, string> entries = ResxTranslator.ParseResx(resxFilePath);
        Console.WriteLine($"Parsed {entries.Count} entries from {resxFilePath}");

        var batches = Batching.BatchEntries(entries);
        Console.WriteLine($"Created {batches.Count} batches for translation");

        List<Dictionary<string, string>> translatedBatches;
        if (batches.Count > 0)
        {
            try
            {
                translatedBatches = await Translator.TranslateBatches(batches, apiKey, languageCode, model);
                Console.WriteLine("Translation of batches completed");
            }
            catch (Exception ex)
            {
                LogFailedTranslation(outputFilePath, languageCode); // Pass languageCode here
                Console.WriteLine($"Translation failed for {resxFilePath}: {ex.Message}");
                File.Copy(resxFilePath, outputFilePath, true);
                return;
            }
        }
        else
        {
            translatedBatches = new List<Dictionary<string, string>>();
            Console.WriteLine("No batches to translate");
        }

        try
        {
            ResxReconstructor.ReconstructResx(resxFilePath, entries, translatedBatches, outputFilePath);
            Console.WriteLine($"Reconstructed and saved translated .resx file to {outputFilePath}");
        }
        catch (Exception ex)
        {
            LogFailedTranslation(resxFilePath, languageCode); // Pass languageCode here
            Console.WriteLine($"Reconstruction failed for {resxFilePath}: {ex.Message}");
            File.Copy(resxFilePath, outputFilePath, true);
        }
    }

    private static async Task TranslateAndSaveJsonFile(string jsonFilePath, string outputFilePath, string languageCode, string apiKey, string model)
    {
        Dictionary<string, string> entries = JsonTranslator.ParseJson(jsonFilePath);
        Console.WriteLine($"Parsed {entries.Count} entries from {jsonFilePath}");

        var batches = Batching.BatchEntries(entries);
        Console.WriteLine($"Created {batches.Count} batches for translation");

        List<Dictionary<string, string>> translatedBatches;
        if (batches.Count > 0)
        {
            try
            {
                translatedBatches = await Translator.TranslateBatches(batches, apiKey, languageCode, model);
                Console.WriteLine("Translation of batches completed");
            }
            catch (Exception ex)
            {
                LogFailedTranslation(outputFilePath, languageCode); // Pass languageCode here
                Console.WriteLine($"Translation failed for {jsonFilePath}: {ex.Message}");
                File.Copy(jsonFilePath, outputFilePath, true);
                return;
            }
        }
        else
        {
            translatedBatches = new List<Dictionary<string, string>>();
            Console.WriteLine("No batches to translate");
        }

        try
        {
            JsonTranslator.ReconstructJson(jsonFilePath, entries, translatedBatches, outputFilePath);
            Console.WriteLine($"Reconstructed and saved translated JSON file to {outputFilePath}");
        }
        catch (Exception ex)
        {
            LogFailedTranslation(jsonFilePath, languageCode); // Pass languageCode here
            Console.WriteLine($"Reconstruction failed for {jsonFilePath}: {ex.Message}");
            File.Copy(jsonFilePath, outputFilePath, true);
        }
    }

    private static async Task RetryTranslations(string apiKey, string model)
    {
        var failedTranslations = File.ReadAllLines("failed_translations.log").ToList();
        var remainingFailures = new List<string>();

        if (failedTranslations.Count <= 0)
        {
            Console.WriteLine("There are no files that failed translation.");
            return;
        }
        Console.WriteLine("Starting retry process for failed translations...");

        foreach (var entry in failedTranslations)
        {
            var parts = entry.Split('|');
            if (parts.Length != 2) continue;

            var filePath = parts[0];
            var languageCode = parts[1];

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"File not found: {filePath}");
                continue;
            }

            Console.WriteLine($"Retrying translation for file: {filePath}");

            try
            {
                Dictionary<string, string> entries;
                var extension = Path.GetExtension(filePath).ToLower();

                if (extension == ".resx")
                {
                    entries = ResxTranslator.ParseResx(filePath);
                    Console.WriteLine($"Parsed {entries.Count} entries from {filePath}");
                }
                else if (extension == ".json")
                {
                    entries = JsonTranslator.ParseJson(filePath);
                    Console.WriteLine($"Parsed {entries.Count} entries from {filePath}");
                }
                else
                {
                    Console.WriteLine($"Unsupported file type: {filePath}");
                    continue; // Skip unsupported file types
                }

                var batches = Batching.BatchEntries(entries);
                Console.WriteLine($"Created {batches.Count} batches for translation");

                List<Dictionary<string, string>> translatedBatches;
                if (batches.Count > 0)
                {
                    try
                    {
                        translatedBatches = await Translator.TranslateBatches(batches, apiKey, languageCode, model);
                        Console.WriteLine("Translation of batches completed");

                        if (extension == ".resx")
                        {
                            ResxReconstructor.ReconstructResx(filePath, entries, translatedBatches, filePath);
                            Console.WriteLine($"Reconstructed and saved translated .resx file to {filePath}");
                        }
                        else if (extension == ".json")
                        {
                            JsonTranslator.ReconstructJson(filePath, entries, translatedBatches, filePath);
                            Console.WriteLine($"Reconstructed and saved translated JSON file to {filePath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Translation failed for {filePath}: {ex.Message}");
                        remainingFailures.Add(entry);
                    }
                }
                else
                {
                    Console.WriteLine($"No batches to translate for {filePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing {filePath}: {ex.Message}");
                remainingFailures.Add(entry);
            }
        }

        File.WriteAllLines("failed_translations.log", remainingFailures);

        // Reuse LogAllFailedTranslations to log any remaining failures
        LogAllFailedTranslations();
    }


    private static void LogFailedTranslation(string filePath, string languageCode)
    {
        string logFilePath = "failed_translations.log";
        using (StreamWriter writer = new StreamWriter(logFilePath, true))
        {
            writer.WriteLine($"{filePath}|{languageCode}");
        }
    }

    private static void LogAllFailedTranslations()
    {
        string logFilePath = "failed_translations.log";
        if (File.Exists(logFilePath))
        {
            var failedTranslations = File.ReadAllLines(logFilePath).ToList();
            if (failedTranslations.Count > 0)
            {
                Console.WriteLine("\nThe following files failed to translate:");
                foreach (var entry in failedTranslations)
                {
                    var parts = entry.Split('|');
                    if (parts.Length >= 1)
                    {
                        Console.WriteLine(parts[0]);
                    }
                }
                Console.WriteLine("\nTo attempt translating the failed files run 'dotnet run --retry'");
            }
        }
    }

    private static string ReplaceLanguageCodeInFilename(string filename, string newLanguageCode)
    {
        if (filename == null)
        {
            throw new ArgumentNullException(nameof(filename), "Filename cannot be null");
        }
        string pattern = @"\.[a-z]{2}\.json$";
        if (Regex.IsMatch(filename, pattern))
        {
            return Regex.Replace(filename, pattern, $".{newLanguageCode}.json");
        }
        else
        {
            string directory = Path.GetDirectoryName(filename) ?? string.Empty;
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
            string newFilename = $"{nameWithoutExtension}.{newLanguageCode}.json";
            return Path.Combine(directory ?? string.Empty, newFilename);
        }
    }

}
