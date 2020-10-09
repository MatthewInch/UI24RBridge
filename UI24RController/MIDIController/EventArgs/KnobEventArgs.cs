using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.MIDIController
{
    public class KnobEventArgs : EventArgs
    {
        public KnobEventArgs(int channelNumber, int knobDirection): base()
        {
            ChannelNumber = channelNumber;
            KnobDirection = knobDirection;
        }
        public int ChannelNumber { get; internal set; }
        /// <summary>
        /// it can be 1 or -1 (increese or decreese)
        /// </summary>
        public int KnobDirection { get; internal set; }
    }
}
