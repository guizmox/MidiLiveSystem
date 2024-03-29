using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;

namespace MidiTools
{
    [Serializable]
    public class SequencerData
    {
        public Sequencer[] Sequencer;
        public string StartStopListener = "";
        public static int LowKeyTranspose = 21;
        public static int HighKeyTranspose = 48;

        public static Timer SequencerClock;
        public static double TimerFrequency = 0;
        public static int Tempo = 120;
        public static string Quantization = "";

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

        public static void InitTimer(int tempo, string quantization)
        {
            Tempo = tempo;
            Quantization = quantization;

            if (SequencerClock != null)
            {
                SequencerClock.Stop();
                SequencerClock.Enabled = false;
                SequencerClock = null;
            }

            TimerFrequency = Tools.GetMidiClockIntervalDouble(tempo, quantization);

            SequencerClock = new System.Timers.Timer();
            SequencerClock.Interval = (int)Math.Round(TimerFrequency);
        }

        public static void ChangeTempo(int iNewValue)
        {
            if (Tempo != iNewValue)
            {
                Tempo = iNewValue;
                if (SequencerClock != null)
                {
                    TimerFrequency = Tools.GetMidiClockIntervalDouble(iNewValue, Quantization);
                    SequencerClock.Stop();
                    SequencerClock.Interval = (int)Math.Round(TimerFrequency);
                    SequencerClock.Start();
                }
            }
        }

        public static void StopTimer()
        {
            SequencerClock.Stop();
            SequencerClock.Enabled = false;
            SequencerClock = null;
        }
    }
}
