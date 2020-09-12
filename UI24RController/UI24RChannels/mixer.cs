using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels
{
    public class Mixer
    {
        public bool IsMultitrackRecordingRun { get; set; }
        public bool IsTwoTrackRecordingRun { get; set; }

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

        public string Get2TrackPlayMessage()
        {
            return "3:::MEDIA_PLAY";
        }
        public string Get2TrackStopMessage()
        {
            return "3:::MEDIA_STOP";
        }

        public string Get2TrackNextMessage()
        {
            return "3:::MEDIA_NEXT";
        }
        public string Get2TrackPrevMessage()
        {
            return "3:::MEDIA_PREV";
        }

    }
}
