using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels
{
    public class PlayerChannel: ChannelBase
    {
       
        public PlayerChannel(int channelNumber): base(channelNumber)
        {
            this.Name = this.ChannelNumber == 0 ? "Play L" : "Play R";
        }
        public override int ChannelNumberInMixer => this.ChannelNumber + 26;

        public override string MixFaderMessage()
        {
            return $"3:::SETD^p.{this.ChannelNumber}.mix^{this.ChannelFaderValue.ToString().Replace(',', '.')}";
        }
        public override string MuteMessage()
        {
            return $"3:::SETD^p.{this.ChannelNumber}.mute^{(this.IsMute ? 1 : 0)}";
        }
        public override string SoloMessage()
        {
            return $"3:::SETD^p.{this.ChannelNumber}.solo^{(this.IsSolo ? 1 : 0)}";
        }
    }
}
