using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.MIDIController
{
    public class FaderEventArgs : EventArgs
    {
        public FaderEventArgs(int channelNumber, double faderValue): base()
        {
            ChannelNumber = channelNumber;
            FaderValue = faderValue;
        }
        public int ChannelNumber { get; internal set; }
        public double FaderValue { get; internal set; }
    }
}
