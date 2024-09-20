using System;
using System.Collections.Generic;
using UI24RController;
using UI24RController.MIDIController;

public interface IMIDIController
{
    #region Connection
    string[] GetInputDeviceNames();
    string[] GetOutputDeviceNames();
    bool ConnectInputDevice(string deviceName);
    bool ConnectOutputDevice(string deviceName);

    bool ReConnectDevice();

    bool IsConnectionErrorOccured { get; }
    bool IsConnected { get; }

    event EventHandler<EventArgs> ConnectionErrorEvent;

    #endregion

    event EventHandler<MessageEventArgs> MessageReceived;

    #region Button Events
    event EventHandler<FaderEventArgs> FaderEvent;
    event EventHandler<KnobEventArgs> KnobEvent;
    event EventHandler<WheelEventArgs> WheelEvent;
    event EventHandler<ChannelEventArgs> SelectChannelEvent;
    event EventHandler<ChannelEventArgs> MuteChannelEvent;
    event EventHandler<ChannelEventArgs> SoloChannelEvent;
    event EventHandler<ChannelEventArgs> RecChannelEvent;

    event EventHandler<EventArgs> TrackEvent;
    event EventHandler<EventArgs> PanEvent;
    event EventHandler<EventArgs> EqEvent;
    event EventHandler<EventArgs> SendEvent;
    event EventHandler<EventArgs> PlugInEvent;
    event EventHandler<EventArgs> InstrEvent;

    event EventHandler<EventArgs> DisplayBtnEvent;
    event EventHandler<EventArgs> SmtpeBeatsBtnEvent;

    event EventHandler<EventArgs> GlobalViewEvent;

    event EventHandler<EventArgs> MidiTracksEvent;
    event EventHandler<EventArgs> InputsEvent;
    event EventHandler<EventArgs> AudioTracksEvent;
    event EventHandler<EventArgs> AudioInstEvent;
    event EventHandler<EventArgs> AuxBtnEvent;
    event EventHandler<EventArgs> BusesBtnEvent;
    event EventHandler<EventArgs> OutputsEvent;
    event EventHandler<FunctionEventArgs> UserBtnEvent;

    event EventHandler<FunctionEventArgs> AuxButtonEvent;       //F1-F8
    event EventHandler<FunctionEventArgs> FxButtonEvent;        //Modify buttons
    event EventHandler<FunctionEventArgs> MuteGroupButtonEvent; //Automation buttons

    event EventHandler<EventArgs> SaveEvent;
    event EventHandler<EventArgs> UndoEvent;
    event EventHandler<EventArgs> CancelEvent;
    event EventHandler<EventArgs> EnterEvent;

    event EventHandler<EventArgs> MarkerEvent;
    event EventHandler<EventArgs> NudgeEvent;
    event EventHandler<EventArgs> CycleEvent;
    event EventHandler<EventArgs> DropEvent;
    event EventHandler<EventArgs> ReplaceEvent;
    event EventHandler<EventArgs> ClickEvent;
    event EventHandler<EventArgs> SoloEvent;

    event EventHandler<EventArgs> PrevEvent;
    event EventHandler<EventArgs> NextEvent;
    event EventHandler<EventArgs> StopEvent;
    event EventHandler<EventArgs> PlayEvent;
    event EventHandler<EventArgs> RecEvent;

    event EventHandler<EventArgs> LayerUp;
    event EventHandler<EventArgs> LayerDown;
    event EventHandler<EventArgs> BankUp;
    event EventHandler<EventArgs> BankDown;

    event EventHandler<EventArgs> UpEvent;
    event EventHandler<EventArgs> DownEvent;
    event EventHandler<EventArgs> LeftEvent;
    event EventHandler<EventArgs> RightEvent;
    event EventHandler<EventArgs> CenterEvent;

    event EventHandler<ButtonEventArgs> ScrubEvent;

    #endregion

    //ButtonsID _buttonsID { get; set; }

    bool SetFader(int channelNumber, double faderValue);
    bool SetKnobLed(int channelNumber, double knobValue);

    void SetSelectLed(int channelNumber, bool turnOn);
    void SetMuteLed(int channelNumber, bool turnOn);
    void SetSoloLed(int channelNumber, bool turnOn);
    void SetRecLed(int channelNumber, bool turnOn);
    void SetLed(ButtonsEnum buttonName, bool turnOn);

    public void WriteTextToChannelLCD(int channelNumber, string text, int line);
    public void WriteTextToChannelLCDFirstLine(int channelNumber, string text);
    public void WriteTextToChannelLCDSecondLine(int channelNumber, string text);
    public void WriteTextToLCDSecondLine(string text);
    public void WriteTextToLCDSecondLine(string text, int delay);

    public void WriteTextToAssignmentDisplay(string text);
    public void WriteTextToMainDisplay(string text, int position, int maxChar = 1);
    public void WriteTextToBarsDisplay(string text);
    public void WriteTextToBeatsDisplay(string text);
    public void WriteTextToSubDivisionDisplay(string text);
    public void WriteTextToTicksDisplay(string text);

    void WriteChannelMeter(int channelNumber, byte value);
    void TurnOffClipLed(int channelNumber);

    void InitializeController(IControllerSettings settings=null);
}
