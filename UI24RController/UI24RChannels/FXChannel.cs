using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels
{
    public class FXChannel: ChannelBase
    {

        public FXChannel(int channelNumber): base(channelNumber)
        {

        }

        protected override string GetDefaultName()
        {
            return $"FX {(this.ChannelNumber + 1):D2}";
        }
        public override int ChannelNumberInMixer => this.ChannelNumber + 28;
        public override string MixFaderMessage()
        {
            return $"3:::SETD^f.{this.ChannelNumber}.mix^{this.ChannelFaderValue.ToString().Replace(',', '.')}";
        }
        public override string MuteMessage()
        {
            return $"3:::SETD^f.{this.ChannelNumber}.mute^{(this.IsMute ? 1 : 0)}";
        }
        public override string SoloMessage()
        {
            return $"3:::SETD^f.{this.ChannelNumber}.solo^{(this.IsSolo ? 1 : 0)}";
        }
    }
}
