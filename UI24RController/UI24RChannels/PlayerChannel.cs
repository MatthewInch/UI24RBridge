using System;
using System.Collections.Generic;
using System.Text;
using UI24RController.UI24RChannels.Interfaces;

namespace UI24RController.UI24RChannels
{
    public class PlayerChannel: ChannelBase, IStereoLinkable
    {
        public override int ChannelNumberInMixer => this.ChannelNumber + 26;

        public int LinkedWith { get ; set ; }
       
        public PlayerChannel(int channelNumber): base(channelNumber)
        {
            LinkedWith = -1; //-1: not linked, 0 left, 1 right
            channelTypeID = "p";

        }
        protected override string GetDefaultName()
        {
            return this.ChannelNumber == 0 ? "Play L" : "Play R";
        }

        public override string MixFaderMessage()
        {
            return $"3:::SETD^p.{this.ChannelNumber}.mix^{this.ChannelFaderValue.ToString().Replace(',', '.')}";
        }
        public override string SoloMessage()
        {
            return $"3:::SETD^p.{this.ChannelNumber}.solo^{(this.IsSolo ? 1 : 0)}";
        }
    }
}
