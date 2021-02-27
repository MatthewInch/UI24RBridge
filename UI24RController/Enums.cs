using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace UI24RController
{
    public enum ButtonsEnum
    {
        Track, Pan, Eq, Send, PlugIn, Instr, Display, Smtpe,
        GlobalView, MidiTracks, Inputs, AudioTracks, AudioInst, AuxBtn, BusesBtn, Outputs, User,
        Aux1, Aux2, Aux3, Aux4, Aux5, Aux6, Aux7, Aux8,
        Fx1, Fx2, Fx3, Fx4,
        MuteGroup1, MuteGroup2, MuteGroup3, MuteGroup4, MuteGroup5, MuteGroup6,
        Save, Undo, Cancel, Enter,
        Marker, Nudge, Cycle, Drop, Replace, Click, Solo,
        PlayPrev, PlayNext, Play, Rec, Stop,
        FaderBankUp, FaderBankDown, ChannelUp, ChannelDown,
        Up, Down, Left, Right, Center,
        Scrub//,
        //channel buttons
        //Ch1Rec,Ch1Solo,Ch1Mute,Ch1Select,Ch1Knob
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

    public enum SrcTypeEnum
    {
        Hw, Line, None
    }

    public static class EnumExtension
    {
        public static int AuxToInt(this SelectedLayoutEnum layout)
        {
            switch (layout)
            {
                case SelectedLayoutEnum.Aux1:
                    return 0;
                case SelectedLayoutEnum.Aux2:
                    return 1;
                case SelectedLayoutEnum.Aux3:
                    return 2;
                case SelectedLayoutEnum.Aux4:
                    return 3;
                case SelectedLayoutEnum.Aux5:
                    return 4;
                case SelectedLayoutEnum.Aux6:
                    return 5;
                case SelectedLayoutEnum.Aux7:
                    return 6;
                case SelectedLayoutEnum.Aux8:
                default:
                    return 7;

            }
        }

        public static SelectedLayoutEnum ToAux(this int aux)
        {
            switch (aux)
            {
                case 0:
                    return SelectedLayoutEnum.Aux1;
                case 1:
                    return SelectedLayoutEnum.Aux2;
                case 2:
                    return SelectedLayoutEnum.Aux3;
                case 3:
                    return SelectedLayoutEnum.Aux4;
                case 4:
                    return SelectedLayoutEnum.Aux5;
                case 5:
                    return SelectedLayoutEnum.Aux6;
                case 6:
                    return SelectedLayoutEnum.Aux7;
                case 7:
                default:
                    return SelectedLayoutEnum.Aux8;
            }
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

        public static int FxToInt(this SelectedLayoutEnum layout)
        {
            switch (layout)
            {
                case SelectedLayoutEnum.Fx1:
                    return 0;
                case SelectedLayoutEnum.Fx2:
                    return 1;
                case SelectedLayoutEnum.Fx3:
                    return 2;
                case SelectedLayoutEnum.Fx4:
                default:
                    return 3;
            }
        }

        public static SelectedLayoutEnum ToFx(this int fx)
        {
            switch (fx)
            {
                case 0:
                    return SelectedLayoutEnum.Fx1;
                case 1:
                    return SelectedLayoutEnum.Fx2;
                case 2:
                    return SelectedLayoutEnum.Fx3;
                case 3:
                default:
                    return SelectedLayoutEnum.Fx4;


            }
        }

        public static bool IsFx(this SelectedLayoutEnum layout)
        {
            return layout == SelectedLayoutEnum.Fx1 ||
                 layout == SelectedLayoutEnum.Fx2 ||
                 layout == SelectedLayoutEnum.Fx3 ||
                 layout == SelectedLayoutEnum.Fx4;
        }


        public static SrcTypeEnum StringToSrcType(this string srcStr)
        {
            srcStr = srcStr.ToLower();
            switch (srcStr)
            {
                case "hw":
                    return SrcTypeEnum.Hw;
                case "li":
                    return SrcTypeEnum.Line;
                default:
                    return SrcTypeEnum.None;
            }
        }

        public static string SrcTypeToString(this SrcTypeEnum srcType)
        {
            switch (srcType)
            {
                case SrcTypeEnum.Hw:
                    return "hw";
                case SrcTypeEnum.Line:
                    return "li";
                default:
                    return "none";
            }
        }

        public static SelectedLayoutEnum ToLayoutEnum(this ButtonsEnum button)
        {
            switch (button)
            {
                case ButtonsEnum.Aux1:
                    return SelectedLayoutEnum.Aux1;
                case ButtonsEnum.Aux2:
                    return SelectedLayoutEnum.Aux3;
                case ButtonsEnum.Aux3:
                    return SelectedLayoutEnum.Aux3;
                case ButtonsEnum.Aux4:
                    return SelectedLayoutEnum.Aux4;
                case ButtonsEnum.Aux5:
                    return SelectedLayoutEnum.Aux5;
                case ButtonsEnum.Aux6:
                    return SelectedLayoutEnum.Aux6;
                case ButtonsEnum.Aux7:
                    return SelectedLayoutEnum.Aux7;
                case ButtonsEnum.Aux8:
                default:
                    return SelectedLayoutEnum.Aux8;
            }
        }
        public static ButtonsEnum ToButtonsEnum(this SelectedLayoutEnum layout)
        {
            switch (layout)
            {
                case SelectedLayoutEnum.Aux1:
                    return ButtonsEnum.Aux1;
                case SelectedLayoutEnum.Aux2:
                    return ButtonsEnum.Aux2;
                case SelectedLayoutEnum.Aux3:
                    return ButtonsEnum.Aux3;
                case SelectedLayoutEnum.Aux4:
                    return ButtonsEnum.Aux4;
                case SelectedLayoutEnum.Aux5:
                    return ButtonsEnum.Aux5;
                case SelectedLayoutEnum.Aux6:
                    return ButtonsEnum.Aux6;
                case SelectedLayoutEnum.Aux7:
                    return ButtonsEnum.Aux7;
                case SelectedLayoutEnum.Aux8:
                    return ButtonsEnum.Aux8;
                case SelectedLayoutEnum.Fx1:
                    return ButtonsEnum.Fx1;
                case SelectedLayoutEnum.Fx2:
                    return ButtonsEnum.Fx2;
                case SelectedLayoutEnum.Fx3:
                    return ButtonsEnum.Fx3;
                case SelectedLayoutEnum.Fx4:
                    return ButtonsEnum.Fx4;
                default:
                    return ButtonsEnum.Aux1;
            }
        }
    }
}
