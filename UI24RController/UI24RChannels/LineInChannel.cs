using System;
using System.Collections.Generic;
using System.Text;
using UI24RController.UI24RChannels.Interfaces;

namespace UI24RController.UI24RChannels
{
    public class LineInChannel: ChannelBase, IRecordable, IStereoLinkable
    {

        public override int ChannelNumberInMixer => this.ChannelNumber + 24;
        public bool IsRec { get ; set; }
        public int LinkedWith { get; set; }

        public LineInChannel(int channelNumber): base(channelNumber)
        {
            IsRec = false;
            LinkedWith = -1; //-1: not linked, 0 left, 1 right
            channelTypeID = "l";

        }

        protected override string GetDefaultName()
        {
            return this.ChannelNumber == 0 ? "L-IN L" : "L-IN R";
        }

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
        public virtual string RecMessage()
        {
            return $"3:::SETD^l.{this.ChannelNumber}.mtkrec^{(this.IsRec ? 1 : 0)}";
        }
    }
}
