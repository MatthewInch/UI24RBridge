using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.MIDIController
{
    public abstract class MIDIControllerFactory
    {
        public static IMIDIController GetMidiController(string protocol)
        {
            switch (protocol.ToUpper())
            {
                case "HUI":
                    return new MackieHUI();
                case "MC":
                    return new MC();
                default:
                    return new BehringerUniversalMIDI();
            }
        }


    }
}
