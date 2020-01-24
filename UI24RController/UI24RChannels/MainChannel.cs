using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels
{
    public class MainChannel: ChannelBase
    {
        public MainChannel(): base(0)
        {
            this.Name = "Main";
        }
        protected override string GetDefaultName()
        {
            return "Main";
        }

        public override int ChannelNumberInMixer => 54;

        public override string MixFaderMessage()
        {
            return $"3:::SETD^m.mix^{this.ChannelFaderValue.ToString().Replace(',', '.')}";
        }
        public override string SelectChannelMessage(string syncID)
        {
            return $"3:::BMSG^SYNC^{syncID}^-1";
        }
        public override string MuteMessage()
        {
            return $"3:::SETD^m.{this.ChannelNumber}.mute^{(this.IsMute ? 1 : 0)}";
        }
        public override string SoloMessage()
        {
            return $"3:::SETD^m.{this.ChannelNumber}.solo^{(this.IsSolo ? 1 : 0)}";
        }

    }
}
