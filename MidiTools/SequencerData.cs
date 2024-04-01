using System;
using System.Collections.Generic;
using System.Linq;
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

        public static MicroLibrary.MicroTimer[] SequencerClock = new MicroLibrary.MicroTimer[8];
        public static long[] TimerFrequency = new long[8];
        public static int[] Tempo = new int[8];
        public static string[] Quantization = new string[8];

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

        public static void InitTimer(Sequencer sequence, int index)
        {
            if (SequencerClock != null && SequencerClock[index] != null)
            {
                SequencerClock[index].Stop();
                SequencerClock[index].Enabled = false;
                SequencerClock[index] = null;
            }

            Tempo[index] = sequence.Tempo;
            Quantization[index] = sequence.Quantization;
            SequencerClock[index] = new MicroLibrary.MicroTimer();
            TimerFrequency[index] = Tools.GetMidiClockIntervalLong(sequence.Tempo, sequence.Quantization);
            SequencerClock[index].Interval = TimerFrequency[index];
        }

        public static void ChangeTempo(int iNewValue, int iSequence)
        {
            if (Tempo != null && Tempo[iSequence - 1] != iNewValue)
            {
                Tempo[iSequence - 1] = iNewValue;
                if (SequencerClock[iSequence - 1] != null)
                {
                    TimerFrequency[iSequence - 1] = Tools.GetMidiClockIntervalLong(iNewValue, Quantization[iSequence - 1]);
                    SequencerClock[iSequence - 1].Stop();
                    SequencerClock[iSequence - 1].Interval = TimerFrequency[iSequence - 1];
                    SequencerClock[iSequence - 1].Start();
                }
            }
        }

        public static void StopTimers()
        {
            foreach (var clock in SequencerClock.Where(c => c != null))
            {
                clock.Stop();
                clock.Enabled = false;
            }
            SequencerClock = new MicroLibrary.MicroTimer[8];
        }

        public static void StartTimers()
        {
            foreach (var clock in SequencerClock.Where(c => c != null))
            {
                clock.Start();
            }
        }
    }
}
