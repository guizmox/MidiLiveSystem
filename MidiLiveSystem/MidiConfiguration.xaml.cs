﻿using MessagePack;
using Microsoft.Win32;
using MidiTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace MidiLiveSystem
{
    public partial class MidiConfiguration : Window
    {
        internal ProjectConfiguration Configuration = new();
        private readonly MidiRouting Routing;

        public MidiConfiguration()
        {
            InitializeComponent();
            InitPage(false, null);
        }

        public MidiConfiguration(ProjectConfiguration pc, List<RoutingBox> boxes, MidiRouting routing)
        {
            Configuration = pc;
            Configuration = Configuration.Clone();
            Routing = routing;

            InitializeComponent();
            InitPage(true, boxes);
        }

        private void InitPage(bool bFromSave, List<RoutingBox> boxes)
        {
            //comparaison avec le projet et les devices réels
            if (bFromSave)
            {
                tbProjectName.Text = Configuration.ProjectName;

                if (Configuration.HorizontalGrid > -1)
                {
                    cbHorizontalItems.SelectedValue = Configuration.HorizontalGrid.ToString();
                    cbVerticalItems.SelectedValue = Configuration.VerticalGrid.ToString();
                }

                if (Configuration.DevicesIN == null)
                {
                    var listDevicesIn = new List<string>();
                    foreach (var v in MidiTools.MidiRouting.InputDevices)
                    {
                        listDevicesIn.Add(v.Name);
                    }
                    Configuration.DevicesIN = listDevicesIn;
                }

                if (Configuration.DevicesOUT == null)
                {
                    var listDevicesOut = new List<string>();
                    foreach (var v in MidiTools.MidiRouting.OutputDevices)
                    {
                        listDevicesOut.Add(v.Name);
                    }
                    Configuration.DevicesOUT = listDevicesOut;
                }

                cbMidiInClock.Items.Add(new ComboBoxItem() { Tag = Tools.INTERNAL_GENERATOR, Content = Tools.INTERNAL_GENERATOR });
                foreach (var d in Configuration.DevicesIN)
                {
                    if (!MidiRouting.InputDevices.Any(dev => dev.Name.Equals(d)))
                    {
                        cbMidiIn.Items.Add(new ComboBoxItem() { Tag = d, Content = string.Concat(d, " (NOT FOUND !)") });
                        cbAllInputs.Items.Add(new CheckBox() { Tag = string.Concat(d, " (NOT FOUND !)"), IsChecked = false, Content = string.Concat(d, " (NOT FOUND !)") });
                        cbMidiInClock.Items.Add(new ComboBoxItem() { Tag = d, Content = string.Concat(d, " (NOT FOUND !)") });
                    }
                    else
                    {
                        cbMidiIn.Items.Add(new ComboBoxItem() { Tag = d, Content = d });
                        cbAllInputs.Items.Add(new CheckBox() { Tag = d, IsChecked = false, Content = d });
                        cbMidiInClock.Items.Add(new ComboBoxItem() { Tag = d, Content = d });
                        cbMidiInRecall.Items.Add(new ComboBoxItem() { Tag = d, Content = d });
                    }
                }

                foreach (var d in Configuration.DevicesOUT)
                {
                    if (!MidiRouting.OutputDevices.Any(dev => dev.Name.Equals(d)))
                    {
                        cbMidiOut.Items.Add(new ComboBoxItem() { Tag = d, Content = string.Concat(d, " (NOT FOUND !)") });
                    }
                    else
                    {
                        cbMidiOut.Items.Add(new ComboBoxItem() { Tag = d, Content = d });
                    }
                }

                foreach (CheckBox cb in cbAllInputs.Items)
                {
                    if (Configuration.AllInputs.Contains(cb.Tag.ToString()))
                    {
                        cb.IsChecked = true;
                    }
                }

                tbBPM.Text = Configuration.BPM.ToString();
                ckActivateClock.IsChecked = Configuration.ClockActivated;
                try
                {
                    cbMidiInClock.SelectedValue = Configuration.ClockDevice;
                }
                catch
                {
                    cbMidiInClock.SelectedIndex = 0;
                    MessageBox.Show("Unable to set Master Clock Device. Set to default");
                }

                tbRecallButtonsTrigger.Text = Configuration.TriggerRecallButtonsValue.ToString();
                cbRecallButtonsTrigger.SelectedValue = Configuration.TriggerRecallButtons;
                cbMidiInRecall.SelectedValue = Configuration.TriggerRecallDevice;

            }
            else
            {
                tbBPM.Text = "120";
                cbMidiInClock.SelectedIndex = 0;
                ckActivateClock.IsChecked = false;

                tbProjectName.Text = "My Project";
                cbHorizontalItems.SelectedValue = "-1";
                cbVerticalItems.SelectedValue = "-1";

                tbRecallButtonsTrigger.Text = "UI";
                cbRecallButtonsTrigger.SelectedValue = "0";

                cbMidiInClock.Items.Add(new ComboBoxItem() { Tag = Tools.INTERNAL_GENERATOR, Content = Tools.INTERNAL_GENERATOR });
                foreach (var s in MidiTools.MidiRouting.InputDevices)
                {
                    cbMidiIn.Items.Add(new ComboBoxItem() { Tag = s.Name, Content = s.Name });
                    cbAllInputs.Items.Add(new CheckBox() { Tag = s.Name, IsChecked = false, Content = s.Name });
                    cbMidiInClock.Items.Add(new ComboBoxItem() { Tag = s.Name, Content = s.Name });
                    cbMidiInRecall.Items.Add(new ComboBoxItem() { Tag = s.Name, Content = s.Name });
                }
                cbMidiInClock.SelectedIndex = 0;

                foreach (var s in MidiTools.MidiRouting.OutputDevices)
                {
                    cbMidiOut.Items.Add(new ComboBoxItem() { Tag = s.Name, Content = s.Name });
                }
            }

            if (boxes != null)
            {
                int iB = 0;
                foreach (var box in boxes.OrderBy(b => b.GridPosition))
                {
                    iB++;
                    cbRoutingNames.Items.Add(new TextBox() { Text = box.BoxName, Tag = box.BoxGuid });
                }
            }
        }

        private void InitCC(string sDevice)
        {
            var device = CubaseInstrumentData.Instruments.FirstOrDefault(i => i.Device.Equals(sDevice));

            if (device != null)
            {
                foreach (var item in cbCCDefault.Items)
                {
                    ComboBoxCustomItem cb = (ComboBoxCustomItem)item;

                    switch (cb.Id)
                    {
                        case "tbCC_Chorus":
                            cb.Value = device.GetCCParameter(InstrumentData.CC_Parameters.CC_Chorus).ToString();
                            break;
                        case "tbCC_Pan":
                            cb.Value = device.GetCCParameter(InstrumentData.CC_Parameters.CC_Pan).ToString();
                            break;
                        case "tbCC_Volume":
                            cb.Value = device.GetCCParameter(InstrumentData.CC_Parameters.CC_Volume).ToString();
                            break;
                        case "tbCC_Attack":
                            cb.Value = device.GetCCParameter(InstrumentData.CC_Parameters.CC_Attack).ToString();
                            break;
                        case "tbCC_Decay":
                            cb.Value = device.GetCCParameter(InstrumentData.CC_Parameters.CC_Decay).ToString();
                            break;
                        case "tbCC_Release":
                            cb.Value = device.GetCCParameter(InstrumentData.CC_Parameters.CC_Release).ToString();
                            break;
                        case "tbCC_Reverb":
                            cb.Value = device.GetCCParameter(InstrumentData.CC_Parameters.CC_Reverb).ToString();
                            break;
                        case "tbCC_Timbre":
                            cb.Value = device.GetCCParameter(InstrumentData.CC_Parameters.CC_Timbre).ToString();
                            break;
                        case "tbCC_CutOff":
                            cb.Value = device.GetCCParameter(InstrumentData.CC_Parameters.CC_FilterCutOff).ToString();
                            break;
                    }
                }
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            GetConfiguration();
        }

        private void btnSaveDefaultCC_Click(object sender, RoutedEventArgs e)
        {
            if (cbMidiOut.SelectedIndex >= 0)
            {
                InstrumentData instr = CubaseInstrumentData.Instruments.FirstOrDefault(i => i.Device.Equals(((ComboBoxItem)cbMidiOut.SelectedItem).Tag.ToString()));

                if (instr == null)
                {
                    CubaseInstrumentData.Instruments.Add(new InstrumentData() { Device = ((ComboBoxItem)cbMidiOut.SelectedItem).Tag.ToString() });
                    instr = CubaseInstrumentData.Instruments.Last();
                }

                foreach (var item in cbCCDefault.Items)
                {
                    ComboBoxCustomItem cb = (ComboBoxCustomItem)item;

                    switch (cb.Id)
                    {
                        case "tbCC_Chorus":
                            cb.Value = instr.AddCCParameter(InstrumentData.CC_Parameters.CC_Chorus, cb.Value);
                            break;
                        case "tbCC_Pan":
                            cb.Value = instr.AddCCParameter(InstrumentData.CC_Parameters.CC_Pan, cb.Value);
                            break;
                        case "tbCC_Volume":
                            cb.Value = instr.AddCCParameter(InstrumentData.CC_Parameters.CC_Volume, cb.Value);
                            break;
                        case "tbCC_Attack":
                            cb.Value = instr.AddCCParameter(InstrumentData.CC_Parameters.CC_Attack, cb.Value);
                            break;
                        case "tbCC_Decay":
                            cb.Value = instr.AddCCParameter(InstrumentData.CC_Parameters.CC_Decay, cb.Value);
                            break;
                        case "tbCC_Release":
                            cb.Value = instr.AddCCParameter(InstrumentData.CC_Parameters.CC_Release, cb.Value);
                            break; ;
                        case "tbCC_Reverb":
                            cb.Value = instr.AddCCParameter(InstrumentData.CC_Parameters.CC_Reverb, cb.Value);
                            break;
                        case "tbCC_Timbre":
                            cb.Value = instr.AddCCParameter(InstrumentData.CC_Parameters.CC_Timbre, cb.Value);
                            break;
                        case "tbCC_CutOff":
                            cb.Value = instr.AddCCParameter(InstrumentData.CC_Parameters.CC_FilterCutOff, cb.Value);
                            break;
                    }
                }
            }
            else
            {
                MessageBox.Show("You must select a OUT Port");
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnLoadPresetFile_Click(object sender, RoutedEventArgs e)
        {
            bool bOK = false;
            bool bNeedUpdate = false;

            if (cbMidiOut.SelectedIndex >= 0)
            {
                string sPort = ((ComboBoxItem)cbMidiOut.SelectedItem).Tag.ToString();
                var instr = CubaseInstrumentData.Instruments.FirstOrDefault(i => i.Device.Equals(sPort));
                if (instr != null)
                {
                    var confirm = MessageBox.Show("There's already a Preset List for that Device. Are you sure to update ?", "Update ?", MessageBoxButton.YesNo);
                    if (confirm == MessageBoxResult.Yes)
                    {
                        bOK = true;
                        bNeedUpdate = true;
                    }
                }
                else { bOK = true; }

                if (bOK)
                {
                    OpenFileDialog fi = new()
                    {
                        Filter = "(*.txt)|*.txt"
                    };
                    fi.ShowDialog();
                    string sFile = fi.FileName;

                    if (File.Exists(sFile))
                    {
                        InstrumentData data = new(sFile);
                        if (data != null && data.Device.Length > 0)
                        {
                            MessageBox.Show("Instrument successfully loaded (" + data.Device + ")");
                            data.ChangeDevice(sPort);

                            if (bNeedUpdate)
                            {
                                data = CubaseInstrumentData.UpdateData(instr, data);
                            }

                            if (instr != null)
                            {
                                CubaseInstrumentData.Instruments.Remove(instr);
                            }
                            CubaseInstrumentData.Instruments.Add(data);
                        }
                        else
                        {
                            MessageBox.Show("Invalid Instrument File. Please provide a valid Cubase File");
                        }
                    }
                    else
                    {
                        MessageBox.Show("File not available : " + Path.GetFileName(sFile));
                    }
                }
            }
            else
            {
                MessageBox.Show("You must select a OUT Port");
            }

        }

        private void btnShowPresets_Click(object sender, RoutedEventArgs e)
        {
            var device = cbMidiOut.SelectedItem;
            if (device != null)
            {
                var instr = CubaseInstrumentData.Instruments.FirstOrDefault(i => i.Device.Equals(((ComboBoxItem)device).Tag.ToString()));

                if (instr == null)
                {
                    MessageBox.Show("No Instrument Data associated. Please load a valid Cubase file first.");
                }
                else
                {
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
                    {
                        PresetBrowser pb = new(instr);
                        pb.ShowDialog();
                    }));
                }
            }
        }

        private void btnInitializeSysEx_Click(object sender, RoutedEventArgs e)
        {
            if (cbMidiOut.SelectedIndex >= 0)
            {
                string sPort = ((ComboBoxItem)cbMidiOut.SelectedItem).Tag.ToString();
                var instr = CubaseInstrumentData.Instruments.FirstOrDefault(i => i.Device.Equals(sPort));
                SysExInput sys;

                if (instr == null)
                {
                    sys = new SysExInput();
                }
                else
                {
                    sys = new SysExInput(instr.SysExInitializer);
                }

                sys.ShowDialog();
                if (sys.InvalidData)
                {
                    MessageBox.Show("Cancelled.");
                }
                else
                {
                    TextRange textRange = new(sys.rtbSysEx.Document.ContentStart, sys.rtbSysEx.Document.ContentEnd);
                    string sSysex = textRange.Text.Replace("-", "").Trim();


                    if (instr != null)
                    {
                        instr.SysExInitializer = sSysex;
                    }
                    else
                    {
                        InstrumentData newInstrument = new()
                        {
                            Device = sPort,
                            SysExInitializer = sSysex
                        };
                        CubaseInstrumentData.Instruments.Add(newInstrument);
                    }
                }
            }
            else
            {
                MessageBox.Show("You must select a OUT Port");
            }
        }

        private void cbMidiOut_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem cb = (ComboBoxItem)e.AddedItems[0];
            if (cb != null)
            {
                InitCC(cb.Tag.ToString());
            }
        }

        private void btnPanic_Click(object sender, RoutedEventArgs e)
        {
            MidiRouting.Panic(true);
        }

        private void GetConfiguration()
        {
            string projectName = tbProjectName.Text.Trim();

            List<string> sDevicesIn = new();
            List<string> sDevicesOut = new();
            List<string> allinputs = new();

            foreach (ComboBoxItem cb in cbMidiIn.Items)
            {
                sDevicesIn.Add(cb.Tag.ToString());
            }
            foreach (ComboBoxItem cb in cbMidiOut.Items)
            {
                sDevicesOut.Add(cb.Tag.ToString());
            }
            foreach (CheckBox cb in cbAllInputs.Items)
            {
                if (cb.IsChecked.Value)
                {
                    allinputs.Add(cb.Tag.ToString());
                }
            }

            List<string[]> boxnames = new();
            int iPos = 0;
            foreach (TextBox cb in cbRoutingNames.Items)
            {
                boxnames.Add(new string[] { cb.Text, cb.Tag.ToString(), iPos.ToString() });
                iPos++;
            }

            Configuration.IsDefaultConfig = false;
            Configuration.BoxNames = boxnames;
            Configuration.ProjectName = projectName;
            Configuration.DevicesIN = sDevicesIn;
            Configuration.DevicesOUT = sDevicesOut;
            Configuration.AllInputs = allinputs;
            Configuration.HorizontalGrid = Convert.ToInt32(cbHorizontalItems.SelectedValue.ToString());
            Configuration.VerticalGrid = Convert.ToInt32(cbVerticalItems.SelectedValue.ToString());
            Configuration.TriggerRecallButtons = ((ComboBoxItem)cbRecallButtonsTrigger.SelectedItem).Tag.ToString();
            Configuration.AddTriggerDevice(cbMidiInRecall.SelectedItem == null ? "" : ((ComboBoxItem)cbMidiInRecall.SelectedItem).Tag.ToString());
            Configuration.AddClockDevice(cbMidiInClock.SelectedItem == null ? "" : ((ComboBoxItem)cbMidiInClock.SelectedItem).Tag.ToString());
            Configuration.ClockActivated = ckActivateClock.IsChecked.Value;

            if (int.TryParse(tbRecallButtonsTrigger.Text, out int iTriggerValue))
            {
                if (iTriggerValue < 0 || iTriggerValue > 127)
                {
                    iTriggerValue = 0;
                }
            }

            Configuration.TriggerRecallButtonsValue = iTriggerValue;

            if (int.TryParse(tbBPM.Text.Trim(), out int iBpm))
            {
                if (iBpm >= 40 && iBpm <= 300)
                {
                    Configuration.BPM = iBpm;
                }
                else
                {
                    MessageBox.Show("Invalid BPM Value (range : 40 - 300). Set to default (120).");
                    Configuration.BPM = 120;
                }
            }
            else
            {
                MessageBox.Show("Invalid BPM Value (range : 40 - 300). Set to default (120).");
                Configuration.BPM = 120;
            }
        }

    }

    [MessagePackObject]
    [Serializable]
    public class ProjectConfiguration
    {
        [MessagePackObject]
        [Serializable]
        public class RecallConfiguration
        {
            [Key("ButtonName")]
            public string ButtonName = "";
            [Key("ButtonIndex")]
            public int ButtonIndex = 0;
            [Key("BoxGuids")]
            public List<Guid> BoxGuids = new();
            [Key("BoxPresets")]
            public List<int> BoxPresets = new();

            public RecallConfiguration()
            {

            }

            public RecallConfiguration(List<Guid> boxguids, List<int> boxpresets, string name, int iButton)
            {
                BoxGuids = boxguids;
                BoxPresets = boxpresets;
                ButtonName = name;
                ButtonIndex = iButton;
            }
        }

        private List<string> _listDevicesIn = null;
        private List<string> _listDevicesOut = null;
        private List<string> _allinputs = new();

        [Key("ProjectId")]
        public Guid ProjectId { get; set; } = Guid.NewGuid();

        [Key("ProjectName")]
        public string ProjectName { get; set; } = "My Project";

        [Key("DevicesOUT")]
        public List<string> DevicesOUT
        {
            get { return _listDevicesOut; }
            set { _listDevicesOut = value; }
        }

        [Key("AllInputs")]
        public List<string> AllInputs
        {
            get { return _allinputs; }
            set { _allinputs = value; }
        }

        [Key("RecallData")]
        public List<RecallConfiguration> RecallData { get; set; } = new List<RecallConfiguration>();

        [Key("DevicesIN")]
        public List<string> DevicesIN
        {
            get { return _listDevicesIn; }
            set { _listDevicesIn = value; }
        }

        [Key("IsDefaultConfig")]
        public bool IsDefaultConfig { get; set; } = true;

        [Key("RecordedSequence")]
        public MidiSequence RecordedSequence { get; set; }

        [Key("BoxNames")]
        public List<string[]> BoxNames { get; set; } = null;

        [Key("HorizontalGrid")]
        public int HorizontalGrid { get; set; } = -1;

        [Key("VerticalGrid")]
        public int VerticalGrid { get; set; } = -1;

        [IgnoreMember]
        private string _clockDevice = "";

        [Key("ClockDevice")]
        public string ClockDevice
        {
            get { return _clockDevice; }
            set { AddClockDevice(value); }
        }

        [Key("BPM")]
        public int BPM { get; set; } = 120;

        [Key("ClockActivated")]
        public bool ClockActivated { get; set; } = false;

        [IgnoreMember]
        private string _triggerDevice = "";

        [Key("TriggerRecallButtons")]
        public string TriggerRecallButtons { get; set; } = "UI";

        [Key("TriggerRecallButtonsValue")]
        public int TriggerRecallButtonsValue { get; set; } = 0;

        [Key("TriggerRecallDevice")]
        public string TriggerRecallDevice
        {
            get { return _triggerDevice; }
            set { AddTriggerDevice(value); }
        }

        public ProjectConfiguration()
        {

        }

        public void AddClockDevice(string sDevice)
        {
            MidiRouting.CheckAndCloseINPort(_clockDevice);
            MidiRouting.CheckAndOpenINPort(sDevice);
            _clockDevice = sDevice;
        }

        public void AddTriggerDevice(string sDevice)
        {
            MidiRouting.CheckAndCloseINPort(_triggerDevice);
            MidiRouting.CheckAndOpenINPort(sDevice);
            _triggerDevice = sDevice;
        }

        internal ProjectConfiguration Clone()
        {
            return (ProjectConfiguration)this.MemberwiseClone();
        }
    }
}
