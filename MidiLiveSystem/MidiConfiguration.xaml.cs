using Microsoft.Win32;
using MidiTools;
using RtMidi.Core.Devices;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MidiLiveSystem
{
    public partial class MidiConfiguration : Window
    {
        internal ProjectConfiguration Configuration = new ProjectConfiguration();
        private MidiRouting Routing;

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
                    tbHorizontalItems.Text = Configuration.HorizontalGrid.ToString();
                    tbVerticalItems.Text = Configuration.VerticalGrid.ToString();
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
                    if (MidiTools.MidiRouting.InputDevices.Count(dev => dev.Name.Equals(d)) == 0)
                    {
                        cbMidiIn.Items.Add(new ComboBoxItem() { Tag = d, Content = string.Concat(d, " (NOT FOUND !)") });
                        cbMidiInClock.Items.Add(new ComboBoxItem() { Tag = d, Content = string.Concat(d, " (NOT FOUND !)") });
                    }
                    else
                    {
                        cbMidiIn.Items.Add(new ComboBoxItem() { Tag = d, Content = d });
                        cbMidiInClock.Items.Add(new ComboBoxItem() { Tag = d, Content = d });
                        cbMidiInRecall.Items.Add(new ComboBoxItem() { Tag = d, Content = d });
                    }
                }

                foreach (var d in Configuration.DevicesOUT)
                {
                    if (MidiTools.MidiRouting.OutputDevices.Count(dev => dev.Name.Equals(d)) == 0)
                    {
                        cbMidiOut.Items.Add(new ComboBoxItem() { Tag = d, Content = string.Concat(d, " (NOT FOUND !)") });
                    }
                    else
                    {
                        cbMidiOut.Items.Add(new ComboBoxItem() { Tag = d, Content = d });
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
                tbHorizontalItems.Text = "-1";
                tbVerticalItems.Text = "-1";

                tbRecallButtonsTrigger.Text = "UI";
                cbRecallButtonsTrigger.SelectedValue = "0";

                cbMidiInClock.Items.Add(new ComboBoxItem() { Tag = Tools.INTERNAL_GENERATOR, Content = Tools.INTERNAL_GENERATOR });
                foreach (var s in MidiTools.MidiRouting.InputDevices)
                {
                    cbMidiIn.Items.Add(new ComboBoxItem() { Tag = s.Name, Content = s.Name });
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

            if (cbMidiOut.SelectedIndex >= 0)
            {
                string sPort = ((ComboBoxItem)cbMidiOut.SelectedItem).Tag.ToString();
                var instr = CubaseInstrumentData.Instruments.FirstOrDefault(i => i.Device.Equals(sPort));
                if (instr != null)
                {
                    var confirm = MessageBox.Show("There's already a Preset List for that Device. Are you sure ?", "Overwrite ?", MessageBoxButton.YesNo);
                    if (confirm == MessageBoxResult.Yes)
                    {
                        bOK = true;
                    }
                }
                else { bOK = true; }

                if (bOK)
                {
                    OpenFileDialog fi = new OpenFileDialog();
                    fi.Filter = "(*.txt)|*.txt";
                    fi.ShowDialog();
                    string sFile = fi.FileName;

                    if (File.Exists(sFile))
                    {
                        InstrumentData data = new InstrumentData(sFile);
                        if (data != null && data.Device.Length > 0)
                        {
                            MessageBox.Show("Instrument successfully loaded (" + data.Device + ")");
                            data.ChangeDevice(sPort);

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
                        PresetBrowser pb = new PresetBrowser(instr);
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
                    sys = new SysExInput(Routing);
                }
                else
                {
                    sys = new SysExInput(instr.SysExInitializer, Routing);
                }
                
                sys.ShowDialog();
                if (sys.InvalidData)
                {
                    MessageBox.Show("Cancelled.");
                }
                else
                {
                    TextRange textRange = new TextRange(sys.rtbSysEx.Document.ContentStart, sys.rtbSysEx.Document.ContentEnd);
                    string sSysex = textRange.Text.Replace("-", "").Trim();

                    
                    if (instr != null)
                    {
                        instr.SysExInitializer = sSysex;
                    }
                    else
                    {
                        InstrumentData newInstrument = new InstrumentData();
                        newInstrument.Device = sPort;
                        newInstrument.SysExInitializer = sSysex;
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

        private async void btnPanic_Click(object sender, RoutedEventArgs e)
        {
            if (Routing != null)
            {
                await Routing.Panic();
            }
        }

        private void GetConfiguration()
        {
            string projectName = tbProjectName.Text.Trim();
            string verticalgrid = tbVerticalItems.Text.Trim();
            string horizontalgrid = tbHorizontalItems.Text.Trim();

            int ivertical = 0;
            int ihorizontal = 0;
            if (!int.TryParse(verticalgrid, out ivertical))
            {
                MessageBox.Show("Invalid Vertical Grid value");
                ivertical = 3;
            }
            if (!int.TryParse(horizontalgrid, out ihorizontal))
            {
                MessageBox.Show("Invalid Horizontal Grid value");
                ihorizontal = 4;
            }
            if (ivertical > 6)
            {
                MessageBox.Show("Invalid Vertical Grid value. Set to 6");
                ivertical = 6;
            }
            if (ivertical != -1)
            {
                if (ivertical < 1)
                {
                    MessageBox.Show("Invalid Vertical Grid value. Set to 1");
                    ivertical = 1;
                }
            }
            if (ihorizontal > 6)
            {
                MessageBox.Show("Invalid Horizontal Grid value. Set to 6");
                ihorizontal = 6;
            }
            if (ihorizontal != -1)
            {
                if (ihorizontal < 1)
                {
                    MessageBox.Show("Invalid Horizontal Grid value. Set to 1");
                    ihorizontal = 1;
                }
            }

            List<string> sDevicesIn = new List<string>();
            List<string> sDevicesOut = new List<string>();

            foreach (ComboBoxItem cb in cbMidiIn.Items)
            {
                sDevicesIn.Add(cb.Tag.ToString());
            }
            foreach (ComboBoxItem cb in cbMidiOut.Items)
            {
                sDevicesOut.Add(cb.Tag.ToString());
            }

            List<string[]> boxnames = new List<string[]>();
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
            Configuration.HorizontalGrid = ihorizontal;
            Configuration.VerticalGrid = ivertical;
            Configuration.TriggerRecallButtons = ((ComboBoxItem)cbRecallButtonsTrigger.SelectedItem).Tag.ToString();
            Configuration.TriggerRecallDevice = cbMidiInRecall.SelectedItem == null ? "" : ((ComboBoxItem)cbMidiInRecall.SelectedItem).Tag.ToString();
            Configuration.ClockDevice = cbMidiInClock.SelectedItem == null ? "" : ((ComboBoxItem)cbMidiInClock.SelectedItem).Tag.ToString();
            Configuration.ClockActivated = ckActivateClock.IsChecked.Value;

            int iTriggerValue = 0;
            if (int.TryParse(tbRecallButtonsTrigger.Text, out iTriggerValue))
            {
                if (iTriggerValue < 0 || iTriggerValue > 127)
                {
                    iTriggerValue = 0;
                }
            }

            Configuration.TriggerRecallButtonsValue = iTriggerValue;

            int iBpm = 0;
            if (int.TryParse(tbBPM.Text.Trim(), out iBpm))
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

    [Serializable]
    public class ProjectConfiguration
    {
        [Serializable]
        public class RecallConfiguration
        {
            public string ButtonName = "";
            public int ButtonIndex = 0;
            public List<Guid> BoxGuids = new List<Guid>();
            public List<int> BoxPresets = new List<int>();

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

        public Guid ProjectId = Guid.NewGuid();
        public string ProjectName = "My Project";
        public List<string> DevicesOUT
        {
            get
            {
                return _listDevicesOut;
            }
            set
            {
                _listDevicesOut = value;
            }
        }

        public List<RecallConfiguration> RecallData = new List<RecallConfiguration>();

        public List<string> DevicesIN
        {
            get
            {
                return _listDevicesIn;
            }
            set
            {
                _listDevicesIn = value;
            }
        }

        public bool IsDefaultConfig = true;

        public MidiSequence RecordedSequence;

        public List<string[]> BoxNames = null;

        public int HorizontalGrid = -1;
        public int VerticalGrid = -1;

        private string _clockDevice = "";
        internal string ClockDevice
        {
            get { return _clockDevice; }
            set
            {
                MidiRouting.CheckAndCloseINPort(_clockDevice);
                MidiRouting.CheckAndOpenINPort(value);
                _clockDevice = value;
            }
        }

        internal int BPM = 120;
        internal bool ClockActivated = false;

        private string _triggerDevice = "";

        public string TriggerRecallButtons = "UI";
        public int TriggerRecallButtonsValue = 0;

        internal string TriggerRecallDevice { 
            get { return _triggerDevice; }
            set
            {
                MidiRouting.CheckAndCloseINPort(_triggerDevice);
                MidiRouting.CheckAndOpenINPort(value);
                _triggerDevice = value;
            }
        }

        public ProjectConfiguration()
        {

        }

        internal ProjectConfiguration Clone()
        {
            return (ProjectConfiguration)this.MemberwiseClone();
        }
    }
}
