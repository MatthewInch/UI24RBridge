using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.MIDIController
{
    public class ButtonsID 
    {
        //Thread safe Singleton
        private static readonly ButtonsID _instance = new ButtonsID();
        public static ButtonsID Instance 
        { 
            get 
            { 
                return _instance; 
            } 
        }
        
        protected Dictionary<ButtonsEnum, byte> _buttonsDictionary;

        public byte this[ButtonsEnum s]
        {
            get { return _buttonsDictionary[s]; }
            set { _buttonsDictionary[s] = value; }
        }

        protected ButtonsID() : base()
        {
            _buttonsDictionary = new Dictionary<ButtonsEnum, byte>();
            //It will be configurable 
            _buttonsDictionary.Add(ButtonsEnum.Track, 0x28);
            _buttonsDictionary.Add(ButtonsEnum.Pan, 0x2a);
            _buttonsDictionary.Add(ButtonsEnum.Eq, 0x2c);
            _buttonsDictionary.Add(ButtonsEnum.Send, 0x29);
            _buttonsDictionary.Add(ButtonsEnum.PlugIn, 0x2b);
            _buttonsDictionary.Add(ButtonsEnum.Instr, 0x2d);
            _buttonsDictionary.Add(ButtonsEnum.Display, 0x34);
            _buttonsDictionary.Add(ButtonsEnum.Smtpe, 0x35);

            _buttonsDictionary.Add(ButtonsEnum.GlobalView, 0x33);

            _buttonsDictionary.Add(ButtonsEnum.MidiTracks, 0x3e);
            _buttonsDictionary.Add(ButtonsEnum.Inputs, 0x3f);
            _buttonsDictionary.Add(ButtonsEnum.AudioTracks, 0x40);
            _buttonsDictionary.Add(ButtonsEnum.AudioInst, 0x41);
            _buttonsDictionary.Add(ButtonsEnum.AuxBtn, 0x42);
            _buttonsDictionary.Add(ButtonsEnum.BusesBtn, 0x43);
            _buttonsDictionary.Add(ButtonsEnum.Outputs, 0x44);
            _buttonsDictionary.Add(ButtonsEnum.User, 0x45);

            _buttonsDictionary.Add(ButtonsEnum.Aux1, 0x36); //use for aux1
            _buttonsDictionary.Add(ButtonsEnum.Aux2, 0x37);
            _buttonsDictionary.Add(ButtonsEnum.Aux3, 0x38);
            _buttonsDictionary.Add(ButtonsEnum.Aux4, 0x39);
            _buttonsDictionary.Add(ButtonsEnum.Aux5, 0x3a);
            _buttonsDictionary.Add(ButtonsEnum.Aux6, 0x3b);
            _buttonsDictionary.Add(ButtonsEnum.Aux7, 0x3c);
            _buttonsDictionary.Add(ButtonsEnum.Aux8, 0x3d); //use for aux8
            _buttonsDictionary.Add(ButtonsEnum.Fx1, 0x46);
            _buttonsDictionary.Add(ButtonsEnum.Fx2, 0x47);
            _buttonsDictionary.Add(ButtonsEnum.Fx3, 0x48);
            _buttonsDictionary.Add(ButtonsEnum.Fx4, 0x49);


            _buttonsDictionary.Add(ButtonsEnum.MuteGroup1, 0x4A);
            _buttonsDictionary.Add(ButtonsEnum.MuteGroup2, 0x4B);
            _buttonsDictionary.Add(ButtonsEnum.MuteGroup3, 0x4C);
            _buttonsDictionary.Add(ButtonsEnum.MuteGroup4, 0x4D);
            _buttonsDictionary.Add(ButtonsEnum.MuteGroup5, 0x4E);
            _buttonsDictionary.Add(ButtonsEnum.MuteGroup6, 0x4F);

            _buttonsDictionary.Add(ButtonsEnum.Save, 0x50);
            _buttonsDictionary.Add(ButtonsEnum.Undo, 0x51);
            _buttonsDictionary.Add(ButtonsEnum.Cancel, 0x52);
            _buttonsDictionary.Add(ButtonsEnum.Enter, 0x53);

            _buttonsDictionary.Add(ButtonsEnum.Marker, 0x54);
            _buttonsDictionary.Add(ButtonsEnum.Nudge, 0x55);
            _buttonsDictionary.Add(ButtonsEnum.Cycle, 0x56);
            _buttonsDictionary.Add(ButtonsEnum.Drop, 0x57);
            _buttonsDictionary.Add(ButtonsEnum.Replace, 0x58);
            _buttonsDictionary.Add(ButtonsEnum.Click, 0x59);
            _buttonsDictionary.Add(ButtonsEnum.Solo, 0x5a);

            _buttonsDictionary.Add(ButtonsEnum.PlayPrev, 0x5b);
            _buttonsDictionary.Add(ButtonsEnum.PlayNext, 0x5c);
            _buttonsDictionary.Add(ButtonsEnum.Stop, 0x5d);
            _buttonsDictionary.Add(ButtonsEnum.Play, 0x5e);
            _buttonsDictionary.Add(ButtonsEnum.Rec, 0x5f);

            _buttonsDictionary.Add(ButtonsEnum.FaderBankDown, 0x2e);
            _buttonsDictionary.Add(ButtonsEnum.FaderBankUp, 0x2f);
            _buttonsDictionary.Add(ButtonsEnum.ChannelDown, 0x30);
            _buttonsDictionary.Add(ButtonsEnum.ChannelUp, 0x31);

            _buttonsDictionary.Add(ButtonsEnum.Up, 0x60);
            _buttonsDictionary.Add(ButtonsEnum.Down, 0x61);
            _buttonsDictionary.Add(ButtonsEnum.Left, 0x62);
            _buttonsDictionary.Add(ButtonsEnum.Right, 0x63);
            _buttonsDictionary.Add(ButtonsEnum.Center, 0x64);

            _buttonsDictionary.Add(ButtonsEnum.Scrub, 0x65);
        }

        public (bool isFX, int fxNum) GetFxButton(byte value)
        {
            if (value == _buttonsDictionary[ButtonsEnum.Fx1])
            {
                return (true, 0);
            }
            else if (value == _buttonsDictionary[ButtonsEnum.Fx2])
            {
                return (true, 1);
            }
            else if (value == _buttonsDictionary[ButtonsEnum.Fx3])
            {
                return (true, 2);
            }
            else if (value == _buttonsDictionary[ButtonsEnum.Fx4])
            {
                return (true, 3);
            }
            return (false, 0);
        }
        public (bool isMuteGroupButton, int muteGroupNum) GetMuteGroupsButton(byte value)
        {
            if (value == _buttonsDictionary[ButtonsEnum.MuteGroup1])
            {
                return (true, 0);
            }
            else if (value == _buttonsDictionary[ButtonsEnum.MuteGroup2])
            {
                return (true, 1);
            }
            else if (value == _buttonsDictionary[ButtonsEnum.MuteGroup3])
            {
                return (true, 2);
            }
            else if (value == _buttonsDictionary[ButtonsEnum.MuteGroup4])
            {
                return (true, 3);
            }
            else if (value == _buttonsDictionary[ButtonsEnum.MuteGroup5])
            {
                return (true, 4);
            }
            else if (value == _buttonsDictionary[ButtonsEnum.MuteGroup6])
            {
                return (true, 5);
            }
            return (false, 0);
        }
    }
}
