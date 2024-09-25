using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI24RController.UI24RChannels
{
    /// <summary>
    /// encapsulate EQ related stuffs
    /// </summary>
    public class EqBase
    {
        public double HPF { get; set; }
        public int ChannelNumber { get; set; }
        public string ChannelType { get; set; }



        public virtual string HPFMessage()
        {
            //vca and empty channel has not eq
            if (ChannelType != "v" && ChannelNumber >-1)
            {
                return $"3:::SETD^{ChannelType}.{ChannelNumber}.eq.hpf.freq^{HPF.ToString().Replace(',', '.')}";
            }
            else
            {
                return "";
            }
        }
    }
}
