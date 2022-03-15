using Microsoft.Extensions.Configuration;
using System;
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
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            var configuration = builder.Build();
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
            
            var controller = MIDIControllerFactory.GetMidiController(protocol);
            IMIDIController controllerSecond = null;
            if (secondaryMidiInputDevice != null)
            {
                controllerSecond = MIDIControllerFactory.GetMidiController(protocol);
            }
            
            if (args.Length > 0)
                WriteMIDIDeviceNames(controller);
            else
            {
                Console.WriteLine("Create Bridge Object");
                if (midiInputDevice == null || midiOutputDevice == null)
                {
                    if (midiInputDevice == null)
                        Console.WriteLine("The input device name is mandantory in the config file. (MIDI-Input-Name)");
                    if (midiOutputDevice == null)
                        Console.WriteLine("The ouput device name is mandantory in the config file. (MIDI-Output-Name)");
                    WriteMIDIDeviceNames(controller);
                }
                else
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

                    if (secondaryMidiInputDevice != null && secondaryMidiOutputDevice != null)
                    {

                        Console.WriteLine("Connect secondary input device...");
                        controllerSecond.ConnectInputDevice(secondaryMidiInputDevice);
                        Console.WriteLine("Connect secondary output device...");
                        controllerSecond.ConnectOutputDevice(secondaryMidiOutputDevice);
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

                BridgeSettings settings = new BridgeSettings(address, controller, messageWriter);
                BridgeSettings settingsSecondary = null;
                if (controllerSecond != null)
                {
                    settingsSecondary = new BridgeSettings(address, controllerSecond, messageWriter);
                    if (secondaryIsExtender)
                    {
                        settingsSecondary.ControllerIsExtender = true;
                    }
                    if (secondaryChannelStart != null)
                    {
                        settingsSecondary.ControllerStartChannel = secondaryChannelStart;
                    }
                }
                if (primaryIsExtender)
                {
                    settings.ControllerIsExtender = true;
                }
               
                if (primaryChannelStart != null)
                {
                    settings.ControllerStartChannel = primaryChannelStart;
                }
                
                if (syncID != null)
                {
                    settings.SyncID = syncID;
                    if ( settingsSecondary != null)
                        settingsSecondary.SyncID = syncID;
                }
                if (recButtonBahavior != null)
                {
                    switch (recButtonBahavior.ToLower())
                    {
                        case "onlymtk":
                            settings.RecButtonBehavior = BridgeSettings.RecButtonBehaviorEnum.OnlyMTK;
                            if (settingsSecondary != null)
                                settingsSecondary.RecButtonBehavior = BridgeSettings.RecButtonBehaviorEnum.OnlyMTK;
                            break;
                        case "only2track":
                            settings.RecButtonBehavior = BridgeSettings.RecButtonBehaviorEnum.OnlyTwoTrack;
                            if (settingsSecondary != null)
                                settingsSecondary.RecButtonBehavior = BridgeSettings.RecButtonBehaviorEnum.OnlyTwoTrack;
                            break;
                        default:
                            settings.RecButtonBehavior = BridgeSettings.RecButtonBehaviorEnum.TwoTrackAndMTK;
                            if (settingsSecondary != null)
                                settingsSecondary.RecButtonBehavior = BridgeSettings.RecButtonBehaviorEnum.TwoTrackAndMTK;
                            break;
                    }
                }
                if (channelRecButtonBahavior != null)
                {
                    switch (channelRecButtonBahavior.ToLower())
                    {
                        case "phantom":
                            settings.ChannelRecButtonBehavior = BridgeSettings.ChannelRecButtonBehaviorEnum.Phantom;
                            if (settingsSecondary != null)
                                settingsSecondary.ChannelRecButtonBehavior = BridgeSettings.ChannelRecButtonBehaviorEnum.Phantom;
                            break;
                        default:
                            settings.ChannelRecButtonBehavior = BridgeSettings.ChannelRecButtonBehaviorEnum.Rec;
                            if (settingsSecondary != null)
                                settingsSecondary.ChannelRecButtonBehavior = BridgeSettings.ChannelRecButtonBehaviorEnum.Rec;
                            break;
                    }
                }
                if (auxButtonBehavior != null)
                {
                    switch(auxButtonBehavior.ToLower() )
                    {
                        case "lock":
                            settings.AuxButtonBehavior = BridgeSettings.AuxButtonBehaviorEnum.Lock;
                            if (settingsSecondary != null)
                                settingsSecondary.AuxButtonBehavior = BridgeSettings.AuxButtonBehaviorEnum.Lock;
                            break;
                        default:
                            settings.AuxButtonBehavior = BridgeSettings.AuxButtonBehaviorEnum.Release;
                            if (settingsSecondary != null)
                                settingsSecondary.AuxButtonBehavior = BridgeSettings.AuxButtonBehaviorEnum.Release;
                            break;
                    }
                }
                if (buttonsValues != null)
                {
                    settings.ButtonsValuesFileName = buttonsValues;
                    if (settingsSecondary != null)
                        settingsSecondary.ButtonsValuesFileName = buttonsValues;
                }
                if (startBank != null)
                {
                    settings.StartBank = 0;
                    if (startBank == "1") settings.StartBank = 1;
                    if (startBank == "2") settings.StartBank = 2;
                }
                UI24RBridge bridgeSecondary = null;
                if (settingsSecondary != null)
                {
                    bridgeSecondary = new UI24RBridge(settingsSecondary);
                }
                Console.WriteLine("Press 'ESC' to exit.");
                bool isExit = false;
                using (UI24RBridge bridge = new UI24RBridge(settings, bridgeSecondary))
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
