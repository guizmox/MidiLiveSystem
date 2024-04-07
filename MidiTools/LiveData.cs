using MessagePack;
using RtMidi.Core.Devices;
using RtMidi.Core.Enums;
using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;
using VSTHost;
using static MidiTools.MidiDevice;

namespace MidiTools
{
    [MessagePackObject]
    [Serializable]
    public class LiveData
    {
        [Key("StartCC")]
        public List<int[]> StartCC { get; set; } = new List<int[]>();

        [Key("StartOptions")]
        public MidiOptions StartOptions { get; set; }

        [Key("Channel")]
        public Channel Channel { get; set; }

        [Key("DeviceOUT")]
        public string DeviceOUT { get; set; }

        [Key("RoutingGuid")]
        public Guid RoutingGuid { get; set; }

        [Key("InitProgram")]
        public MidiPreset InitProgram { get; set; }

        [Key("InitVSTState")]
        public byte[] InitVSTState { get; set; }

        [Key("InitVSTParameters")]
        public List<VSTParameter> InitVSTParameters { get; set; }

        [Key("InitVSTProgram")]
        public int InitVSTProgram { get; set; }


        public LiveData()
        {

        }
    }
}
