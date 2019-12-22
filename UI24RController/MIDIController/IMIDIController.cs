using System;

public interface IMIDIController
{
    Action FaderEvent(int channelNumber, double faderValue);
    bool SetFader(int channelNumber, double faderValue);
}
