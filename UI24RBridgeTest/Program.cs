using Microsoft.Extensions.Configuration;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UI24RController;
using UI24RController.MIDIController;

namespace UI24RBridgeTest
{
    class Program
    {
        private const int CurrentConfigVersion = 2;
        private static readonly object balanceLock = new object();

        static void Main(string[] args)
        {
            if (!File.Exists("appsettings.json"))
            {
                CreateAppsettings("appsettings.json");
                return;
            }

            if (!CheckConfigVersion("appsettings.json"))
                return;

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            var configuration = builder.Build();

            var controllersSetting = new List<UI24RController.ControllerSettings>();
            configuration.GetSection("MidiControllers").Bind(controllersSetting);

            var address = configuration["UI24R-Url"];
            var protocol = configuration["Protocol"];
            var syncID = configuration["SyncID"];
            var viewDebugMessage = configuration["DebugMessages"] == "true";
            var recButtonBahavior = configuration["DefaultRecButton"];
            var channelRecButtonBahavior = configuration["DefaultChannelRecButton"];
            var auxButtonBehavior = configuration["AuxButtonBehavior"];
            var startBank = configuration["StartBank"];
            var talkBack = configuration["TalkBack"];
            var rtaOnWhenSelect = configuration["RtaOnWhenSelect"] == "true";
            var enableUserBank = configuration["EnableUserBank"] != "false";

            var controllers = new List<IMIDIController>();

            if (args.Length > 0)
                WriteMIDIDeviceNames(MIDIControllerFactory.GetMidiController(protocol));
            else
            {
                if (controllersSetting.Count == 0)
                {
                    Console.WriteLine("No controllers found in MidiControllers config section.");
                    WriteMIDIDeviceNames(MIDIControllerFactory.GetMidiController(protocol));
                    return;
                }
                else
                {
                    for (int i = 0; i < controllersSetting.Count; i++)
                    {
                        var controllerSetting = controllersSetting[i];
                        Console.Write($"Connecting to controller {i + 1}/{controllersSetting.Count}...");
                        var controller = MIDIControllerFactory.GetMidiController(protocol);

                        if (viewDebugMessage)
                        {
                            controller.MessageReceived += (obj, e) =>
                            {
                                lock (balanceLock)
                                {
                                    Console.WriteLine(e.Message);
                                }
                            };
                        }

                        controller.IsExtender = controllerSetting.IsExtender;
                        controller.ChannelOffset = controllerSetting.ChannelOffset;

                        if (controllerSetting.PrimaryButtonsConfig != null)
                            controller.ButtonsFileName = controllerSetting.PrimaryButtonsConfig;

                        if (!controller.ConnectInputDevice(controllerSetting.InputName).GetAwaiter().GetResult())
                        {
                            Console.WriteLine($"\n  Error: Failed to connect input device '{controllerSetting.InputName}'.");
                            return;
                        }
                        if (!controller.ConnectOutputDevice(controllerSetting.OutputName).GetAwaiter().GetResult())
                        {
                            Console.WriteLine($"\n  Error: Failed to connect output device '{controllerSetting.OutputName}'.");
                            return;
                        }
                        Console.WriteLine(" Success");
                        controllers.Add(controller);
                    }
                }

                Action<string, bool> messageWriter = (string messages, bool isDebugMessage) =>
                 {
                     if (!isDebugMessage || (isDebugMessage && viewDebugMessage))
                     {
                         var m = messages.Split('\n');
                         foreach (var message in m)
                         {
                             if (!message.StartsWith("3:::RTA^") && !message.StartsWith("RTA^") &&
                                 !message.StartsWith("3:::VU2^") && !message.StartsWith("VU2^")
                             )
                             {
                                 lock (balanceLock)
                                 {
                                     Console.WriteLine(message);
                                 }
                             }
                         }
                     }
                 };
                Console.WriteLine("Start bridge...");

                BridgeSettings settings = new BridgeSettings(address, messageWriter);

                if (syncID != null)
                {
                    settings.SyncID = syncID;
                }
                if (recButtonBahavior != null)
                {
                    switch (recButtonBahavior.ToLower())
                    {
                        case "onlymtk":
                            settings.RecButtonBehavior = BridgeSettings.RecButtonBehaviorEnum.OnlyMTK;
                            break;
                        case "only2track":
                            settings.RecButtonBehavior = BridgeSettings.RecButtonBehaviorEnum.OnlyTwoTrack;
                            break;
                        default:
                            settings.RecButtonBehavior = BridgeSettings.RecButtonBehaviorEnum.TwoTrackAndMTK;
                            break;
                    }
                }
                if (channelRecButtonBahavior != null)
                {
                    switch (channelRecButtonBahavior.ToLower())
                    {
                        case "phantom":
                            settings.ChannelRecButtonBehavior = BridgeSettings.ChannelRecButtonBehaviorEnum.Phantom;
                            break;
                        default:
                            settings.ChannelRecButtonBehavior = BridgeSettings.ChannelRecButtonBehaviorEnum.Rec;
                            break;
                    }
                }
                if (auxButtonBehavior != null)
                {
                    switch(auxButtonBehavior.ToLower() )
                    {
                        case "lock":
                            settings.AuxButtonBehavior = BridgeSettings.AuxButtonBehaviorEnum.Lock;
                            break;
                        default:
                            settings.AuxButtonBehavior = BridgeSettings.AuxButtonBehaviorEnum.Release;
                            break;
                    }
                }
                if (startBank != null)
                {
                    settings.StartBank = 0;
                    if (startBank == "1") settings.StartBank = 1;
                    if (startBank == "2") settings.StartBank = 2;
                }
                if (talkBack != null)
                {
                    int talkBackChannel = 0;
                    if  (int.TryParse(talkBack, out talkBackChannel))
                    {
                        settings.TalkBack = talkBackChannel;
                    }

                }

                settings.RtaOnWhenSelect = rtaOnWhenSelect;
                settings.EnableUserBank = enableUserBank;

                Console.WriteLine("Press 'ESC' to exit.");
                bool isExit = false;
                using (UI24RBridge bridge = new UI24RBridge(settings, controllers))
                {
                    while (!isExit)
                    {
                        if (Console.KeyAvailable)
                        {
                            var pressedKey = Console.ReadKey();
                            switch (pressedKey.Key)
                            {
                                case ConsoleKey.Escape:
                                    isExit = true;
                                    break;
                                case ConsoleKey.M:
                                    bridge._midiController_LayerUp(null, new EventArgs());
                                    break;
                                case ConsoleKey.N:
                                    bridge._midiController_LayerDown(null, new EventArgs());
                                    break;
                                case ConsoleKey.K:
                                    bridge._midiController_BankUp(null, new EventArgs());
                                    break;
                                case ConsoleKey.J:
                                    bridge._midiController_BankDown(null, new EventArgs());
                                    break;
                            }
                        }
                    }

                }
            }
        }
        private static bool CheckConfigVersion(string fileName)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(fileName, optional: false, reloadOnChange: false);
            var config = builder.Build();

