﻿using System;
using System.Collections.Generic;
using System.Text;

namespace MidiTools
{
    public enum PlayModes
    {
        NORMAL = 1,
        AFTERTOUCH = 2,
        MONO_LOW = 3,
        MONO_HIGH = 4,
        MONO_INTERMEDIATE_HIGH = 5,
        MONO_INTERMEDIATE_LOW = 6,
        HARMONY = 7,
        MONO_IN_BETWEEN = 8,
        PIZZICATO_FAST = 9,
        PIZZICATO_SLOW = 10
    }

    public enum Harmony
    {
        MINOR = 1,
        MAJOR = 2
    }

    [Serializable]
    public class MidiOptions
    {
        public int[] DefaultCCMix = new int[] { 1, 7, 10, 11, 70, 71, 91, 93 };

        public bool Active = true;

        private int _TranspositionOffset = 0;

        private int _VelocityFilterLow = 0;
        private int _VelocityFilterHigh = 127;
        private int _NoteFilterLow = 0;
        private int _NoteFilterHigh = 127;

        private int _CC_Pan_Value = -1;
        private int _CC_Volume_Value = -1;
        private int _CC_Reverb_Value = -1;
        private int _CC_Chorus_Value = -1;
        private int _CC_Release_Value = -1;
        private int _CC_Attack_Value = -1;
        private int _CC_Decay_Value = -1;
        private int _CC_Timbre_Value = -1;
        private int _CC_FilterCutOff_Value = -1;

        public int VelocityFilterLow
        {
            get { return _VelocityFilterLow; }
            set
            {
                if (value < 0) { _VelocityFilterLow = 0; }
                else if (value > 127) { _VelocityFilterLow = 127; }
                else { _VelocityFilterLow = value; }
                if (_VelocityFilterLow > _VelocityFilterHigh) { _VelocityFilterLow = 0; }
            }
        }
        public int VelocityFilterHigh
        {
            get { return _VelocityFilterHigh; }
            set
            {
                if (value < 0) { _VelocityFilterHigh = 0; }
                else if (value > 127) { _VelocityFilterHigh = 127; }
                else { _VelocityFilterHigh = value; }
                if (_VelocityFilterHigh < _VelocityFilterLow) { _VelocityFilterHigh = 127; }
            }
        }
        public int NoteFilterLow
        {
            get { return _NoteFilterLow; }
            set
            {
                if (value < 0) { _NoteFilterLow = 0; }
                else if (value > 127) { _NoteFilterLow = 127; }
                else { _NoteFilterLow = value; }
                if (_NoteFilterLow > _NoteFilterHigh) { _NoteFilterLow = 0; }
            }
        }
        public int NoteFilterHigh
        {
            get { return _NoteFilterHigh; }
            set
            {
                if (value < 0)
                { _NoteFilterHigh = 0; }
                else if (value > 127) { _NoteFilterHigh = 127; }
                else { _NoteFilterHigh = value; }
                if (_NoteFilterHigh < _NoteFilterLow) { _NoteFilterHigh = 127; }
            }
        }

        public bool TransposeNoteRange = false;
        public bool CompressVelocityRange = false;

        public NoteGenerator PlayNote;
        public bool PlayNote_LowestNote = false;

        public int TranspositionOffset
        {
            get { return _TranspositionOffset; }
            set
            {
                if (value < -127) { _TranspositionOffset = -127; }
                else if (value > 127) { _TranspositionOffset = 127; }
                else { _TranspositionOffset = value; }
            }
        }

        public List<int> UndefinedCC = new List<int> { 14, 15, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 85, 86, 87, 89, 90, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119  };

        public PlayModes PlayMode = PlayModes.NORMAL;

        public bool AllowModulation = true;
        public bool AllowNotes = true;
        public bool AllowAllCC = true;
        public bool AllowSysex = true;
        public bool AllowNrpn = true;
        public bool AllowAftertouch = true;
        public bool AllowPitchBend = true;
        public bool AllowProgramChange = true;
        public bool AllowUndefinedCC = true;

