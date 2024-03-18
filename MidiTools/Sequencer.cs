using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace MidiTools
{
    [Serializable]
    public class Sequencer
    {
        public delegate void SequencerStepHandler(SequenceStep notes, double lengthInMs);
        public event SequencerStepHandler OnInternalSequencerStep;

        public int StartNote = -1;
        public bool Transpose = false;
        public int Channel = 0;
        public int Tempo = 120;
        public string Quantization = "4";
        public int Steps = 32;

        public SequenceStep[] Sequence;

        private Timer SequencerClock;
        private int Loop;
        private double TimerFrequency = 0;

        public Sequencer()
        { }

        public Sequencer(int iChannel, string sQuantization, int iSteps, int iTempo, SequenceStep[] data, bool bTranspose)
        {
            Channel = iChannel;
            Tempo = iTempo;
            Quantization = sQuantization;
            Steps = iSteps;
            Sequence = data;
            Transpose = bTranspose;
            StartNote = data != null ? data[0].NotesAndVelocity[0][0] : -1;
        }

        public async Task StartSequence()
        {
            await Task.Run(() =>
            {
                if (SequencerClock != null)
                {
                    SequencerClock.Stop();
                    SequencerClock.Enabled = false;
                    SequencerClock = null;
                    Loop = 0;
                }
                if (Sequence != null && Sequence.Length > 0)
                {
                    TimerFrequency = Tools.GetMidiClockIntervalDouble(Tempo, Quantization);
                    Loop = 0;
                    SequencerClock = new System.Timers.Timer();
                    SequencerClock.Elapsed += TriggerStep;
                    SequencerClock.Interval = (int)Math.Round(TimerFrequency);
                    SequencerClock.Start();
                }
            });
        }

        public async Task StopSequence()
        {
            await Task.Run(() =>
            {
                if (SequencerClock != null)
                {
                    SequencerClock.Stop();
                    SequencerClock.Enabled = false;
                    SequencerClock = null;
                    Loop = 0;
                }
            });
        }

        private void TriggerStep(object sender, ElapsedEventArgs e)
        {
            if (Loop > Steps - 1)
            {
                Loop = 0;
            }

            OnInternalSequencerStep?.Invoke(Sequence[Loop], ((TimerFrequency * Sequence[Loop].StepCount) * (Sequence[Loop].GatePercent / 100)));

            Loop += 1;
        }


        public void Clear()
        {
            Sequence = null;
            StartNote = -1;
            Transpose = false;
            Channel = 0;
            Tempo = 120;
            Quantization = "4";
            Steps = 0;
        }
    }

    [Serializable]
    public class SequenceStep
    {
        public int Step = 0;
        public List<int[]> NotesAndVelocity = new List<int[]>();
        public int GatePercent = 50;
        public int StepCount = 1;

        private SequenceStep()
        {

        }

        public SequenceStep(int iStep, int gatePercent, int iStepsCount, List<int[]> iNotes)
        {
            Step = iStep;
            NotesAndVelocity = iNotes;
            GatePercent = gatePercent;
            StepCount = iStepsCount;
        }
    }
}
