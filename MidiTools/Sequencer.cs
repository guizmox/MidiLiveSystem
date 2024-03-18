using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace MidiTools
{
    [Serializable]
    public class Sequencer
    {
        public delegate void SequencerStepHandler(SequenceStep notes, double lengthInMs, int lastPositionInSequence, int positionInSequence);
        public event SequencerStepHandler OnInternalSequencerStep;

        public bool[] LiveNotes = new bool[128];
        public int StartNote { get { return Sequence[0].NotesAndVelocity.Count > 0 ? Sequence[0].NotesAndVelocity[0][0] : -1; } }
        public bool Transpose = false;
        public int Channel = 0;
        public int Tempo { get; set; } = 120;
        public int TransposeOffset { get; set; } = 0;

        public string Quantization = "4";
        public int Steps = 32;
        public bool Muted = false;

        public SequenceStep[] Sequence;

        private Timer SequencerClock;
        private int Loop;
        private double TimerFrequency = 0;

        private int LastPositionInSequence = 0;

        public Sequencer()
        {
           
        }

        public Sequencer(int iChannel, string sQuantization, int iSteps, int iTempo, List<SequenceStep> data, bool bTranspose)
        {
            Channel = iChannel;
            Tempo = iTempo;
            Quantization = sQuantization;
            Steps = iSteps;
            SetSequence(data);
            Transpose = bTranspose;
        }

        private void MidiRouting_StaticIncomingMidiMessage(MidiEvent ev)
        {
            if (ev.Type == MidiDevice.TypeEvent.NOTE_ON)
            {
                LiveNotes[ev.Values[0]] = true;
            }
            else if (ev.Type == MidiDevice.TypeEvent.NOTE_OFF)
            {
                LiveNotes[ev.Values[0]] = false;
            }

            if (Transpose)
            {
                int iFirstNote = GetLowestNote();
                if (iFirstNote > -1)
                {
                    int iDelta = StartNote - iFirstNote;
                    TransposeOffset = iDelta;
                }
            }

        }

        public void SetSequence(List<SequenceStep> data)
        {
            Sequence = new SequenceStep[Steps];

            if (data != null)
            {
                for (int i = 0; i < Steps; i++)
                {
                    var step = data.FirstOrDefault(d => d.Step == i);
                    if (step != null)
                    {
                        Sequence[i] = step;
                    }
                    else
                    {
                        Sequence[i] = null;
                    }           
                }
            }
            else
            {
                for (int i = 0; i < Steps; i++)
                {
                    Sequence[i] = new SequenceStep(i, -1, -1, new List<int[]>());
                }
            }
        }

        public async Task StartSequence()
        {
            await Task.Run(() =>
            {
                if (SequenceHasData())
                {
                    if (SequencerClock != null)
                    {
                        SequencerClock.Stop();
                        SequencerClock.Enabled = false;
                        SequencerClock = null;
                        Loop = 0;
                    }

                    MidiRouting.InputStaticMidiMessage -= MidiRouting_StaticIncomingMidiMessage;
                    MidiRouting.InputStaticMidiMessage += MidiRouting_StaticIncomingMidiMessage;

                    TimerFrequency = Tools.GetMidiClockIntervalDouble(Tempo, Quantization);
                    Loop = 0;
                    SequencerClock = new System.Timers.Timer();
                    SequencerClock.Elapsed += TriggerStep;
                    SequencerClock.Interval = (int)Math.Round(TimerFrequency);
                    SequencerClock.Start();
                }
            });
        }

        private bool SequenceHasData()
        {
            for (int i = 0; i < Sequence.Length; i++)
            {
                if (Sequence[i] != null && Sequence[i].StepCount <= 0)
                { return false; }
            }
            return true;
        }

        public async Task StopSequence()
        {
            await Task.Run(() =>
            {
                if (SequenceHasData())
                {
                    if (SequencerClock != null)
                    {
                        MidiRouting.InputStaticMidiMessage -= MidiRouting_StaticIncomingMidiMessage;

                        SequencerClock.Stop();
                        SequencerClock.Enabled = false;
                        SequencerClock = null;
                        Loop = 0;
                    }
                }
            });
        }

        public async Task ChangeTempo(int iNewValue)
        {
            await Task.Run(() =>
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
            });
        }

        private void TriggerStep(object sender, ElapsedEventArgs e)
        {
            if (Loop > Steps - 1)
            {
                Loop = 0;
            }

            if (Sequence[Loop] != null) //c'est une tie
            {
                double length = ((TimerFrequency * Sequence[Loop].StepCount) * (Sequence[Loop].GatePercent / 100.0));
                OnInternalSequencerStep?.Invoke(Sequence[Loop], (int)length, LastPositionInSequence, Loop);
                LastPositionInSequence = Loop;
            }

            Loop += 1;
        }

        public void InitSequence(int iSeqLen)
        {
            Transpose = false;
            Channel = 0;
            Tempo = 120;
            Quantization = "4";
            Steps = iSeqLen;
            SetSequence(null);
        }

        internal int GetLowestNote()
        {
            int iNote = -1;
            for (int i = 0; i < 128; i++)
            {
                if (LiveNotes[i])
                {
                    return i;
                }
            }
            return iNote;
        }
    }

    [Serializable]
    public class SequenceStep
    {
        public int Step = 0;
        public List<int[]> NotesAndVelocity = new List<int[]>();
        public double GatePercent = 50.0;
        public int StepCount = 1;

        private SequenceStep()
        {

        }

        public SequenceStep(int iStep, double gatePercent, int iStepsCount, List<int[]> iNotes)
        {
            Step = iStep;
            NotesAndVelocity = iNotes;
            GatePercent = gatePercent;
            StepCount = iStepsCount;
        }
    }
}
