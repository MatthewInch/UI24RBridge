using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace UI24RController
{
    public enum ChannelTypeEnum
    {
        Input, LineIn, Player, Fx, Subgroup, AUX, VCA,
        Main, HW, Var, Uknown
    }
    public enum SystemVarTypeEnum
    {
        MtkRecCurrentState, Uknown
    }

    public enum MessageTypeEnum
    {
        mix, gain, mute, solo, name, mtkrec, stereoIndex, mtk, auxFaderValue, uknown
    }

    public class UI24Message
    {
        // 3:::SETD^i.0.mix^0.7012820512871794
        //0-23: input channels
        //24-25: Linie In L/R
        //26-27: Player L/R
        //28-31: FX channels
        //32-37: Subgroups
        //38-47: AUX 1-10
        //48-53: VCA 1-6

        public int ChannelNumber { 
            get { 
                switch (this.ChannelType)
                {
                    case ChannelTypeEnum.LineIn:
                        return this.ChannelTypeNumber + 24;
                    case ChannelTypeEnum.Player:
                        return this.ChannelTypeNumber + 26;
                    case ChannelTypeEnum.Fx:
                        return this.ChannelTypeNumber + 28;
                    case ChannelTypeEnum.Subgroup:
                        return this.ChannelTypeNumber + 32;
                    case ChannelTypeEnum.AUX:
                        return this.ChannelTypeNumber + 38;
                    case ChannelTypeEnum.VCA:
                        return this.ChannelTypeNumber + 48;
                    case ChannelTypeEnum.Main:
                        return 54;
                    default: //ChannelTypeEnum.Input
                        return this.ChannelTypeNumber;
                }
            } 
        }
        public string ChannelName { get; set; }
        public int ChannelTypeNumber { get; internal set; }
        public double FaderValue { get; set; }
        public double Gain { get; set; }
        public bool IsValid { get; internal set; }
        public bool LogicValue { get; internal set; }
        public int IntValue { get; internal set; }
        public ChannelTypeEnum ChannelType { get; internal set; }
        public MessageTypeEnum MessageType { get; internal set; }
        public SystemVarTypeEnum SystemVarType { get; internal set; }

        public UI24Message(int channelNumber)
        {

        }

        public UI24Message(string message)
        {
            IsValid = false;
            SystemVarType = SystemVarTypeEnum.Uknown;
            var messageParts = message.Split('^');
            if (messageParts.Count() > 2)
            {
                var messageTypes = messageParts[1].Split('.');
                var channelNumber = 0;
                this.ChannelType = GetChannelType(messageTypes[0]);
                if (this.ChannelType == ChannelTypeEnum.Main && messageTypes.Count() >= 2) 
                {
                    this.MessageType = GetMessageType(messageTypes[1]);
                }
                else if (ChannelType == ChannelTypeEnum.Var)
                {
                    this.MessageType = GetMessageType(messageTypes[1]);
                }
                else if (ChannelType != ChannelTypeEnum.Uknown && messageTypes.Count() >= 3)
                {
                    this.MessageType = GetMessageType(messageTypes[2]);
                    int.TryParse(messageTypes[1], out channelNumber);
                }
                else
                {
                    this.MessageType = MessageTypeEnum.uknown;
                }
                this.ChannelTypeNumber = channelNumber;
                int intValue;
                double faderValue;
                switch (this.MessageType)
                {
                    case MessageTypeEnum.mix:
                        if (double.TryParse(messageParts[2], NumberStyles.Number, CultureInfo.InvariantCulture, out faderValue))
                        {
                            FaderValue = faderValue;
                            IsValid = true;
                        }
                        break;
                    case MessageTypeEnum.gain:
                        double gain;
                        if (double.TryParse(messageParts[2], NumberStyles.Number, CultureInfo.InvariantCulture, out gain))
                        {
                            Gain = gain;
                            IsValid = true;
                        }
                        break;
                    case MessageTypeEnum.name:
                        this.ChannelName = messageParts[2];
                        IsValid = true;
                        break;
                    case MessageTypeEnum.mute:
                        if (messageParts[2] == "1")
                            LogicValue = true;
                        else
                            LogicValue = false;
                        IsValid = true;
                        break;
                    case MessageTypeEnum.solo:
                        if (messageParts[2] == "1")
                            LogicValue = true;
                        else
                            LogicValue = false;
                        IsValid = true;
                        break;
                    case MessageTypeEnum.mtkrec:
                        if (messageParts[2] == "1")
                            LogicValue = true;
                        else
                            LogicValue = false;
                        IsValid = true;
                        break;
                    case MessageTypeEnum.stereoIndex:
                        if (int.TryParse(messageParts[2], out intValue))
                        {
                            this.IntValue = intValue;
                            IsValid = true;
                        }
                        break;
                    case MessageTypeEnum.mtk:
                        if (messageParts[1] == "var.mtk.rec.currentState")
                        {
                            SystemVarType = SystemVarTypeEnum.MtkRecCurrentState;
                            if (messageParts[2] == "1")
                                LogicValue = true;
                            else
                                LogicValue = false;
                            IsValid = true;
                            break;
                        }
                        break;
                    case MessageTypeEnum.auxFaderValue:
                        if (messageTypes.Length > 4) //SETD^i.0.aux.0.value^val 
                        {
                            if (int.TryParse(messageTypes[3], out intValue) 
                                && messageTypes[4] == "value" &&
                                double.TryParse(messageParts[2], NumberStyles.Number, CultureInfo.InvariantCulture, out faderValue))
                            {
                                this.IntValue = intValue;
                                this.FaderValue = faderValue;
                                IsValid = true;
                            }
                        }
                        break;
                }
            }
        }

        protected ChannelTypeEnum GetChannelType(string channelType)
        {
            switch (channelType)
            {
                case "l":
                    return ChannelTypeEnum.LineIn;
                case "p":
                    return ChannelTypeEnum.Player;
                case "f":
                    return ChannelTypeEnum.Fx;
                case "s":
                    return ChannelTypeEnum.Subgroup;
                case "a":
                    return ChannelTypeEnum.AUX;
                case "v":
                    return ChannelTypeEnum.VCA;
                case "m":
                    return ChannelTypeEnum.Main;
                case "hw":
                    return ChannelTypeEnum.HW;
                case "i":
                    return ChannelTypeEnum.Input;
                case "var":
                    return ChannelTypeEnum.Var;
                default: // "i":
                    return ChannelTypeEnum.Uknown;
            }
        }

        protected MessageTypeEnum GetMessageType(string messageType)
        {
            switch (messageType)
            {
                case "mix":
                    return MessageTypeEnum.mix;
                case "name":
                    return MessageTypeEnum.name;
                case "gain":
                    return MessageTypeEnum.gain;
                case "mute":
                    return MessageTypeEnum.mute;
                case "solo":
                    return MessageTypeEnum.solo;
                case "mtkrec":
                    return MessageTypeEnum.mtkrec;
                case "mtk":
                    return MessageTypeEnum.mtk;
                case "stereoIndex":
                    return MessageTypeEnum.stereoIndex;
                case "aux":
                    return MessageTypeEnum.auxFaderValue;
                default: // "i":
                    return MessageTypeEnum.uknown;
            }
        }
    }
}
