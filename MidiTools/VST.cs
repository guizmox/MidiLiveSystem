using Jacobi.Vst.Core;
using Jacobi.Vst.Host.Interop;
using MidiTools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

// Copied from the microDRUM project
// https://github.com/microDRUM
// I think it is created by massimo.bernava@gmail.com
// Modified by perivar@nerseth.com
namespace CommonUtils.VSTPlugin
{
    public class VST
    {
        private System.Timers.Timer StackTimer;
        public VstPluginContext pluginContext = null;
        private ConcurrentBag<VstEvent> MidiStack = new ConcurrentBag<VstEvent>();
        private ConcurrentBag<VstEvent> MidiStackNoteOn = new ConcurrentBag<VstEvent>();

        public event EventHandler<VSTStreamEventArgs> StreamCall = null;

        public VST()
        {
            StackTimer = new System.Timers.Timer();
            StackTimer.Elapsed += StackTimer_Elapsed;
            StackTimer.Interval = 10;
            StackTimer.Start();
        }

        private void StackTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //je force les évènements NOTE ON à être lancés avant pour éviter le risque de notes non relâchées (NOTE OFF avant NOTE ON)
            if (MidiStackNoteOn.Count > 0)
            {
                pluginContext.PluginCommandStub.Commands.ProcessEvents(MidiStackNoteOn.ToArray());
                MidiStackNoteOn.Clear();
            }

            if (MidiStack.Count > 0)
            {
                pluginContext.PluginCommandStub.Commands.ProcessEvents(MidiStack.ToArray());
                MidiStack.Clear();
            }
        }

        //EditParametersForm edit = new EditParametersForm();

        internal void Dispose()
        {
            StackTimer.Stop();
            StackTimer.Enabled = false;
            StackTimer = null;

            //edit.Close();
            if (pluginContext != null)
            {
                pluginContext.Dispose();
            }
        }

        public void MIDI_NoteOn(byte Note, byte Velocity)
        {
            byte Cmd = 0x90;
            MIDI(true, Cmd, Note, Velocity);
        }

        public void MIDI_NoteOff(byte Note, byte Velocity)
        {
            byte Cmd = 0x80;
            MIDI(false, Cmd, Note, Velocity);
        }

        public void MIDI_ProgramChange(byte programNumber)
        {
            byte Cmd = 0xC0; // Code de commande MIDI pour le changement de programme
            MIDI(false, Cmd, programNumber, 0); // La vélocité est généralement 0 pour un changement de programme
        }

        public void MIDI_PitchBend(int pitchValue)
        {
            // Assurez-vous que la valeur du pitch bend est dans la plage valide (-8192 à 8191 inclus)
            if (pitchValue < -8192 || pitchValue > 8191)
            {
                throw new ArgumentOutOfRangeException("pitchValue", "La valeur du pitch bend doit être comprise entre -8192 et 8191.");
            }

            // Calculer les valeurs MSB et LSB à partir de la valeur du pitch bend
            byte lsb = (byte)(pitchValue & 0x7F);
            byte msb = (byte)((pitchValue >> 7) & 0x7F);

            byte cmd = 0xE0; // Code de commande MIDI pour le pitch bend
            MIDI(false, cmd, lsb, msb);
        }

        public void MIDI_Aftertouch(byte pressureValue)
        {
            // Assurez-vous que la valeur de la pression est dans la plage valide (0 à 127 inclus)
            if (pressureValue < 0 || pressureValue > 127)
            {
                throw new ArgumentOutOfRangeException("pressureValue", "La valeur de la pression doit être comprise entre 0 et 127.");
            }

            byte cmd = 0xD0; // Code de commande MIDI pour l'aftertouch (pression du canal)
            MIDI(false, cmd, pressureValue, 0); // Le deuxième paramètre est la valeur de pression, le troisième est souvent ignoré dans le cas de l'aftertouch
        }

        public void MIDI_PolyphonicAftertouch(byte note, byte pressureValue)
        {
            // Assurez-vous que la note et la valeur de la pression sont dans les plages valides (0 à 127 inclus)
            if (note < 0 || note > 127 || pressureValue < 0 || pressureValue > 127)
            {
                throw new ArgumentOutOfRangeException("note", "La note doit être comprise entre 0 et 127, et la valeur de la pression doit être comprise entre 0 et 127.");
            }

            byte cmd = 0xA0; // Code de commande MIDI pour le polyphonic aftertouch
            MIDI(false, cmd, note, pressureValue);
        }

        public void MIDI_CC(byte Number, byte Value)
        {
            byte Cmd = 0xB0;
            MIDI(false, Cmd, Number, Value);
        }

        private void MIDI(bool bNoteOn, byte Cmd, byte Val1, byte Val2)
        {
            var midiData = new byte[4];
            midiData[0] = Cmd;
            midiData[1] = Val1;
            midiData[2] = Val2;
            midiData[3] = 0;    // Reserved, unused

            var vse = new VstMidiEvent(/*DeltaFrames*/ 0,
                                       /*NoteLength*/ 0,
                                       /*NoteOffset*/  0,
                                       midiData,
                                       /*Detune*/        0,
                                       /*NoteOffVelocity*/ 127);

            if (bNoteOn)
            {
                MidiStackNoteOn.Add(vse);
            }
            else
            {
                MidiStack.Add(vse);
            }
        }

        //internal void ShowEditParameters()
        //{
        //	edit.AddParameters(pluginContext);
        //	edit.Show();
        //}

        internal void Stream_ProcessCalled(object sender, VSTStreamEventArgs e)
        {
            if (StreamCall != null) StreamCall(sender, e);
        }
    }
}
