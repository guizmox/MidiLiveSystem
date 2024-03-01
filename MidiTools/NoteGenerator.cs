using System;
using System.Collections.Generic;
using System.Text;

namespace MidiTools
{


    [Serializable]
    public class NoteGenerator
    {
        public int Velocity = 64;
        public int Note = 64;
        public int Octave = 3;
        public int Channel = 1;
        public decimal Length = 1;

        public NoteGenerator(int iChannel, int iOctave, int iNote, int iVelocity, decimal dLength)
        {
            Channel = iChannel;
            SetOctave(iOctave);
            SetNote(iNote.ToString());
            SetVelocity(iVelocity.ToString());
            SetLength(dLength);
        }

        public NoteGenerator()
        {

        }

        private void SetLength(decimal dLength)
        {
            if (dLength > 9) { Length = 9; }
            else if (dLength < 0) { Length = 0; }
            else { Length = dLength; }
        }

        public void SetOctave(int iOctave)
        {
            if (iOctave > -1 && iOctave <= 9)
            {
                Octave = iOctave;
            }
        }

        public void ChangeOctave(int iOffset)
        {
            if ((Octave + iOffset) > -1 && (Octave + iOffset) <= 9)
            {
                Octave += iOffset;
            }
        }

        public void SetNote(string sNote)
        {
            int iNote = 0;
            if (int.TryParse(sNote.Trim(), out iNote))
            {
                if (iNote + (12 * Octave) > -1 && iNote + (12 * Octave) <= 127)
                {
                    Note = (iNote + (12 * Octave));
                }
            }
        }

        public void SetVelocity(string sVel)
        {
            int iVel = 0;
            if (int.TryParse(sVel.Trim(), out iVel))
            {
                if (iVel > -1 && iVel <= 127)
                {
                    Velocity = iVel;
                }
            }
        }
    }
}
