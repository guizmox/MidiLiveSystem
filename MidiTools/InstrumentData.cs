using MessagePack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MidiTools
{
    public static class CubaseInstrumentData
    {
        public static List<InstrumentData> Instruments = new List<InstrumentData>();
    }

    [MessagePackObject]
    [Serializable]
    public class InstrumentData
    {
        public delegate void SysExInitializerEventHandler(InstrumentData instr);
        public static event SysExInitializerEventHandler OnSysExInitializerChanged;

        public enum CC_Parameters
        {
            CC_Pan = 10,
            CC_Volume = 7,
            CC_Reverb = 91,
            CC_Chorus = 93,
            CC_Release = 72,
            CC_Attack = 73,
            CC_Decay = 75,
            CC_Timbre = 71,
            CC_FilterCutOff = 74
        }

        [MessagePackObject]
        [Serializable]
        public class ParamToCC
        {
            [Key("CC")]
            public int CC = 0;
            [Key("Param")]
            public CC_Parameters Param;

            public ParamToCC()
            {
            }
            public ParamToCC(CC_Parameters cc, int iValue)
            {
                Param = cc;
                CC = iValue;
            }
        }

        [Key("Categories")]
        public List<PresetHierarchy> Categories { get; } = new List<PresetHierarchy>();
        [Key("Device")]
        public string Device { get; set; } = "";
        [Key("CubaseFile")]
        public string CubaseFile { get; set; } = "";
        [Key("SortedByBank")]
        public bool SortedByBank = false;
        [Key("SysExInitializer")]
        public string SysExInitializer
        {
            get { return _sysexinit; }
            set
            {
                if (!_sysexinit.Equals(value))
                { OnSysExInitializerChanged?.Invoke(this); }
                _sysexinit = value;
            }
        }
        [Key("DefaultCC")]
        public List<ParamToCC> DefaultCC = new List<ParamToCC>();

        private string _sysexinit = "";

        public InstrumentData()
        {
        }

        internal InstrumentData(List<PresetHierarchy> sCats, string sDevice, string sCubaseFile, bool bSortedByBank)
        {
            Categories = sCats;
            Device = sDevice;
            CubaseFile = sCubaseFile;
            SortedByBank = bSortedByBank;
        }

        public InstrumentData(string sFile)
        {
            if (File.Exists(sFile))
            {
                string[] sData = File.ReadAllLines(sFile);
                string sDevice = MidiDeviceContent.SearchDevice(sData);
                if (sDevice.Length > 0)
                {
                    CubaseFile = sFile;

                    var groups = MidiDeviceContent.GetCategories(sData);
                    for (int iG = 0; iG < groups.Count; iG++)
                    {
                        groups[iG].Presets = MidiDeviceContent.GetPresets(groups[iG], sData);
                    }
                    Categories = groups;
                    Device = sDevice;
                }
            }
        }

        public MidiPreset GetPreset(string idx)
        {
            foreach (var c in Categories)
            {
                foreach (var p in c.Presets)
                {
                    if (p.Id.Equals(idx))
                    { return p; }
                }
            }
            return null;
        }

        public InstrumentData Sort(bool bByBank)
        {
            bool bTmpSort = SortedByBank;
            SortedByBank = bByBank;
            if (bByBank)
            {
                if (!bTmpSort)
                {
                    List<MidiPreset> instrP = new List<MidiPreset>();
                    List<PresetHierarchy> instrH = new List<PresetHierarchy>();
                    foreach (var cat in Categories)
                    {
                        instrP.AddRange(cat.Presets);
                    }

                    int iMsb = instrP.Select(p => p.Msb).Distinct().Count();
                    int iLsb = instrP.Select(p => p.Lsb).Distinct().Count();

                    bool bMsb = iMsb < iLsb ? true : false;

                    if (bMsb)
                    {
                        instrP = instrP.OrderBy(p => p.Msb).ThenBy(p => p.Lsb).ThenBy(p => p.Prg).ToList();
                    }
                    else
                    {
                        instrP = instrP.OrderBy(p => p.Lsb).ThenBy(p => p.Msb).ThenBy(p => p.Prg).ToList();
                    }

                    for (int iC = 0; iC < instrP.Count; iC++)
                    {
                        string sCat = bMsb ? instrP[iC].Msb.ToString("000") : instrP[iC].Lsb.ToString("000");
                        if (instrH.Count(i => i.Category.Equals(sCat)) == 0)
                        {
                            instrH.Add(new PresetHierarchy(iC, sCat, sCat, 1));
                        }
                    }
                    for (int iC = 0; iC < instrP.Count; iC++)
                    {
                        string sCat = bMsb ? string.Concat(instrP[iC].Msb.ToString("000"), "-", instrP[iC].Lsb.ToString("000")) : string.Concat(instrP[iC].Lsb.ToString("000"), "-", instrP[iC].Msb.ToString("000"));
                        if (instrH.Count(i => i.Category.Equals(sCat) && i.Level == 2) == 0)
                        {
                            instrH.Add(new PresetHierarchy(iC, sCat, sCat, 2));
                        }
                    }

                    foreach (var cat in instrH.Where(i => i.Level == 2))
                    {
                        if (bMsb)
                        {
                            var list = instrP.Where(p => string.Concat(p.Msb.ToString("000"), "-", p.Lsb.ToString("000")).Equals(cat.Category)).ToList();
                            cat.Presets.AddRange(list);
                        }
                        else
                        {
                            var list = instrP.Where(p => string.Concat(p.Lsb.ToString("000"), "-", p.Msb.ToString("000")).Equals(cat.Category)).ToList();
                            cat.Presets.AddRange(list);
                        }
                    }

                    instrH = instrH.OrderBy(c => c.Category).ToList();

                    foreach (var cat in instrH)
                    {
                        if (cat.Level == 1)
                        {
                            cat.Category = string.Concat((bMsb ? "MSB : " : "LSB : ") + Convert.ToInt32(cat.Category.Split('-')[0]).ToString("000"));
                        }
                        else if (cat.Level == 2)
                        {
                            cat.Category = string.Concat((bMsb ? "LSB : " : "MSB : ") + Convert.ToInt32(cat.Category.Split('-')[1]).ToString("000"));
                        }
                    }

                    return new InstrumentData(instrH, Device, CubaseFile, true);
                }
                else { return this; }
            }
            else
            {
                if (bTmpSort)
                {
                    return new InstrumentData(CubaseFile);
                }
                else
                {
                    return this;
                }
            }
        }

        public void ChangeDevice(string sNewName)
        {
            if (sNewName.Length > 0)
            {
                Device = sNewName;
            }
        }

        public int GetCCParameter(CC_Parameters p)
        {
            var param = DefaultCC.FirstOrDefault(cc => cc.Param == p);

            if (param != null)
            {
                return ((int)param.CC);
            }
            else
            {
                return ((int)p);
            }
        }

        public string AddCCParameter(CC_Parameters p, string sCC)
        {
            int iCC = -1;
            if (int.TryParse(sCC, out iCC))
            {
                if (iCC < 0 && iCC > 127)
                {
                    return ((int)p).ToString();
                }
                else
                {
                    var param = DefaultCC.FirstOrDefault(cc => cc.Param == p);
                    if (param == null)
                    {
                        DefaultCC.Add(new ParamToCC(p, iCC));
                        return iCC.ToString();
                    }
                    else
                    {
                        if (param.CC != iCC)
                        {
                            param.CC = iCC;
                            return iCC.ToString();
                        }
                        else { return iCC.ToString(); }
                    }

                }
            }
            else { return ((int)p).ToString(); }
        }
    }

    [MessagePackObject]
    [Serializable]
    public class PresetHierarchy
    {
        [Key("Category")]
        public string Category = "";
        [Key("Level")]
        public int Level = 0;
        [Key("IndexInFile")]
        public int IndexInFile = 0;
        [Key("Raw")]
        public string Raw = "";
        [Key("Presets")]
        public List<MidiPreset> Presets { get; set; } = new List<MidiPreset>();

        public PresetHierarchy(int iIndex, string sRaw, string sCategory, int iLevel)
        {
            this.Category = sCategory;
            this.Level = iLevel;
            this.Raw = sRaw;
            this.IndexInFile = iIndex;
        }

        public PresetHierarchy()
        {

        }
    }
}
