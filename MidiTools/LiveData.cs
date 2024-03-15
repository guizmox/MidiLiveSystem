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
        public List<int[]> StartCC = new List<int[]>();
        public MidiOptions StartOptions;
        public Channel Channel;
        public string DeviceOUT;
        public Guid RoutingGuid;
        public MidiPreset InitProgram;


        public LiveData()
        {

        }
    }
}