            if (!int.TryParse(config["ConfigVersion"], out int version) || version < CurrentConfigVersion)
            {
                AnsiConsole.MarkupLine($"[red]appsettings.json is outdated (version {(version == 0 ? "unknown" : version)}, current is {CurrentConfigVersion}).[/]");
                bool recreate = AnsiConsole.Prompt(
                    new TextPrompt<bool>("Do you want to delete it and create a new one?")
                        .AddChoice(true)
                        .AddChoice(false)
                        .DefaultValue(false)
                        .WithConverter(choice => choice ? "y" : "n"));

                if (recreate)
                {
                    File.Delete(fileName);
                    CreateAppsettings(fileName);
                }
                return false;
            }
            return true;
        }

        private static string PromptDeviceChoice(string promptText, IEnumerable<string> deviceNames)
        {
            var nameList = deviceNames.ToList();

            AnsiConsole.WriteLine(promptText);
            for (int i = 0; i < nameList.Count; i++)
                AnsiConsole.MarkupLine($"  [blue][[{i + 1}]][/]: {nameList[i]}");

            int chosen = AnsiConsole.Prompt(
                new TextPrompt<int>("Enter number:")
                    .DefaultValue(1)
                    .Validate(n => n >= 1 && n <= nameList.Count
                        ? ValidationResult.Success()
                        : ValidationResult.Error($"Please enter a number between 1 and {nameList.Count}")));

            return nameList[chosen - 1];
        }

