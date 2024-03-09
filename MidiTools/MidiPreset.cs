using System;
using System.Collections.Generic;
using System.Text;

namespace MidiTools
{

    [Serializable]
    public class MidiPreset
    {
        public string InstrumentGroup = "";
        public int Prg = 0;
        public int Msb = 0;
        public int Lsb = 0;
        public string PresetName = "";
        public int Channel = 1;
        public bool IsFavourite = false;

        public string Id { get { return string.Concat(Prg, "-", Msb, "-", Lsb); } }

        public string Tag { get { return string.Concat(Prg, "-", Msb, "-", Lsb); } }

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
