using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace MidiTools
{
    [MessagePackObject]
    [Serializable]
    public class MidiPreset
    {
        [Key("InstrumentGroup")]
        public string InstrumentGroup { get; set; } = "";

        [Key("Prg")]
        public int Prg = 0;

        [Key("Msb")]
        public int Msb = 0;

        [Key("Lsb")]
        public int Lsb = 0;

        [Key("PresetName")]
        public string PresetName = "";

        [Key("Channel")]
        public int Channel = 1;

        [Key("IsFavourite")]
        public bool IsFavourite = false;

        [IgnoreMember] // Ignorer cette propriété lors de la sérialisation, car elle est dérivée
        public string Id => string.Concat(Prg, "-", Msb, "-", Lsb);

        [IgnoreMember] // Ignorer cette propriété lors de la sérialisation, car elle est dérivée
        public string Tag => string.Concat(Prg, "-", Msb, "-", Lsb);

        public MidiPreset(string sSection, int iChannel, int iPrg, int iMsb, int iLsb, string sPName)
        {
            this.InstrumentGroup = sSection;
            this.Prg = iPrg;
            this.Msb = iMsb;
            this.Lsb = iLsb;
            this.PresetName = sPName;
            this.Channel = iChannel;
        }
        public MidiPreset()
        {

        }
    }
}
