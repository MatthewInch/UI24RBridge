using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.MIDIController
{
    public class ButtonsID 
    {
        
        protected Dictionary<ButtonsEnum, byte> _buttonsDictionary;

        public byte this[ButtonsEnum s]
        {
            get { return _buttonsDictionary[s]; }
            set { _buttonsDictionary[s] = value; }
        }

        public ButtonsID() : base()
        {
            _buttonsDictionary = new Dictionary<ButtonsEnum, byte>();
            //It will be configurable 
            _buttonsDictionary.Add(ButtonsEnum.PlayPrev, 0x5b);
            _buttonsDictionary.Add(ButtonsEnum.PlayNext, 0x5c);
            _buttonsDictionary.Add(ButtonsEnum.Play, 0x5e);
            _buttonsDictionary.Add(ButtonsEnum.Rec, 0x5f);
            _buttonsDictionary.Add(ButtonsEnum.Stop, 0x5d);
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
    }
}
