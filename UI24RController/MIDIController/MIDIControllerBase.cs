using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.MIDIController
{
    public abstract class MIDIControllerBase : IMIDIController
    {
        public static IMIDIController GetMidiController(string protocol)
        {
            switch (protocol)
            {
                case "HUI":
                    return new MackieHUI();
                default:
                    return new BehringerUniversalMIDI();
            }
        }

        public event EventHandler<MessageEventArgs> MessageReceived;
        public event EventHandler<FaderEventArgs> FaderEvent;

        public bool ConnectInputDevice(string deviceName)
        {
            throw new NotImplementedException();
        }

        public bool ConnectOutputDevice(string deviceName)
        {
            throw new NotImplementedException();
        }

        public string[] GetInputDeviceNames()
        {
            throw new NotImplementedException();
        }

        public string[] GetOutputDeviceNames()
        {
            throw new NotImplementedException();
        }

        public bool SetFader(int channelNumber, double faderValue)
        {
            throw new NotImplementedException();
        }
    }
}
