using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace UI24RController.MIDIController
{
    public class ButtonsID 
    {


        public Dictionary<ButtonsEnum, byte> ButtonsDictionary { get; set; }

        public byte this[ButtonsEnum s]
        {
            get { return ButtonsDictionary.TryGetValue(s, out var v) ? v : (byte)0xFF; }
            set { ButtonsDictionary[s] = value; }
        }

        public ButtonsID()
        {
            ButtonsDictionary = new Dictionary<ButtonsEnum, byte>();
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
