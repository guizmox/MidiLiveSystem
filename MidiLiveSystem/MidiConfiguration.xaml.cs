using Microsoft.Win32;
using MidiTools;
using RtMidi.Core.Devices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MidiLiveSystem
{
    public static class CubaseInstrumentData
    {
        public static List<InstrumentData> Instruments = new List<InstrumentData>();
    }

    public partial class MidiConfiguration : Window
    {
        public ProjectConfiguration Configuration = new ProjectConfiguration();

        public MidiConfiguration()
        {
            InitializeComponent();
            InitPage(false, null);
        }

        public MidiConfiguration(List<RoutingBox> boxes)
        {
            InitializeComponent();
            InitPage(false, boxes);
        }

        public MidiConfiguration(ProjectConfiguration pc, List<RoutingBox> boxes)
        {
            Configuration = pc;
            InitializeComponent();
            InitPage(true, boxes);
        }

        private void InitPage(bool bFromSave, List<RoutingBox> boxes)
        {
            //comparaison avec le projet et les devices réels
            if (bFromSave)
            {
                tbProjectName.Text = Configuration.ProjectName;
                tbHorizontalItems.Text = Configuration.HorizontalGrid.ToString();
                tbVerticalItems.Text = Configuration.VerticalGrid.ToString();

                foreach (var d in Configuration.DevicesIN)
                {
                    if (MidiTools.MidiRouting.InputDevices.Count(dev => dev.Name.Equals(d)) == 0)
                    {
                        cbMidiIn.Items.Add(new ComboBoxItem() { Tag = d, Content = string.Concat(d, " (NOT FOUND !)") });
                    }
                    else
                    {
                        cbMidiIn.Items.Add(new ComboBoxItem() { Tag = d, Content = d });
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
            }
            else
            {
                tbProjectName.Text = "My Project";
                tbHorizontalItems.Text = "4";
                tbVerticalItems.Text = "3";

                foreach (var s in MidiTools.MidiRouting.InputDevices)
                {
                    cbMidiIn.Items.Add(new ComboBoxItem() { Tag = s.Name, Content = s.Name });
                }
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

        private void Window_Closed(object sender, EventArgs e)
        {
            Configuration = GetConfiguration();
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
                        PresetBrowser pb = new PresetBrowser(instr, true);
                        pb.ShowDialog();
                    }));
                }
            }
        }

        private ProjectConfiguration GetConfiguration()
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
            if (ivertical < 1)
            {
                MessageBox.Show("Invalid Vertical Grid value. Set to 0");
                ivertical = 1;
            }
            if (ihorizontal > 6)
            {
                MessageBox.Show("Invalid Horizontal Grid value. Set to 6");
                ihorizontal = 6;
            }
            if (ihorizontal < 1)
            {
                MessageBox.Show("Invalid Horizontal Grid value. Set to 0");
                ihorizontal = 1;
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

            ProjectConfiguration pc = new ProjectConfiguration();
            pc.BoxNames = boxnames;
            pc.ProjectName = projectName;
            pc.DevicesIN = sDevicesIn;
            pc.DevicesOUT = sDevicesOut;
            pc.HorizontalGrid = ihorizontal;
            pc.VerticalGrid = ivertical;

            return pc;
        }
    }

    [Serializable]
    public class ProjectConfiguration
    {
        private List<string> _sBoxNames = null;
        private List<string> _listDevicesIn = null;
        private List<string> _listDevicesOut = null;

        public Guid ProjectId = Guid.NewGuid();
        public string ProjectName = "My Project";
        public List<string> DevicesOUT
        {
            get
            {
                if (_listDevicesOut == null)
                {
                    _listDevicesOut = new List<string>();
                    foreach (var v in MidiTools.MidiRouting.OutputDevices)
                    {
                        _listDevicesOut.Add(v.Name);
                    }
                }
                return _listDevicesOut;
            }
            set
            {
                _listDevicesOut = value;
            }
        }

        public List<string> DevicesIN
        {
            get
            {
                if (_listDevicesIn == null)
                {
                    _listDevicesIn = new List<string>();
                    foreach (var v in MidiTools.MidiRouting.InputDevices)
                    {
                        _listDevicesIn.Add(v.Name);
                    }
                }
                return _listDevicesIn;
            }
            set
            {
                _listDevicesIn = value;
            }
        }

        public MidiSequence RecordedSequence;

        public List<string[]> BoxNames = null;

        public int HorizontalGrid = 4;
        public int VerticalGrid = 3;

        public ProjectConfiguration()
        {
        }
    }
}