        private static void CreateAppsettings(string fileName)
        {
            var controller = MIDIControllerFactory.GetMidiController("MC");
            var inputDevicenames = controller.GetInputDeviceNames();
            var outputDevicenames = controller.GetOutputDeviceNames();

            AnsiConsole.WriteLine("appsettings.json is not found.");
            AnsiConsole.WriteLine("Creating of the configuration file is starting.");
            var address = AnsiConsole.Ask<string>(@"Please write the mixer address (eg: ws://192.168.3.12): ");

            var controllerEntries = new List<string>();
            bool isFirst = true;
            bool addAnother = true;

            while (addAnother)
            {
                string label = isFirst ? "primary" : "additional";
                var inputDevice = PromptDeviceChoice($"Choose {label} input device:", inputDevicenames);
                var outputDevice = PromptDeviceChoice($"Choose {label} output device:", outputDevicenames);

                bool isExtender = AnsiConsole.Prompt(
                    new TextPrompt<bool>($"Is {label} device an extender?")
                        .AddChoice(true)
                        .AddChoice(false)
                        .DefaultValue(false)
                        .WithConverter(choice => choice ? "y" : "n"));

                var channelOffset = AnsiConsole.Prompt(
                    new TextPrompt<string>($"{char.ToUpper(label[0]) + label[1..]} controller offset (1-8ch: 0, 9-16ch: 1)")
                        .AddChoices(["0", "1"])
                        .DefaultValue(isFirst ? "0" : "1"));

                controllerEntries.Add($@"    {{
      ""InputName"": ""{inputDevice}"",
      ""OutputName"": ""{outputDevice}"",
      ""IsExtender"": {isExtender.ToString().ToLower()},
      ""ChannelOffset"": {channelOffset},
      ""PrimaryButtonsConfig"": ""ButtonsXTouch.json""
    }}");

                isFirst = false;
                addAnother = AnsiConsole.Prompt(
                    new TextPrompt<bool>("Do you want to add another controller?")
                        .AddChoice(true)
                        .AddChoice(false)
                        .DefaultValue(false)
                        .WithConverter(choice => choice ? "y" : "n"));
            }

            string controllersJson = string.Join(",\n", controllerEntries);

            string settingsContent = $@"{{
  ""ConfigVersion"": {CurrentConfigVersion},
  ""UI24R-Url"": ""{address}"",
  ""MidiControllers"": [
{controllersJson}
  ],
  ""Protocol"": ""MC"",
  ""SyncID"": ""SYNC_ID"",
  ""DefaultRecButton"": ""2TrackAndMTK"", //possible values: ""onlyMTK"", ""only2Track"", ""2TrackAndMTK""; default is ""2TrackAndMTK""
  ""DefaultChannelRecButton"": ""rec"", //possible values: ""phantom"", ""rec""; default is ""rec""
  ""DebugMessages"": ""false"",
  ""EnableUserBank"": ""false"",
  ""AuxButtonBehavior"": ""Release"", //possible values: ""Release"", ""Lock""; default is ""Release""
  ""StartBank"": ""0"",
  ""TalkBack"": ""20"", // If it is uncommented it unmute the channel (number in value) if button is release the channel will mute
  ""RtaOnWhenSelect"": ""true"" //set RTA on channel when select the channel on the controller
}}
";

            File.WriteAllText(fileName, settingsContent);
        }

        /// <summary>
        /// Write the MIDI device names to the console. It help to set the config file.
        /// </summary>
        /// <param name="controller"></param>
        private static void WriteMIDIDeviceNames(IMIDIController controller)
        {
            var inputDevicenames = controller.GetInputDeviceNames();
            foreach (var inputDevice in inputDevicenames)
            {
                Console.WriteLine($"Input device name: {inputDevice}");
            }

            var outputDevicenames = controller.GetOutputDeviceNames();
            foreach (var outputDevice in outputDevicenames)
            {
                Console.WriteLine($"Output device name: {outputDevice}");
            }
            Console.WriteLine();
        }
    }
}
