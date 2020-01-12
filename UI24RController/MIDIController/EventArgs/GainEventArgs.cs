using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.MIDIController
{
    public class GainEventArgs : EventArgs
    {
        public GainEventArgs(int channelNumber, int gainDirection): base()
        {
            ChannelNumber = channelNumber;
            GainDirection = gainDirection;
        }
        public int ChannelNumber { get; internal set; }
        /// <summary>
        /// it can be 1 or -1 (increese or decreese)
        /// </summary>
        public int GainDirection { get; internal set; }
    }
}
