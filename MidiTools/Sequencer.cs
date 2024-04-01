using Microsoft.Extensions.Primitives;
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
        EventPool Tasks = new EventPool("Sequencer");

        public delegate void SequencerStepHandler(SequenceStep notes, SequenceStep lastnotes, double lengthInMs, int lastPositionInSequence, int positionInSequence);
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

        private int Loop;

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
                int iFirstNote = GetLowestNote(SequencerData.LowKeyTranspose, SequencerData.HighKeyTranspose);
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

        public void InitSequence()
        {
            if (SequenceHasData())
            {
                MidiRouting.InputStaticMidiMessage -= MidiRouting_StaticIncomingMidiMessage;
                MidiRouting.InputStaticMidiMessage += MidiRouting_StaticIncomingMidiMessage;

                Loop = 0;
                SequencerData.SequencerClock[Channel - 1].MicroTimerElapsed += TriggerStep;
            }
        }

        public bool SequenceHasData()
        {
            for (int i = 0; i < Sequence.Length; i++)
            {
                if (Sequence[i] != null && Sequence[i].StepCount <= 0)
                { return false; }
            }
            return true;
        }

        public void StopSequence()
        {
            if (SequenceHasData())
            {
                if (SequencerData.SequencerClock[Channel - 1] != null)
                {
                    MidiRouting.InputStaticMidiMessage -= MidiRouting_StaticIncomingMidiMessage;
                    SequencerData.SequencerClock[Channel - 1].MicroTimerElapsed -= TriggerStep;
                    Loop = 0;
                }
            }
        }

        private void TriggerStep(object sender, MicroLibrary.MicroTimerEventArgs timerEventArgs)
        {
            if (Loop > Steps - 1)
            {
                Loop = 0;
            }

            if (Sequence[Loop] != null) //c'est une tie
            {
                double freq = SequencerData.TimerFrequency[Channel - 1] / 1000;
                double length = ((freq * Sequence[Loop].StepCount) * (Sequence[Loop].GatePercent / 100.0));
                OnInternalSequencerStep?.Invoke(Sequence[Loop], Sequence[LastPositionInSequence], (int)length, LastPositionInSequence, Loop);
                LastPositionInSequence = Loop;
            }

            Loop += 1;
        }

        public void ReinitializeSequence(int iSeqLen)
        {
            Transpose = false;
            Channel = 0;
            Tempo = 120;
            Quantization = "4";
            Steps = iSeqLen;
            SetSequence(null);
        }

        internal int GetLowestNote(int iLowNote, int iHighNote)
        {
            int iNote = -1;
            for (int i = iLowNote; i <= iHighNote; i++)
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
