using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels
{
    public class Mixer
    {
        public bool IsMultitrackRecordingRun { get; set; }

        public string GetStartMTKRecordMessage()
        {
            return "3:::MTK_REC_TOGGLE";
        }

        public string GetStopMTKRecordMessage()
        {
            return "3:::MTK_REC_TOGGLE";
        }

        public string GetStartRecordMessage()
        {
            return "3:::RECTOGGLE";
        }
        public string GetStopRecordMessage()
        {
            return "3:::RECTOGGLE";
        }

    }
}
