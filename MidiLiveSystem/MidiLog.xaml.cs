using MidiTools;
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
using System.Windows.Shapes;

namespace MidiLiveSystem
{
    /// <summary>
    /// Logique d'interaction pour MidiLog.xaml
    /// </summary>
    public partial class MidiLog : Window
    {
        private bool OnlyIN = false;

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
                cbMidiDevices.Items.Add(new ComboBoxItem() { Tag = dev.Name, Content = string.Concat("[IN] ", dev.Name) });
            }
            foreach (var dev in MidiRouting.OutputDevices)
            {
                cbMidiDevices.Items.Add(new ComboBoxItem() { Tag = dev.Name, Content = string.Concat("[OUT] ", dev.Name) });
            }
            cbMidiDevices.SelectedIndex = 0;
        }

        internal void AddLog(string sDevice, bool bIn, string sLog)
        {
            if (!bIn && OnlyIN) //on ne veut voir que les signaux IN
            {

            }
            else
            {
                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.Invoke(() =>
                {
                    if (cbMidiDevices.SelectedIndex == 0 || cbMidiDevices.SelectedValue.ToString().Equals(sDevice))
                    {
                        Paragraph paragraph = new Paragraph(new Run(sLog));
                        paragraph.LineHeight = 1;
                        rtbMidiLog.Document.Blocks.Add(paragraph);
                        if (rtbMidiLog.Document.Blocks.Count > 100)
                        {
                            rtbMidiLog.Document.Blocks.Clear();
                        }
                        rtbMidiLog.ScrollToEnd();
                    }
                });
                }
                else
                {
                    if (cbMidiDevices.SelectedIndex == 0 || cbMidiDevices.SelectedValue.ToString().Equals(sDevice))
                    {
                        Paragraph paragraph = new Paragraph(new Run(sLog));
                        paragraph.LineHeight = 1;
                        rtbMidiLog.Document.Blocks.Add(paragraph);
                        if (rtbMidiLog.Document.Blocks.Count > 100)
                        {
                            rtbMidiLog.Document.Blocks.Clear();
                        }
                        rtbMidiLog.ScrollToEnd();
                    }
                }
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
    }
}
