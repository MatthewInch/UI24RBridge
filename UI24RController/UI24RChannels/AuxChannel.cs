using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels
{
    public class AuxChannel: ChannelBase
    {

        public AuxChannel(int channelNumber): base(channelNumber)
        {
            this.Name = $"AUX {(this.ChannelNumber + 1):D2}";
        }

        public override int ChannelNumberInMixer => this.ChannelNumber + 38;
        public override string MixFaderMessage()
        {
            return $"3:::SETD^a.{this.ChannelNumber}.mix^{this.ChannelFaderValue.ToString().Replace(',', '.')}";
        }

        public override string MuteMessage()
        {
            return $"3:::SETD^a.{this.ChannelNumber}.mute^{(this.IsMute ? 1 : 0)}";
        }

        public override string SoloMessage()
        {
            return $"3:::SETD^a.{this.ChannelNumber}.solo^{(this.IsSolo ? 1 : 0)}";
        }
    }
}

