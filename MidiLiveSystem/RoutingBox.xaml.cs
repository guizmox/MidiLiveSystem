﻿using MessagePack;
using MidiTools;
using RtMidi.Core.Devices.Infos;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VSTHost;

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
        private System.Timers.Timer UIBlink;

        public VSTHost.MainWindow VSTWindow;

        public Guid RoutingGuid { get; private set; }
        public bool Detached { get; internal set; } = false;
        public bool HasVSTAttached { get { return TempMemory[CurrentPreset].VSTData != null; } }

        public int GridPosition = 0;
        public int CurrentPreset = 0;

        public Guid BoxGuid { get; private set; } = Guid.NewGuid();
        public string BoxName { get; private set; } = "Routing Box";
        public string PresetName { get { return Dispatcher.Invoke(() => tbPresetName.Text.Trim()); } }

        public bool ChangingPreset { get { return UIBlink != null; } }

        public delegate void RoutingBoxEventHandler(Guid gBox, string sControl, object sValue);
        public event RoutingBoxEventHandler OnUIEvent;

        BoxPreset[] TempMemory = new BoxPreset[8];
        VSTPlugin[] TempVST = new VSTPlugin[8];
        readonly int[,] TempCCMix = new int[8, 128];
        readonly int[,] TempCCHigh = new int[8, 128];
        readonly int[,] TempCCLow = new int[8, 128];
        readonly int[,] TempCCDefault = new int[8, 8];

        PresetBrowser InstrumentPresets = null;

        public RoutingBox(List<IMidiInputDeviceInfo> inputDevices, List<IMidiOutputDeviceInfo> outputDevices, int gridPosition, Guid boxGuid, Guid routingGuid, BoxPreset[] presets)
        {
            InitializeComponent();

            BoxGuid = boxGuid;
            RoutingGuid = routingGuid;
            LoadMemory(presets);

            GridPosition = gridPosition;
            BoxName = presets != null ? presets[0].BoxName : "Routing Box " + (GridPosition + 1).ToString();

            InitPage(inputDevices, outputDevices);

            MidiRouting.OutputMidiMessage += MidiRouting_OutputMidiMessage;
        }

        public RoutingBox(List<IMidiInputDeviceInfo> inputDevices, List<IMidiOutputDeviceInfo> outputDevices, int gridPosition)
        {
            InitializeComponent();

            GridPosition = gridPosition;
            BoxName = "Routing Box " + (GridPosition + 1).ToString();

            for (int iP = 0; iP < 8; iP++)
            {
                for (int iCC = 0; iCC < 128; iCC++)
                {
                    int iValue = -1;
                    if (iCC == 10) { iValue = 64; }
                    else if (iCC == 7) { iValue = 100; }
                    TempCCMix[iP, iCC] = iValue;
                    TempCCLow[iP, iCC] = 0;
                    TempCCHigh[iP, iCC] = 127;
                }
                TempCCDefault[iP, 0] = 1;
                TempCCDefault[iP, 1] = 7;
                TempCCDefault[iP, 2] = 10;
                TempCCDefault[iP, 3] = 11;
                TempCCDefault[iP, 4] = 70;
                TempCCDefault[iP, 5] = 71;
                TempCCDefault[iP, 6] = 91;
                TempCCDefault[iP, 7] = 93;
            }

            TempVST = new VSTPlugin[8] { new(), new(), new(), new(), new(), new(), new(), new() };

            TempMemory = new BoxPreset[8] { new(RoutingGuid, BoxGuid, 0, "Preset 1", BoxName), new(RoutingGuid, BoxGuid, 1, "Preset 2", BoxName), new(RoutingGuid, BoxGuid, 2, "Preset 3", BoxName),
                                            new(RoutingGuid, BoxGuid, 3, "Preset 4", BoxName), new(RoutingGuid, BoxGuid, 4, "Preset 5", BoxName), new(RoutingGuid, BoxGuid, 5, "Preset 6", BoxName),
                                            new(RoutingGuid, BoxGuid, 6, "Preset 7", BoxName), new(RoutingGuid, BoxGuid, 7, "Preset 8", BoxName) };

            InitPage(inputDevices, outputDevices);

            MidiRouting.OutputMidiMessage += MidiRouting_OutputMidiMessage;
        }

        private async void VSTWindow_OnVSTHostEvent(int iPreset, int iAction)
        {
            if (iAction == 0) //fermer la fenêtre
            {
                //TempMemory[iPreset].VSTData = TempVST[CurrentPreset].VSTHostInfo; //pas certain
                VSTWindow.OnVSTHostEvent -= VSTWindow_OnVSTHostEvent;
                VSTWindow = null;
            }
            else if (iAction == 1) //chargement initial du VST
            {
                //TempMemory[iPreset].VSTData = TempVST[CurrentPreset].VSTHostInfo; //pas certain
                OnUIEvent?.Invoke(BoxGuid, "INITIALIZE_AUDIO", TempVST[iPreset]); //pour initialiser l'audio
                await VSTWindow.LoadPlugin();
            }
            else if (iAction == 2)
            {
                int iCh = await cbChannelMidiOut.Dispatcher.InvokeAsync(() => Convert.ToInt32(cbChannelMidiOut.SelectedValue));
                await tbPresetName.Dispatcher.InvokeAsync(() =>
                {
                    if (!tbPresetName.IsFocused && (tbPresetName.Text.Length == 0 || tbPresetName.Text.StartsWith("Preset ")))
                    {
                        if (iCh > 0)
                        {
                            string sPrg = TempVST[iPreset].VSTHostInfo.ChannelPrograms[iCh - 1];
                            if (sPrg.Length > 0) { tbPresetName.Text = sPrg; }
                        }
                    }
                });
            }
        }

        private async void MidiRouting_OutputMidiMessage(bool b, Guid routingGuid)
        {
            if (RoutingGuid == routingGuid)
            {
                await tbRoutingPlay.Dispatcher.InvokeAsync(() =>
                {
                    if (b)
                    {
                        tbRoutingPlay.Foreground = Brushes.IndianRed;
                    }
                    else
                    {
                        tbRoutingPlay.Foreground = Brushes.White;
                    }
                });
            }
        }

        private async void InitPage(List<IMidiInputDeviceInfo> inputDevices, List<IMidiOutputDeviceInfo> outputDevices)
        {
            for (int i = -36; i <= 36; i++)
            {
                cbNoteTransposition.Items.Add(new ComboBoxItem() { Tag = i.ToString(), Content = i.ToString() });
            }
            cbNoteTransposition.SelectedValue = "0";

            for (int i = 0; i <= 500; i += 10)
            {
                cbDelayNotes.Items.Add(new ComboBoxItem() { Tag = i.ToString(), Content = i.ToString() + " ms" });
            }
            cbDelayNotes.SelectedValue = "0";

            for (int i = 0; i <= 5000; i += 100)
            {
                cbSmoothCC.Items.Add(new ComboBoxItem() { Tag = i.ToString(), Content = i.ToString() + " ms" });
                cbSmoothPresetChange.Items.Add(new ComboBoxItem() { Tag = i.ToString(), Content = i.ToString() + " ms" });
            }
            cbSmoothCC.SelectedValue = "0";
            cbSmoothPresetChange.SelectedValue = "0";

            foreach (var s in inputDevices)
            {
                cbMidiIn.Items.Add(new ComboBoxItem() { Tag = s.Name, Content = s.Name });
            }

            cbMidiIn.Items.Add(new ComboBoxItem() { Tag = Tools.INTERNAL_GENERATOR, Content = Tools.INTERNAL_GENERATOR });
            cbMidiIn.Items.Add(new ComboBoxItem() { Tag = Tools.INTERNAL_SEQUENCER, Content = Tools.INTERNAL_SEQUENCER });
            cbMidiIn.Items.Add(new ComboBoxItem() { Tag = Tools.ALL_INPUTS, Content = Tools.ALL_INPUTS });

            foreach (var s in outputDevices)
            {
                cbMidiOut.Items.Add(new ComboBoxItem() { Tag = s.Name, Content = s.Name });
            }
            cbMidiOut.Items.Add(new ComboBoxItem() { Tag = Tools.VST_HOST, Content = Tools.VST_HOST });

            cbChannelMidiOut.Items.Add(new ComboBoxItem() { Tag = "0", Content = "NA" });
            for (int i = 0; i <= 16; i++)
            {
                cbChannelMidiIn.Items.Add(new ComboBoxItem() { Tag = i, Content = i == 0 ? "ALL" : ("Ch." + i.ToString()) });
                if (i > 0) { cbChannelMidiOut.Items.Add(new ComboBoxItem() { Tag = i, Content = "Ch." + i.ToString() }); }
            }
            cbChannelMidiIn.SelectedIndex = 1;

            cbChannelMidiOut.SelectedIndex = 0;

            for (int i = 1; i <= 8; i++)
            {
                cbPresetButton.Items.Add(new ComboBoxItem() { Tag = (i - 1).ToString(), Content = "BUTTON " + i.ToString() });
            }

            await PresetButtonPushed();

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
            ContextMenu contextMenu = (ContextMenu)this.Resources["RoutingBoxContextMenu"];
            contextMenu.IsOpen = true;
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = (MenuItem)sender;
            OnUIEvent?.Invoke(BoxGuid, menuItem.Tag.ToString(), null);
        }

        private async void cbPresetButton_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var itemOLD = e.RemovedItems.Count > 0 ? (ComboBoxItem)e.RemovedItems[0] : null;
            var itemNEW = e.AddedItems.Count > 0 ? (ComboBoxItem)e.AddedItems[0] : null;

            if (itemOLD != null)
            {
                try
                {
                    var preset = await MemCurrentPreset();
                    int iPreset = Convert.ToInt32(itemOLD.Tag);
                    TempMemory[Convert.ToInt32(iPreset)] = preset;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to change box preset : " + ex.Message);
                }

            }

            if (itemNEW != null)
            {
                int iPreset = Convert.ToInt32(itemNEW.Tag);
                CurrentPreset = iPreset;
                await PresetButtonPushed();
            }
        }

        private async void cbPlayMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                Enum.TryParse<PlayModes>(cbPlayMode.SelectedValue.ToString(), out PlayModes playmode);

                switch (playmode)
                {
                    case PlayModes.OCTAVE_DOWN:
                        tbPlayModeOption.Visibility = Visibility.Visible;
                        cbPlayModeOption.Visibility = Visibility.Visible;
                        tbPlayModeOption.Text = "% VEL";
                        cbPlayModeOption.Items.Clear();
                        for (int i = 10; i <= 150; i += 10)
                        {
                            cbPlayModeOption.Items.Add(new ComboBoxItem() { Tag = i.ToString(), Content = string.Concat(i.ToString(), " %") });
                        }
                        cbPlayModeOption.SelectedValue = "100";
                        break;
                    case PlayModes.OCTAVE_UP:
                        tbPlayModeOption.Visibility = Visibility.Visible;
                        cbPlayModeOption.Visibility = Visibility.Visible;
                        tbPlayModeOption.Visibility = Visibility.Visible;
                        cbPlayModeOption.Visibility = Visibility.Visible;
                        tbPlayModeOption.Text = "% VEL";
                        cbPlayModeOption.Items.Clear();
                        for (int i = 10; i <= 150; i += 10)
                        {
                            cbPlayModeOption.Items.Add(new ComboBoxItem() { Tag = i.ToString(), Content = string.Concat(i.ToString(), " %") });
                        }
                        cbPlayModeOption.SelectedValue = "100";
                        break;
                    case PlayModes.REPEAT_NOTE_OFF_FAST:
                        tbPlayModeOption.Visibility = Visibility.Visible;
                        cbPlayModeOption.Visibility = Visibility.Visible;
                        tbPlayModeOption.Visibility = Visibility.Visible;
                        cbPlayModeOption.Visibility = Visibility.Visible;
                        tbPlayModeOption.Text = "% VEL";
                        cbPlayModeOption.Items.Clear();
                        for (int i = 10; i <= 150; i += 10)
                        {
                            cbPlayModeOption.Items.Add(new ComboBoxItem() { Tag = i.ToString(), Content = string.Concat(i.ToString(), " %") });
                        }
                        cbPlayModeOption.SelectedValue = "100";
                        break;
                    case PlayModes.REPEAT_NOTE_OFF_SLOW:
                        tbPlayModeOption.Visibility = Visibility.Visible;
                        cbPlayModeOption.Visibility = Visibility.Visible;
                        tbPlayModeOption.Visibility = Visibility.Visible;
                        cbPlayModeOption.Visibility = Visibility.Visible;
                        tbPlayModeOption.Text = "% VEL";
                        cbPlayModeOption.Items.Clear();
                        for (int i = 10; i <= 150; i += 10)
                        {
                            cbPlayModeOption.Items.Add(new ComboBoxItem() { Tag = i.ToString(), Content = string.Concat(i.ToString(), " %") });
                        }
                        cbPlayModeOption.SelectedValue = "100";
                        break;
                    case PlayModes.PIZZICATO_FAST:
                        tbPlayModeOption.Visibility = Visibility.Visible;
                        cbPlayModeOption.Visibility = Visibility.Visible;
                        tbPlayModeOption.Visibility = Visibility.Visible;
                        cbPlayModeOption.Visibility = Visibility.Visible;
                        tbPlayModeOption.Text = "LENGTH";
                        cbPlayModeOption.Items.Clear();
                        for (int i = 20; i <= 300; i += 20)
                        {
                            cbPlayModeOption.Items.Add(new ComboBoxItem() { Tag = i.ToString(), Content = string.Concat(i.ToString(), " ms") });
                        }
                        cbPlayModeOption.SelectedValue = "200";
                        break;
                    case PlayModes.PIZZICATO_SLOW:
                        tbPlayModeOption.Visibility = Visibility.Visible;
                        cbPlayModeOption.Visibility = Visibility.Visible;
                        tbPlayModeOption.Text = "LENGTH";
                        cbPlayModeOption.Items.Clear();
                        for (int i = 400; i <= 800; i += 20)
                        {
                            cbPlayModeOption.Items.Add(new ComboBoxItem() { Tag = i.ToString(), Content = string.Concat(i.ToString(), " ms") });
                        }
                        cbPlayModeOption.SelectedValue = "600";
                        break;
                    default:
                        tbPlayModeOption.Visibility = Visibility.Hidden;
                        cbPlayModeOption.Visibility = Visibility.Hidden;
                        cbPlayModeOption.Items.Clear();
                        break;
                }
            });
        }

        private async void cbSmoothPresetChange_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                var item = e.AddedItems.Count > 0 ? (ComboBoxItem)e.AddedItems[0] : null;

                if (item != null && ((ComboBox)sender).IsFocused && !item.Tag.Equals("0"))
                {
                    string sMidiOut = string.Concat(cbMidiOut.SelectedValue.ToString(), "#|#", cbChannelMidiOut.SelectedValue);
                    TempMemory[CurrentPreset].MidiOptions.PresetMorphing = Convert.ToInt32(item.Tag);
                    OnUIEvent?.Invoke(BoxGuid, "CHECK_OUT_CHANNEL", sMidiOut);
                }
            });
        }

        private void ckInternalGeneratorLowestKey_Checked(object sender, RoutedEventArgs e)
        {
            tbInternalGeneratorKeyLabel.Visibility = Visibility.Hidden;
            tbInternalGeneratorKey.Visibility = Visibility.Hidden;
        }

        private void ckInternalGeneratorLowestKey_Unchecked(object sender, RoutedEventArgs e)
        {
            tbInternalGeneratorKeyLabel.Visibility = Visibility.Visible;
            tbInternalGeneratorKey.Visibility = Visibility.Visible;
        }

        private async void tbChoosePreset_Click(object sender, RoutedEventArgs e)
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

                    var preset = await GetPreset();

                    InstrumentPresets = new PresetBrowser(instr, preset);
                    InstrumentPresets.OnPresetChanged += PresetBrowser_OnPresetChanged;
                    InstrumentPresets.ShowDialog();
                    InstrumentPresets.GetPreset();
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
                    InstrumentPresets.GetPreset();
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

        private async void PresetBrowser_OnPresetChanged(MidiPreset mp)
        {
            if (!mp.PresetName.Equals("[ERROR]"))
            {
                lbPreset.Text = mp.PresetName;
                lbPreset.Tag = mp.Tag;

                tbPresetName.Text = mp.PresetName;

                try
                {
                    var preset = await MemCurrentPreset();

                    OnUIEvent?.Invoke(BoxGuid, "PRESET_CHANGE", preset);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to save current box preset : " + ex.Message);
                }
            }
        }

        private void btnCCMix_Click(object sender, RoutedEventArgs e)
        {
            OnUIEvent?.Invoke(BoxGuid, "OPEN_CC_MIX", null);
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
                TempMemory[CurrentPreset].MidiOptions.Active = true;
                OnUIEvent?.Invoke(BoxGuid, "MUTE", false);
                tbMute.Background = Brushes.DarkGray;
            }
            else
            {
                TempMemory[CurrentPreset].MidiOptions.Active = false;
                OnUIEvent?.Invoke(BoxGuid, "MUTE", true);
                tbMute.Background = Brushes.IndianRed;
            }
        }

        private void cbMidiIn_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = e.AddedItems.Count > 0 ? (ComboBoxItem)e.AddedItems[0] : null;

            if (item != null)
            {
                if (item.Tag.Equals(Tools.INTERNAL_GENERATOR))
                {
                    tbVelocityRangeLabel.Visibility = Visibility.Hidden;
                    tbNoteRangeLabel.Visibility = Visibility.Hidden;
                    pnlNoteRange.Visibility = Visibility.Hidden;
                    pnlInternalGenerator.Visibility = Visibility.Visible;
                }
                else if (item.Tag.Equals(Tools.VST_HOST))
                {
                    tbVelocityRangeLabel.Visibility = Visibility.Hidden;
                    tbNoteRangeLabel.Visibility = Visibility.Hidden;
                    pnlNoteRange.Visibility = Visibility.Hidden;
                    pnlInternalGenerator.Visibility = Visibility.Hidden;
                }
                else
                {
                    tbVelocityRangeLabel.Visibility = Visibility.Visible;
                    tbNoteRangeLabel.Visibility = Visibility.Visible;
                    pnlNoteRange.Visibility = Visibility.Visible;
                    pnlInternalGenerator.Visibility = Visibility.Hidden;
                }
            }

        }

        private void cbVSTSlot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbMidiOut.SelectedItem != null && cbVSTSlot.SelectedIndex > 0 && ((ComboBoxItem)e.AddedItems[0]).IsFocused) //((ComboBoxItem)e.AddedItems[0]).IsFocused && 
            {
                if (cbMidiOut.SelectedValue.Equals(Tools.VST_HOST))
                {
                    int iSlot = Convert.ToInt32(cbVSTSlot.SelectedValue);
                    OnUIEvent?.Invoke(BoxGuid, "CHECK_VST_HOST", string.Concat(cbMidiOut.SelectedValue.ToString(), "-{", iSlot, "}"));
                }
            }
        }

        private async void cbMidiOut_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = e.AddedItems.Count > 0 ? (ComboBoxItem)e.AddedItems[0] : null;

            if (item != null && item.IsFocused)
            {
                if (e.RemovedItems.Count > 0) //pour éviter de péter le VSTHost lorsqu'on charge un projet (removeditems est dans ce cas à vide)
                {
                    await CloseVSTHost(true); //je ne supprime pas le solut VST à ce stade pour pouvoir le réutiliser si besoin
                }

                if (item.Tag.Equals(Tools.VST_HOST))
                {
                    await SwitchVSTPanel(true);
                    if (cbVSTSlot.SelectedIndex > 0)
                    {
                        OnUIEvent?.Invoke(BoxGuid, "CHECK_VST_HOST", string.Concat(item.Tag.ToString(), "-{", cbVSTSlot.SelectedValue.ToString(), "}"));
                    }
                }
                else
                {
                    await SwitchVSTPanel(false);
                }
                OnUIEvent?.Invoke(BoxGuid, "CHECK_OUT_CHANNEL", item.Tag.ToString() + "#|#" + cbChannelMidiOut.SelectedValue);
            }
        }

        private void cbChannelMidiOut_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbMidiOut.SelectedItem != null && ((ComboBoxItem)e.AddedItems[0]).IsFocused && cbChannelMidiOut.SelectedIndex > 0)
            {
                ComboBoxItem devOut = (ComboBoxItem)cbMidiOut.SelectedItem;

                OnUIEvent?.Invoke(BoxGuid, "CHECK_OUT_CHANNEL", devOut.Tag.ToString() + "#|#" + cbChannelMidiOut.SelectedValue);
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
                    cbCCConvert.SelectedIndex = cbCCConvert.Items.Count - 1;
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
                    cbNOTEConvert.SelectedIndex = cbNOTEConvert.Items.Count - 1;
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
                cbCCConvert.Items.Remove(cbCCConvert.SelectedItem);
            }
        }

        private void btnRemoveNOTEConvert_Click(object sender, RoutedEventArgs e)
        {
            if (cbNOTEConvert.SelectedItem != null)
            {
                cbNOTEConvert.Items.Remove(cbNOTEConvert.SelectedItem);
            }
        }

        private void btnAddTranslator_Click(object sender, RoutedEventArgs e)
        {
            MidiTranslator mt = new();
            mt.ShowDialog();
            MessageTranslator sTranslator = mt.GetTranslatorConfiguration();
            if (sTranslator == null || mt.InvalidData)
            {
                MessageBox.Show("There's an issue with the input. Translator info can't be processed. Please try again.");
            }
            else
            {
                bool bExists = false;
                string sTag = sTranslator.Tag();
                foreach (ComboBoxItem item in cbTranslators.Items)
                {
                    if (item.Tag.Equals(sTag))
                    {
                        bExists = true;
                        break;
                    }
                }
                if (!bExists)
                {
                    cbTranslators.Items.Add(new ComboBoxItem() { Tag = sTranslator.Tag(), Content = sTranslator });
                    cbTranslators.SelectedIndex = cbTranslators.Items.Count - 1;
                }
                else { MessageBox.Show("A similar Translation has already been set."); }

            }
        }

        private void btnRemoveTranslator_Click(object sender, RoutedEventArgs e)
        {
            if (cbTranslators.SelectedItem != null)
            {
                cbTranslators.Items.Remove(cbTranslators.SelectedItem);
            }
        }

        private async void btnPreset_Click(object sender, RoutedEventArgs e)
        {
            int i = Convert.ToInt32(((Button)sender).Tag);
            if (cbPresetButton.SelectedIndex == i) //il ne déclenchera pas l'évènement...
            {
                await PresetButtonPushed();
            }
            else
            {
                cbPresetButton.SelectedIndex = i;
            }
        }

        private async void tbOpenVST_Click(object sender, RoutedEventArgs e)
        {
            await OpenVSTHost();
        }

        private async void tbRemoveVST_Click(object sender, RoutedEventArgs e)
        {
            if (TempMemory[CurrentPreset].VSTData != null)
            {
                var result = MessageBox.Show("Are you sure to remove " + TempMemory[CurrentPreset].VSTData.VSTName + " ? If there are other presets using that VST instrument, they will also be deleted.", "Remove VST instrument", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                {
                    await CloseVSTHost(false);
                }
            }
            else
            {
                MessageBox.Show("Nothing to remove.");
            }
        }

        private async void btnCopyPreset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var bp = await MemCurrentPreset();
                OnUIEvent?.Invoke(BoxGuid, "COPY_PRESET", bp);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to Save Preset (" + ex.Message + ")");
            }
        }

        private async void btnPastePreset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var copied = MainWindow.CopiedPreset;
                if (copied != null)
                {
                    await FillUI(copied, CurrentPreset == 0);
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

        public async Task PresetButtonPushed()
        {
            if (TempMemory[CurrentPreset].DeviceIn.Equals(Tools.INTERNAL_GENERATOR))
            {
                OnUIEvent?.Invoke(BoxGuid, "PLAY_NOTE", TempMemory[CurrentPreset]);
                await LoadPreset(CurrentPreset);
            }
            else
            {
                OnUIEvent?.Invoke(BoxGuid, "PRESET_CHANGE", TempMemory[CurrentPreset]);
                await LoadPreset(CurrentPreset);
            }

            if (TempMemory[CurrentPreset].DeviceOut.StartsWith(Tools.VST_HOST))
            {
                await SwitchVSTPanel(true);
                await Dispatcher.InvokeAsync(() =>
                {
                    OnUIEvent?.Invoke(BoxGuid, "CHECK_VST_HOST", string.Concat(cbMidiOut.SelectedValue.ToString(), "-{", cbVSTSlot.SelectedValue.ToString(), "}"));
                });
            }
            else
            {
                await SwitchVSTPanel(false);
            }
        }

        private async Task SwitchVSTPanel(bool bShowVSTControls)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (bShowVSTControls)
                {
                    lbPreset.Text = "VST Program";
                    lbPreset.Tag = "0-0-0";
                    cbVSTSlot.Visibility = Visibility.Visible;
                    tbChoosePreset.Visibility = Visibility.Hidden;
                    lbPreset.Visibility = Visibility.Hidden;
                    tbOpenVST.Visibility = Visibility.Visible;
                    tbRemoveVST.Visibility = Visibility.Visible;
                    tbProgram.Text = "VST HOST";
                    tbProgram.Visibility = Visibility.Hidden;
                }
                else
                {
                    cbVSTSlot.Visibility = Visibility.Hidden;
                    tbChoosePreset.Visibility = Visibility.Visible;
                    lbPreset.Visibility = Visibility.Visible;
                    tbOpenVST.Visibility = Visibility.Hidden;
                    tbRemoveVST.Visibility = Visibility.Hidden;
                    tbProgram.Text = "PROGRAM";
                    tbProgram.Visibility = Visibility.Visible;
                }
            });
        }

        public async void LoadMemory(BoxPreset[] mem)
        {
            if (mem != null && mem.Length > 0)
            {
                if (mem.Count(m => m.Index == 0) == 8) //vieilles sauvegardes
                {
                    for (int i = 0; i < 8; i++)
                    {
                        mem[i].Index = i;
                    }
                }

                TempMemory = mem;

                if (TempMemory != null && TempMemory.Length > 0)
                {
                    await FillUI(TempMemory[0], true);
                }
            }

            TempVST = new VSTPlugin[8] { new(), new(), new(), new(), new(), new(), new(), new() };
            for (int i = 0; i < 8; i++)
            {
                TempVST[i].VSTHostInfo = mem[i].VSTData;
            }

            //for (int iP = 0; iP < 8; iP++)
            //{
            //    for (int iCC = 0; iCC < 128; iCC++)
            //    {
            //        int iValue = -1;
            //        if (iCC == 7) { iValue = 100; }
            //        else if (iCC == 10) { iValue = 64; }
            //        TempCCMix[iP, iCC] = iValue;
            //    }
            //}

            for (int i = 0; i < 8; i++)
            {
                for (int iCC = 0; iCC < 128; iCC++)
                {
                    TempCCMix[i, iCC] = mem[i].MidiOptions.DefaultRoutingCC[iCC];
                    TempCCHigh[i, iCC] = mem[i].MidiOptions.CC_HighLimiter[iCC];
                    TempCCLow[i, iCC] = mem[i].MidiOptions.CC_LowLimiter[iCC];
                }
                for (int i2 = 0; i2 < 8; i2++)
                {
                    TempCCDefault[i, i2] = mem[i].MidiOptions.CCMixDefaultParameters[i2];
                }
            }
        }

        private async Task LoadPreset(int iNew)
        {
            int iPrec = -1;

            await Dispatcher.InvokeAsync(() =>
            {
                //identification du preset en cours
                if (btnPreset1.Background == Brushes.IndianRed) { iPrec = 0; }
                else if (btnPreset2.Background == Brushes.IndianRed) { iPrec = 1; }
                else if (btnPreset3.Background == Brushes.IndianRed) { iPrec = 2; }
                else if (btnPreset4.Background == Brushes.IndianRed) { iPrec = 3; }
                else if (btnPreset5.Background == Brushes.IndianRed) { iPrec = 4; }
                else if (btnPreset6.Background == Brushes.IndianRed) { iPrec = 5; }
                else if (btnPreset7.Background == Brushes.IndianRed) { iPrec = 6; }
                else if (btnPreset8.Background == Brushes.IndianRed) { iPrec = 7; }
            });

            if (TempMemory[iNew] != null)
            {
                await FillUI(TempMemory[iNew], iNew <= 0);
            }
            else
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show("Unable to recall Preset");
                });
            }

            await Dispatcher.InvokeAsync(() =>
            {
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
            });
        }

        public async Task<BoxPreset> Snapshot()
        {
            try
            {
                var bp = await MemCurrentPreset();
                TempMemory[CurrentPreset] = bp;
                await LoadPreset(CurrentPreset);
                return bp;
            }
            catch { return new BoxPreset(); }
        }

        private async Task<BoxPreset> MemCurrentPreset()
        {
            BoxPreset boxPreset = null;

            string presetname = "";
            string routingname = "";
            string sDeviceIn = "";
            string sDeviceOut = "";
            int iChannelIn = 0;
            int iChannelOut = 0;

            try
            {

                await Dispatcher.InvokeAsync(() =>
                {
                    //mémorisation des données en cours
                    presetname = tbPresetName.Text.Trim();
                    routingname = tbRoutingName.Text.Trim();
                    sDeviceIn = cbMidiIn.SelectedItem == null ? "" : ((ComboBoxItem)cbMidiIn.SelectedItem).Tag.ToString();
                    sDeviceOut = cbMidiOut.SelectedItem == null ? "" : ((ComboBoxItem)cbMidiOut.SelectedItem).Tag.ToString();

                    if (cbMidiOut.SelectedItem != null && ((ComboBoxItem)cbMidiOut.SelectedItem).Tag.ToString().Equals(Tools.VST_HOST) && cbVSTSlot.SelectedIndex > 0)
                    {
                        sDeviceOut = string.Concat(sDeviceOut, "-{", cbVSTSlot.SelectedValue.ToString(), "}");
                    }

                    iChannelIn = cbChannelMidiIn.SelectedItem == null ? 1 : Convert.ToInt32(((ComboBoxItem)cbChannelMidiIn.SelectedItem).Tag.ToString());
                    iChannelOut = cbChannelMidiOut.SelectedItem == null ? 1 : Convert.ToInt32(((ComboBoxItem)cbChannelMidiOut.SelectedItem).Tag.ToString());
                });

                var options = await GetOptions();
                var preset = await GetPreset();
                VSTHostInfo vst = TempVST[CurrentPreset].VSTHostInfo;

                var bp = new BoxPreset(RoutingGuid, BoxGuid, CurrentPreset, routingname, presetname, options, preset, sDeviceIn, sDeviceOut, vst, iChannelIn, iChannelOut);

                boxPreset = bp;

            }
            catch { throw; }

            return boxPreset;
        }

        private async Task FillUI(BoxPreset bp, bool bIsFirst)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                tbOpenVST.Content = bp.VSTData == null ? "Add VST instr." : bp.VSTData.VSTName;

                tbMute.Background = bp.MidiOptions.Active ? Brushes.DarkGray : Brushes.IndianRed;

                //remplissage des champs
                if (!tbPresetName.IsFocused)
                {
                    tbPresetName.Text = bp.PresetName;
                }

                if (!tbRoutingName.IsFocused && bIsFirst) { tbRoutingName.Text = bp.BoxName; }

                if (bp.MidiOptions.PlayNote != null)
                {
                    if (!tbInternalGeneratorKey.IsFocused) { tbInternalGeneratorKey.Text = bp.MidiOptions.PlayNote.Note.ToString(); }
                    if (!tbInternalGeneratorVelocity.IsFocused) { tbInternalGeneratorVelocity.Text = bp.MidiOptions.PlayNote.Velocity.ToString(); }
                    if (!tbInternalGeneratorLength.IsFocused) { tbInternalGeneratorLength.Text = bp.MidiOptions.PlayNote.Length.ToString(); }
                }
                ckInternalGeneratorLowestKey.IsChecked = bp.MidiOptions.PlayNote_LowestNote;

                //if (bIsFirst)
                //{
                cbMidiIn.SelectedValue = bp.DeviceIn;
                cbMidiOut.SelectedValue = bp.DeviceOut.Split("-{")[0];
                cbChannelMidiIn.SelectedValue = bp.ChannelIn;
                cbChannelMidiOut.SelectedValue = bp.ChannelOut;

                if (bp.VSTData != null)
                {
                    cbVSTSlot.SelectedValue = bp.VSTData.Slot;
                }

                lbPreset.Text = bp.MidiPreset.PresetName;
                lbPreset.Tag = bp.MidiPreset.Tag;

                if (!tbFilterHighNote.IsFocused) { tbFilterHighNote.Text = string.Concat(bp.MidiOptions.NoteFilterHigh.ToString(), " [", Tools.MidiNoteNumberToNoteName(bp.MidiOptions.NoteFilterHigh), "]"); }
                if (!tbFilterLowNote.IsFocused) { tbFilterLowNote.Text = string.Concat(bp.MidiOptions.NoteFilterLow.ToString(), " [", Tools.MidiNoteNumberToNoteName(bp.MidiOptions.NoteFilterLow), "]"); }
                if (!tbFilterHighVelo.IsFocused) { tbFilterHighVelo.Text = bp.MidiOptions.VelocityFilterHigh.ToString(); }
                if (!tbFilterLowVelo.IsFocused) { tbFilterLowVelo.Text = bp.MidiOptions.VelocityFilterLow.ToString(); }

                ckCompressVelocityRange.IsChecked = bp.MidiOptions.CompressVelocityRange;
                ckTransposeNoteRange.IsChecked = bp.MidiOptions.TransposeNoteRange;

                ckAllowAftertouch.IsChecked = bp.MidiOptions.AllowAftertouch;
                ckAllowAllCC.IsChecked = bp.MidiOptions.AllowAllCC;
                ckAllowUndefinedCC.IsChecked = bp.MidiOptions.AllowUndefinedCC;
                ckAllowModulation.IsChecked = bp.MidiOptions.AllowModulation;
                ckAllowNotes.IsChecked = bp.MidiOptions.AllowNotes;
                ckAllowNrpn.IsChecked = bp.MidiOptions.AllowNrpn;
                ckAllowPitchBend.IsChecked = bp.MidiOptions.AllowPitchBend;
                ckAllowProgramChange.IsChecked = bp.MidiOptions.AllowProgramChange;
                ckAllowSysex.IsChecked = bp.MidiOptions.AllowSysex;

                cbPlayMode.SelectedValue = bp.MidiOptions.PlayMode;
                if (cbPlayModeOption.Items.Count > 0) { cbPlayModeOption.SelectedValue = bp.MidiOptions.PlayModeOption; }

                if (!cbNoteTransposition.IsFocused) { cbNoteTransposition.SelectedValue = bp.MidiOptions.TranspositionOffset.ToString(); }

                if (!cbSmoothCC.IsFocused && bp.MidiOptions.SmoothCCLength % 100 == 0) { cbSmoothCC.SelectedValue = bp.MidiOptions.SmoothCCLength.ToString(); }
                if (!cbDelayNotes.IsFocused) { cbDelayNotes.SelectedValue = bp.MidiOptions.DelayNotesLength.ToString(); }
                if (!cbSmoothPresetChange.IsFocused) { cbSmoothPresetChange.SelectedValue = bp.MidiOptions.PresetMorphing.ToString(); }
                if (!cbAddLife.IsFocused) { cbAddLife.SelectedValue = bp.MidiOptions.AddLife.ToString(); }

                int iCCConvertIndex = cbCCConvert.SelectedIndex;
                cbCCConvert.Items.Clear();
                foreach (var item in bp.MidiOptions.CC_Converters)
                {
                    string sTag = string.Concat(item[0], "-", item[1]);
                    string sText = string.Concat("FROM ", item[0], " TO ", item[1]);
                    cbCCConvert.Items.Add(new ComboBoxItem { Tag = sTag, Content = sText });
                }
                cbCCConvert.SelectedIndex = iCCConvertIndex;

                int iNOTEConvertIndex = cbNOTEConvert.SelectedIndex;
                cbNOTEConvert.Items.Clear();
                foreach (var item in bp.MidiOptions.Note_Converters)
                {
                    string sTag = string.Concat(item[0], "-", item[1]);
                    string sText = string.Concat("FROM ", item[0], " TO ", item[1]);
                    cbNOTEConvert.Items.Add(new ComboBoxItem { Tag = sTag, Content = sText });
                }
                cbNOTEConvert.SelectedIndex = iNOTEConvertIndex;

                int iTranslatorIndex = cbTranslators.SelectedIndex;
                cbTranslators.Items.Clear();
                foreach (MessageTranslator item in bp.MidiOptions.Translators)
                {
                    cbTranslators.Items.Add(new ComboBoxItem { Tag = item.Tag(), Content = item });
                }
                cbTranslators.SelectedIndex = iTranslatorIndex;
            });

            int itranscount = await cbTranslators.Dispatcher.InvokeAsync(() => cbTranslators.Items.Count);
            await EnableDisableFields(itranscount);
        }

        private async Task EnableDisableFields(int translatorcount)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (translatorcount == 0)
                {
                    //tbSmoothCC.Text = "0";
                    cbSmoothCC.IsEnabled = true;
                    //tbSmoothPresetChange.Text = "0";
                    cbSmoothPresetChange.IsEnabled = true;
                    cbAddLife.IsEnabled = true;
                    //cbCCConvert.Items.Clear();
                    //cbNOTEConvert.Items.Clear();
                    btnAddCCConvert.IsEnabled = true;
                    btnAddNOTEConvert.IsEnabled = true;
                    btnRemoveCCConvert.IsEnabled = true;
                    btnRemoveNOTEConvert.IsEnabled = true;
                    cbFilters.IsEnabled = true;
                    //tbFilterLowNote.Text = "0";
                    tbFilterLowNote.IsEnabled = true;
                    //tbFilterHighNote.Text = "127";
                    tbFilterHighNote.IsEnabled = true;
                    //tbFilterLowVelo.Text = "0";
                    tbFilterLowVelo.IsEnabled = true;
                    //tbFilterHighVelo.Text = "127";
                    tbFilterHighVelo.IsEnabled = true;
                    //cbPlayMode.SelectedIndex = 0;
                    cbPlayMode.IsEnabled = true;
                    cbPlayModeOption.IsEnabled = true;
                    //tbNoteTransposition.Text = "0";
                    cbNoteTransposition.IsEnabled = true;
                    //tbDelayNotes.Text = "0";
                    cbDelayNotes.IsEnabled = true;
                    ckTransposeNoteRange.IsEnabled = true;
                    ckCompressVelocityRange.IsEnabled = true;
                }
                else
                {
                    //tbSmoothCC.Text = "0";
                    cbSmoothCC.IsEnabled = false;
                    //tbSmoothPresetChange.Text = "0";
                    cbSmoothPresetChange.IsEnabled = false;
                    cbAddLife.IsEnabled = false;
                    //cbCCConvert.Items.Clear();
                    //cbNOTEConvert.Items.Clear();
                    btnAddCCConvert.IsEnabled = false;
                    btnAddNOTEConvert.IsEnabled = false;
                    btnRemoveCCConvert.IsEnabled = false;
                    btnRemoveNOTEConvert.IsEnabled = false;
                    cbFilters.IsEnabled = false;
                    //tbFilterLowNote.Text = "0";
                    tbFilterLowNote.IsEnabled = false;
                    //tbFilterHighNote.Text = "127";
                    tbFilterHighNote.IsEnabled = false;
                    //tbFilterLowVelo.Text = "0";
                    tbFilterLowVelo.IsEnabled = false;
                    //tbFilterHighVelo.Text = "127";
                    tbFilterHighVelo.IsEnabled = false;
                    cbPlayMode.SelectedIndex = 0;
                    cbPlayMode.IsEnabled = false;
                    cbPlayModeOption.IsEnabled = false;
                    //tbNoteTransposition.Text = "0";
                    cbNoteTransposition.IsEnabled = false;
                    //tbDelayNotes.Text = "0";
                    cbDelayNotes.IsEnabled = false;
                    ckTransposeNoteRange.IsEnabled = false;
                    ckCompressVelocityRange.IsEnabled = false;
                }
            });
        }

        public async Task<MidiPreset> GetPreset()
        {
            MidiPreset mp = null;

            await Dispatcher.InvokeAsync(() =>
                {
                    if (cbChannelMidiOut.SelectedItem != null)
                    {
                        try
                        {
                            int iPrg = Convert.ToInt32(lbPreset.Tag.ToString().Split('-')[0]);
                            int iMsb = Convert.ToInt32(lbPreset.Tag.ToString().Split('-')[1]);
                            int iLsb = Convert.ToInt32(lbPreset.Tag.ToString().Split('-')[2]);
                            mp = new MidiPreset("", Convert.ToInt32(((ComboBoxItem)cbChannelMidiOut.SelectedItem).Tag.ToString()), iPrg, iMsb, iLsb, lbPresetSysEx.Text, lbPreset.Text);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Invalid Program (" + ex.Message + ")");
                            mp = new MidiPreset("", 1, 0, 0, 0, "", "Unknown Preset");
                        }
                    }
                    else
                    {
                        mp = new MidiPreset("", 1, 0, 0, 0, "", "Unknown Preset");
                    }
                });

            return mp;
        }

        public async Task<MidiOptions> GetOptions()
        {
            var options = new MidiOptions();
            //TempMemory[CurrentPreset].MidiOptions;

            await Dispatcher.InvokeAsync(() =>
            {
                options.Active = tbMute.Background != Brushes.IndianRed;

                for (int iCC = 0; iCC < 128; iCC++)
                {
                    options.SetDefaultCCValue(new int[] { iCC, TempCCMix[CurrentPreset, iCC] });
                    options.SetDefaultLowHighCCValue(true, new int[] { iCC, TempCCHigh[CurrentPreset, iCC] });
                    options.SetDefaultLowHighCCValue(false, new int[] { iCC, TempCCLow[CurrentPreset, iCC] });
                }
                options.SetDefaultCC(TempCCDefault);

                //options.Active = .Active;

                //options.Active = tbMute.Background == Brushes.IndianRed ? false : true;

                int iNoteGen = -1;
                int iVeloGen = -1;
                int iChannel = -1;
                decimal dLength = 0;
                NumberStyles style = NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands;
                CultureInfo culture = CultureInfo.InvariantCulture; // ou utilisez la culture appropriée selon vos besoins

                var midiin = cbMidiIn.SelectedValue;

                //options.Active = tbMute.Background == Brushes.IndianRed ? false : true;

                if (midiin != null && midiin.ToString().Equals(Tools.INTERNAL_GENERATOR))
                {
                    int.TryParse(tbInternalGeneratorKey.Text, out iNoteGen);
                    int.TryParse(tbInternalGeneratorVelocity.Text, out iVeloGen);
                    int.TryParse(cbChannelMidiOut.SelectedValue.ToString(), out iChannel);
                    decimal.TryParse(tbInternalGeneratorLength.Text, style, culture, out dLength);
                    options.PlayNote = new NoteGenerator(iChannel, 0, iNoteGen, iVeloGen, dLength);
                    options.PlayNote_LowestNote = ckInternalGeneratorLowestKey.IsChecked.Value;
                }
                else
                {
                    options.PlayNote = null;
                    options.PlayNote_LowestNote = false;
                }

                if (options.PlayNote != null)
                {
                    options.NoteFilterHigh = 127;
                    if (!tbFilterHighNote.IsFocused)
                    {
                        tbFilterHighNote.Text = string.Concat(options.NoteFilterHigh.ToString(), " [", Tools.MidiNoteNumberToNoteName(options.NoteFilterHigh), "]");
                    }

                    options.NoteFilterLow = 0;
                    if (!tbFilterLowNote.IsFocused)
                    {
                        tbFilterLowNote.Text = string.Concat(options.NoteFilterLow.ToString(), " [", Tools.MidiNoteNumberToNoteName(options.NoteFilterLow), "]");
                    }

                    options.VelocityFilterHigh = 127;
                    tbFilterHighVelo.Text = options.VelocityFilterHigh.ToString();

                    options.VelocityFilterLow = 0;
                    tbFilterLowVelo.Text = options.VelocityFilterLow.ToString();
                }
                else
                {
                    options.NoteFilterHigh = TextParser(tbFilterHighNote.Text.Split('[')[0].Trim());
                    if (!tbFilterHighNote.IsFocused)
                    {
                        tbFilterHighNote.Text = string.Concat(options.NoteFilterHigh.ToString(), " [", Tools.MidiNoteNumberToNoteName(options.NoteFilterHigh), "]");
                    }

                    options.NoteFilterLow = TextParser(tbFilterLowNote.Text.Split('[')[0].Trim());
                    if (!tbFilterLowNote.IsFocused)
                    {
                        tbFilterLowNote.Text = string.Concat(options.NoteFilterLow.ToString(), " [", Tools.MidiNoteNumberToNoteName(options.NoteFilterLow), "]");
                    }

                    options.VelocityFilterHigh = TextParser(tbFilterHighVelo.Text);
                    tbFilterHighVelo.Text = options.VelocityFilterHigh.ToString();

                    options.VelocityFilterLow = TextParser(tbFilterLowVelo.Text);
                    tbFilterLowVelo.Text = options.VelocityFilterLow.ToString();
                }

                options.CompressVelocityRange = ckCompressVelocityRange.IsChecked.Value;
                options.TransposeNoteRange = ckTransposeNoteRange.IsChecked.Value;

                options.AllowAftertouch = ckAllowAftertouch.IsChecked.Value;
                options.AllowUndefinedCC = ckAllowUndefinedCC.IsChecked.Value;
                options.AllowAllCC = ckAllowAllCC.IsChecked.Value;
                options.AllowModulation = ckAllowModulation.IsChecked.Value;
                options.AllowNotes = ckAllowNotes.IsChecked.Value;
                options.AllowNrpn = ckAllowNrpn.IsChecked.Value;
                options.AllowPitchBend = ckAllowPitchBend.IsChecked.Value;
                options.AllowProgramChange = ckAllowProgramChange.IsChecked.Value;
                options.AllowSysex = ckAllowSysex.IsChecked.Value;

                Enum.TryParse<PlayModes>(cbPlayMode.SelectedValue.ToString(), out options.PlayMode);
                options.PlayModeOption = cbPlayModeOption.Items.Count > 0 && cbPlayModeOption.SelectedIndex > -1 ? Convert.ToInt32(cbPlayModeOption.SelectedValue.ToString()) : 0;

                //pour éviter que le volume soit à 0 après un click sur aftertouch
                if (options.PlayMode == PlayModes.AFTERTOUCH && options.CC_Volume_Value > 0)
                { options.CC_Volume_Value = -1; }
                else if (options.PlayMode != PlayModes.AFTERTOUCH && options.CC_Volume_Value == -1)
                { options.CC_Volume_Value = 100; }

                if (cbNoteTransposition.SelectedValue != null)
                {
                    options.TranspositionOffset = Convert.ToInt32(cbNoteTransposition.SelectedValue.ToString());
                }

                options.SmoothCCLength = Convert.ToInt32(cbSmoothCC.SelectedValue.ToString());
                options.DelayNotesLength = Convert.ToInt32(cbDelayNotes.SelectedValue.ToString());
                options.PresetMorphing = Convert.ToInt32(cbSmoothPresetChange.SelectedValue.ToString());
                options.AddLife = Convert.ToInt32(cbAddLife.SelectedValue.ToString());

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

                foreach (ComboBoxItem item in cbTranslators.Items)
                {
                    var translator = (MessageTranslator)item.Content;
                    options.AddTranslator(translator);
                }
            });

            return options;
        }

        public List<BoxPreset> GetRoutingBoxMemory()
        {
            List<BoxPreset> boxpresets = new();
            foreach (var mem in TempMemory)
            {
                mem.RoutingGuid = RoutingGuid;
                boxpresets.Add(mem);
            }
            return boxpresets;
        }

        public BoxPreset GetCurrentPreset()
        {
            return TempMemory[CurrentPreset];
        }

        private static int TextParser(string sText)
        {
            if (int.TryParse(sText.Trim(), out int i))
            {
                return i;
            }
            else { return 999; }
        }

        private void Page_KeyUp(object sender, KeyEventArgs e)
        {

            if (e.Key == System.Windows.Input.Key.Enter)
            {
                Dispatcher.Invoke(() =>
                {
                    tabSwitch.Focus();
                });
            }
        }

        public void SetMute(bool bMute, bool bActiveState)
        {
            if (bMute)
            {
                tbMute.Background = Brushes.IndianRed;
                tbSolo.Background = Brushes.DarkGray;
            }
            else
            {
                if (bActiveState)
                {
                    tbMute.Background = Brushes.DarkGray;
                }
                else
                {
                    tbMute.Background = Brushes.IndianRed;
                }
                tbSolo.Background = Brushes.DarkGray;
            }
        }

        internal async Task ChangePreset(int iPreset)
        {
            if (await cbPresetButton.Dispatcher.InvokeAsync(() => cbPresetButton.SelectedIndex == iPreset)) //il ne déclenchera pas l'évènement...
            {
                await PresetButtonPushed();
            }
            else
            {
                await cbPresetButton.Dispatcher.InvokeAsync(() =>
                {
                    cbPresetButton.SelectedIndex = iPreset;
                });
            }
        }

        internal async Task<List<string>> GetAllDevices()
        {
            List<string> devices = new();

            await UIEventPool.AddTask(() =>
            {
                for (int i = 0; i < TempMemory.Length; i++)
                {
                    devices.Add("I-" + TempMemory[i].DeviceIn);
                    devices.Add("O-" + TempMemory[i].DeviceOut);
                }
            });

            if (devices.Count > 0)
            {
                return devices.ToList();
            }
            else
            { return new List<string>(); }
        }

        internal async Task InitDefaultCCMixer(int[] sCC)
        {
            await UIEventPool.AddTask(() =>
            {
                for (int i = 0; i < 8; i++)
                {
                    for (int i2 = 0; i2 < 8; i2++)
                    {
                        TempCCDefault[i, i2] = sCC[i2];
                    }
                }
            });
        }

        internal async Task InitDefaultCCLimiter(bool bHigh, int[] sValue)
        {
            await UIEventPool.AddTask(() =>
            {
                if (bHigh)
                {
                    TempCCHigh[CurrentPreset, sValue[0]] = sValue[1];
                }
                else
                {
                    TempCCLow[CurrentPreset, sValue[0]] = sValue[1];
                }
            });
        }

        internal async void CloseVSTWindow()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (VSTWindow != null)
                {
                    VSTWindow.OnVSTHostEvent -= VSTWindow_OnVSTHostEvent;
                    VSTWindow.Close();
                    VSTWindow = null;
                }
            });
            //await OpenVSTHost(false);
        }

        internal async Task CloseVSTHost(bool bDontRemovePlugin)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                VSTWindow?.Close();
                if (!bDontRemovePlugin && cbMidiOut.SelectedValue != null && cbVSTSlot.SelectedIndex > 0)
                {
                    OnUIEvent?.Invoke(BoxGuid, "REMOVE_VST", string.Concat(cbMidiOut.SelectedValue.ToString(), "-{", cbVSTSlot.SelectedValue.ToString(), "}"));
                }
            });
        }

        internal async Task OpenVSTHost()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (cbVSTSlot.SelectedIndex == 0)
                {
                    MessageBox.Show("You must choose a VST slot in the drop down list.");
                }
                else
                {
                    if (VSTWindow == null)
                    {
                        TempVST[CurrentPreset].SetSlot(Convert.ToInt32(cbVSTSlot.SelectedValue));
                        //TempVST.VSTHostInfo = preset.VSTData;
                        VSTWindow = new VSTHost.MainWindow(BoxName, CurrentPreset, TempVST[CurrentPreset]);
                        VSTWindow.OnVSTHostEvent += VSTWindow_OnVSTHostEvent;
                        VSTWindow.Show();
                    }
                }
            });
        }

        internal void SetRoutingGuid(Guid routingguid)
        {
            RoutingGuid = routingguid;
        }

        internal void SetBoxName(string boxname)
        {
            foreach (var p in TempMemory)
            {
                p.BoxName = boxname;
            }

            BoxName = boxname;
        }

        internal async Task ClearVST(int iPreset)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                TempVST[CurrentPreset] = new VSTPlugin(Convert.ToInt32(cbVSTSlot.SelectedValue));
                TempMemory[iPreset].VSTData = null;
            });
        }

        internal async Task SetVST(VSTPlugin vst, int iSlot, int iPreset)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (VSTWindow != null)
                {
                    VSTWindow.Close();
                    VSTWindow = null;
                }
                TempVST[iPreset] = vst ?? new VSTPlugin(iSlot);
                TempMemory[iPreset].VSTData = vst?.VSTHostInfo;
            });
        }

        internal async Task CheckAndRemoveVST(string sValue)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (VSTWindow != null)
                { VSTWindow.Close(); VSTWindow = null; }

                for (int i = 0; i < TempMemory.Length; i++)
                {
                    if (TempMemory[i].DeviceOut.Equals(sValue))
                    {
                        TempMemory[i].DeviceOut = "";
                        //TempMemory[i].ChannelOut = 1;
                        TempMemory[i].VSTData = null;
                        TempVST[i] = new VSTPlugin();
                    }
                }
            });
        }

        internal VSTPlugin GetVST()
        {
            return TempVST[CurrentPreset];
        }

        internal void SetInitCC(int[] iCC)
        {
            TempCCMix[CurrentPreset, iCC[0]] = iCC[1];
            OnUIEvent?.Invoke(BoxGuid, "SEND_CC_DATA", iCC);
            //var options = TempMemory[CurrentPreset].MidiOptions;
            //options.SetDefaultCCValue(iCC);
        }

        internal int GetCCValue(int cc)
        {
            return TempCCMix[CurrentPreset, cc];
            //return TempMemory[CurrentPreset].MidiOptions.DefaultRoutingCC[cc];
        }

        internal async Task BlinkPreset(bool bBlink, int iPreset)
        {
            if (bBlink)
            {
                if (UIBlink != null) { UIBlink.Enabled = false; UIBlink = null; }
                UIBlink = new System.Timers.Timer
                {
                    Interval = 100
                };
                UIBlink.Elapsed += UIBlink_Elapsed;
                UIBlink.Enabled = true;
                UIBlink.Start();
            }
            else
            {
                if (UIBlink != null)
                {
                    UIBlink.Elapsed -= UIBlink_Elapsed;
                    UIBlink.Enabled = false;
                    UIBlink.Stop();
                    UIBlink = null;

                    Button btn = null;
                    switch (iPreset)
                    {
                        case 0:
                            btn = btnPreset1;
                            break;
                        case 1:
                            btn = btnPreset2;
                            break;
                        case 2:
                            btn = btnPreset3;
                            break;
                        case 3:
                            btn = btnPreset4;
                            break;
                        case 4:
                            btn = btnPreset5;
                            break;
                        case 5:
                            btn = btnPreset6;
                            break;
                        case 6:
                            btn = btnPreset7;
                            break;
                        case 7:
                            btn = btnPreset8;
                            break;
                    }

                    await btnPreset1.Dispatcher.InvokeAsync(() => btnPreset1.Background = Brushes.MediumPurple);
                    await btnPreset2.Dispatcher.InvokeAsync(() => btnPreset2.Background = Brushes.MediumPurple);
                    await btnPreset3.Dispatcher.InvokeAsync(() => btnPreset3.Background = Brushes.MediumPurple);
                    await btnPreset4.Dispatcher.InvokeAsync(() => btnPreset4.Background = Brushes.MediumPurple);
                    await btnPreset5.Dispatcher.InvokeAsync(() => btnPreset5.Background = Brushes.MediumPurple);
                    await btnPreset6.Dispatcher.InvokeAsync(() => btnPreset6.Background = Brushes.MediumPurple);
                    await btnPreset7.Dispatcher.InvokeAsync(() => btnPreset7.Background = Brushes.MediumPurple);
                    await btnPreset8.Dispatcher.InvokeAsync(() => btnPreset8.Background = Brushes.MediumPurple);

                    await btn.Dispatcher.InvokeAsync(() => btn.Background = Brushes.IndianRed);
                }
            }
        }

        private async void UIBlink_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                Button btn = null;
                switch (CurrentPreset)
                {
                    case 0:
                        btn = btnPreset1;
                        break;
                    case 1:
                        btn = btnPreset2;
                        break;
                    case 2:
                        btn = btnPreset3;
                        break;
                    case 3:
                        btn = btnPreset4;
                        break;
                    case 4:
                        btn = btnPreset5;
                        break;
                    case 5:
                        btn = btnPreset6;
                        break;
                    case 6:
                        btn = btnPreset7;
                        break;
                    case 7:
                        btn = btnPreset8;
                        break;
                }
                await btn.Dispatcher.InvokeAsync(() =>
                {
                    if (btn.Background == Brushes.MediumPurple)
                    {
                        btn.Background = Brushes.IndianRed;
                    }
                    else
                    {
                        btn.Background = Brushes.MediumPurple;
                    }
                });
            }
            catch (OperationCanceledException)
            {

            }
        }

    }

    [MessagePackObject]
    [Serializable]
    public class BoxPreset
    {
        [Key("Index")]
        public int Index { get; set; }

        [Key("VSTData")]
        public VSTHostInfo VSTData { get; set; }

        [Key("RoutingGuid")]
        public Guid RoutingGuid { get; set; } = Guid.Empty;

        [Key("BoxGuid")]
        public Guid BoxGuid { get; set; } = Guid.Empty;

        [Key("BoxName")]
        public string BoxName { get; set; } = "Routing Name";

        [Key("PresetName")]
        public string PresetName { get; set; } = "";

        [Key("MidiOptions")]
        public MidiOptions MidiOptions { get; set; } = new MidiOptions();

        [Key("MidiPreset")]
        public MidiPreset MidiPreset { get; set; } = new MidiPreset("", 1, 0, 0, 0, "", "");

        [Key("DeviceIn")]
        public string DeviceIn { get; set; } = "";

        [Key("DeviceOut")]
        public string DeviceOut { get; set; } = "";

        [Key("ChannelIn")]
        public int ChannelIn { get; set; } = 1;

        [Key("ChannelOut")]
        public int ChannelOut { get; set; } = 0;

        public BoxPreset()
        {

        }

        public BoxPreset(Guid routingGuid, Guid boxGuid, int iPreset, string sName, string sBoxName)
        {
            Index = iPreset;
            RoutingGuid = routingGuid;
            BoxGuid = boxGuid;
            PresetName = sName;
            BoxName = sBoxName;
        }

        internal BoxPreset(Guid routingGuid, Guid boxGuid, int iPreset, string boxName, string presetName, MidiOptions midiOptions, MidiPreset midiPreset, string deviceIn, string deviceOut, VSTHostInfo vst, int channelIn, int channelOut)
        {
            Index = iPreset;
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
            VSTData = vst;
        }
    }
}
