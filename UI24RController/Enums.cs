using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace UI24RController
{
    public enum ButtonsEnum
    {
        PlayPrev, PlayNext,
        Play, Rec, Stop,
        Aux1, Aux2, Aux3, Aux4, Aux5, Aux6, Aux7, Aux8,
        Fx1, Fx2, Fx3, Fx4
    }

    public enum ViewTypeEnum
    {
        GlobalView, MidiTracks, Inputs, AudioTrack, AudioInst, Aux, Buses, Outputs, User
    }

    public enum SelectedLayoutEnum
    {
        Channels,
        Aux1, Aux2, Aux3, Aux4, Aux5, Aux6, Aux7, Aux8,
        Fx1, Fx2, Fx3, Fx4
    }

    public static class EnumExtension
    {
        public static int AuxToInt(this SelectedLayoutEnum layout)
        {
            int result = 0;
            switch (layout)
            {
                case SelectedLayoutEnum.Aux1:
                    result = 0;
                    break;
                case SelectedLayoutEnum.Aux2:
                    result = 1;
                    break;
                case SelectedLayoutEnum.Aux3:
                    result = 2;
                    break;
                case SelectedLayoutEnum.Aux4:
                    result = 3;
                    break;
                case SelectedLayoutEnum.Aux5:
                    result = 4;
                    break;
                case SelectedLayoutEnum.Aux6:
                    result = 5;
                    break;
                case SelectedLayoutEnum.Aux7:
                    result = 6;
                    break;
                case SelectedLayoutEnum.Aux8:
                    result = 7;
                    break;

            }
            return result;
        }

        public static SelectedLayoutEnum ToAux(this int aux)
        {
            SelectedLayoutEnum result = SelectedLayoutEnum.Aux1;
            switch (aux)
            {
                case 0:
                    result = SelectedLayoutEnum.Aux1;
                    break;
                case 1:
                    result = SelectedLayoutEnum.Aux2;
                    break;
                case 2:
                    result = SelectedLayoutEnum.Aux3;
                    break;
                case 3:
                    result = SelectedLayoutEnum.Aux4;
                    break;
                case 4:
                    result = SelectedLayoutEnum.Aux5;
                    break;
                case 5:
                    result = SelectedLayoutEnum.Aux6;
                    break;
                case 6:
                    result = SelectedLayoutEnum.Aux7;
                    break;
                case 7:
                    result = SelectedLayoutEnum.Aux8;
                    break;
            }
            return result;
        }

        public static bool IsAux(this SelectedLayoutEnum layout)
        {
            return layout == SelectedLayoutEnum.Aux1 ||
                 layout == SelectedLayoutEnum.Aux2 ||
                 layout == SelectedLayoutEnum.Aux3 ||
                 layout == SelectedLayoutEnum.Aux4 ||
                 layout == SelectedLayoutEnum.Aux5 ||
                 layout == SelectedLayoutEnum.Aux6 ||
                 layout == SelectedLayoutEnum.Aux7 ||
                 layout == SelectedLayoutEnum.Aux8;
        }

        public static SelectedLayoutEnum ToLayoutEnum(this ButtonsEnum button)
        {
            SelectedLayoutEnum layout = SelectedLayoutEnum.Aux1;
            switch (button)
            {
                case ButtonsEnum.Aux1:
                    layout = SelectedLayoutEnum.Aux1;
                    break;
                case ButtonsEnum.Aux2:
                    layout = SelectedLayoutEnum.Aux3;
                    break;
                case ButtonsEnum.Aux3:
                    layout = SelectedLayoutEnum.Aux3;
                    break;
                case ButtonsEnum.Aux4:
                    layout = SelectedLayoutEnum.Aux4;
                    break;
                case ButtonsEnum.Aux5:
                    layout = SelectedLayoutEnum.Aux5;
                    break;
                case ButtonsEnum.Aux6:
                    layout = SelectedLayoutEnum.Aux6;
                    break;
                case ButtonsEnum.Aux7:
                    layout = SelectedLayoutEnum.Aux7;
                    break;
                case ButtonsEnum.Aux8:
                    layout = SelectedLayoutEnum.Aux8;
                    break;
            }
            return layout;
        }
        public static ButtonsEnum ToButtonsEnum(this SelectedLayoutEnum layout)
        {
            ButtonsEnum button = ButtonsEnum.Aux1;
            switch (layout)
            {
                case SelectedLayoutEnum.Aux1:
                    button = ButtonsEnum.Aux1;
                    break;
                case SelectedLayoutEnum.Aux2:
                    button = ButtonsEnum.Aux2;
                    break;
                case SelectedLayoutEnum.Aux3:
                    button = ButtonsEnum.Aux3;
                    break;
                case SelectedLayoutEnum.Aux4:
                    button = ButtonsEnum.Aux4;
                    break;
                case SelectedLayoutEnum.Aux5:
                    button = ButtonsEnum.Aux5;
                    break;
                case SelectedLayoutEnum.Aux6:
                    button = ButtonsEnum.Aux6;
                    break;
                case SelectedLayoutEnum.Aux7:
                    button = ButtonsEnum.Aux7;
                    break;
                case SelectedLayoutEnum.Aux8:
                    button = ButtonsEnum.Aux8;
                    break;
            }
            return button;
        }
    }
}
