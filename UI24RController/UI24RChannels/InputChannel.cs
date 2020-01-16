using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels
{
    public class InputChannel: ChannelBase
    {


        public InputChannel(int channelNumber): base(channelNumber)
        {
           this.Name = $"CH {(this.ChannelNumber + 1):D2}";
        }

        public override string MixFaderMessage()
        {
            return $"3:::SETD^i.{this.ChannelNumber}.mix^{this.ChannelFaderValue.ToString().Replace(',', '.')}";
        }
        public string GainMessage()
        {
            return $"3:::SETD^hw.{this.ChannelNumber}.gain^{this.Gain.ToString().Replace(',', '.')}";
        }
        public override string MuteMessage()
        {
            return $"3:::SETD^i.{this.ChannelNumber}.mute^{(this.IsMute ? 1 : 0)}";
        }
        public override string SoloMessage()
        {
            return $"3:::SETD^i.{this.ChannelNumber}.solo^{(this.IsSolo ? 1 : 0)}";
        }
        public override string RecMessage()
        {
            return $"3:::SETD^i.{this.ChannelNumber}.mtkrec^{(this.IsRec ? 1 : 0)}";
        }
    }
}
