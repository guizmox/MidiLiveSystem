using MidiTools;
using NAudio.CoreAudioApi;
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
        public bool InvalidData = false;
        private bool DeviceAdded = false;
        private string DeviceListener = "";

        public SysExInput()
        {
            InitializeComponent();

            InitPage("");
        }

        public SysExInput(string sData)
        {
            InitializeComponent();

            InitPage(sData);
        }

        public void InitPage(string sSysex)
        {
            foreach (var d in MidiRouting.InputDevices)
            {
                cbMidiIn.Items.Add(new ComboBoxItem() { Tag = "I-" + d.Name, Content = d.Name });
            }

            if (sSysex.Length > 0)
            {
                Paragraph paragraph = new(new Run(sSysex));
                rtbSysEx.Document.Blocks.Add(paragraph);
            }

            MidiRouting.InputStaticMidiMessage += Routing_IncomingMidiMessage;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (DeviceAdded && cbMidiIn.SelectedValue != null)
            {
                MidiRouting.CheckAndCloseINPort(cbMidiIn.SelectedValue.ToString().Substring(2));
            }

            MidiRouting.InputStaticMidiMessage -= Routing_IncomingMidiMessage;
        }

        private void cbMidiIn_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem cbNew = (ComboBoxItem)e.AddedItems[0];
            ComboBoxItem cbOld = null;
            if (e.RemovedItems != null && e.RemovedItems.Count > 0) { cbOld = (ComboBoxItem)e.RemovedItems[0]; }

            if (cbOld != null && DeviceAdded && cbOld.Tag.ToString().StartsWith("I-"))
            {
                var oldDevice = cbOld.Tag.ToString().Substring(2);
                MidiRouting.CheckAndCloseINPort(oldDevice);
            }

            if (cbNew != null && cbNew.Tag.ToString().StartsWith("I-"))
            {
                DeviceListener = cbNew.Tag.ToString().Substring(2);
                DeviceAdded = MidiRouting.CheckAndOpenINPort(DeviceListener);
            }
        }

        private void Routing_IncomingMidiMessage(MidiEvent ev)
        {
            if (ev.Type == MidiDevice.TypeEvent.SYSEX)
            {
                Dispatcher.Invoke(() =>
                {
                    Paragraph paragraph = new(new Run(ev.SysExData));
                    rtbSysEx.Document.Blocks.Add(paragraph);
                });
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            TextRange textRange = new(rtbSysEx.Document.ContentStart, rtbSysEx.Document.ContentEnd);
            string sSys = textRange.Text.Replace("-", "").Trim();

            if (!Regex.IsMatch(sSys, Tools.SYSEX_CHECK, RegexOptions.IgnoreCase) && sSys.Length > 0)
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
