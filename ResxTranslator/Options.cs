using CommandLine;

public class Options
{
    [Option('i', "input", Required = false, HelpText = "Input folder or file path.")]
    public string? InputPath { get; set; }

    [Option('l', "language", Required = false, HelpText = "Language code for translation.")]
    public string? LanguageCode { get; set; }

    [Option('k', "apikey", Required = false, HelpText = "OpenAI API key.")]
    public string? ApiKey { get; set; }

    [Option('m', "model", Required = false, HelpText = "AI Model to use.")]
    public string? Model { get; set; }

    [Option("retry", Required = false, HelpText = "Retry translation on failed files.")]
    public bool RetryMode { get; set; }
}
