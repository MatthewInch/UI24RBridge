using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.MIDIController
{
    public class WheelEventArgs : EventArgs
    {
        public WheelEventArgs(int wheelDirection): base()
        {
            WheelDirection = wheelDirection;
        }
        /// <summary>
        /// it can be 1 or -1 (increese or decreese)
        /// </summary>
        public int WheelDirection { get; internal set; }
    }
}
