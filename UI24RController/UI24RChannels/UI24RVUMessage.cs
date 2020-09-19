using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace UI24RController.UI24RChannels
{
    class UI24RVUMessage
    {
        public interface IVUChannel
        {
            byte GetPreValue();
            byte GetPostValue();
        }

        public class VUInputMediaChannel : IVUChannel
        {
            public byte vuPre; 
            public byte vuPost; 
            public byte vuPostFader;
            public byte vuGateIn;
            public byte vuCompOut; 
            public byte vuCompMeter;

            public byte GetPostValue()
            {
                return vuPostFader;
            }

            public byte GetPreValue()
            {
                return vuPre;
            }
        }

        public class VUSubgroupFXChannel : IVUChannel
        {
            public byte vuPostL;
            public byte vuPostR;
            public byte vuPostFaderL;
            public byte vuPostFaderR;
            public byte vuGateIn;
            public byte vuCompOut;
            public byte vuCompMeter;

            public byte GetPostValue()
            {
                if (vuPostFaderR > vuPostFaderL)
                    return vuPostFaderR;
                else
                    return vuPostFaderL;
            }

            public byte GetPreValue()
            {
                if (vuPostR > vuPostL)
                    return vuPostR;
                else
                    return vuPostL;
            }
        }

        public class VUAuxMasterChannel : IVUChannel
        {
            public byte vuPost;
            public byte vuPostFader;
            public byte vuGateIn;
            public byte vuCompOut;
            public byte vuCompMeter;

            public byte GetPostValue()
            {
                return vuPostFader;
            }

            public byte GetPreValue()
            {
                return vuPost;
            }
        }

        public struct VUHeader {
            public byte NINPUTS;
            public byte NMEDIA;
            public byte NSUBGROUPS;
            public byte NFX;
            public byte NAUX;
            public byte NMASTERS;
            public byte NLINEIN;
            public byte ZeroValue;
        }

        private const int NUM_CHANNELS = 55;

        public List<IVUChannel> VUChannels = new List<IVUChannel>(NUM_CHANNELS);

        public UI24RVUMessage(string inputMessage)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            MemoryStream memStream = new MemoryStream();
            //Cut VU2^ from the messsage
            var encodedString = inputMessage.Substring(4);
            var decodedByteArray = Convert.FromBase64String(encodedString);
            var headerByeArray = decodedByteArray.Take(8).ToArray();
            var done = 8;
            VUHeader header = new VUHeader() 
            { 
                NINPUTS   =headerByeArray[0],
                NMEDIA    = headerByeArray[1],
                NSUBGROUPS= headerByeArray[2],
                NFX       = headerByeArray[3],
                NAUX      = headerByeArray[4],
                NMASTERS  = headerByeArray[5],
                NLINEIN   = headerByeArray[6],
                ZeroValue = headerByeArray[7]
            };
            var inputsByteArray = decodedByteArray.Skip(done)
                .Take(header.NINPUTS*6 + header.NMEDIA*6 + header.NSUBGROUPS*7
                + header.NFX*7 + header.NAUX*5 + header.NMASTERS*5 + header.NLINEIN*6).ToArray();
            done += header.NINPUTS * 6;
            int i = 0;

            //0-23: input channels
            while (i < header.NINPUTS * 6)
            {
                VUInputMediaChannel vu = new VUInputMediaChannel()
                {
                    vuPre           = inputsByteArray[i],
                    vuPost          = inputsByteArray[i+1],
                    vuPostFader     = inputsByteArray[i+2],
                    vuGateIn        = inputsByteArray[i+3],
                    vuCompOut       = inputsByteArray[i+4],
                    vuCompMeter     = inputsByteArray[i+5]
                };
                i += 6;
                VUChannels.Add(vu);
            }

            //26-27: Player L/R
            for (int j=0; j < header.NMEDIA; j++)
            {
                VUInputMediaChannel vu = new VUInputMediaChannel()
                {
                    vuPre = inputsByteArray[i],
                    vuPost = inputsByteArray[i + 1],
                    vuPostFader = inputsByteArray[i + 2],
                    vuGateIn = inputsByteArray[i + 3],
                    vuCompOut = inputsByteArray[i + 4],
                    vuCompMeter = inputsByteArray[i + 5]
                };
                i += 6;
                VUChannels.Add(vu);
            }
            //32-37: Subgroups
            for (int j = 0; j < header.NSUBGROUPS; j++)
            {
                VUSubgroupFXChannel vu = new VUSubgroupFXChannel()
                {
                    vuPostL = inputsByteArray[i],
                    vuPostR = inputsByteArray[i + 1],
                    vuPostFaderL = inputsByteArray[i + 2],
                    vuPostFaderR = inputsByteArray[i + 3],
                    vuGateIn = inputsByteArray[i + 4],
                    vuCompOut = inputsByteArray[i + 5],
                    vuCompMeter = inputsByteArray[i + 6]
                };
                i += 7;
                VUChannels.Add(vu);
            }
            //28-31: FX channels
            for (int j = 0; j < header.NFX; j++)
            {
                VUSubgroupFXChannel vu = new VUSubgroupFXChannel()
                {
                    vuPostL = inputsByteArray[i],
                    vuPostR = inputsByteArray[i + 1],
                    vuPostFaderL = inputsByteArray[i + 2],
                    vuPostFaderR = inputsByteArray[i + 3],
                    vuGateIn = inputsByteArray[i + 4],
                    vuCompOut = inputsByteArray[i + 5],
                    vuCompMeter = inputsByteArray[i + 6]
                };
                i += 7;
                VUChannels.Insert(header.NINPUTS + header.NMEDIA + j, vu);
            }

            //38-47: AUX 1-10
            for (int j = 0; j < header.NAUX; j++)
            {
                VUAuxMasterChannel vu = new VUAuxMasterChannel()
                {
                    vuPost = inputsByteArray[i],
                    vuPostFader = inputsByteArray[i + 1],
                    vuGateIn = inputsByteArray[i + 2],
                    vuCompOut = inputsByteArray[i + 3],
                    vuCompMeter = inputsByteArray[i + 4]
                };
                i += 5;
                VUChannels.Add(vu);
            }
            //48-53: VCA 1-6 - none

            //MAIN -> skip 2*5 bytes
            i += 10;

            //24-25: Linie In L/R
            for (int j = 0; j < header.NMEDIA; j++)
            {
                VUInputMediaChannel vu = new VUInputMediaChannel()
                {
                    vuPre = inputsByteArray[i],
                    vuPost = inputsByteArray[i + 1],
                    vuPostFader = inputsByteArray[i + 2],
                    vuGateIn = inputsByteArray[i + 3],
                    vuCompOut = inputsByteArray[i + 4],
                    vuCompMeter = inputsByteArray[i + 5]
                };
                i += 6;
                VUChannels.Insert(header.NINPUTS+j, vu);
            }
        }
    }
}
