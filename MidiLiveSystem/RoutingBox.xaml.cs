using MidiTools;
using RtMidi.Core.Devices.Infos;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MidiLiveSystem
{
    /// <summary>
    /// Logique d'interaction pour RoutingBox.xaml
    /// </summary>
    public partial class RoutingBox : Page
    {
        public Guid RoutingGuid { get; set; }
        public Guid BoxGuid { get; internal set; }

        public RoutingBox(List<IMidiInputDeviceInfo> inputDevices, List<IMidiOutputDeviceInfo> outputDevices)
        {
            BoxGuid = Guid.NewGuid();

            InitializeComponent();
            InitPage(inputDevices, outputDevices);
        }

        private void InitPage(List<IMidiInputDeviceInfo> inputDevices, List<IMidiOutputDeviceInfo> outputDevices)
        {
            foreach (var s in inputDevices)
            {
                cbMidiIn.Items.Add(new ComboBoxItem() { Tag = s.Name, Content = s.Name });
            }
            foreach (var s in outputDevices)
            {
                cbMidiOut.Items.Add(new ComboBoxItem() { Tag = s.Name, Content = s.Name });
            }
            for (int i = 0; i <= 16; i++)
            {
                cbChannelMidiIn.Items.Add(new ComboBoxItem() { Tag = i, Content = i == 0 ? "ALL" : ("Ch." + i.ToString()) });
                cbChannelMidiOut.Items.Add(new ComboBoxItem() { Tag = i, Content = i == 0 ? "ALL" : ("Ch." + i.ToString()) });
            }
            cbChannelMidiIn.SelectedIndex = 1;
            cbChannelMidiOut.SelectedIndex = 1;
        }

        public MidiOptions GetOptions()
        {
            return new MidiOptions();
        }

        private void tbChoosePreset_Click(object sender, RoutedEventArgs e)
        {
            string sInstrTemp = Directory.GetCurrentDirectory() + "\\SYNTH\\E-MU_UltraProteus.txt";
            //charger la liste des presets de l'instrument
            MidiTools.InstrumentData Instrument = new InstrumentData(sInstrTemp);
            PresetBrowser pB = new PresetBrowser(Instrument);
            pB.ShowDialog();
            lbPreset.Content = pB.SelectedPreset[0];
            lbPreset.Tag = pB.SelectedPreset[1];
        }
    }
}
