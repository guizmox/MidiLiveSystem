using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace MidiTools
{

    [MessagePackObject]
    [Serializable]
    public class NoteGenerator
    {
        [Key("Velocity")]
        public int Velocity = 64;
        [Key("Note")]
        public int Note = 64;
        [Key("Octave")]
        public int Octave = 3;
        [Key("Channel")]
        public int Channel = 1;
        [Key("Length")]
        public decimal Length = 100;

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
            if (dLength > 10000) { Length = 10000; }
            else if (dLength < 100) { Length = 100; }
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
