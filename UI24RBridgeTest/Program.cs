using Microsoft.Extensions.Configuration;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.IO;
using UI24RController;
using UI24RController.MIDIController;

namespace UI24RBridgeTest
{
    class Program
    {
        private static readonly object balanceLock = new object();
        static void Main(string[] args)
        {
            if (!File.Exists("appsettings.json"))
            {
                CreateAppsettings("appsettings.json");
                return;
            }
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            var configuration = builder.Build();

            var controllersSetting = new List<UI24RController.ControllerSettings>();
            configuration.GetSection("MidiControllers").Bind(controllersSetting);

            
            var address = configuration["UI24R-Url"];
            var midiInputDevice = configuration["MIDI-Input-Name"];
            var midiOutputDevice = configuration["MIDI-Output-Name"];
            var secondaryMidiInputDevice = configuration["MIDI-Input-Name-Second"];
            var secondaryMidiOutputDevice = configuration["MIDI-Output-Name-Second"];
            var primaryIsExtender = configuration["PrimaryIsExtender"] == "true";
            var secondaryIsExtender = configuration["SecondaryIsExtender"] == "true";
            var primaryChannelStart = configuration["PrimaryChannelStart"];
            var secondaryChannelStart = configuration["SecondaryChannelStart"];
            var protocol = configuration["Protocol"];
            var syncID = configuration["SyncID"];
            var viewDebugMessage = configuration["DebugMessages"] == "true";
            var recButtonBahavior = configuration["DefaultRecButton"];
            var channelRecButtonBahavior = configuration["DefaultChannelRecButton"];
            var auxButtonBehavior = configuration["AuxButtonBehavior"];
            var buttonsValues = configuration["PrimaryButtons"];
            var startBank = configuration["StartBank"];
            var talkBack = configuration["TalkBack"];
            var rtaOnWhenSelect = configuration["RtaOnWhenSelect"] == "true";


            var controller = MIDIControllerFactory.GetMidiController(protocol);
            var controllers = new List<IMIDIController>();
            IMIDIController controllerSecond = null;
            if (!string.IsNullOrEmpty(secondaryMidiInputDevice))
            {
                controllerSecond = MIDIControllerFactory.GetMidiController(protocol);
            }
            
            if (args.Length > 0)
                WriteMIDIDeviceNames(controller);
            else
            {
                Console.WriteLine("Create Bridge Object");
                if ((controllersSetting.Count == 0) && ( midiInputDevice == null || midiOutputDevice == null))
                {
                    if (midiInputDevice == null)
                        Console.WriteLine("The input device name is mandantory in the config file. (MIDI-Input-Name)");
                    if (midiOutputDevice == null)
                        Console.WriteLine("The ouput device name is mandantory in the config file. (MIDI-Output-Name)");
                    WriteMIDIDeviceNames(controller);
                    return;
                }
                else
                {
                    if (controllers.Count == 0)
                    {
                        Console.WriteLine("Set controller message event....");
                        controller.MessageReceived += (obj, e) =>
                        {
                            lock (balanceLock)
                            {
                                Console.WriteLine(e.Message);
                            }
                        };

                        Console.WriteLine("Connect input device...");
                        controller.ConnectInputDevice(midiInputDevice);
                        Console.WriteLine("Connect output device...");
                        controller.ConnectOutputDevice(midiOutputDevice);
                        controller.IsExtender = primaryIsExtender;
                        controller.ChannelOffset = primaryChannelStart == "0" ? 0 : 1;
                        if  (buttonsValues != null) controller.ButtonsValuesFileName = buttonsValues.ToString();
                        controllers.Add(controller);

                        if (secondaryMidiInputDevice != null && secondaryMidiOutputDevice != null)
                        {

                            Console.WriteLine("Connect secondary input device...");
                            controllerSecond.ConnectInputDevice(secondaryMidiInputDevice);
                            Console.WriteLine("Connect secondary output device...");
                            controllerSecond.ConnectOutputDevice(secondaryMidiOutputDevice);
                            controllerSecond.IsExtender = secondaryIsExtender;
                            controllerSecond.ChannelOffset = secondaryChannelStart == "0" ? 0 : 1;
                            if (buttonsValues != null) controller.ButtonsValuesFileName = buttonsValues;
                            controllers.Add(controllerSecond);
                        }
                    }
                    else
                    {
                        foreach (var controllerSetting in controllersSetting)
                        {
                            Console.WriteLine("Set controller message event....");
                            controller = MIDIControllerFactory.GetMidiController(protocol);
                            controller.MessageReceived += (obj, e) =>
                            {
                                lock (balanceLock)
                                {
                                    Console.WriteLine(e.Message);
                                }
                            };

                            Console.WriteLine("Connect input device...");
                            controller.ConnectInputDevice(controllerSetting.InputName);
                            Console.WriteLine("Connect output device...");
                            controller.ConnectOutputDevice(controllerSetting.OutputName);
                            controller.IsExtender = controllerSetting.IsExtender;
                            controller.ChannelOffset = controllerSetting.ChannelOffset;
                            if (controllerSetting.PrimaryButtons != null)
                            {
                                controller.ButtonsValuesFileName = controllerSetting.PrimaryButtons;
                            }
                        }

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
                if (buttonsValues != null)
                {
                    controllers.ForEach(controller => controller.ButtonsValuesFileName = buttonsValues);
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

        private static void CreateAppsettings(string fileName)
        {
            var controller = MIDIControllerFactory.GetMidiController("MC");
            var inputDevicenames = controller.GetInputDeviceNames();
            var outputDevicenames = controller.GetOutputDeviceNames();
            
            AnsiConsole.WriteLine("appsettings.json is not found.");
            AnsiConsole.WriteLine("Creating of the configuration file is starting.");
            var address = AnsiConsole.Ask<string>(@"Please write the mixer address (eg: ws:\\192.168.3.12): "); //"UI24R-Url"
            var midiInputDevice = AnsiConsole.Prompt(
                new TextPrompt<string>("Choose primary input device. (It is case sensitive.)")
                .AddChoices(inputDevicenames));   //configuration["MIDI-Input-Name"];

            var midiOutputDevice = AnsiConsole.Prompt(
                new TextPrompt<string>("Choose primary output device. (It is case sensitive.)")
                .AddChoices(outputDevicenames));   //configuration["MIDI-Output-Name"];

            //var primaryIsExtender = configuration["PrimaryIsExtender"] == "true";
            string primaryIsExtender = "false";
            if (AnsiConsole.Prompt(
                new TextPrompt<bool>("Is primary device an extender?")
                    .AddChoice(true)
                    .AddChoice(false)
                    .DefaultValue(false)
                    .WithConverter(choice => choice ? "y" : "n")
                ))
            {
                primaryIsExtender = "true";
            }
            var primaryChannelStart =      // configuration["PrimaryChannelStart"];
                AnsiConsole.Prompt(
                new TextPrompt<string>("Primary controller offset (show 1-8ch: 0 9-16ch: 1")
                .AddChoices(["0", "1"])
                .DefaultValue("0"));
           
            var isAddSecondaryDevice = AnsiConsole.Prompt(
                new TextPrompt<bool>("Do you want to add secondary device?")
                    .AddChoice(true)
                    .AddChoice(false)
                    .DefaultValue(false)
                    .WithConverter(choice => choice ? "y" : "n")
                );

            string secondaryMidiInputDevice = null;
            string secondaryMidiOutputDevice = null;
            string secondaryIsExtender = "false";
            string secondaryChannelStart = "1";
            //var secondaryMidiInputDevice = configuration["MIDI-Input-Name-Second"];
            //var secondaryMidiOutputDevice = configuration["MIDI-Output-Name-Second"]; 
            //var secondaryIsExtender = configuration["SecondaryIsExtender"] == "true";
            if (isAddSecondaryDevice)
            {
                secondaryMidiInputDevice = AnsiConsole.Prompt(
                new TextPrompt<string>("Choose secondary input device. (It is case sensitive.)")
                .AddChoices(inputDevicenames));   //configuration["MIDI-Input-Name"];

                secondaryMidiOutputDevice = AnsiConsole.Prompt(
                    new TextPrompt<string>("Choose secondary output device. (It is case sensitive.)")
                    .AddChoices(outputDevicenames));   //configuration["MIDI-Output-Name"];

                if (AnsiConsole.Prompt(
                    new TextPrompt<bool>("Is secondary device an extender?")
                        .AddChoice(true)
                        .AddChoice(false)
                        .DefaultValue(false)
                        .WithConverter(choice => choice ? "y" : "n")
                    ))
                {
                    secondaryIsExtender = "true";
                }

                secondaryChannelStart =      // configuration["SecondaryChannelStart"];
               AnsiConsole.Prompt(
                   new TextPrompt<string>("Secondary controller offset (show 1-8ch: 0 9-16ch: 1")
                   .AddChoices(["0", "1"])
                   .DefaultValue("1"));
            }



            var protocol = "MC"; // configuration["Protocol"];
            var syncID = "SYNC_ID"; //configuration["SyncID"];
            var viewDebugMessage = "false"; // configuration["DebugMessages"] == "true";
            var recButtonBahavior = "2TrackAndMTK"; //configuration["DefaultRecButton"];
            var channelRecButtonBahavior = "rec"; //configuration["DefaultChannelRecButton"];
            var auxButtonBehavior = "Release";// configuration["AuxButtonBehavior"];
            var buttonsValues = "ButtonsDefault.json"; // configuration["PrimaryButtons"];
            //var startBank = configuration["StartBank"];
            var talkBack = "20"; // configuration["TalkBack"];
            var rtaOnWhenSelect = "true"; // configuration["RtaOnWhenSelect"] == "true";

            string settingsContent = $@"{{
  ""UI24R-Url"": ""{address}"",
  ""MIDI-Input-Name"": ""{midiInputDevice}"", //Behringer BCF 2000: ""BCF2000""
  ""MIDI-Output-Name"": ""{midiOutputDevice}"", //Behringer BCF 2000: ""BCF2000""
  ""MIDI-Input-Name-Second"": ""{secondaryMidiInputDevice}"", //Behringer BCF 2000: ""BCF2000""
  ""MIDI-Output-Name-Second"": ""{secondaryMidiOutputDevice}"", //Behringer BCF 2000: ""BCF2000""
  ""PrimaryIsExtender"": ""{primaryIsExtender}"",
  ""SecondaryIsExtender"": ""{secondaryIsExtender}"",
  ""PrimaryChannelStart"": ""{primaryChannelStart}"", //0: 1-8ch, 1: 9-16
  ""SecondaryChannelStart"": ""{secondaryChannelStart}"", //0: 1-8ch, 1: 9-16
  ""Protocol"": ""MC"",
  ""SyncID"": ""{syncID}"",
  ""DefaultRecButton"": ""{recButtonBahavior}"", //possible values: ""onlyMTK"", ""only2Track"", ""2TrackAndMTK""; default is ""2TrackAndMTK
  ""DefaultChannelRecButton"": ""{channelRecButtonBahavior}"", //possible values: ""phantom"", ""rec""; default is ""rec
  ""DebugMessages"": ""false"",
  ""AuxButtonBehavior"": ""{auxButtonBehavior}"", //possible values: ""Release"", ""Lock""; Default is ""Release""
  ""PrimaryButtons"": ""{buttonsValues}"",
  ""TalkBack"": ""{talkBack}"", //use scrub button. if it is uncommented it unmute the channel (number in value) if button is release the channel will mute
  ""RtaOnWhenSelect"" : ""{rtaOnWhenSelect}"" //set RTA on channel when select the channel on the controller
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
