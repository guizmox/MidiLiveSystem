using System;
using System.Collections.Generic;
using System.Text;

namespace MidiTools
{
    [Serializable]
    public class SequencerData
    {
        public Sequencer[] Sequencer;
        public string StartStopListener = "";
        public static int LowKeyTranspose = 21;
        public static int HighKeyTranspose = 48;

        public SequencerData()
        {

        }

        public int[] SetTransposition(string sLowKey, string sHighey)
        {
            int iLow = 0;
            int iHigh = 0;
            bool bOK = false;

            if (int.TryParse(sLowKey, out iLow))
            {
                if (iLow > 0 && iLow < 128)
                {
                    bOK = true;
                }
            }
            if (int.TryParse(sHighey, out iHigh))
            {
                if (iHigh > 0 && iLow < iHigh)
                {
                    bOK = true;
                }
            }

            if (bOK && iLow < iHigh)
            {
                LowKeyTranspose = iLow;
                HighKeyTranspose = iHigh;
            }

            return new int[] { LowKeyTranspose, HighKeyTranspose };
        }
    }
}
