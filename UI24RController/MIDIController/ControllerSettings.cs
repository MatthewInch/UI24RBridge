using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.MIDIController
{
    public class ControllerSettings : IControllerSettings
    {
        public bool IsExtender { get; set; }

        public ControllerSettings(bool isExtender)
        {
            this.IsExtender = isExtender;
        }
    }
}
