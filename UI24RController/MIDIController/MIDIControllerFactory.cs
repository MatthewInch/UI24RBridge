using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.MIDIController
{
    public abstract class MIDIControllerFactory
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


    }
}
