using System;
using System.Collections.Generic;
using System.Text;
using UI24RController.UI24RChannels.Interfaces;

namespace UI24RController.UI24RChannels
{
    public class InputChannel: ChannelBase, IRecordable, IStereoLinkable
    {

        public bool IsRec { get; set; }
        public int LinkedWith { get; set ; }

        public InputChannel(int channelNumber): base(channelNumber)
        {
            IsRec = false;
            LinkedWith = -1; //-1: not linked, 0 left, 1 right
        }
        protected override string GetDefaultName()
        {
            return $"CH {(this.ChannelNumber + 1):D2}";
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
        public virtual string RecMessage()
        {
            return $"3:::SETD^i.{this.ChannelNumber}.mtkrec^{(this.IsRec ? 1 : 0)}";
        }

    }
}
