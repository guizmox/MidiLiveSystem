using MidiTools;
using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MidiLiveSystem
{
    /// <summary>
    /// Logique d'interaction pour MidiLog.xaml
    /// </summary>
    public partial class MidiLog : Window
    {
        private bool OnlyIN = false;
        private bool DeviceAdded = false;

        public MidiLog()
        {
            InitializeComponent();
            InitPage();
        }

        private void InitPage()
        {
            cbMidiDevices.Items.Add(new ComboBoxItem() { Tag = "ALL", Content = "All Devices" });

            foreach (var dev in MidiRouting.InputDevices)
            {
                cbMidiDevices.Items.Add(new ComboBoxItem() { Tag = "I-" + dev.Name, Content = string.Concat("[IN] ", dev.Name) });
            }
            foreach (var dev in MidiRouting.OutputDevices)
            {
                cbMidiDevices.Items.Add(new ComboBoxItem() { Tag = "O-" + dev.Name, Content = string.Concat("[OUT] ", dev.Name) });
            }
            cbMidiDevices.SelectedIndex = 0;
        }

        internal async Task AddLog(string sDevice, bool bIn, string sLog)
        {
            if (!bIn && OnlyIN) //on ne veut voir que les signaux IN
            {

            }
            else
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    if (cbMidiDevices.SelectedIndex == 0 || cbMidiDevices.SelectedValue.ToString().Substring(2).Equals(sDevice))
                    {
                        Paragraph paragraph = new(new Run(sLog))
                        {
                            LineHeight = 1
                        };
                        rtbMidiLog.Document.Blocks.Add(paragraph);
                        if (rtbMidiLog.Document.Blocks.Count > 1000)
                        {
                            rtbMidiLog.Document.Blocks.Clear();
                        }
                        rtbMidiLog.ScrollToEnd();
                    }
                });
            }
        }

        private void ckOnlyIn_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            if (cb.IsChecked.Value)
            {
                OnlyIN = true;
            }
            else
            {
                OnlyIN = false;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {

        }

        private void cbMidiDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem cbNew = (ComboBoxItem)e.AddedItems[0];
            ComboBoxItem cbOld = null;
            if (e.RemovedItems != null && e.RemovedItems.Count > 0) { cbOld = (ComboBoxItem)e.RemovedItems[0]; }

            if (cbOld != null && DeviceAdded && cbOld.Tag.ToString().StartsWith("I-"))
            {
                MidiRouting.CheckAndCloseINPort(cbOld.Tag.ToString().Substring(2));
            }

            if (cbNew != null && cbNew.Tag.ToString().StartsWith("I-"))
            {
                DeviceAdded = MidiRouting.CheckAndOpenINPort(cbNew.Tag.ToString().Substring(2));
            }
        }
    }
}
