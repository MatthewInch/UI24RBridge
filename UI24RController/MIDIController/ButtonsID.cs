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

            _buttonsDictionary.Add(ButtonsEnum.PlayPrev, 0x5b);
            _buttonsDictionary.Add(ButtonsEnum.PlayNext, 0x5c);
            _buttonsDictionary.Add(ButtonsEnum.Play, 0x5e);
            _buttonsDictionary.Add(ButtonsEnum.Rec, 0x5f);
            _buttonsDictionary.Add(ButtonsEnum.Stop, 0x5d);
            _buttonsDictionary.Add(ButtonsEnum.Scrub, 0x65);
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
    }
}
