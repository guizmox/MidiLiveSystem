using MidiTools;
using RtMidi.Core.Devices.Infos;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
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
    public class ComboBoxCustomItem
    {
        public string Description { get; set; }
        public string Value { get; set; }
        public string Id { get; set; }
    }

    public partial class RoutingBox : Page
    {
        public Guid RoutingGuid { get; set; }
        public Guid BoxGuid { get; internal set; }

        public delegate void RoutingBoxEventHandler(Guid gBox, string sControl, object sValue);
        public event RoutingBoxEventHandler OnUIEvent;

        private BoxPreset[] TempMemory = new BoxPreset[8] { new BoxPreset(), new BoxPreset(), new BoxPreset(), new BoxPreset(), new BoxPreset(), new BoxPreset(), new BoxPreset(), new BoxPreset() };


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
                if (i > 0)
                {
                    cbChannelPreset.Items.Add(new ComboBoxItem() { Tag = i, Content = i == 0 ? "ALL" : ("Ch." + i.ToString()) });
                }
            }
            cbChannelMidiIn.SelectedIndex = 1;
            cbChannelMidiOut.SelectedIndex = 1;
            cbChannelPreset.SelectedIndex = 0;

            for (int i = 1; i <= 8; i++)
            {
                cbPresetButton.Items.Add(new ComboBoxItem() { Tag = (i - 1).ToString(), Content = "BUTTON " + i.ToString() });
            }

            cbPresetButton.SelectedIndex = 0;
        }

        private void tbChoosePreset_Click(object sender, RoutedEventArgs e)
        {
            string sInstrTemp = Directory.GetCurrentDirectory() + "\\SYNTH\\E-MU_UltraProteus.txt";
            //charger la liste des presets de l'instrument
            MidiTools.InstrumentData Instrument = new InstrumentData(sInstrTemp);
            PresetBrowser pB = new PresetBrowser(Instrument, false);
            pB.ShowDialog();
            lbPreset.Content = pB.SelectedPreset[0];
            lbPreset.Tag = pB.SelectedPreset[1];
        }

        private void tbSolo_Click(object sender, RoutedEventArgs e)
        {
            if (tbSolo.Background == Brushes.IndianRed)
            {
                OnUIEvent?.Invoke(BoxGuid, "SOLO", false);
                tbSolo.Background = Brushes.DarkGray;
            }
            else
            {
                OnUIEvent?.Invoke(BoxGuid, "SOLO", true);
                tbSolo.Background = Brushes.IndianRed;
            }
        }

        private void tbMute_Click(object sender, RoutedEventArgs e)
        {
            if (tbMute.Background == Brushes.IndianRed)
            {
                OnUIEvent?.Invoke(BoxGuid, "MUTE", false);
                tbMute.Background = Brushes.DarkGray;
            }
            else
            {
                OnUIEvent?.Invoke(BoxGuid, "MUTE", true);
                tbMute.Background = Brushes.IndianRed;
            }
        }

        private void cbPresetButton_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (e.RemovedItems.Count > 0)
                {
                    ComboBoxItem cbOut = (ComboBoxItem)e.RemovedItems[0];
                    ComboBoxItem cbIn = (ComboBoxItem)e.AddedItems[0];

                    var bp = MemCurrentPreset();
                    int idxOut = Convert.ToInt32(cbOut.Tag.ToString());
                    TempMemory[idxOut] = bp;

                    int idxIn = Convert.ToInt32(cbIn.Tag.ToString());
                    FillUI(TempMemory[idxIn]);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to Save Preset (" + ex.Message + ")");
            }
        }


        private void btnAddCCConvert_Click(object sender, RoutedEventArgs e)
        {
            int iFrom = TextParser(tbCCConvertFrom.Text);
            int iTo = TextParser(tbCCConvertTo.Text);

            if (iFrom != iTo && iFrom != 999 && iTo != 999)
            {
                string sTag = string.Concat(iFrom, "-", iTo);
                bool bExists = false;
                foreach (ComboBoxItem item in cbCCConvert.Items)
                {
                    if (item.Tag.Equals(sTag))
                    {
                        bExists = true;
                        break;
                    }
                }
                if (!bExists)
                {
                    cbCCConvert.Items.Add(new ComboBoxItem() { Tag = sTag, Content = string.Concat("FROM ", iFrom, " TO ", iTo) });
                }
            }
            else
            {
                MessageBox.Show("Incorrect CC Converter Value (expecting values from 0 to 127) : " + iFrom + " - " + iTo);
            }
        }

        private void btnAddNOTEConvert_Click(object sender, RoutedEventArgs e)
        {
            int iFrom = TextParser(tbNOTEConvertFrom.Text);
            int iTo = TextParser(tbNOTEConvertTo.Text);

            if (iFrom != iTo && iFrom != 999 && iTo != 999)
            {
                string sTag = string.Concat(iFrom, "-", iTo);
                bool bExists = false;
                foreach (ComboBoxItem item in cbNOTEConvert.Items)
                {
                    if (item.Tag.Equals(sTag))
                    {
                        bExists = true;
                        break;
                    }
                }
                if (!bExists)
                {
                    cbNOTEConvert.Items.Add(new ComboBoxItem() { Tag = sTag, Content = string.Concat("FROM ", iFrom, " TO ", iTo) });
                }
            }
            else
            {
                MessageBox.Show("Incorrect NOTE Converter Value (expecting values from 0 to 127) : " + iFrom + " - " + iTo);
            }
        }

        private void btnRemoveCCConvert_Click(object sender, RoutedEventArgs e)
        {
            if (cbCCConvert.SelectedItem != null)
            {
                var cb = cbCCConvert.SelectedItem;
                cbCCConvert.Items.Remove(cb);
            }
        }

        private void btnRemoveNOTEConvert_Click(object sender, RoutedEventArgs e)
        {
            if (cbNOTEConvert.SelectedItem != null)
            {
                var cb = cbNOTEConvert.SelectedItem;
                cbNOTEConvert.Items.Remove(cb);
            }
        }

        private void btnPreset_Click(object sender, RoutedEventArgs e)
        {
            RecallRoutingPreset(((Button)sender).Tag.ToString());
        }

        private void btnCopyPreset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var bp = MemCurrentPreset();
                OnUIEvent?.Invoke(BoxGuid, "COPY_PRESET", bp);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to Save Preset (" + ex.Message + ")");
            }
        }

        private void btnPastePreset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var copied = MidiLiveSystem.MainWindow.CopiedPreset;
                if (copied != null)
                {
                    FillUI(copied);
                }
                else
                {
                    MessageBox.Show("Nothing to Copy");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to Save Preset (" + ex.Message + ")");
            }
        }

        private void RecallRoutingPreset(string sPresetIndex)
        {
            //RECALL PRESET
            int idx = Convert.ToInt32(sPresetIndex);
            BoxPreset bp = TempMemory[idx];

            if (bp != null)
            {
                FillUI(bp);

                OnUIEvent?.Invoke(BoxGuid, "PRESET_CHANGE", bp.MidiOptions);

                OnUIEvent?.Invoke(BoxGuid, "PROGRAM_CHANGE", bp.MidiPreset);
            }
            else
            {
                MessageBox.Show("Unable to recall Preset");
            }
        }

        private BoxPreset MemCurrentPreset()
        {
            try
            {
                //mémorisation des données en cours
                var options = LoadOptions();
                var preset = GetPreset();
                var presetname = tbPresetName.Text.Trim();
                var routingname = tbRoutingName.Text.Trim();
                string sDeviceIn = cbMidiIn.SelectedItem == null ? "" : ((ComboBoxItem)cbMidiIn.SelectedItem).Tag.ToString();
                string sDeviceOut = cbMidiOut.SelectedItem == null ? "" : ((ComboBoxItem)cbMidiOut.SelectedItem).Tag.ToString();
                int iChannelIn = cbChannelMidiIn.SelectedItem == null ? 1 : Convert.ToInt32(((ComboBoxItem)cbChannelMidiIn.SelectedItem).Tag.ToString());
                int iChannelOut = cbChannelMidiOut.SelectedItem == null ? 1 : Convert.ToInt32(((ComboBoxItem)cbChannelMidiOut.SelectedItem).Tag.ToString());

                var bp = new BoxPreset(RoutingGuid, BoxGuid, routingname, presetname, options, preset, sDeviceIn, sDeviceOut, iChannelIn, iChannelOut);

                return bp;
            }
            catch { throw; }
        }

        private void FillUI(BoxPreset bp)
        {
            //remplissage des champs
            tbPresetName.Text = bp.PresetName;
            tbRoutingName.Text = bp.BoxName;
            cbMidiIn.SelectedValue = bp.DeviceIn;
            cbMidiOut.SelectedValue = bp.DeviceOut;
            cbChannelMidiIn.SelectedItem = bp.ChannelIn;
            cbChannelMidiOut.SelectedItem = bp.ChannelOut;
            cbChannelPreset.SelectedItem = bp.MidiPreset.Channel;
            lbPreset.Content = bp.MidiPreset.PresetName;

            tbFilterHighNote.Text = bp.MidiOptions.NoteFilterHigh.ToString();
            tbFilterLowNote.Text = bp.MidiOptions.NoteFilterLow.ToString();
            tbFilterHighVelo.Text = bp.MidiOptions.VelocityFilterHigh.ToString();
            tbFilterLowVelo.Text = bp.MidiOptions.VelocityFilterLow.ToString();

            ckAllowAftertouch.IsChecked = bp.MidiOptions.AllowAftertouch;
            ckAllowAllCC.IsChecked = bp.MidiOptions.AllowAllCC;
            ckAllowModulation.IsChecked = bp.MidiOptions.AllowModulation;
            ckAllowNotes.IsChecked = bp.MidiOptions.AllowNotes;
            ckAllowNrpn.IsChecked = bp.MidiOptions.AllowNrpn;
            ckAllowPitchBend.IsChecked = bp.MidiOptions.AllowPitchBend;
            ckAllowProgramChange.IsChecked = bp.MidiOptions.AllowProgramChange;
            ckAllowSysex.IsChecked = bp.MidiOptions.AllowSysex;

            ckAftertouchToNote.IsChecked = bp.MidiOptions.AftertouchVolume;
            tbNoteTransposition.Text = bp.MidiOptions.TranspositionOffset.ToString();

            foreach (var item in cbCCDefaultValues.Items)
            {
                ComboBoxCustomItem cb = (ComboBoxCustomItem)item;

                switch (cb.Id)
                {
                    case "tbCC_Chorus":
                        cb.Value = bp.MidiOptions.CC_Chorus_Value.ToString();
                        break;
                    case "tbCC_Pan":
                        cb.Value = bp.MidiOptions.CC_Pan_Value.ToString();
                        break;
                    case "tbCC_Volume":
                        cb.Value = bp.MidiOptions.CC_Volume_Value.ToString();
                        break;
                    case "tbCC_Attack":
                        cb.Value = bp.MidiOptions.CC_Attack_Value.ToString();
                        break;
                    case "tbCC_Decay":
                        cb.Value = bp.MidiOptions.CC_Decay_Value.ToString();
                        break;
                    case "tbCC_Release":
                        cb.Value = bp.MidiOptions.CC_Release_Value.ToString();
                        break;
                    case "tbCC_Reverb":
                        cb.Value = bp.MidiOptions.CC_Reverb_Value.ToString();
                        break;
                    case "tbCC_Bright":
                        cb.Value = bp.MidiOptions.CC_Brightness_Value.ToString();
                        break;
                }
            }

            cbCCConvert.Items.Clear();
            foreach (var item in bp.MidiOptions.CC_Converters)
            {
                string sTag = string.Concat(item[0], "-", item[1]);
                string sText = string.Concat("FROM ", item[0], " TO ", item[1]);
                cbCCConvert.Items.Add(new ComboBoxItem { Tag = sTag, Content = sText });
            }

            cbNOTEConvert.Items.Clear();
            foreach (var item in bp.MidiOptions.Note_Converters)
            {
                string sTag = string.Concat(item[0], "-", item[1]);
                string sText = string.Concat("FROM ", item[0], " TO ", item[1]);
                cbNOTEConvert.Items.Add(new ComboBoxItem { Tag = sTag, Content = sText });
            }
        }

        public MidiPreset GetPreset()
        {
            if (cbChannelPreset.SelectedItem != null)
            {
                return new MidiPreset("", Convert.ToInt32(((ComboBoxItem)cbChannelPreset.SelectedItem).Tag.ToString()), 0, 0, 0, lbPreset.Content.ToString());
            }
            else
            {
                return new MidiPreset("", 1, 0, 0, 0, "Unknown Preset");
            }
        }

        public MidiOptions LoadOptions()
        {
            var options = new MidiOptions();

            options.NoteFilterHigh = TextParser(tbFilterHighNote.Text);
            tbFilterHighNote.Text = options.NoteFilterHigh.ToString();

            options.NoteFilterLow = TextParser(tbFilterLowNote.Text);
            tbFilterLowNote.Text = options.NoteFilterLow.ToString();

            options.VelocityFilterHigh = TextParser(tbFilterHighVelo.Text);
            tbFilterHighVelo.Text = options.VelocityFilterHigh.ToString();

            options.VelocityFilterLow = TextParser(tbFilterLowVelo.Text);
            tbFilterLowVelo.Text = options.VelocityFilterLow.ToString();

            options.AllowAftertouch = ckAllowAftertouch.IsChecked.Value;
            options.AllowAllCC = ckAllowAllCC.IsChecked.Value;
            options.AllowModulation = ckAllowModulation.IsChecked.Value;
            options.AllowNotes = ckAllowNotes.IsChecked.Value;
            options.AllowNrpn = ckAllowNrpn.IsChecked.Value;
            options.AllowPitchBend = ckAllowPitchBend.IsChecked.Value;
            options.AllowProgramChange = ckAllowProgramChange.IsChecked.Value;
            options.AllowSysex = ckAllowSysex.IsChecked.Value;

            foreach (ComboBoxCustomItem cb in cbCCDefaultValues.Items)
            {
                switch (cb.Id)
                {
                    case "tbCC_Chorus":
                        options.CC_Chorus_Value = TextParser(cb.Value);
                        cb.Value = options.CC_Chorus_Value.ToString();
                        break;
                    case "tbCC_Pan":
                        options.CC_Pan_Value = TextParser(cb.Value);
                        cb.Value = options.CC_Pan_Value.ToString();
                        break;
                    case "tbCC_Volume":
                        options.CC_Volume_Value = TextParser(cb.Value);
                        cb.Value = options.CC_Volume_Value.ToString();
                        break;
                    case "tbCC_Attack":
                        options.CC_Attack_Value = TextParser(cb.Value);
                        cb.Value = options.CC_Attack_Value.ToString();
                        break;
                    case "tbCC_Decay":
                        options.CC_Decay_Value = TextParser(cb.Value);
                        cb.Value = options.CC_Decay_Value.ToString();
                        break;
                    case "tbCC_Release":
                        options.CC_Release_Value = TextParser(cb.Value);
                        cb.Value = options.CC_Release_Value.ToString();
                        break;
                    case "tbCC_Reverb":
                        options.CC_Reverb_Value = TextParser(cb.Value);
                        cb.Value = options.CC_Reverb_Value.ToString();
                        break;
                    case "tbCC_Bright":
                        options.CC_Brightness_Value = TextParser(cb.Value);
                        cb.Value = options.CC_Brightness_Value.ToString();
                        break;
                }
            }

            options.AftertouchVolume = ckAftertouchToNote.IsChecked.Value;

            options.TranspositionOffset = TextParser(tbNoteTransposition.Text);

            foreach (var item in cbCCConvert.Items)
            {
                string text = ((ComboBoxItem)item).Tag.ToString();
                int iFrom = TextParser(text.Split('-')[0]);
                int iTo = TextParser(text.Split('-')[1]);

                if (iFrom != 999 && iTo != 999)
                {
                    bool bOK = options.AddCCConverter(iFrom, iTo);
                    if (!bOK)
                    { MessageBox.Show("Incorrect CC Converter Value (expecting values from 0 to 127) : " + iFrom + " - " + iTo); }
                }
            }

            foreach (var item in cbNOTEConvert.Items)
            {
                string text = ((ComboBoxItem)item).Tag.ToString();
                int iFrom = TextParser(text.Split('-')[0]);
                int iTo = TextParser(text.Split('-')[1]);

                if (iFrom != 999 && iTo != 999)
                {
                    bool bOK = options.AddNoteConverter(iFrom, iTo);
                    if (!bOK)
                    { MessageBox.Show("Incorrect NOTE Converter Value (expecting values from 0 to 127) : " + iFrom + " - " + iTo); }
                }
            }

            return options;
        }

        public RoutingBoxes GetRoutingBoxMemory()
        {
            return new RoutingBoxes { AllPresets = TempMemory };
        }

        private int TextParser(string sText)
        {
            int i = 0;
            if (int.TryParse(sText.Trim(), out i))
            {
                return i;
            }
            else { return 999; }
        }

    }

    [Serializable]
    public class BoxPreset
    {
        public Guid RoutingGuid { get; set; } = Guid.Empty;
        public Guid BoxGuid { get; set; } = Guid.Empty;
        public string BoxName { get; set; } = "Routing Name";
        public string PresetName { get; set; } = "My Preset";
        public MidiOptions MidiOptions { get; set; } = new MidiOptions();
        public MidiPreset MidiPreset { get; set; } = new MidiPreset("", 1, 0, 0, 0, "");
        public string DeviceIn { get; set; } = "";
        public string DeviceOut { get; set; } = "";
        public int ChannelIn { get; set; } = 1;
        public int ChannelOut { get; set; } = 1;

        public BoxPreset()
        {

        }

        internal BoxPreset(Guid routingGuid, Guid boxGuid, string boxName, string presetName, MidiOptions midiOptions, MidiPreset midiPreset, string deviceIn, string deviceOut, int channelIn, int channelOut)
        {
            RoutingGuid = routingGuid;
            BoxGuid = boxGuid;
            if (boxName.Length > 0) { BoxName = boxName; }
            if (presetName.Length > 0) { PresetName = presetName; }
            MidiOptions = midiOptions;
            MidiPreset = midiPreset;
            DeviceIn = deviceIn;
            DeviceOut = deviceOut;
            ChannelIn = channelIn;
            ChannelOut = channelOut;
        }

    }
}
