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
        Main
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
        public int ChannelTypeNumber { get; internal set; }
        public double FaderValue { get; set; }
        public double Gain { get; set; }
        public bool IsValid { get; internal set; }
        public ChannelTypeEnum ChannelType { get; internal set; }

        public UI24Message(int channelNumber)
        {

        }

        public UI24Message(string message)
        {
            IsValid = false;
            var messageParts = message.Split('^');
            if (messageParts.Count() > 2)
            {
                var messageTypes = messageParts[1].Split('.');
                var channelNumber = 0;
                if ((messageTypes.Count() >= 3) && 
                    (messageTypes[2] == "mix" &&
                    int.TryParse(messageTypes[1], out channelNumber) ) || ((messageTypes.Count() >= 2) && (messageTypes[0] == "m") && (messageTypes[1] == "mix")))
                {
                    this.ChannelType = GetChannelType(messageTypes[0]);
                    this.ChannelTypeNumber = channelNumber;
                    double faderValue;
                    if (double.TryParse(messageParts[2], NumberStyles.Number, CultureInfo.InvariantCulture, out faderValue))
                    {
                        FaderValue = faderValue;
                        IsValid = true;
                    }
                } 
                else if (messageTypes.Count() >= 3 && messageTypes[0] == "hw" && messageTypes[2] == "gain" &&
                    int.TryParse(messageTypes[1], out channelNumber))
                {
                    this.ChannelType = GetChannelType(messageTypes[0]);
                    this.ChannelTypeNumber = channelNumber;
                    double gain;
                    if (double.TryParse(messageParts[2], NumberStyles.Number, CultureInfo.InvariantCulture, out gain))
                    {
                        Gain = gain;
                        IsValid = true;
                    }
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
                    return ChannelTypeEnum.Input;
                default: // "i":
                    return ChannelTypeEnum.Input;
            }
        }


    }
}
