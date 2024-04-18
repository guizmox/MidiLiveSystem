using MidiTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
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

namespace MidiLiveSystem
{
    /// <summary>
    /// Logique d'interaction pour CCMixer.xaml
    /// </summary>
    public partial class CCMixer : Window
    {
        public delegate void RoutingBoxEventHandler(Guid gBox, string sControl, object sValue);
        public event RoutingBoxEventHandler OnUIEvent;

        private readonly List<ComboBox> ComboBoxesCC = new();
        private readonly List<Slider> Sliders = new();
        private readonly List<ComboBox> ComboBoxesUpperLimit = new();
        private readonly List<ComboBox> ComboBoxesLowerLimit = new();

        private readonly Guid RoutingGuid;
        private readonly Guid BoxGuid;
        internal int[] BoxMixers = new int[8] { 1, 7, 10, 11, 70, 71, 91, 93 };
        internal int[] BoxMixersValues = new int[8] { 0, 0, 0, 0, 0, 0, 0, 0 };

        public CCMixer(Guid boxGuid, Guid routingGuid, string boxname, string presetname, int[] boxmix)
        {
            InitializeComponent();

            BoxGuid = boxGuid;
            RoutingGuid = routingGuid;
            if (boxmix != null)
            {
                BoxMixers = boxmix;
            }

            Task.Run(() => InitPage(boxname, presetname));

            MidiRouting.OutputCCValues += MidiRouting_OutputCCValues;

        }

        private void Window_Closed(object sender, EventArgs e)
        {
            OnUIEvent?.Invoke(BoxGuid, "CC_SAVE_MIX_DEFAULT", BoxMixers);
            MainWindow.CCMixData -= MainWindow_CCMixData;
            MidiRouting.OutputCCValues -= MidiRouting_OutputCCValues;
        }

        public async Task InitPage(string boxname, string presetname)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                gdMix.Children.Clear();
                Sliders.Clear();
                ComboBoxesCC.Clear();
                BoxMixersValues = new int[8] { 0, 0, 0, 0, 0, 0, 0, 0 };

                Title = string.Concat("Control Change Mixer", " [", boxname, " - Preset : ", presetname, "]");
                MainWindow.CCMixData += MainWindow_CCMixData;

                var CC = CCList();

                for (int i = 0; i < BoxMixers.Length; i++)
                {
                    ComboBox cbLower = new()
                    {
                        Name = "cbLowerCC" + (i + 1),
                        SelectedValuePath = "Tag",
                        Tag = i
                    };
                    cbLower.SelectionChanged += CbLower_SelectionChanged;
                    ComboBoxesLowerLimit.Add(cbLower);
                    FillComboBoxUpperLower(false, ComboBoxesLowerLimit.Last());

                    StackPanel spLower = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        VerticalAlignment = VerticalAlignment.Center

                    };
                    Grid.SetColumn(spLower, i);
                    Grid.SetRow(spLower, 3);


                    spLower.Children.Add(new TextBlock() { Text = "MIN LIMIT ", Foreground = Brushes.LightYellow });
                    spLower.Children.Add(cbLower);

                    gdMix.Children.Add(spLower);

                    ComboBox cbUpper = new()
                    {
                        Name = "cbUpperCC" + (i + 1),
                        SelectedValuePath = "Tag",
                        Tag = i
                    };
                    cbUpper.SelectionChanged += CbUpper_SelectionChanged;
                    ComboBoxesUpperLimit.Add(cbUpper);
                    FillComboBoxUpperLower(true, ComboBoxesUpperLimit.Last());

                    StackPanel spUpper = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(spUpper, i);
                    Grid.SetRow(spUpper, 4);


                    spUpper.Children.Add(new TextBlock() { Text = "MAX LIMIT ", Foreground = Brushes.LightYellow });
                    spUpper.Children.Add(cbUpper);

                    gdMix.Children.Add(spUpper);

                    ComboBox comboBox = new()
                    {
                        Name = "cbCC" + (i + 1),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        MinWidth = 100,
                        SelectedValuePath = "Tag",
                        Tag = (i + 1)
                    };
                    comboBox.SelectionChanged += ComboBox_SelectionChanged;
                    Grid.SetColumn(comboBox, i);
                    Grid.SetRow(comboBox, 0);
                    gdMix.Children.Add(comboBox);
                    ComboBoxesCC.Add(comboBox);
                    FillComboBox(CC, ComboBoxesCC.Last());

