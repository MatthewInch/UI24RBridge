using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels
{
    public class LineInChannel: ChannelBase
    {


        public LineInChannel(int channelNumber): base(channelNumber)
        {
            this.Name = this.ChannelNumber == 0 ? "L-IN L" : "L-IN R";
        }
        public override int ChannelNumberInMixer => this.ChannelNumber + 24;

        public override string MixFaderMessage()
        {
            return $"3:::SETD^l.{this.ChannelNumber}.mix^{this.ChannelFaderValue.ToString().Replace(',', '.')}";
        }
        public override string MuteMessage()
        {
            return $"3:::SETD^l.{this.ChannelNumber}.mute^{(this.IsMute ? 1 : 0)}";
        }
        public override string SoloMessage()
        {
            return $"3:::SETD^l.{this.ChannelNumber}.solo^{(this.IsSolo ? 1 : 0)}";
        }
        public override string RecMessage()
        {
            return $"3:::SETD^l.{this.ChannelNumber}.mtkrec^{(this.IsRec ? 1 : 0)}";
        }
    }
}
