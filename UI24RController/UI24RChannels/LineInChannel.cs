using System;
using System.Collections.Generic;
using System.Text;
using UI24RController.UI24RChannels.Interfaces;

namespace UI24RController.UI24RChannels
{
    public class LineInChannel: ChannelBase, IRecordable, IStereoLinkable, IInputable
    {

        public override int ChannelNumberInMixer => this.ChannelNumber + 24;
        public bool IsRec { get ; set; }
        public bool IsPhantom { get; set; }
        public int LinkedWith { get; set; }
        public SrcTypeEnum SrcType { get; set; }
        public int SrcNumber { get; set; }
        public double Gain { get; set; }

        public LineInChannel(int channelNumber): base(channelNumber)
        {
            IsRec = false;
            LinkedWith = -1; //-1: not linked, 0 left, 1 right
            channelTypeID = "l";
            SrcType = SrcTypeEnum.Line;
            SrcNumber = channelNumber;
            Gain = 0;
            IsPhantom = false;
        }

        protected override string GetDefaultName()
        {
            return this.ChannelNumber == 0 ? "L-IN L" : "L-IN R";
        }

        public override string MixFaderMessage()
        {
            return $"3:::SETD^l.{this.ChannelNumber}.mix^{this.ChannelFaderValue.ToString().Replace(',', '.')}";
        }
        public string GainMessage()
        {
            if (SrcType == SrcTypeEnum.Hw)
                return $"3:::SETD^{this.SrcType.SrcTypeToString()}.{this.SrcNumber}.gain^{this.Gain.ToString().Replace(',', '.')}";
            return "";
        }
        public string PhantomMessage()
        {
            if (SrcType == SrcTypeEnum.Hw)
                return $"3:::SETD^{this.SrcType.SrcTypeToString()}.{this.SrcNumber}.phantom^{(this.IsPhantom ? 1 : 0)}";
            return "";
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
