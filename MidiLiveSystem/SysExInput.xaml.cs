using MidiTools;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;

namespace MidiLiveSystem
{
    /// <summary>
    /// Logique d'interaction pour SysExInput.xaml
    /// </summary>
    public partial class SysExInput : Window
    {
        MidiRouting Routing = null;
        Guid RoutingGuid = Guid.Empty;

        public bool InvalidData = false;

        public SysExInput()
        {
            InitializeComponent();
            InitPage();
        }

        public void InitPage()
        {
            foreach (var d in MidiRouting.InputDevices)
            {
                cbMidiIn.Items.Add(new ComboBoxItem() { Tag = d.Name, Content = d.Name });
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (Routing != null)
            {
                Routing.CloseUsedPorts(true);
                Routing.DeleteAllRouting();
                Routing.IncomingMidiMessage -= Routing_IncomingMidiMessage;
                Routing = null;
            }
        }

        private void cbMidiIn_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem item = (ComboBoxItem)e.AddedItems[0];

            if (item != null)
            {
                if (Routing != null)
                {
                    Routing.ModifyRouting(RoutingGuid, item.Tag.ToString(), "", 0, 0, new MidiOptions(), new MidiPreset());
                }
                else
                {
                    //ouvrir le device et fermer le précédent pour écouter les messages entrants
                    Routing = new MidiRouting();
                    RoutingGuid = Routing.AddRouting(item.Tag.ToString(), "", 0, 0, new MidiOptions(), new MidiPreset());
                    Routing.IncomingMidiMessage += Routing_IncomingMidiMessage;
                }

            }
        }

        private void Routing_IncomingMidiMessage(MidiDevice.MidiEvent ev)
        {
            if (ev.Type == MidiDevice.TypeEvent.SYSEX)
            {
                Dispatcher.Invoke(() =>
                {
                    Paragraph paragraph = new Paragraph(new Run(ev.SysExData));
                    rtbSysEx.Document.Blocks.Add(paragraph);
                });
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            TextRange textRange = new TextRange(rtbSysEx.Document.ContentStart, rtbSysEx.Document.ContentEnd);

            if (!Regex.IsMatch(textRange.Text.Replace("-", "").Trim(), Tools.SYSEX_CHECK, RegexOptions.IgnoreCase))
            {
                var dialog = MessageBox.Show("Invalid SYSEX data (expecting F0 ... F7) : ", "Input Error", MessageBoxButton.OKCancel);
                if (dialog == MessageBoxResult.Cancel)
                {

                }
                else
                {
                    InvalidData = true;
                    Close();
                }
            }
            else { Close(); }
        }
    }
}
