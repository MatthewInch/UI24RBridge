using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.MIDIController
{
    public class MessageEventArgs : EventArgs
    {
        public MessageEventArgs(string message)
        {
            Message = message;
        }
        public string Message { get; set; }
    }
}
