using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.MIDIController
{
    public class ChannelEventArgs : EventArgs
    {
        public ChannelEventArgs(int channelNumber): base()
        {
            ChannelNumber = channelNumber;
        }
        public int ChannelNumber { get; internal set; }
    }
}
