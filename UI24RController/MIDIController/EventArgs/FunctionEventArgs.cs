using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.MIDIController
{
    public class FunctionEventArgs : EventArgs
    {
        public FunctionEventArgs(int functionNumber, bool isPress): base()
        {
            FunctionButton = functionNumber;
            IsPress = isPress;
        }
        public int FunctionButton { get; internal set; }
        public bool IsPress { get; internal set; }
    }
}
