// See https://aka.ms/new-console-template for more information
using Spectre.Console;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Configuration;


const String TEXT_TO_SPEECH_OPERATION = "Text to Speech";
const String SPEECH_TO_TEXT_OPERATION = "Speech to text";
const String CONSOLE_SOURCE = "Console";
const String FILE_SOURCE = "File";
const String AUDIO_DESTINATION = "Audio";
const String FILE_DESTINATION = "File";

// Set the voice name, refer to https://aka.ms/speech/voices/neural for full list.
const String SPEECH_SYNTHESIS_VOICE_NAME = "en-US-AvaMultilingualNeural";


var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

// Check if we can accept key strokes
if (!AnsiConsole.Profile.Capabilities.Interactive)
{
    AnsiConsole.MarkupLine("[red]Environment does not support interaction.[/]");
    return;
}

var operation = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select the [green]service[/]:")
                .AddChoices(new[] {
                    TEXT_TO_SPEECH_OPERATION,
                    SPEECH_TO_TEXT_OPERATION
                }));

// Creates an instance of a speech config with specified subscription key and service region.
// Replace with your own subscription key and service region (e.g., "westus").
var speechConfig = SpeechConfig.FromSubscription(
        config["subscriptionKey"], 
        config["region"]
    );

switch (operation)
{
    case TEXT_TO_SPEECH_OPERATION:
        await SynthesisToSpeakerAsync(speechConfig);
        break;
    case SPEECH_TO_TEXT_OPERATION:
        await RecognizeSpeechAsync(speechConfig);
        break;

}

async Task RecognizeSpeechAsync(SpeechConfig config)
{
    using (var recognizer = new SpeechRecognizer(config))
    {
        Write("Say something...");

        // Starts speech recognition, and returns after a single utterance is recognized. The end of a
        // single utterance is determined by listening for silence at the end or until a maximum of 15
        // seconds of audio is processed.  The task returns the recognition text as result. 
        // Note: Since RecognizeOnceAsync() returns only a single utterance, it is suitable only for single
        // shot recognition like command or query. 
        // For long-running multi-utterance recognition, use StartContinuousRecognitionAsync() instead.
        bool finish = false;
        while (!finish)
        {
            var result = await recognizer.RecognizeOnceAsync();

            if (result.Reason == ResultReason.RecognizedSpeech)
            {
                Write($"We recognized: {result.Text}");
            }
            else if (result.Reason == ResultReason.NoMatch)
            {
                Write($"[yellow]NOMATCH: Speech could not be recognized.[/]");
            }
            else if (result.Reason == ResultReason.Canceled)
            {
                finish = true;
                var cancellation = CancellationDetails.FromResult(result);
                Write($"[red]CANCELED:[/] Reason={cancellation.Reason}");

                if (cancellation.Reason == CancellationReason.Error)
                {
                    Write($"[red]CANCELED:[/] ErrorCode={cancellation.ErrorCode}");
                    Write($"[red]CANCELED:[/] ErrorDetails={cancellation.ErrorDetails}");
                    Write($"[red]CANCELED:[/] Did you update the subscription info?");
                }
            }
        }
    }
}

static async Task SynthesisToSpeakerAsync(SpeechConfig config)
{
    // To support Chinese Characters on Windows platform
    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
    {
        Console.InputEncoding = System.Text.Encoding.Unicode;
        Console.OutputEncoding = System.Text.Encoding.Unicode;
    }

    var source = AnsiConsole.Prompt(
    new SelectionPrompt<string>()
        .Title("Select [green]source[/]")
        .AddChoices(new[] {
                    CONSOLE_SOURCE,
                    FILE_SOURCE
        }));

    var destination = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("Select [green]destination[/]")
            .AddChoices(new[] {
                    AUDIO_DESTINATION,
                    FILE_DESTINATION
        }));

    bool fileSource = (source == FILE_SOURCE);

    config.SpeechSynthesisVoiceName = SPEECH_SYNTHESIS_VOICE_NAME;

    AudioConfig audioConfig = AudioConfig.FromDefaultSpeakerOutput();

    if (destination == FILE_DESTINATION)
    {
        var fileToWrite = AnsiConsole.Ask<string>("Which is the [green]path of the file[/] to write?");
        if (File.Exists(fileToWrite))
        {
            Write($"The file [red]{fileToWrite} exits[/]");
            return;
        }

        audioConfig = AudioConfig.FromWavFileOutput(fileToWrite);

    }
    // Creates a speech synthesizer using the default speaker as audio output.
    using (var synthesizer = new SpeechSynthesizer(config, audioConfig))
    {
        if (fileSource)
        {
            var fileToRead = AnsiConsole.Ask<string>("Which is the [green]path of the file[/] to read?");
            if (!File.Exists(fileToRead))
            {
                Write($"The file [red]{fileToRead} doesn't exits[/]");
                return;
            }
            await SynthesisTextToSpeakerAsync(synthesizer, File.ReadAllText(fileToRead));
        }
        else
        {
            // Receive a text from console input and synthesize it to speaker.
            Write("Type some text that you want to speak...");
            Console.Write("> ");
            string? text = Console.ReadLine();

            while (!String.IsNullOrEmpty(text) && text != ":q")
            {
                await SynthesisTextToSpeakerAsync(synthesizer, text);
                Write("Type some text that you want to speak...");
                Console.Write("> ");
                text = Console.ReadLine();
            }
        }
    }
}

static async Task SynthesisTextToSpeakerAsync(SpeechSynthesizer synthesizer, string text)
{
    using (var result = await synthesizer.SpeakTextAsync(text))
    {
        if (result.Reason == ResultReason.SynthesizingAudioCompleted)
        {
            Write($"Speech synthesized to speaker for text {text}");
        }
        else if (result.Reason == ResultReason.Canceled)
        {
            var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
            Write($"[red]CANCELED:[/] Reason={cancellation.Reason}");

            if (cancellation.Reason == CancellationReason.Error)
            {
                Write($"[red]ANCELED[/]: ErrorCode={cancellation.ErrorCode}");
                Write($"[red]ANCELED[/]: ErrorDetails=[{cancellation.ErrorDetails}]");
                Write($"[red]ANCELED[/]: Did you update the subscription info?");
            }
        }
    }
}

static void Write(string msg)
{
    AnsiConsole.MarkupLine(msg);
}