using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI24RController
{
    /// <summary>
    /// Provide a serializable version of the controller's settings
    /// </summary>
    public class ControllerSettings
    {
        public string InputName { get; set; }
        public string OutputName { get; set; }
        public bool IsExtender { get; set; }
        public int ChannelOffset {  get; set; }
    }
}
