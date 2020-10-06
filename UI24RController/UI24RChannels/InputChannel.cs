using System;
using System.Collections.Generic;
using System.Text;
using UI24RController.UI24RChannels.Interfaces;

namespace UI24RController.UI24RChannels
{
    public class InputChannel: ChannelBase, IRecordable, IStereoLinkable, IInputable
    {

        public bool IsRec { get; set; }
        public bool IsPhantom { get; set; }
        public int LinkedWith { get; set ; }
        public SrcTypeEnum SrcType { get; set; }
        public int SrcNumber { get; set; }
        public double Gain { get; set; }

        public InputChannel(int channelNumber): base(channelNumber)
        {
            IsRec = false;
            IsPhantom = false;
            LinkedWith = -1; //-1: not linked, 0 left, 1 right
            channelTypeID = "i";
            SrcType = SrcTypeEnum.Hw;
            SrcNumber = channelNumber;
            Gain = 0;
            IsPhantom = false;
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
            if(SrcType == SrcTypeEnum.Hw)
                return $"3:::SETD^{this.SrcType.SrcTypeToString()}.{this.SrcNumber}.gain^{this.Gain.ToString().Replace(',', '.')}";
            return "";
        }
        public string PhantomMessage()
        {
            if (SrcType == SrcTypeEnum.Hw)
                return $"3:::SETD^{this.SrcType.SrcTypeToString()}.{this.SrcNumber}.phantom^{(this.IsPhantom ? 1 : 0)}";
            return "";
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
