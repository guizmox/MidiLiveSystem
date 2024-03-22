using Jacobi.Vst.Core;
using Jacobi.Vst.Host.Interop;
using System;

// Copied from the microDRUM project
// https://github.com/microDRUM
// I think it is created by massimo.bernava@gmail.com
// Modified by perivar@nerseth.com
namespace CommonUtils.VSTPlugin
{
    public class VST
	{
		public VstPluginContext pluginContext = null;
		public event EventHandler<VSTStreamEventArgs> StreamCall=null;
		
		//EditParametersForm edit = new EditParametersForm();

		internal void Dispose()
		{
			//edit.Close();
			if(pluginContext!=null) pluginContext.Dispose();
		}
		
		public void MIDI_NoteOn(byte Note, byte Velocity)
		{
			byte Cmd = 0x90;
			MIDI(Cmd, Note, Velocity);
		}

        public void MIDI_NoteOff(byte Note, byte Velocity)
        {
            byte Cmd = 0x80;
            MIDI(Cmd, Note, Velocity);
        }

        public void MIDI_ProgramChange(byte programNumber)
        {
            byte Cmd = 0xC0; // Code de commande MIDI pour le changement de programme
            MIDI(Cmd, programNumber, 0); // La vélocité est généralement 0 pour un changement de programme
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
            MIDI(cmd, lsb, msb);
        }

        public void MIDI_Aftertouch(byte pressureValue)
        {
            // Assurez-vous que la valeur de la pression est dans la plage valide (0 à 127 inclus)
            if (pressureValue < 0 || pressureValue > 127)
            {
                throw new ArgumentOutOfRangeException("pressureValue", "La valeur de la pression doit être comprise entre 0 et 127.");
            }

            byte cmd = 0xD0; // Code de commande MIDI pour l'aftertouch (pression du canal)
            MIDI(cmd, pressureValue, 0); // Le deuxième paramètre est la valeur de pression, le troisième est souvent ignoré dans le cas de l'aftertouch
        }

        public void MIDI_PolyphonicAftertouch(byte note, byte pressureValue)
        {
            // Assurez-vous que la note et la valeur de la pression sont dans les plages valides (0 à 127 inclus)
            if (note < 0 || note > 127 || pressureValue < 0 || pressureValue > 127)
            {
                throw new ArgumentOutOfRangeException("note", "La note doit être comprise entre 0 et 127, et la valeur de la pression doit être comprise entre 0 et 127.");
            }

            byte cmd = 0xA0; // Code de commande MIDI pour le polyphonic aftertouch
            MIDI(cmd, note, pressureValue);
        }

        public void MIDI_CC(byte Number, byte Value)
		{
			byte Cmd = 0xB0;
			MIDI(Cmd, Number, Value);
		}

		private void MIDI(byte Cmd,byte Val1,byte Val2)
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

			var ve = new VstEvent[1];
			ve[0] = vse;

			pluginContext.PluginCommandStub.Commands.ProcessEvents(ve);
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
