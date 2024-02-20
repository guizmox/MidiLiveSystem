using MidiTools;
using RtMidi.Core.Devices.Infos;
using System;
using System.Collections.Generic;
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

        public enum RoutingBoxEvent
        {
            ADD_ROUTING = 1,
            MODIFY_ROUTING = 2,
            CHANGE_PRESET = 3
        }

        public delegate void RoutingBoxEventHandler(Guid boxId, RoutingBoxEvent ev, MidiOptions options, string DeviceIn, string DeviceOut, int ChannelIn, int ChannelOut, string Preset);
        public event RoutingBoxEventHandler OnRoutingBoxChange;

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

        private void btnMem_Click(object sender, RoutedEventArgs e)
        {
            if (RoutingGuid != Guid.Empty)
            {
                MidiOptions mo = GetOptions();
                OnRoutingBoxChange?.Invoke(BoxGuid, RoutingBoxEvent.MODIFY_ROUTING, mo, ((ComboBoxItem)cbMidiIn.SelectedItem).Tag.ToString(),
                                                                            ((ComboBoxItem)cbMidiOut.SelectedItem).Tag.ToString(),
                                                                            Convert.ToInt32(((ComboBoxItem)cbChannelMidiIn.SelectedItem).Tag.ToString()),
                                                                            Convert.ToInt32(((ComboBoxItem)cbChannelMidiOut.SelectedItem).Tag.ToString()),
                                                                            "");
            }
            else
            {
                MidiOptions mo = GetOptions();
                OnRoutingBoxChange?.Invoke(BoxGuid, RoutingBoxEvent.ADD_ROUTING, mo, ((ComboBoxItem)cbMidiIn.SelectedItem).Tag.ToString(),
                                                                            ((ComboBoxItem)cbMidiOut.SelectedItem).Tag.ToString(),
                                                                            Convert.ToInt32(((ComboBoxItem)cbChannelMidiIn.SelectedItem).Tag.ToString()),
                                                                            Convert.ToInt32(((ComboBoxItem)cbChannelMidiOut.SelectedItem).Tag.ToString()),
                                                                            "");
            }
        }

        private MidiOptions GetOptions()
        {
            return new MidiOptions();
        }
    }
}
