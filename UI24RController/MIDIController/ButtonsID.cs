using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

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

        public Dictionary<ButtonsEnum, byte> ButtonsDictionary { get; set; }

        public byte this[ButtonsEnum s]
        {
            get { return ButtonsDictionary[s]; }
            set { ButtonsDictionary[s] = value; }
        }

        protected ButtonsID() : base()
        {
            ButtonsDictionary = new Dictionary<ButtonsEnum, byte>();
            //It will be configurable 
            ButtonsDictionary.Add(ButtonsEnum.Track, 0x28);
            ButtonsDictionary.Add(ButtonsEnum.Pan, 0x2a);
            ButtonsDictionary.Add(ButtonsEnum.Eq, 0x2c);
            ButtonsDictionary.Add(ButtonsEnum.Send, 0x29);
            ButtonsDictionary.Add(ButtonsEnum.PlugIn, 0x2b);
            ButtonsDictionary.Add(ButtonsEnum.Instr, 0x2d);
            ButtonsDictionary.Add(ButtonsEnum.Display, 0x34);
            ButtonsDictionary.Add(ButtonsEnum.Smtpe, 0x35);

            ButtonsDictionary.Add(ButtonsEnum.GlobalView, 0x33);

            ButtonsDictionary.Add(ButtonsEnum.MidiTracks, 0x3e);
            ButtonsDictionary.Add(ButtonsEnum.Inputs, 0x3f);
            ButtonsDictionary.Add(ButtonsEnum.AudioTracks, 0x40);
            ButtonsDictionary.Add(ButtonsEnum.AudioInst, 0x41);
            ButtonsDictionary.Add(ButtonsEnum.AuxBtn, 0x42);
            ButtonsDictionary.Add(ButtonsEnum.BusesBtn, 0x43);
            ButtonsDictionary.Add(ButtonsEnum.Outputs, 0x44);
            ButtonsDictionary.Add(ButtonsEnum.User, 0x45);

            ButtonsDictionary.Add(ButtonsEnum.Aux1, 0x36); //use for aux1
            ButtonsDictionary.Add(ButtonsEnum.Aux2, 0x37);
            ButtonsDictionary.Add(ButtonsEnum.Aux3, 0x38);
            ButtonsDictionary.Add(ButtonsEnum.Aux4, 0x39);
            ButtonsDictionary.Add(ButtonsEnum.Aux5, 0x3a);
            ButtonsDictionary.Add(ButtonsEnum.Aux6, 0x3b);
            ButtonsDictionary.Add(ButtonsEnum.Aux7, 0x3c);
            ButtonsDictionary.Add(ButtonsEnum.Aux8, 0x3d); //use for aux8
            ButtonsDictionary.Add(ButtonsEnum.Fx1, 0x46);
            ButtonsDictionary.Add(ButtonsEnum.Fx2, 0x47);
            ButtonsDictionary.Add(ButtonsEnum.Fx3, 0x48);
            ButtonsDictionary.Add(ButtonsEnum.Fx4, 0x49);


            ButtonsDictionary.Add(ButtonsEnum.MuteGroup1, 0x4A);
            ButtonsDictionary.Add(ButtonsEnum.MuteGroup2, 0x4B);
            ButtonsDictionary.Add(ButtonsEnum.MuteGroup3, 0x4C);
            ButtonsDictionary.Add(ButtonsEnum.MuteGroup4, 0x4D);
            ButtonsDictionary.Add(ButtonsEnum.MuteGroup5, 0x4E);
            ButtonsDictionary.Add(ButtonsEnum.MuteGroup6, 0x4F);

            ButtonsDictionary.Add(ButtonsEnum.Save, 0x50);
            ButtonsDictionary.Add(ButtonsEnum.Undo, 0x51);
            ButtonsDictionary.Add(ButtonsEnum.Cancel, 0x52);
            ButtonsDictionary.Add(ButtonsEnum.Enter, 0x53);

            ButtonsDictionary.Add(ButtonsEnum.Marker, 0x54);
            ButtonsDictionary.Add(ButtonsEnum.Nudge, 0x55);
            ButtonsDictionary.Add(ButtonsEnum.Cycle, 0x56);
            ButtonsDictionary.Add(ButtonsEnum.Drop, 0x57);
            ButtonsDictionary.Add(ButtonsEnum.Replace, 0x58);
            ButtonsDictionary.Add(ButtonsEnum.Click, 0x59);
            ButtonsDictionary.Add(ButtonsEnum.Solo, 0x5a);

            ButtonsDictionary.Add(ButtonsEnum.PlayPrev, 0x5b);
            ButtonsDictionary.Add(ButtonsEnum.PlayNext, 0x5c);
            ButtonsDictionary.Add(ButtonsEnum.Stop, 0x5d);
            ButtonsDictionary.Add(ButtonsEnum.Play, 0x5e);
            ButtonsDictionary.Add(ButtonsEnum.Rec, 0x5f);

            ButtonsDictionary.Add(ButtonsEnum.FaderBankDown, 0x2e);
            ButtonsDictionary.Add(ButtonsEnum.FaderBankUp, 0x2f);
            ButtonsDictionary.Add(ButtonsEnum.ChannelDown, 0x30);
            ButtonsDictionary.Add(ButtonsEnum.ChannelUp, 0x31);

            ButtonsDictionary.Add(ButtonsEnum.Up, 0x60);
            ButtonsDictionary.Add(ButtonsEnum.Down, 0x61);
            ButtonsDictionary.Add(ButtonsEnum.Left, 0x62);
            ButtonsDictionary.Add(ButtonsEnum.Right, 0x63);
            ButtonsDictionary.Add(ButtonsEnum.Center, 0x64);

            ButtonsDictionary.Add(ButtonsEnum.Scrub, 0x65);
        }

        public Dictionary<ButtonsEnum, byte> GetButtonsDictionary()
        {
            return ButtonsDictionary;
        }




        public (bool isFX, int fxNum) GetFxButton(byte value)
        {
            if (value == ButtonsDictionary[ButtonsEnum.Fx1])
            {
                return (true, 0);
            }
            else if (value == ButtonsDictionary[ButtonsEnum.Fx2])
            {
                return (true, 1);
            }
            else if (value == ButtonsDictionary[ButtonsEnum.Fx3])
            {
                return (true, 2);
            }
            else if (value == ButtonsDictionary[ButtonsEnum.Fx4])
            {
                return (true, 3);
            }
            return (false, 0);
        }

        public (bool isAux, int auxNum) GetAuxButton(Byte value)
        {
            if (value == ButtonsDictionary[ButtonsEnum.Aux1])
            {
                return (true, 0);
            }
            else if (value == ButtonsDictionary[ButtonsEnum.Aux2])
            {
                return (true, 1);
            }
            else if (value == ButtonsDictionary[ButtonsEnum.Aux3])
            {
                return (true, 2);
            }
            else if (value == ButtonsDictionary[ButtonsEnum.Aux4])
            {
                return (true, 3);
            }
            else if (value == ButtonsDictionary[ButtonsEnum.Aux5])
            {
                return (true, 4);
            }
            else if (value == ButtonsDictionary[ButtonsEnum.Aux6])
            {
                return (true, 5);
            }
            else if (value == ButtonsDictionary[ButtonsEnum.Aux7])
            {
                return (true, 6);
            }
            else if (value == ButtonsDictionary[ButtonsEnum.Aux8])
            {
                return (true, 7);
            }
            return (false, 0);
        }


        public (bool isMuteGroupButton, int muteGroupNum) GetMuteGroupsButton(byte value)
        {
            if (value == ButtonsDictionary[ButtonsEnum.MuteGroup1])
            {
                return (true, 0);
            }
            else if (value == ButtonsDictionary[ButtonsEnum.MuteGroup2])
            {
                return (true, 1);
            }
            else if (value == ButtonsDictionary[ButtonsEnum.MuteGroup3])
            {
                return (true, 2);
            }
            else if (value == ButtonsDictionary[ButtonsEnum.MuteGroup4])
            {
                return (true, 3);
            }
            else if (value == ButtonsDictionary[ButtonsEnum.MuteGroup5])
            {
                return (true, 4);
            }
            else if (value == ButtonsDictionary[ButtonsEnum.MuteGroup6])
            {
                return (true, 5);
            }
            return (false, 0);
        }
    }
}