        public int CC_ToVolume { get; set; } = -1; //convertisseur de CC pour le volume (ex : CC 102 -> CC 7)

        public int CC_Pan_Value { get { return _CC_Pan_Value; } set { if (value < -1) { _CC_Pan_Value = -1; } else if (value > 127) { _CC_Pan_Value = 64; } else { _CC_Pan_Value = value; } } }
        public int CC_Volume_Value { get { return _CC_Volume_Value; } set { if (value < -1) { _CC_Volume_Value = -1; } else if (value > 127) { _CC_Volume_Value = 127; } else { _CC_Volume_Value = value; } } }
        public int CC_Reverb_Value { get { return _CC_Reverb_Value; } set { if (value < -1) { _CC_Reverb_Value = -1; } else if (value > 127) { _CC_Reverb_Value = 127; } else { _CC_Reverb_Value = value; } } }
        public int CC_Chorus_Value { get { return _CC_Chorus_Value; } set { if (value < -1) { _CC_Chorus_Value = -1; } else if (value > 127) { _CC_Chorus_Value = 127; } else { _CC_Chorus_Value = value; } } }
        public int CC_Release_Value { get { return _CC_Release_Value; } set { if (value < -1) { _CC_Release_Value = -1; } else if (value > 127) { _CC_Release_Value = 127; } else { _CC_Release_Value = value; } } }
        public int CC_Attack_Value { get { return _CC_Attack_Value; } set { if (value < -1) { _CC_Attack_Value = -1; } else if (value > 127) { _CC_Attack_Value = 127; } else { _CC_Attack_Value = value; } } }
        public int CC_Decay_Value { get { return _CC_Decay_Value; } set { if (value < -1) { _CC_Decay_Value = -1; } else if (value > 127) { _CC_Decay_Value = 127; } else { _CC_Decay_Value = value; } } }
        public int CC_Timbre_Value { get { return _CC_Timbre_Value; } set { if (value < -1) { _CC_Timbre_Value = -1; } else if (value > 127) { _CC_Timbre_Value = 127; } else { _CC_Timbre_Value = value; } } }
        public int CC_FilterCutOff_Value { get { return _CC_FilterCutOff_Value; } set { if (value < -1) { _CC_FilterCutOff_Value = -1; } else if (value > 127) { _CC_FilterCutOff_Value = 127; } else { _CC_FilterCutOff_Value = value; } } }

        public List<int[]> CC_Converters { get; private set; } = new List<int[]>();
        public List<int[]> Note_Converters { get; private set; } = new List<int[]>();

        public List<string[]> Translators { get; private set; } = new List<string[]>();

        public bool SmoothCC { get { return SmoothCCLength > 0 ? true : false; } }
        public int SmoothCCLength = 0;
        public int DelayNotesLength = 0;

        public bool AddCCConverter(int iFrom, int iTo)
        {
            if (iFrom != iTo && (iFrom >= 0 && iFrom <= 127) && (iTo >= 0 && iTo <= 127))
            {
                CC_Converters.Add(new int[] { iFrom, iTo });
                return true;
            }
            else { return false; }
        }

        public bool AddNoteConverter(int iFrom, int iTo)
        {
            if (iFrom != iTo && (iFrom >= 0 && iFrom <= 127) && (iTo >= 0 && iTo <= 127))
            {
                Note_Converters.Add(new int[] { iFrom, iTo });
                return true;
            }
            else { return false; }
        }

        public void AddTranslator(string sScript, string sName)
        {
            //TODO : réaliser un contrôle supplémentaire de l'UI ?
            Translators.Add(new string[] { sScript, sName });
        }

        internal MidiOptions Clone()
        {
            return (MidiOptions)this.MemberwiseClone();
        }
    }
}
