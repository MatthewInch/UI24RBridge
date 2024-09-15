using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels
{
    public  class BlankChannel: ChannelBase
    {

        public BlankChannel() : base(0)
        {

        }
        protected override string GetDefaultName()
        {
            return $"BLANK";
        }
        public override string SetAuxValueMessage(SelectedLayoutEnum selectedLayout)
        {
            int auxNumber = selectedLayout.AuxToInt();
            return $"";
        }

        public override string SetFxValueMessage(SelectedLayoutEnum selectedLayout)
        {
            return $"";
        }

        public override string MixFaderMessage()
        {
            return $"";
        }


        public override string SelectChannelMessage(string syncID)
        {
            return $"";
        }

        public override string TurnOnRTAMessage()
        {
            return $"";
        }

        public override string MuteMessage()
        {
            return $"";
        }

        public override string ForceUnMuteMessage()
        {
            return $"";
        }

        public override string SoloMessage()
        {
            return $"";
        }
    }
}
