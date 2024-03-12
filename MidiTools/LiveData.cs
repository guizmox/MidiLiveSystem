using RtMidi.Core.Devices;
using RtMidi.Core.Enums;
using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;
using static MidiTools.MidiDevice;

namespace MidiTools
{
    [Serializable]
    public class LiveData
    {
        public List<LiveCC> StartCC = new List<LiveCC>();
        public MidiOptions StartOptions;
        public Channel Channel;
        public string DeviceOUT;
        public Guid RoutingGuid;
        public MidiPreset InitProgram;


        public LiveData()
        {

        }
    }

    public class LiveCC
    {
        public string Device = "";
        public Channel Channel;
        public int CC = -1;
        public int CCValue = -1;
        internal bool BlockIncoming = false;

        public LiveCC(string device, Channel channel, int iCC, int ccValue)
        {
            Device = device;
            Channel = channel;
            CC = iCC;
            CCValue = ccValue;
        }

        public LiveCC()
        {

        }
    }

}