                    Slider slider = new()
                    {
                        Name = "slCC" + (i + 1),
                        Minimum = -1,
                        Maximum = 127,
                        SmallChange = 1,
                        Tag = (i + 1),
                        Value = -1,
                        Foreground = Brushes.IndianRed,
                        //slider.Foreground = Brushes.White;
                        Orientation = Orientation.Vertical,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    slider.ValueChanged += slCC_ValueChanged;
                    Grid.SetColumn(slider, i);
                    Grid.SetRow(slider, 1);
                    gdMix.Children.Add(slider);
                    Sliders.Add(slider);
                }
            });
        }

        private async void CbLower_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (((ComboBox)sender).IsFocused && e.AddedItems != null && e.AddedItems.Count > 0)
                {
                    int ilowLimit = Convert.ToInt32(((ComboBoxItem)e.AddedItems[0]).Tag.ToString());
                    int icc = BoxMixers[Convert.ToInt32(((ComboBox)sender).Tag)];
                    OnUIEvent?.Invoke(BoxGuid, "CC_LOW_LIMIT", new int[] { icc, ilowLimit });
                }
            });
        }

        private async void CbUpper_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (((ComboBox)sender).IsFocused && e.AddedItems != null && e.AddedItems.Count > 0)
                {
                    int ihighLimit = Convert.ToInt32(((ComboBoxItem)e.AddedItems[0]).Tag.ToString());
                    int icc = BoxMixers[Convert.ToInt32(((ComboBox)sender).Tag)];
                    OnUIEvent?.Invoke(BoxGuid, "CC_HIGH_LIMIT", new int[] { icc, ihighLimit });
                }
            });
        }

        private async void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                int iIndex = Convert.ToInt32(((ComboBox)sender).Tag);
                BoxMixers[iIndex - 1] = Convert.ToInt32(((ComboBox)sender).SelectedValue);
                BoxMixersValues[iIndex - 1] = (int)Sliders[iIndex - 1].Value;
            });
        }

        internal async Task InitMixer()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                OnUIEvent?.Invoke(BoxGuid, "CC_MIX_DATA", BoxMixers);
            });
        }

        private async void MidiRouting_OutputCCValues(Guid routingGuid, List<int> sValues)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (RoutingGuid == routingGuid)
                {
                    int iCb = -1;
                    for (int i = 0; i < ComboBoxesCC.Count; i++)
                    {
                        if (ComboBoxesCC[i].SelectedValue.Equals(sValues[0]))
                        { iCb = i; break; }
                    }
                    if (iCb > -1)
                    {
                        Sliders[iCb].Value = sValues[1];
                    }
                }
            });
        }

        private async void MainWindow_CCMixData(Guid routingguid, MidiOptions options)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (routingguid == RoutingGuid)
                {
                    for (int iCC = 0; iCC < options.DefaultRoutingCC.Length; iCC++)
                    {
                        for (int iBox = 0; iBox < 8; iBox++)
                        {
                            if (BoxMixers[iBox] == iCC)
                            {
                                Sliders[iBox].Value = options.DefaultRoutingCC[iCC];
                                ComboBoxesLowerLimit[iBox].SelectedValue = options.CC_LowLimiter[iCC];
                                ComboBoxesUpperLimit[iBox].SelectedValue = options.CC_HighLimiter[iCC];
                                break;
                            }
                        }
                    }
                }
            });
        }

        private async void slCC_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                Slider sl = (Slider)sender;

                if (sl.Value == -1) { sl.Foreground = Brushes.IndianRed; }
                else { sl.Foreground = Brushes.BlueViolet; }

                if (sl.IsFocused)
                {
                    int iIndex = Convert.ToInt32(sl.Tag) - 1;
                    int iValue = (int)sl.Value;
                    int ilowlimit = Convert.ToInt32(ComboBoxesLowerLimit[iIndex].SelectedValue);
                    int ihighlimit = Convert.ToInt32(ComboBoxesUpperLimit[iIndex].SelectedValue);

                    if (iValue < ilowlimit)
                    {
                        sl.Value = ilowlimit;
                    }
                    else if (iValue > ihighlimit)
                    {
                        sl.Value = ihighlimit;
                    }
                    else 
                    {
                        BoxMixersValues[iIndex] = iValue;

                        OnUIEvent?.Invoke(BoxGuid, "CC_SEND_MIX_DATA", new int[] { BoxMixers[iIndex], BoxMixersValues[iIndex] });
                    }
                }
            });
        }

        private void FillComboBox(List<string[]> ccList, ComboBox cb)
        {
            foreach (var cc in ccList)
            {
                cb.Items.Add(new ComboBoxItem { Content = string.Concat(cc[0], " - ", cc[1]), Tag = cc[0] });
            }
            int iIndex = Convert.ToInt32(cb.Tag);
            cb.SelectedValue = BoxMixers[iIndex - 1];
        }

        private void FillComboBoxUpperLower(bool bIsUpper, ComboBox cb)
        {
            if (bIsUpper)
            {
                for (int i = 64; i < 128; i++)
                {
                    cb.Items.Add(new ComboBoxItem { Content = i.ToString(), Tag = i.ToString() });
                }
            }
            else
            {
                for (int i = 0; i < 64; i++)
                {
                    cb.Items.Add(new ComboBoxItem { Content = i.ToString(), Tag = i.ToString() });
                }
            }
        }

        private static List<string[]> CCList()
        {
            List<string[]> midiControllers = new()
            {
            new string[] {"1", "Modulation Wheel", "Generally this CC controls a vibrato effect (pitch, loudness, brightness). What is modulated is based on the patch."},
            new string[] {"2", "Breath Controller", "Oftentimes associated with aftertouch messages. It was originally intended for use with a breath MIDI controller in which blowing harder produced higher MIDI control values. It can be used for modulation as well."},
            new string[] {"4", "Foot Pedal", "Often used with aftertouch messages. It can send a continuous stream of values based on how the pedal is used."},
            new string[] {"5", "Portamento Time", "Controls portamento rate to slide between 2 notes played subsequently."},
            new string[] {"7", "Volume", "Controls the volume of the channel."},
            new string[] {"8", "Balance", "Controls the left and right balance, generally for stereo patches. A value of 64 equals the center."},
            new string[] {"10", "Pan", "Controls the left and right balance, generally for mono patches. A value of 64 equals the center."},
            new string[] {"11", "Expression", "Expression is a percentage of volume (CC7)."},
            new string[] {"12", "Effect Controller 1", "Usually used to control a parameter of an effect within the synth or workstation."},
            new string[] {"13", "Effect Controller 2", "Usually used to control a parameter of an effect within the synth or workstation."},
            new string[] {"64", "Damper Pedal on/off", "On/off switch that controls sustain pedal. Nearly every synth will react to CC 64. (See also Sostenuto CC 66)"},
            new string[] {"65", "Portamento on/off", "On/off switch"},
            new string[] {"66", "Sostenuto Pedal on/off", "On/off switch – Like the Sustain controller (CC 64), However, it only holds notes that were “On” when the pedal was pressed. People use it to “hold” chords” and play melodies over the held chord."},
            new string[] {"67", "Soft Pedal on/off", "On/off switch – Lowers the volume of notes played."},
            new string[] {"68", "Legato FootSwitch", "On/off switch – Turns Legato effect between 2 subsequent notes on or off."},
            new string[] {"69", "Hold 2", "Another way to “hold notes” (see MIDI CC 64 and MIDI CC 66). However notes fade out according to their release parameter rather than when the pedal is released."},
            new string[] {"70", "Sound Controller 1", "Usually controls the way a sound is produced. Default = Sound Variation."},
            new string[] {"71", "Sound Controller 2", "Allows shaping the Voltage Controlled Filter (VCF). Default = Resonance also (Timbre or Harmonics)"},
            new string[] {"72", "Sound Controller 3", "Controls release time of the Voltage controlled Amplifier (VCA). Default = Release Time."},
            new string[] {"73", "Sound Controller 4", "Controls the “Attack’ of a sound. The attack is the amount of time it takes for the sound to reach maximum amplitude."},
            new string[] {"74", "Sound Controller 5", "Controls VCFs cutoff frequency of the filter."},
            new string[] {"80", "General Purpose MIDI CC Controller", "Decay Generic or on/off switch"},
            new string[] {"81", "General Purpose MIDI CC Controller", "Hi-Pass Filter Frequency or Generic on/off switch"},
            new string[] {"84", "Portamento CC Control", "Controls the amount of Portamento."},
            new string[] {"88", "High Resolution Velocity Prefix", "Extends the range of possible velocity values"},
            new string[] {"91", "Effect 1 Depth", "Usually controls reverb send amount"},
            new string[] {"92", "Effect 2 Depth", "Usually controls tremolo amount"},
            new string[] {"93", "Effect 3 Depth", "Usually controls chorus amount"},
            new string[] {"94", "Effect 4 Depth", "Usually controls detune amount"},
            new string[] {"95", "Effect 5 Depth", "Usually controls phaser amount"},
            new string[] {"120", "All Sound Off", "Mutes all sound. It does so regardless of release time or sustain. (See MIDI CC 123)"},
            new string[] {"121", "Reset All Controllers", "It will reset all controllers to their default."},
            new string[] {"122", "Local on/off Switch", "Turns internal connection of a MIDI keyboard or workstation, etc. on or off."},
            new string[] {"123", "All Notes Off", "Mutes all sounding notes. Release time will still be maintained, and notes held by sustain will not turn off until sustain pedal is depressed."},
            new string[] {"124", "Omni Mode Off", "Sets to “Omni Off” mode."},
            new string[] {"125", "Omni Mode On", "Sets to “Omni On” mode."},
            new string[] {"126", "Mono Mode", "Sets device mode to Monophonic."},
            new string[] {"127", "Poly Mode", "Sets device mode to Polyphonic."}
        };
            return midiControllers;
        }
    }
}
