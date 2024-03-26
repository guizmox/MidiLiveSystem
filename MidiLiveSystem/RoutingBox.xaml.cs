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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
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
        public VSTHost.MainWindow VSTWindow;

        public Guid RoutingGuid { get; private set; }
        public bool Detached { get; internal set; } = false;
        public bool HasVSTAttached { get { return TempMemory[CurrentPreset].VSTData != null ? true : false; } }

        public VSTPlugin GetVST { get { return TempVST; } }

        public int GridPosition = 0;
        public int CurrentPreset = 1;

        public Guid BoxGuid { get; private set; } = Guid.NewGuid();
        public string BoxName { get; private set; } = "Routing Box";
        private ProjectConfiguration Project;

        public delegate void RoutingBoxEventHandler(Guid gBox, string sControl, object sValue);
        public event RoutingBoxEventHandler OnUIEvent;

        BoxPreset[] TempMemory = new BoxPreset[8];
        VSTPlugin TempVST;

        PresetBrowser InstrumentPresets = null;

        public RoutingBox(ProjectConfiguration conf, List<IMidiInputDeviceInfo> inputDevices, List<IMidiOutputDeviceInfo> outputDevices, int gridPosition, Guid boxGuid, string sBoxName, Guid routingGuid, BoxPreset[] presets)
        {
            BoxGuid = boxGuid;
            RoutingGuid = routingGuid;
            TempVST = new VSTPlugin(boxGuid);           
            LoadMemory(presets);

            GridPosition = gridPosition;
            BoxName = "Routing Box " + (GridPosition + 1).ToString();

            TempMemory = new BoxPreset[8] { new BoxPreset(RoutingGuid, BoxGuid, "Preset 1", BoxName), new BoxPreset(RoutingGuid, BoxGuid, "Preset 2", BoxName), new BoxPreset(RoutingGuid, BoxGuid, "Preset 3", BoxName),
                                            new BoxPreset(RoutingGuid, BoxGuid, "Preset 4", BoxName), new BoxPreset(RoutingGuid, BoxGuid, "Preset 5", BoxName), new BoxPreset(RoutingGuid, BoxGuid, "Preset 6", BoxName),
                                            new BoxPreset(RoutingGuid, BoxGuid, "Preset 7", BoxName), new BoxPreset(RoutingGuid, BoxGuid, "Preset 8", BoxName) };
            Project = conf;

            InitializeComponent();

            InitPage(inputDevices, outputDevices);

            MidiRouting.OutputMidiMessage += MidiRouting_OutputMidiMessage;
        }

        public RoutingBox(ProjectConfiguration conf, List<IMidiInputDeviceInfo> inputDevices, List<IMidiOutputDeviceInfo> outputDevices, int gridPosition)
        {
            GridPosition = gridPosition;
            BoxName = "Routing Box " + (GridPosition + 1).ToString();
            TempVST = new VSTPlugin(BoxGuid);

            TempMemory = new BoxPreset[8] { new BoxPreset(RoutingGuid, BoxGuid, "Preset 1", BoxName), new BoxPreset(RoutingGuid, BoxGuid, "Preset 2", BoxName), new BoxPreset(RoutingGuid, BoxGuid, "Preset 3", BoxName),
                                            new BoxPreset(RoutingGuid, BoxGuid, "Preset 4", BoxName), new BoxPreset(RoutingGuid, BoxGuid, "Preset 5", BoxName), new BoxPreset(RoutingGuid, BoxGuid, "Preset 6", BoxName),
                                            new BoxPreset(RoutingGuid, BoxGuid, "Preset 7", BoxName), new BoxPreset(RoutingGuid, BoxGuid, "Preset 8", BoxName) };
            Project = conf;

            InitializeComponent();

            InitPage(inputDevices, outputDevices);

            MidiRouting.OutputMidiMessage += MidiRouting_OutputMidiMessage;
        }

        private async void VSTWindow_OnVSTHostEvent(int iPreset, int iAction)
        {
            if (iAction == 0) //fermer la fenêtre
            {
                TempMemory[CurrentPreset].VSTData = TempVST.VSTHostInfo; //pas certain
                VSTWindow.OnVSTHostEvent -= VSTWindow_OnVSTHostEvent;
                VSTWindow = null;
            }
            else if (iAction == 1) //chargement initial du VST
            {
                TempMemory[CurrentPreset].VSTData = TempVST.VSTHostInfo; //pas certain
                OnUIEvent?.Invoke(BoxGuid, "PLUG_VST_TO_DEVICE", TempVST); //pour initialiser l'audio
                await VSTWindow.LoadPlugin();             
            }
        }

        private void MidiRouting_OutputMidiMessage(bool b, Guid routingGuid)
        {
            if (RoutingGuid == routingGuid)
            {
                Dispatcher.Invoke(() =>
                {
                    if (b)
                    {
                        tbRoutingPlay.Foreground = Brushes.Red;
                    }
                    else
                    {
                        tbRoutingPlay.Foreground = Brushes.Yellow;
                    }
                });
            }
        }

        private void InitPage(List<IMidiInputDeviceInfo> inputDevices, List<IMidiOutputDeviceInfo> outputDevices)
        {
            foreach (var s in inputDevices)
            {
                cbMidiIn.Items.Add(new ComboBoxItem() { Tag = s.Name, Content = s.Name });
            }

            cbMidiIn.Items.Add(new ComboBoxItem() { Tag = Tools.INTERNAL_GENERATOR, Content = Tools.INTERNAL_GENERATOR });
            cbMidiIn.Items.Add(new ComboBoxItem() { Tag = Tools.INTERNAL_SEQUENCER, Content = Tools.INTERNAL_SEQUENCER });

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
                var preset = await MemCurrentPreset();
                int iPreset = Convert.ToInt32(itemOLD.Tag);
                TempMemory[Convert.ToInt32(iPreset)] = preset;
            }

            if (itemNEW != null)
            {
                int iPreset = Convert.ToInt32(itemNEW.Tag);
                CurrentPreset = iPreset;
                await PresetButtonPushed();
            }
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

                var preset = await MemCurrentPreset();

                OnUIEvent?.Invoke(BoxGuid, "PRESET_CHANGE", preset);
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
                    pnlVelocityRange.Visibility = Visibility.Hidden;
                    pnlInternalGenerator.Visibility = Visibility.Visible;
                }
                else if (item.Tag.Equals(Tools.VST_HOST))
                {
                    tbVelocityRangeLabel.Visibility = Visibility.Hidden;
                    tbNoteRangeLabel.Visibility = Visibility.Hidden;
                    pnlNoteRange.Visibility = Visibility.Hidden;
                    pnlVelocityRange.Visibility = Visibility.Hidden;
                    pnlInternalGenerator.Visibility = Visibility.Hidden;
                }
                else
                {
                    tbVelocityRangeLabel.Visibility = Visibility.Visible;
                    tbNoteRangeLabel.Visibility = Visibility.Visible;
                    pnlNoteRange.Visibility = Visibility.Visible;
                    pnlVelocityRange.Visibility = Visibility.Visible;
                    pnlInternalGenerator.Visibility = Visibility.Hidden;
                }
            }

        }

        private async void cbMidiOut_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = e.AddedItems.Count > 0 ? (ComboBoxItem)e.AddedItems[0] : null;

            if (item != null)
            {
                if (e.RemovedItems.Count > 0) //pour éviter de péter le VSTHost lorsqu'on charge un projet (removeditems est dans ce cas à vide)
                {
                    await CloseVSTHost();
                }

                if (item.Tag.Equals(Tools.VST_HOST))
                {
                    tbChoosePreset.Visibility = Visibility.Hidden;
                    tbOpenVST.Visibility = Visibility.Visible;
                    tbProgram.Text = "VST HOST";
                }
                else
                {
                    tbChoosePreset.Visibility = Visibility.Visible;
                    tbOpenVST.Visibility = Visibility.Hidden;
                    tbProgram.Text = "PROGRAM";
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
            MidiTranslator mt = new MidiTranslator();
            mt.ShowDialog();
            string[] sTranslator = mt.GetTranslatorConfiguration();
            if (sTranslator == null || mt.InvalidData)
            {
                MessageBox.Show("There's an issue with the input. Translator info can't be processed. Please try again.");
            }
            else
            {
                bool bExists = false;
                string sTag = sTranslator[0];
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
                    cbTranslators.Items.Add(new ComboBoxItem() { Tag = sTranslator[0], Content = sTranslator[1] });
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
            await OpenVSTHost(false);
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
                var copied = MidiLiveSystem.MainWindow.CopiedPreset;
                if (copied != null)
                {
                    await FillUI(copied, cbPresetButton.SelectedIndex == 0 ? true : false);
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
        }

        public async void LoadMemory(BoxPreset[] mem)
        {
            if (mem != null && mem.Length > 0)
            {
                TempMemory = mem;
                if (TempMemory != null && TempMemory.Length > 0)
                {
                    await FillUI(TempMemory[0], true);
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
                await FillUI(TempMemory[iNew], iNew > 0 ? false : true);
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
            var bp = await MemCurrentPreset();
            TempMemory[CurrentPreset] = bp;
            await LoadPreset(CurrentPreset);
            return bp;
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
                    iChannelIn = cbChannelMidiIn.SelectedItem == null ? 1 : Convert.ToInt32(((ComboBoxItem)cbChannelMidiIn.SelectedItem).Tag.ToString());
                    iChannelOut = cbChannelMidiOut.SelectedItem == null ? 1 : Convert.ToInt32(((ComboBoxItem)cbChannelMidiOut.SelectedItem).Tag.ToString());
                });

                var options = await GetOptions();
                var preset = await GetPreset();

                var bp = new BoxPreset(RoutingGuid, BoxGuid, routingname, presetname, options, preset, sDeviceIn, sDeviceOut, TempVST == null ? null : TempVST.VSTHostInfo, iChannelIn, iChannelOut);

                boxPreset = bp;

            }
            catch { throw; }

            return boxPreset;
        }

        private async Task FillUI(BoxPreset bp, bool bIsFirst)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                tbMute.Background = bp.MidiOptions.Active ? Brushes.DarkGray : Brushes.IndianRed;

                //remplissage des champs
                if (!tbPresetName.IsFocused) { tbPresetName.Text = bp.PresetName; }
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
                cbMidiOut.SelectedValue = bp.DeviceOut;
                cbChannelMidiIn.SelectedValue = bp.ChannelIn;
                cbChannelMidiOut.SelectedValue = bp.ChannelOut;
                //}

                lbPreset.Text = bp.MidiPreset.PresetName;
                lbPreset.Tag = bp.MidiPreset.Tag;

                if (!tbFilterHighNote.IsFocused) { tbFilterHighNote.Text = bp.MidiOptions.NoteFilterHigh.ToString(); }
                if (!tbFilterLowNote.IsFocused) { tbFilterLowNote.Text = bp.MidiOptions.NoteFilterLow.ToString(); }
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

                if (!tbNoteTransposition.IsFocused) { tbNoteTransposition.Text = bp.MidiOptions.TranspositionOffset.ToString(); }

                if (!tbSmoothCC.IsFocused) { tbSmoothCC.Text = bp.MidiOptions.SmoothCCLength.ToString(); }
                if (!tbDelayNotes.IsFocused) { tbDelayNotes.Text = bp.MidiOptions.DelayNotesLength.ToString(); }

                if (!cbCCDefaultValues.IsFocused)
                {
                    foreach (var item in cbCCDefaultValues.Items)
                    {
                        ComboBoxCustomItem cb = (ComboBoxCustomItem)item;

                        switch (cb.Id)
                        {
                            case "tbCC_Chorus":
                                cb.Value = bp.MidiOptions.CC_Chorus_Value.ToString();
                                break;
                            case "tbCC_Pan":
                                if (bp.MidiOptions.CC_Pan_Value == -1)
                                { bp.MidiOptions.CC_Pan_Value = 64; }
                                cb.Value = bp.MidiOptions.CC_Pan_Value.ToString();
                                break;
                            case "tbCC_Volume":
                                if (bp.MidiOptions.CC_Volume_Value == -1)
                                { bp.MidiOptions.CC_Volume_Value = 100; }
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
                            case "tbCC_Timbre":
                                cb.Value = bp.MidiOptions.CC_Timbre_Value.ToString();
                                break;
                            case "tbCC_CutOff":
                                cb.Value = bp.MidiOptions.CC_FilterCutOff_Value.ToString();
                                break;
                        }
                    }
                }

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
                foreach (var item in bp.MidiOptions.Translators)
                {
                    string sTag = string.Concat(item[0]);
                    string sText = string.Concat(item[1]);
                    cbTranslators.Items.Add(new ComboBoxItem { Tag = sTag, Content = sText });
                }
                cbTranslators.SelectedIndex = iTranslatorIndex;
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
                            mp = new MidiPreset("", Convert.ToInt32(((ComboBoxItem)cbChannelMidiOut.SelectedItem).Tag.ToString()), iPrg, iMsb, iLsb, lbPreset.Text);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Invalid Program (" + ex.Message + ")");
                            mp = new MidiPreset("", 1, 0, 0, 0, "Unknown Preset");
                        }
                    }
                    else
                    {
                        mp = new MidiPreset("", 1, 0, 0, 0, "Unknown Preset");
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
                options.Active = tbMute.Background == Brushes.IndianRed ? false : true;
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
                    tbFilterHighNote.Text = options.NoteFilterHigh.ToString();

                    options.NoteFilterLow = 0;
                    tbFilterLowNote.Text = options.NoteFilterLow.ToString();

                    options.VelocityFilterHigh = 127;
                    tbFilterHighVelo.Text = options.VelocityFilterHigh.ToString();

                    options.VelocityFilterLow = 0;
                    tbFilterLowVelo.Text = options.VelocityFilterLow.ToString();
                }
                else
                {
                    options.NoteFilterHigh = TextParser(tbFilterHighNote.Text);
                    tbFilterHighNote.Text = options.NoteFilterHigh.ToString();

                    options.NoteFilterLow = TextParser(tbFilterLowNote.Text);
                    tbFilterLowNote.Text = options.NoteFilterLow.ToString();

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
                        case "tbCC_Timbre":
                            options.CC_Timbre_Value = TextParser(cb.Value);
                            cb.Value = options.CC_Timbre_Value.ToString();
                            break;
                        case "tbCC_CutOff":
                            options.CC_FilterCutOff_Value = TextParser(cb.Value);
                            cb.Value = options.CC_FilterCutOff_Value.ToString();
                            break;
                    }
                }

                Enum.TryParse<PlayModes>(cbPlayMode.SelectedValue.ToString(), out options.PlayMode);

                //pour éviter que le volume soit à 0 après un click sur aftertouch
                if (options.PlayMode == PlayModes.AFTERTOUCH && options.CC_Volume_Value > 0)
                { options.CC_Volume_Value = -1; }
                else if (options.PlayMode != PlayModes.AFTERTOUCH && options.CC_Volume_Value == -1)
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

                foreach (var item in cbTranslators.Items)
                {
                    var translator = (ComboBoxItem)item;
                    options.AddTranslator(translator.Tag.ToString(), translator.Content.ToString());
                }

                int iSmooth = 0;
                if (int.TryParse(tbSmoothCC.Text.Trim(), out iSmooth))
                {
                    if (iSmooth >= 0 && iSmooth <= 5000)
                    {
                        options.SmoothCCLength = iSmooth;
                    }
                }

                int iDelay = 0;
                if (int.TryParse(tbDelayNotes.Text.Trim(), out iDelay))
                {
                    if (iDelay >= 0 && iDelay <= 1000)
                    {
                        options.DelayNotesLength = iDelay;
                    }
                }

            });

            return options;
        }

        public List<BoxPreset> GetRoutingBoxMemory()
        {
            List<BoxPreset> boxpresets = new List<BoxPreset>();
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

        private int TextParser(string sText)
        {
            int i = 0;
            if (int.TryParse(sText.Trim(), out i))
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
            List<string> devices = new List<string>();

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
                foreach (var mem in TempMemory)
                {
                    mem.MidiOptions.DefaultCCMix = sCC;
                }
            });
        }

        internal void CloseVSTWindow()
        {
            if (VSTWindow != null)
            {
                VSTWindow.OnVSTHostEvent -= VSTWindow_OnVSTHostEvent;
                VSTWindow.Close();
                VSTWindow = null;
            }
            else { return; }
        }

        internal async Task CloseVSTHost()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                OnUIEvent?.Invoke(BoxGuid, "REMOVE_VST_FROM_DEVICE", TempVST);
                if (VSTWindow != null)
                {
                    VSTWindow.OnVSTHostEvent -= VSTWindow_OnVSTHostEvent;
                    VSTWindow.Close();
                    VSTWindow = null;
                }
                //TempVST = new VSTPlugin();
                //TempMemory[CurrentPreset].VSTData = null;
            });
        }

        internal async Task OpenVSTHost(bool bLoadProject)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (VSTWindow == null)
                {
                    if (bLoadProject)
                    {
                        TempVST.VSTHostInfo = TempMemory[CurrentPreset].VSTData;
                    }
                    VSTWindow = new VSTHost.MainWindow(RoutingGuid, BoxName, CurrentPreset, TempVST);
                    VSTWindow.OnVSTHostEvent += VSTWindow_OnVSTHostEvent;
                    VSTWindow.Show();
                }
            });
        }

        internal void SetRoutingGuid(Guid routingguid)
        {
            RoutingGuid = routingguid;
        }

        internal void SetBoxName(string boxname)
        {
            BoxName = boxname;
        }
    }

    [Serializable]
    public class BoxPreset
    {
        public VSTHostInfo VSTData;
        public Guid RoutingGuid { get; set; } = Guid.Empty;
        public Guid BoxGuid { get; set; } = Guid.Empty;
        public string BoxName { get; set; } = "Routing Name";
        public string PresetName { get; set; } = "My Preset";
        public MidiOptions MidiOptions { get; set; } = new MidiOptions();
        public MidiPreset MidiPreset { get; set; } = new MidiPreset("", 1, 0, 0, 0, "");
        public string DeviceIn { get; set; } = "";
        public string DeviceOut { get; set; } = "";
        public int ChannelIn { get; set; } = 1;
        public int ChannelOut { get; set; } = 0;

        public BoxPreset()
        {

        }

        public BoxPreset(Guid routingGuid, Guid boxGuid, string sName, string sBoxName)
        {
            RoutingGuid = routingGuid;
            BoxGuid = boxGuid;
            PresetName = sName;
            BoxName = sBoxName;
        }

        internal BoxPreset(Guid routingGuid, Guid boxGuid, string boxName, string presetName, MidiOptions midiOptions, MidiPreset midiPreset, string deviceIn, string deviceOut, VSTHostInfo vst, int channelIn, int channelOut)
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
            VSTData = vst;
        }
    }
}
