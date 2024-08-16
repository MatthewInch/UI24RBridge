using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.MIDIController
{
    public class ButtonEventArgs : EventArgs
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isPress">true if the button pressed false ir the button released</param>
        public ButtonEventArgs(bool isPress): base()  
        {
            IsPress = isPress;
        }

        /// <summary>
        /// true if the button pressed false ir the button released
        /// </summary>
        public bool IsPress { get; internal set; }
    }
}
