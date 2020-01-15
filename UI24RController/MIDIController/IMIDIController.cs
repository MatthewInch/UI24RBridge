﻿using System;
using UI24RController.MIDIController;

public interface IMIDIController
{
    #region Connection
    string[] GetInputDeviceNames();
    string[] GetOutputDeviceNames();
    bool ConnectInputDevice(string deviceName);
    bool ConnectOutputDevice(string deviceName);
    #endregion

    event EventHandler<MessageEventArgs> MessageReceived;
    event EventHandler<FaderEventArgs> FaderEvent;
    event EventHandler<GainEventArgs> GainEvent;
    event EventHandler<ChannelEventArgs> SelectChannelEvent;
    event EventHandler<ChannelEventArgs> MuteChannelEvent;

    #region Button Events
    event EventHandler<EventArgs> PresetUp;
    event EventHandler<EventArgs> PresetDown;

    event EventHandler<EventArgs> SaveEvent;
    event EventHandler<EventArgs> UndoEvent;
    event EventHandler<EventArgs> CancelEvent;
    event EventHandler<EventArgs> EnterEvent;

    event EventHandler<EventArgs> UpEvent;
    event EventHandler<EventArgs> DownEvent;
    event EventHandler<EventArgs> LeftEvent;
    event EventHandler<EventArgs> RightEvent;
    event EventHandler<EventArgs> CenterEvent;

    #endregion

    bool SetFader(int channelNumber, double faderValue);
    bool SetGainLed(int channelNumber, double gainValue);

    void SetSelectLed(int channelNumber, bool turnOn);
    void SetMuteLed(int channelNumber, bool turnOn);

    public void WriteTextToChannelLCD(int channelNumber, string text);
    public void WriteTextToLCD(string text);

}
