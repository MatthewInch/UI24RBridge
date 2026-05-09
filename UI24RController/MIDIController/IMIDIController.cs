using System;
using System.Collections.Generic;
using UI24RController;
using UI24RController.MIDIController;
using System.Threading.Tasks;

public interface IMIDIController : IDisposable
{
    #region Connection
    string[] GetInputDeviceNames();
    string[] GetOutputDeviceNames();
    Task<bool> ConnectInputDevice(string deviceName);
    Task<bool> ConnectOutputDevice(string deviceName);

    Task<bool> ReConnectDevice();

    bool IsConnectionErrorOccured { get; }
    bool IsConnected { get; }

    event EventHandler<EventArgs> ConnectionErrorEvent;

    #endregion

    event EventHandler<MessageEventArgs> MessageReceived;
    bool IsExtender { get; set; }
    int ChannelOffset { get; set; }
    string ButtonsFileName { set; }

    #region Button Events
    event EventHandler<FaderEventArgs> FaderEvent;
    event EventHandler<KnobEventArgs> KnobEvent;
    event EventHandler<WheelEventArgs> WheelEvent;
    event EventHandler<ChannelEventArgs> SelectChannelEvent;
    event EventHandler<ChannelEventArgs> MuteChannelEvent;
    event EventHandler<ChannelEventArgs> SoloChannelEvent;
    event EventHandler<ChannelEventArgs> RecChannelEvent;

    event EventHandler<EventArgs> GainModeEvent;
    event EventHandler<EventArgs> PanEvent;

    event EventHandler<EventArgs> TapTempoEvent;
    event EventHandler<EventArgs> SaveUserLayerEvent;

    event EventHandler<FunctionEventArgs> SetUserChannelEvent;

    event EventHandler<FunctionEventArgs> AuxButtonEvent;
    event EventHandler<FunctionEventArgs> FxButtonEvent;
    event EventHandler<FunctionEventArgs> MuteGroupButtonEvent;
    event EventHandler<FunctionEventArgs> ViewGroupButtonEvent;
    event EventHandler<FunctionEventArgs> MastersBankButtonEvent;

    event EventHandler<EventArgs> MuteAllEvent;
    event EventHandler<EventArgs> MuteFXEvent;
    event EventHandler<EventArgs> ClearMuteEvent;
    event EventHandler<EventArgs> ClearSoloEvent;

    event EventHandler<EventArgs> PrevEvent;
    event EventHandler<EventArgs> NextEvent;
    event EventHandler<EventArgs> StopEvent;
    event EventHandler<EventArgs> PlayEvent;
    event EventHandler<EventArgs> RecEvent;

    event EventHandler<EventArgs> LayerUp;
    event EventHandler<EventArgs> LayerDown;
    event EventHandler<EventArgs> BankUp;
    event EventHandler<EventArgs> BankDown;

    event EventHandler<ButtonEventArgs> TalkbackEvent;

    #endregion

    //ButtonsID _buttonsID { get; set; }

    bool SetFader(int channelNumber, double faderValue);
    bool SetKnobLed(int channelNumber, double knobValue);

    void SetSelectLed(int channelNumber, bool turnOn);
    void SetMuteLed(int channelNumber, bool turnOn);
    void SetSoloLed(int channelNumber, bool turnOn);
    void SetRecLed(int channelNumber, bool turnOn);
    void SetLed(ButtonsEnum buttonName, bool turnOn);
    void SetChannelStripColour(int channelNumber, ChannelStripColour colour);

    public void WriteTextToChannelLCD(int channelNumber, string text, int line);
    public void WriteDefaultTextToChannelLCDFirstLine(int channelNumber, string text);
    public void WriteTemporaryTextToChannelLCDFirstLine(int channelNumber, string text, int seconds);
    public void WriteDefaultTextToChannelLCDSecondLine(int channelNumber, string text);
    public void WriteTemporaryTextToChannelLCDSecondLine(int channelNumber, string text, int seconds);
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

    void InitializeController();

    //Dictionary<ButtonsEnum, byte> GetButtonsValues(string fileName);

}
