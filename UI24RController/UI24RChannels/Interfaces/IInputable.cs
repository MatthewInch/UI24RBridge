using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels.Interfaces
{
    interface IInputable
    {
        bool IsPhantom { get; set; }
        public double Gain { get; set; }
        SrcTypeEnum SrcType { get; set; }
        int SrcNumber { get; set; }
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

    }
}
