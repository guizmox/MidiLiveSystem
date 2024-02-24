using MidiTools;
using RtMidi.Core.Devices.Infos;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
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
    public class ComboBoxCustomItem : INotifyPropertyChanged
    {
        private string _value;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Id { get; set; }

        public string Description { get; set; }

        public string Value
        {
            get { return _value; }
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged("Value");
                }
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class RoutingBox : Page
    {
        public Guid RoutingGuid { get; set; }
        public bool Detached { get; internal set; } = false;
        public int GridPosition = 0;

        public Guid BoxGuid = Guid.NewGuid();
        public string BoxName = "Routing Box";
        private ProjectConfiguration Project;

        public delegate void RoutingBoxEventHandler(Guid gBox, string sControl, object sValue);
        public event RoutingBoxEventHandler OnUIEvent;

        BoxPreset[] TempMemory;

        PresetBrowser InstrumentPresets = null;


        public RoutingBox(ProjectConfiguration conf, List<IMidiInputDeviceInfo> inputDevices, List<IMidiOutputDeviceInfo> outputDevices, int gridPosition)
        {
            GridPosition = gridPosition;

            TempMemory = new BoxPreset[8] { new BoxPreset(BoxGuid, "Preset 1"), new BoxPreset(BoxGuid, "Preset 2"), new BoxPreset(BoxGuid, "Preset 3"),
                                            new BoxPreset(BoxGuid, "Preset 4"), new BoxPreset(BoxGuid, "Preset 5"), new BoxPreset(BoxGuid, "Preset 6"),
                                            new BoxPreset(BoxGuid, "Preset 7"), new BoxPreset(BoxGuid, "Preset 8") };
            Project = conf;

            InitializeComponent();
            InitPage(inputDevices, outputDevices);
            GridPosition = gridPosition;
        }

        private void InitPage(List<IMidiInputDeviceInfo> inputDevices, List<IMidiOutputDeviceInfo> outputDevices)
        {
            tbRoutingName.Text = "Routing Box " + (GridPosition + 1).ToString();

            foreach (var s in inputDevices)
            {
                cbMidiIn.Items.Add(new ComboBoxItem() { Tag = s.Name, Content = s.Name });
            }

            cbMidiIn.Items.Add(new ComboBoxItem() { Tag = Tools.INTERNAL_GENERATOR, Content = Tools.INTERNAL_GENERATOR });

            foreach (var s in outputDevices)
            {
                cbMidiOut.Items.Add(new ComboBoxItem() { Tag = s.Name, Content = s.Name });
            }
            for (int i = 0; i <= 16; i++)
            {
                cbChannelMidiIn.Items.Add(new ComboBoxItem() { Tag = i, Content = i == 0 ? "ALL" : ("Ch." + i.ToString()) });
                cbChannelMidiOut.Items.Add(new ComboBoxItem() { Tag = i, Content = i == 0 ? "ALL" : ("Ch." + i.ToString()) });
                cbChannelPreset.Items.Add(new ComboBoxItem() { Tag = i, Content = i == 0 ? "ALL" : ("Ch." + i.ToString()) });
            }
            cbChannelMidiIn.SelectedIndex = 1;
            cbChannelMidiOut.SelectedIndex = 1;
            cbChannelPreset.SelectedIndex = 1;

            for (int i = 1; i <= 8; i++)
            {
                cbPresetButton.Items.Add(new ComboBoxItem() { Tag = (i - 1).ToString(), Content = "BUTTON " + i.ToString() });
            }

            cbPresetButton.SelectedIndex = -1;
            cbPresetButton.SelectedIndex = 0; //trick pour le forcer à déclencher l'évènement

        }

        private void Page_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Récupérer le contexte du menu à partir des ressources
            ContextMenu contextMenu = (ContextMenu)this.Resources["RoutingBoxContextMenu"];

            // Ouvrir le menu contextuel
            contextMenu.IsOpen = true;
        }

        private void Menu_OpenMenu(object sender, MouseButtonEventArgs e)
        {
            // Récupérer le contexte du menu à partir des ressources
            ContextMenu contextMenu = (ContextMenu)this.Resources["RoutingBoxContextMenu"];

            // Ouvrir le menu contextuel
            contextMenu.IsOpen = true;
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = (MenuItem)sender;
            OnUIEvent?.Invoke(BoxGuid, menuItem.Tag.ToString(), null);
        }

        private void cbMidiIn_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = e.AddedItems.Count > 0 ? (ComboBoxItem)e.AddedItems[0] : null;

            if (item != null)
            {
                if (item.Tag.Equals(Tools.INTERNAL_GENERATOR))
                {
                    Grid.SetColumnSpan(cbMidiIn, 1);
                    cbChannelMidiIn.Visibility = Visibility.Hidden;
                    pnlInternalGenerator.Visibility = Visibility.Visible;
                }
                else
                {
                    Grid.SetColumnSpan(cbMidiIn, 2);
                    cbChannelMidiIn.Visibility = Visibility.Visible;
                    pnlInternalGenerator.Visibility = Visibility.Hidden;
                }
            }

        }

        private void cbPresetButton_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = e.RemovedItems.Count > 0 ? (ComboBoxItem)e.RemovedItems[0] : null;

            if (item != null)
            {
                var preset = MemCurrentPreset();
                if (preset != null)
                {
                    TempMemory[Convert.ToInt32(item.Tag)] = preset;
                }
            }
        }

        private void tbChoosePreset_Click(object sender, RoutedEventArgs e)
        {
            //charger la liste des presets de l'instrument
            if (cbMidiOut.SelectedValue != null)
            {
                string sDevice = ((ComboBoxItem)cbMidiOut.SelectedItem).Tag.ToString();

                var bgsolo = tbSolo.Background;

                //passage en mode solo pour écoute
                tbSolo.Background = Brushes.IndianRed;
                OnUIEvent?.Invoke(BoxGuid, "SOLO", true);

                if (CubaseInstrumentData.Instruments != null && CubaseInstrumentData.Instruments.Count > 0 && CubaseInstrumentData.Instruments.FirstOrDefault(i => i.Device.Equals(sDevice)) != null)
                {
                    var instr = CubaseInstrumentData.Instruments.FirstOrDefault(i => i.Device.Equals(sDevice));

                    if (InstrumentPresets != null)
                    {
                        InstrumentPresets.OnPresetChanged -= PresetBrowser_OnPresetChanged;
                    }
                    InstrumentPresets = new PresetBrowser(instr);
                    InstrumentPresets.OnPresetChanged += PresetBrowser_OnPresetChanged;

                    InstrumentPresets.ShowDialog();
                }
                else
                {
                    MessageBox.Show("No Instrument Data. It can be created from 'Configuration' Menu. You can manually set Preset data instead (MSB + LSB + PRG)");

                    if (InstrumentPresets != null)
                    {
                        InstrumentPresets.OnPresetChanged -= PresetBrowser_OnPresetChanged;
                    }

                    InstrumentPresets = new PresetBrowser(null);
                    InstrumentPresets.OnPresetChanged += PresetBrowser_OnPresetChanged;
                    InstrumentPresets.ShowDialog();
                }

                //repasse à l'état précédent
                tbSolo.Background = bgsolo;
                if (bgsolo == Brushes.DarkGray)
                {
                    OnUIEvent?.Invoke(BoxGuid, "SOLO", false);
                }

            }
            else
            {
                MessageBox.Show("Please choose a OUT Device first.");
            }
        }

        private void PresetBrowser_OnPresetChanged(MidiPreset mp)
        {
            lbPreset.Text = mp.PresetName;
            lbPreset.Tag = mp.Tag;

            tbPresetName.Text = mp.PresetName;

            OnUIEvent?.Invoke(BoxGuid, "PRESET_CHANGE", GetPreset());
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

        private void cbChannelMidiOut_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbChannelMidiOut.SelectedIndex > -1)
            {
                ComboBoxItem cbOut = (ComboBoxItem)cbChannelMidiOut.SelectedItem;
                cbChannelPreset.SelectedValue = cbOut.Tag;

                OnUIEvent?.Invoke(BoxGuid, "CHECK_OUT_CHANNEL", Convert.ToInt32(cbOut.Tag.ToString()));
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
                var cb = cbCCConvert.SelectedValue;
                cbCCConvert.Items.Remove(cb);
            }
        }

        private void btnRemoveNOTEConvert_Click(object sender, RoutedEventArgs e)
        {
            if (cbNOTEConvert.SelectedItem != null)
            {
                var cb = cbNOTEConvert.SelectedValue;
                cbNOTEConvert.Items.Remove(cb);
            }
        }

        private void btnPreset_Click(object sender, RoutedEventArgs e)
        {
            string sNew = ((Button)sender).Tag.ToString();
            cbPresetButton.SelectedValue = sNew;
            LoadPreset(Convert.ToInt32(sNew));

            if (pnlInternalGenerator.Visibility == Visibility.Visible)
            {
                OnUIEvent?.Invoke(BoxGuid, "PLAY_NOTE", GetOptions());
            }
            else
            {
                OnUIEvent?.Invoke(BoxGuid, "PRESET_CHANGE", GetPreset());
            }
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
                    FillUI(copied, cbPresetButton.SelectedIndex == 0 ? true : false);
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

        public void LoadMemory(BoxPreset[] mem)
        {
            if (mem != null && mem.Length > 0)
            {
                TempMemory = mem;
                if (TempMemory != null && TempMemory.Length > 0)
                {
                    FillUI(TempMemory[0], true);
                }
            }
        }

        private void LoadPreset(int iNew)
        {
            int iPrec = -1;

            //identification du preset en cours
            if (btnPreset1.Background == Brushes.IndianRed) { iPrec = 0; }
            else if (btnPreset2.Background == Brushes.IndianRed) { iPrec = 1; }
            else if (btnPreset3.Background == Brushes.IndianRed) { iPrec = 2; }
            else if (btnPreset4.Background == Brushes.IndianRed) { iPrec = 3; }
            else if (btnPreset5.Background == Brushes.IndianRed) { iPrec = 4; }
            else if (btnPreset6.Background == Brushes.IndianRed) { iPrec = 5; }
            else if (btnPreset7.Background == Brushes.IndianRed) { iPrec = 6; }
            else if (btnPreset8.Background == Brushes.IndianRed) { iPrec = 7; }

            if (TempMemory[iNew] != null)
            {
                FillUI(TempMemory[iNew], iNew > 0 ? false : true);
            }
            else
            {
                MessageBox.Show("Unable to recall Preset");
            }

            switch (iPrec)
            {
                case 0:
                    btnPreset1.Content = TempMemory[iPrec].PresetName;
                    break;
                case 1:
                    btnPreset2.Content = TempMemory[iPrec].PresetName;
                    break;
                case 2:
                    btnPreset3.Content = TempMemory[iPrec].PresetName;
                    break;
                case 3:
                    btnPreset4.Content = TempMemory[iPrec].PresetName;
                    break;
                case 4:
                    btnPreset5.Content = TempMemory[iPrec].PresetName;
                    break;
                case 5:
                    btnPreset6.Content = TempMemory[iPrec].PresetName;
                    break;
                case 6:
                    btnPreset7.Content = TempMemory[iPrec].PresetName;
                    break;
                case 7:
                    btnPreset8.Content = TempMemory[iPrec].PresetName;
                    break;
            }
            //maj couleur boutons
            switch (iNew)
            {
                case 0:
                    btnPreset1.Content = tbPresetName.Text;
                    btnPreset1.Background = Brushes.IndianRed;
                    btnPreset2.Background = Brushes.MediumPurple;
                    btnPreset3.Background = Brushes.MediumPurple;
                    btnPreset4.Background = Brushes.MediumPurple;
                    btnPreset5.Background = Brushes.MediumPurple;
                    btnPreset6.Background = Brushes.MediumPurple;
                    btnPreset7.Background = Brushes.MediumPurple;
                    btnPreset8.Background = Brushes.MediumPurple;
                    break;
                case 1:
                    btnPreset2.Content = tbPresetName.Text;
                    btnPreset1.Background = Brushes.MediumPurple;
                    btnPreset2.Background = Brushes.IndianRed;
                    btnPreset3.Background = Brushes.MediumPurple;
                    btnPreset4.Background = Brushes.MediumPurple;
                    btnPreset5.Background = Brushes.MediumPurple;
                    btnPreset6.Background = Brushes.MediumPurple;
                    btnPreset7.Background = Brushes.MediumPurple;
                    btnPreset8.Background = Brushes.MediumPurple;
                    break;
                case 2:
                    btnPreset3.Content = tbPresetName.Text;
                    btnPreset1.Background = Brushes.MediumPurple;
                    btnPreset2.Background = Brushes.MediumPurple;
                    btnPreset3.Background = Brushes.IndianRed;
                    btnPreset4.Background = Brushes.MediumPurple;
                    btnPreset5.Background = Brushes.MediumPurple;
                    btnPreset6.Background = Brushes.MediumPurple;
                    btnPreset7.Background = Brushes.MediumPurple;
                    btnPreset8.Background = Brushes.MediumPurple;
                    break;
                case 3:
                    btnPreset4.Content = tbPresetName.Text;
                    btnPreset1.Background = Brushes.MediumPurple;
                    btnPreset2.Background = Brushes.MediumPurple;
                    btnPreset3.Background = Brushes.MediumPurple;
                    btnPreset4.Background = Brushes.IndianRed;
                    btnPreset5.Background = Brushes.MediumPurple;
                    btnPreset6.Background = Brushes.MediumPurple;
                    btnPreset7.Background = Brushes.MediumPurple;
                    btnPreset8.Background = Brushes.MediumPurple;
                    break;
                case 4:
                    btnPreset5.Content = tbPresetName.Text;
                    btnPreset1.Background = Brushes.MediumPurple;
                    btnPreset2.Background = Brushes.MediumPurple;
                    btnPreset3.Background = Brushes.MediumPurple;
                    btnPreset4.Background = Brushes.MediumPurple;
                    btnPreset5.Background = Brushes.IndianRed;
                    btnPreset6.Background = Brushes.MediumPurple;
                    btnPreset7.Background = Brushes.MediumPurple;
                    btnPreset8.Background = Brushes.MediumPurple;
                    break;
                case 5:
                    btnPreset6.Content = tbPresetName.Text;
                    btnPreset1.Background = Brushes.MediumPurple;
                    btnPreset2.Background = Brushes.MediumPurple;
                    btnPreset3.Background = Brushes.MediumPurple;
                    btnPreset4.Background = Brushes.MediumPurple;
                    btnPreset5.Background = Brushes.MediumPurple;
                    btnPreset6.Background = Brushes.IndianRed;
                    btnPreset7.Background = Brushes.MediumPurple;
                    btnPreset8.Background = Brushes.MediumPurple;
                    break;
                case 6:
                    btnPreset7.Content = tbPresetName.Text;
                    btnPreset1.Background = Brushes.MediumPurple;
                    btnPreset2.Background = Brushes.MediumPurple;
                    btnPreset3.Background = Brushes.MediumPurple;
                    btnPreset4.Background = Brushes.MediumPurple;
                    btnPreset5.Background = Brushes.MediumPurple;
                    btnPreset6.Background = Brushes.MediumPurple;
                    btnPreset7.Background = Brushes.IndianRed;
                    btnPreset8.Background = Brushes.MediumPurple;
                    break;
                case 7:
                    btnPreset8.Content = tbPresetName.Text;
                    btnPreset1.Background = Brushes.MediumPurple;
                    btnPreset2.Background = Brushes.MediumPurple;
                    btnPreset3.Background = Brushes.MediumPurple;
                    btnPreset4.Background = Brushes.MediumPurple;
                    btnPreset5.Background = Brushes.MediumPurple;
                    btnPreset6.Background = Brushes.MediumPurple;
                    btnPreset7.Background = Brushes.MediumPurple;
                    btnPreset8.Background = Brushes.IndianRed;
                    break;
            }
        }

        public void Snapshot()
        {
            var bp = MemCurrentPreset();
            var item = (ComboBoxItem)cbPresetButton.SelectedItem;
            if (item != null)
            {
                int idxOut = Convert.ToInt32(item.Tag.ToString());
                TempMemory[idxOut] = bp;
                LoadPreset(idxOut);
            }
        }

        private BoxPreset MemCurrentPreset()
        {
            try
            {
                //mémorisation des données en cours
                var options = GetOptions();
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

        private void FillUI(BoxPreset bp, bool bIsFirst)
        {
            if (bIsFirst)
            {
                cbMidiIn.IsEnabled = true;
                cbMidiOut.IsEnabled = true;
                cbChannelMidiIn.IsEnabled = true;
                cbChannelMidiOut.IsEnabled = true;
            }
            else
            {
                cbMidiIn.IsEnabled = false;
                cbMidiOut.IsEnabled = false;
                cbChannelMidiIn.IsEnabled = false;
                cbChannelMidiOut.IsEnabled = false;
            }

            //remplissage des champs
            tbPresetName.Text = bp.PresetName;
            tbRoutingName.Text = bp.BoxName;

            if (bp.MidiOptions.PlayNote != null)
            {
                tbInternalGeneratorKey.Text = bp.MidiOptions.PlayNote.Note.ToString();
                tbInternalGeneratorVelocity.Text = bp.MidiOptions.PlayNote.Velocity.ToString();
                tbInternalGeneratorLength.Text = bp.MidiOptions.PlayNote.Length.ToString();
            }

            if (bIsFirst)
            {
                cbMidiIn.SelectedValue = bp.DeviceIn;
                cbMidiOut.SelectedValue = bp.DeviceOut;
                cbChannelMidiIn.SelectedValue = bp.ChannelIn;
                cbChannelMidiOut.SelectedValue = bp.ChannelOut;
            }

            cbChannelPreset.SelectedValue = bp.MidiPreset.Channel;
            lbPreset.Text = bp.MidiPreset.PresetName;
            lbPreset.Tag = bp.MidiPreset.Tag;

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
                try
                {
                    int iPrg = Convert.ToInt32(lbPreset.Tag.ToString().Split('-')[0]);
                    int iMsb = Convert.ToInt32(lbPreset.Tag.ToString().Split('-')[1]);
                    int iLsb = Convert.ToInt32(lbPreset.Tag.ToString().Split('-')[2]);
                    return new MidiPreset("", Convert.ToInt32(((ComboBoxItem)cbChannelPreset.SelectedItem).Tag.ToString()), iPrg, iMsb, iLsb, lbPreset.Text);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Invalid Program (" + ex.Message + ")");
                    return new MidiPreset("", 1, 0, 0, 0, "Unknown Preset");
                }
            }
            else
            {
                return new MidiPreset("", 1, 0, 0, 0, "Unknown Preset");
            }
        }

        public MidiOptions GetOptions()
        {
            var options = new MidiOptions();

            int iNoteGen = -1;
            int iVeloGen = -1;
            int iChannel = -1;
            decimal dLength = 0;
            NumberStyles style = NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands;
            CultureInfo culture = CultureInfo.InvariantCulture; // ou utilisez la culture appropriée selon vos besoins

            int.TryParse(tbInternalGeneratorKey.Text, out iNoteGen);
            int.TryParse(tbInternalGeneratorVelocity.Text, out iVeloGen);
            int.TryParse(cbChannelMidiOut.SelectedValue.ToString(), out iChannel);
            decimal.TryParse(tbInternalGeneratorLength.Text, style, culture, out dLength);
            options.PlayNote = new NoteGenerator(iChannel, 0, iNoteGen, iVeloGen, dLength);

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

            //pour éviter que le volume soit à 0 après un click sur aftertouch
            if (ckAftertouchToNote.IsChecked.Value && options.CC_Volume_Value > 0)
            { options.CC_Volume_Value = -1; }
            else if (!ckAftertouchToNote.IsChecked.Value && options.CC_Volume_Value == -1)
            { options.CC_Volume_Value = 100; }

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

        public List<BoxPreset> GetRoutingBoxMemory()
        {
            return TempMemory.ToList();
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

        public BoxPreset(Guid boxGuid, string sName)
        {
            BoxGuid = boxGuid;
            PresetName = sName;
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
