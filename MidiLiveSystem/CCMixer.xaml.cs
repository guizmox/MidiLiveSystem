﻿using MidiTools;
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

        private List<ComboBox> ComboBoxes = new List<ComboBox>();
        private List<Slider> Sliders = new List<Slider>();

        private Guid RoutingGuid;
        private Guid BoxGuid;
        internal int[] BoxMixers = new int[8] { 1, 7, 10, 11, 70, 71, 91, 93 };
        internal int[] BoxMixersValues = new int[8] { 0, 0, 0, 0, 0, 0, 0, 0 };

        public CCMixer(Guid boxGuid, Guid routingGuid, string boxname, int[] boxmix)
        {
            InitializeComponent();

            BoxGuid = boxGuid;
            RoutingGuid = routingGuid;
            if (boxmix != null)
            {
                BoxMixers = boxmix;
            }

            MidiRouting.OutputCCValues += MidiRouting_OutputCCValues;

            InitPage(boxname);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            OnUIEvent?.Invoke(BoxGuid, "CC_SAVE_MIX_DEFAULT", BoxMixers);
            MainWindow.CCMixData -= MainWindow_CCMixData;
            MidiRouting.OutputCCValues -= MidiRouting_OutputCCValues;
        }

        private void InitPage(string boxname)
        {
            Title = string.Concat(Title, " - ", boxname);
            MainWindow.CCMixData += MainWindow_CCMixData;

            var CC = CCList();

            for (int i = 0; i < BoxMixers.Length; i++)
            {
                ComboBox comboBox = new ComboBox();
                comboBox.Name = "cbCC" + (i + 1);
                comboBox.HorizontalAlignment = HorizontalAlignment.Center;
                comboBox.VerticalAlignment = VerticalAlignment.Center;
                comboBox.MinWidth = 100;
                comboBox.SelectedValuePath = "Tag";
                comboBox.Tag = (i + 1);
                comboBox.SelectionChanged += ComboBox_SelectionChanged;
                Grid.SetColumn(comboBox, i);
                Grid.SetRow(comboBox, 0);
                gdMix.Children.Add(comboBox);
                ComboBoxes.Add(comboBox);
                FillComboBox(CC, ComboBoxes.Last());

                Slider slider = new Slider();
                slider.Name = "slCC" + (i + 1);
                slider.Minimum = 0;
                slider.Maximum = 127;
                slider.SmallChange = 1;
                slider.Tag = (i + 1);
                //slider.Foreground = Brushes.White;
                slider.Orientation = Orientation.Vertical;
                slider.HorizontalAlignment = HorizontalAlignment.Center;
                slider.ValueChanged += slCC_ValueChanged;
                Grid.SetColumn(slider, i);
                Grid.SetRow(slider, 1);
                gdMix.Children.Add(slider);
                Sliders.Add(slider);
            }
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
                    for (int i = 0; i < ComboBoxes.Count; i++) 
                    {
                        if (ComboBoxes[i].SelectedValue.Equals(sValues[0]))
                        { iCb = i; break; }
                    }
                    if (iCb > - 1) 
                    {
                        Sliders[iCb].Value = sValues[1];
                    }
                }
            });
        }

        private void MainWindow_CCMixData(Guid routingguid, List<int> sValues)
        {
            if (routingguid == RoutingGuid)
            {
                if (sValues.Count == 8)
                {
                    BoxMixersValues = sValues.ToArray();

                    for (int i = 0; i < BoxMixers.Length; i++)
                    {
                        Sliders[i].Value = sValues[i];
                    }
                }
            }
        }

        private async void slCC_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                Slider sl = (Slider)sender;

                int iIndex = Convert.ToInt32(sl.Tag) - 1;
                int iValue = (int)sl.Value;

                BoxMixersValues[iIndex] = iValue;
                OnUIEvent?.Invoke(BoxGuid, "CC_SEND_MIX_DATA", new int[] { BoxMixers[iIndex], BoxMixersValues[iIndex] });
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

        private static List<string[]> CCList()
        {
            List<string[]> midiControllers = new List<string[]>
        {
            new string[] {"1", "Modulation Wheel (MSB)", "Generally this CC controls a vibrato effect (pitch, loudness, brightness). What is modulated is based on the patch."},
            new string[] {"2", "Breath Controller (MSB)", "Oftentimes associated with aftertouch messages. It was originally intended for use with a breath MIDI controller in which blowing harder produced higher MIDI control values. It can be used for modulation as well."},
            new string[] {"4", "Foot Pedal (MSB)", "Often used with aftertouch messages. It can send a continuous stream of values based on how the pedal is used."},
            new string[] {"5", "Portamento Time (MSB)", "Controls portamento rate to slide between 2 notes played subsequently."},
            new string[] {"7", "Volume (MSB)", "Controls the volume of the channel."},
            new string[] {"8", "Balance (MSB)", "Controls the left and right balance, generally for stereo patches. A value of 64 equals the center."},
            new string[] {"10", "Pan (MSB)", "Controls the left and right balance, generally for mono patches. A value of 64 equals the center."},
            new string[] {"11", "Expression (MSB)", "Expression is a percentage of volume (CC7)."},
            new string[] {"12", "Effect Controller 1 (MSB)", "Usually used to control a parameter of an effect within the synth or workstation."},
            new string[] {"13", "Effect Controller 2 (MSB)", "Usually used to control a parameter of an effect within the synth or workstation."},
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
