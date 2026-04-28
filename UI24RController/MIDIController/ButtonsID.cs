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
            var fxKeys = new[] { ButtonsEnum.Fx1, ButtonsEnum.Fx2, ButtonsEnum.Fx3, ButtonsEnum.Fx4 };
            for (int i = 0; i < fxKeys.Length; i++)
            {
                if (ButtonsDictionary.TryGetValue(fxKeys[i], out var b) && value == b)
                    return (true, i);
            }
            return (false, 0);
        }

        public (bool isAux, int auxNum) GetAuxButton(Byte value)
        {
            var auxKeys = new[] {
                ButtonsEnum.Aux1, ButtonsEnum.Aux2, ButtonsEnum.Aux3, ButtonsEnum.Aux4,
                ButtonsEnum.Aux5, ButtonsEnum.Aux6, ButtonsEnum.Aux7, ButtonsEnum.Aux8,
                ButtonsEnum.Aux9, ButtonsEnum.Aux10
            };
            for (int i = 0; i < auxKeys.Length; i++)
            {
                if (ButtonsDictionary.TryGetValue(auxKeys[i], out var b) && value == b)
                    return (true, i);
            }
            return (false, 0);
        }


        public (bool isMuteGroupButton, int muteGroupNum) GetMuteGroupsButton(byte value)
        {
            var muteKeys = new[] {
                ButtonsEnum.MuteGroup1, ButtonsEnum.MuteGroup2, ButtonsEnum.MuteGroup3,
                ButtonsEnum.MuteGroup4, ButtonsEnum.MuteGroup5, ButtonsEnum.MuteGroup6
            };
            for (int i = 0; i < muteKeys.Length; i++)
            {
                if (ButtonsDictionary.TryGetValue(muteKeys[i], out var b) && value == b)
                    return (true, i);
            }
            return (false, 0);
        }
    }
}
