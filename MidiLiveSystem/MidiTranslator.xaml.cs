using MidiTools;
using System;
using System.Collections.Generic;
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

namespace MidiLiveSystem
{
    /// <summary>
    /// Logique d'interaction pour MidiTranslator.xaml
    /// </summary>
    public partial class MidiTranslator : Window
    {
        public bool InvalidData = false;

        public MidiTranslator()
        {
            InitializeComponent();
        }

        internal string[] GetTranslatorConfiguration()
        {
            string sName = tbTranslatorName.Text.Trim();

            StringBuilder sbINScript = new StringBuilder();

            ComboBoxItem cbIN = (ComboBoxItem)cbEventInType.SelectedItem;

            if (cbIN != null)
            {
                sbINScript.Append("[IN=");

                switch (cbIN.Tag)
                {
                    case "KEY":
                        //[IN=KEY#0-127]
                        sbINScript.Append("KEY#");
                        sbINScript.Append(tbINLowKey.Text.Trim());
                        sbINScript.Append(":");
                        sbINScript.Append(tbINLowVelo.Text.Trim());
                        break;
                    case "KEY_RANGE":
                        sbINScript.Append("KEY#");
                        sbINScript.Append(tbINLowKey.Text.Trim() + "-" + tbINHighKey.Text.Trim());
                        sbINScript.Append(":");
                        sbINScript.Append(tbINLowVelo.Text.Trim() + "-" + tbINHighVelo.Text.Trim());
                        break;
                    case "CC":
                        //[IN=CC#7:0-127]
                        sbINScript.Append("CC#");
                        sbINScript.Append(tbINCC.Text.Trim());
                        sbINScript.Append(":");
                        sbINScript.Append(tbINLowCCValue.Text.Trim());
                        break;
                    case "CC_RANGE":
                        sbINScript.Append("CC#");
                        sbINScript.Append(tbINCC.Text.Trim());
                        sbINScript.Append(":");
                        sbINScript.Append(tbINLowCCValue.Text.Trim() + "-" + tbINHighCCValue.Text.Trim());
                        break;
                    case "PC":
                        //[IN=PC#0-127]
                        sbINScript.Append("PC#");
                        sbINScript.Append(tbINLowPCValue.Text.Trim());
                        break;
                    case "PC_RANGE":
                        sbINScript.Append("PC#");
                        sbINScript.Append(tbINLowPCValue.Text.Trim() + "-" + tbINHighPCValue.Text.Trim());
                        break;
                    case "SYS":
                        //[IN=SYS#F0...F7]
                        sbINScript.Append("SYS#");
                        TextRange textRange = new TextRange(rtbINSys.Document.ContentStart, rtbINSys.Document.ContentEnd);
                        sbINScript.Append(textRange.Text.Replace("-", "").Trim());
                        break;
                    case "AT":
                        sbINScript.Append("AT#");
                        sbINScript.Append(tbINLowATValue.Text.Trim());
                        break;
                    case "AT_RANGE":
                        sbINScript.Append("AT#");
                        sbINScript.Append(tbINLowATValue.Text.Trim() + "-" + tbINHighATValue.Text.Trim());
                        break;
                    case "PB":
                        //[IN=PB#0:0-127] //0=UP, 1=DOWN, 2=BOTH
                        sbINScript.Append("PB#");
                        sbINScript.Append(cbINPBDirection.SelectedIndex.ToString() + ":" + tbINLowPBValue.Text.Trim() + "-" + tbINHighPBValue.Text.Trim());
                        break;
                }

                sbINScript.Append("]");
            }

            StringBuilder sbOUTScript = new StringBuilder();

            ComboBoxItem cbOUT = (ComboBoxItem)cbEventOutType.SelectedItem;

            if (cbOUT != null)
            {
                sbOUTScript.Append("[OUT=");

                switch (cbOUT.Tag)
                {
                    case "KEY":
                        sbOUTScript.Append("KEY#");
                        sbOUTScript.Append(tbOUTKey.Text.Trim());
                        sbOUTScript.Append(":");
                        sbOUTScript.Append(tbOUTVeloKey.Text.Equals("-1") ? "0-127" : tbOUTVeloKey.Text.Trim());
                        sbOUTScript.Append(":");
                        sbOUTScript.Append(tbOUTLengthKey.Text.Trim());
                        break;
                    case "KEY_RANGE":
                        //[OUT=KEY#64:64:1000] -> fixed key, fixed velo, length
                        //[OUT=KEY#0-127:0-127:1000] -> key range, velo range, length
                        sbOUTScript.Append("KEY#");
                        sbOUTScript.Append(tbOUTKey.Text.Equals("-1") ? "0-127" : tbOUTKey.Text.Trim());
                        sbOUTScript.Append(":");
                        sbOUTScript.Append(tbOUTVeloKey.Text.Equals("-1") ? "0-127" : tbOUTVeloKey.Text.Trim());
                        sbOUTScript.Append(":");
                        sbOUTScript.Append(tbOUTLengthKey.Text.Trim());
                        break;
                    case "CC":
                        sbOUTScript.Append("CC#");
                        sbOUTScript.Append(tbOUTCC.Text.Trim());
                        sbOUTScript.Append(":");
                        sbOUTScript.Append(tbOUTCCValue.Text.Trim());
                        break;
                    case "CC_RANGE":
                        //[OUT=CC#64:64-127] 4,5,8
                        //[OUT= CC#64:64]
                        sbOUTScript.Append("CC#");
                        sbOUTScript.Append(tbOUTCC.Text.Trim());
                        sbOUTScript.Append(":");
                        sbOUTScript.Append("0-127");
                        break;
                    case "PC":
                        sbOUTScript.Append("PC#");
                        sbOUTScript.Append(tbOUTPCValue.Text.Trim());
                        break;
                    case "PC_RANGE":
                        //[OUT=PC#0:0:0]
                        //[OUT= PC#0-127:0:0]
                        sbOUTScript.Append("PC#");
                        sbOUTScript.Append("0-127");
                        break;
                    case "SYS":
                        sbOUTScript.Append("SYS#");
                        TextRange textRange = new TextRange(rtbOUTSys.Document.ContentStart, rtbOUTSys.Document.ContentEnd);
                        sbOUTScript.Append(textRange.Text.Replace("-", "").Trim());
                        break;
                    case "AT":
                        sbOUTScript.Append("AT#");
                        sbOUTScript.Append(tbOUTATValue.Text.Trim());
                        break;
                    case "AT_RANGE":
                        sbOUTScript.Append("AT#");
                        sbOUTScript.Append("0-127");
                        break;
                    case "PB":
                        //[OUT=PB#0:0-127] //0=UP, 1=DOWN, 2=BOTH
                        sbINScript.Append("PB#");
                        sbINScript.Append(cbOUTPBDirection.SelectedIndex.ToString() + ":" + tbOUTLowPBValue.Text.Trim() + "-" + tbOUTHighPBValue.Text.Trim());
                        break;
                }

                sbOUTScript.Append("]");
            }

            if (sbINScript.Length > 0 && sbOUTScript.Length > 0)
            { return new string[] { sbINScript.ToString() + sbOUTScript.ToString(), sName }; }
            else { return null; }

        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            string sErrors = CheckFields();

            if (sErrors.Length > 0)
            {
                var dialog = MessageBox.Show("The following errors have been detected : " + Environment.NewLine + sErrors, "Input Error", MessageBoxButton.OKCancel);
                if (dialog == MessageBoxResult.Cancel)
                {

                }
                else
                {
                    InvalidData = true;
                    Close();
                }
            }
            else
            {
                Close();
            }
        }

        private bool ControlValue(string sValue, bool bLessAuthorized)
        {
            int iParse = -1;
            if (int.TryParse(sValue, out iParse))
            {
                if (iParse >= (bLessAuthorized ? -1 : 0) && iParse <= 127)
                {
                    return true;
                }
                else { return false; }
            }
            else { return false; }
        }

        private bool ControlPitchBendValue(string sValue)
        {
            int iParse = -1;
            if (int.TryParse(sValue, out iParse))
            {
                if (iParse >= -8192 && iParse <= 8192)
                {
                    return true;
                }
                else { return false; }
            }
            else { return false; }
        }

        private string CheckFields()
        {
            StringBuilder sbErrors = new StringBuilder();

            if (!ControlValue(tbINLowKey.Text, false))
            {
                sbErrors.AppendLine("IN Low Note (" +tbINLowKey.Text + ")");
            }

            if (!ControlValue(tbINHighKey.Text, true)) 
            {
                sbErrors.AppendLine("IN High Note (" + tbINHighKey.Text + ")");
            }

            if (!ControlValue(tbINLowVelo.Text, false))
            {
                sbErrors.AppendLine("IN Low Velocity (" + tbINLowVelo.Text + ")");
            }

            if (!ControlValue(tbINHighVelo.Text, true))
            {
                sbErrors.AppendLine("IN High Velocity (" + tbINHighVelo.Text + ")");
            }

            if (!ControlValue(tbINCC.Text, false))
            {
                sbErrors.AppendLine("IN Control Change (" + tbINCC.Text + ")");
            }

            if (!ControlValue(tbINLowCCValue.Text, false))
            {
                sbErrors.AppendLine("IN Control Change Low Value (" + tbINLowCCValue.Text + ")");
            }

            if (!ControlValue(tbINHighCCValue.Text, true))
            {
                sbErrors.AppendLine("IN Control Change High Value (" + tbINHighCCValue.Text + ")");
            }

            if (!ControlValue(tbINLowPCValue.Text, false))
            {
                sbErrors.AppendLine("IN Program Change Low Value (" + tbINLowPCValue.Text + ")");
            }

            if (!ControlValue(tbINHighPCValue.Text, false))
            {
                sbErrors.AppendLine("IN Program Change High Value (" + tbINHighPCValue.Text + ")");
            }

            if (!ControlValue(tbINLowATValue.Text, false))
            {
                sbErrors.AppendLine("IN Aftertouch Low Value (" + tbINLowATValue.Text + ")");
            }

            if (!ControlValue(tbINHighATValue.Text, false))
            {
                sbErrors.AppendLine("IN Aftertouch High Value (" + tbINHighATValue.Text + ")");
            }

            if (!ControlValue(tbOUTKey.Text, true))
            {
                sbErrors.AppendLine("OUT Note (" + tbOUTKey.Text + ")");
            }

            if (!ControlValue(tbOUTVeloKey.Text, true))
            {
                sbErrors.AppendLine("OUT Note Velocity (" + tbOUTVeloKey.Text + ")");
            }

            int iLength = 0;
            if (!int.TryParse(tbOUTLengthKey.Text, out iLength))
            {
                sbErrors.AppendLine("OUT Note Length. Expecting a value between 1 and 10000 ms (" + tbOUTLengthKey.Text + ")");
            }
            if (iLength < 0 || iLength > 10000)
            {
                sbErrors.AppendLine("OUT Note Length. Expecting a value between 1 and 10000 ms (" + tbOUTLengthKey.Text + ")");
            }

            if (!ControlValue(tbOUTCC.Text, false))
            {
                sbErrors.AppendLine("OUT Control Change (" + tbOUTCC.Text + ")");
            }

            if (!ControlValue(tbOUTCCValue.Text, true))
            {
                sbErrors.AppendLine("OUT Control Change Value (" + tbOUTCCValue.Text + ")");
            }

            if (!ControlValue(tbOUTPCValue.Text, true))
            {
                sbErrors.AppendLine("OUT Program Change Value (" + tbOUTPCValue.Text + ")");
            }

            if (!ControlValue(tbOUTMSBValue.Text, false))
            {
                sbErrors.AppendLine("OUT MSB Value (" + tbOUTMSBValue.Text + ")");
            }

            if (!ControlValue(tbOUTLSBValue.Text, false))
            {
                sbErrors.AppendLine("OUT LSB Value (" + tbOUTLSBValue.Text + ")");
            }

            if (!ControlValue(tbOUTATValue.Text, true))
            {
                sbErrors.AppendLine("OUT Aftertouch Value (" + tbOUTATValue.Text + ")");
            }

            if (cbEventInType.SelectedItem != null && ((ComboBoxItem)cbEventInType.SelectedItem).Tag.Equals("SYS"))
            {
                var text = new TextRange(rtbINSys.Document.ContentStart, rtbINSys.Document.ContentEnd);
                if (text.Text.Length == 0)
                {
                    sbErrors.AppendLine("IN SysEX Value Empty");
                }
            }

            if (cbEventOutType.SelectedItem != null && ((ComboBoxItem)cbEventOutType.SelectedItem).Tag.Equals("SYS"))
            {
                var text = new TextRange(rtbOUTSys.Document.ContentStart, rtbOUTSys.Document.ContentEnd);
                if (text.Text.Replace("-", "").Trim().Length == 0)
                {
                    sbErrors.AppendLine("OUT SysEX Value Empty");
                }
                else if (!Regex.IsMatch(text.Text.Replace("-", "").Trim(), Tools.SYSEX_CHECK, RegexOptions.IgnoreCase))
                {
                    sbErrors.AppendLine("OUT SysEX Value Invalid (expecting F0 ... F7)");
                }
            }

            if (!ControlPitchBendValue(tbINHighPBValue.Text))
            {
                sbErrors.AppendLine("IN PB High Value (" + tbINHighPBValue.Text + ")");
            }

            if (!ControlPitchBendValue(tbINLowPBValue.Text))
            {
                sbErrors.AppendLine("IN PB Low Value (" + tbINLowPBValue.Text + ")");
            }

            if (!ControlPitchBendValue(tbOUTHighPBValue.Text))
            {
                sbErrors.AppendLine("OUT PB High Value (" + tbOUTHighPBValue.Text + ")");
            }

            if (!ControlPitchBendValue(tbOUTLowPBValue.Text))
            {
                sbErrors.AppendLine("OUT PB Low Value (" + tbOUTLowPBValue.Text + ")");
            }

            return sbErrors.ToString();
        }

        private void cbEventInType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem item = (ComboBoxItem)e.AddedItems[0];
            if (item != null)
            {
                switch (item.Tag)
                {
                    case "KEY":
                        pnlINKEY.Visibility = Visibility.Visible;
                        pnlINCC.Visibility = Visibility.Hidden;
                        pnlINPC.Visibility = Visibility.Hidden;
                        pnlINSYS.Visibility = Visibility.Hidden;
                        pnlINAT.Visibility = Visibility.Hidden;
                        pnlINPB.Visibility = Visibility.Hidden;
                        tbINLowKey.IsEnabled = true;
                        tbINHighKey.IsEnabled = false;
                        tbINLowVelo.IsEnabled = true;
                        tbINHighVelo.IsEnabled = false;
                        tbINHighKey.Text = "-1";
                        tbINLowKey.Text = "0";
                        tbINLowVelo.Text = "0";
                        tbINHighVelo.Text = "-1";
                        break;
                    case "KEY_RANGE":
                        pnlINKEY.Visibility = Visibility.Visible;
                        pnlINCC.Visibility = Visibility.Hidden;
                        pnlINPC.Visibility = Visibility.Hidden;
                        pnlINSYS.Visibility = Visibility.Hidden;
                        pnlINAT.Visibility = Visibility.Hidden;
                        pnlINPB.Visibility = Visibility.Hidden;
                        tbINLowKey.IsEnabled = true;
                        tbINHighKey.IsEnabled = true;
                        tbINLowVelo.IsEnabled = true;
                        tbINHighVelo.IsEnabled = true;
                        tbINHighKey.Text = "127";
                        tbINLowKey.Text = "0";
                        tbINLowVelo.Text = "0";
                        tbINHighVelo.Text = "127";
                        break;
                    case "CC":
                        pnlINKEY.Visibility = Visibility.Hidden;
                        pnlINCC.Visibility = Visibility.Visible;
                        pnlINPC.Visibility = Visibility.Hidden;
                        pnlINSYS.Visibility = Visibility.Hidden;
                        pnlINAT.Visibility = Visibility.Hidden;
                        pnlINPB.Visibility = Visibility.Hidden;
                        tbINLowCCValue.IsEnabled = true;
                        tbINHighCCValue.IsEnabled = false;
                        tbINLowCCValue.Text = "0";
                        tbINHighCCValue.Text = "-1";
                        break;
                    case "CC_RANGE":
                        pnlINKEY.Visibility = Visibility.Hidden;
                        pnlINCC.Visibility = Visibility.Visible;
                        pnlINPC.Visibility = Visibility.Hidden;
                        pnlINSYS.Visibility = Visibility.Hidden;
                        pnlINAT.Visibility = Visibility.Hidden;
                        pnlINPB.Visibility = Visibility.Hidden;
                        tbINLowCCValue.IsEnabled = true;
                        tbINHighCCValue.IsEnabled = true;
                        tbINLowCCValue.Text = "0";
                        tbINHighCCValue.Text = "127";
                        break;
                    case "PC":
                        pnlINKEY.Visibility = Visibility.Hidden;
                        pnlINCC.Visibility = Visibility.Hidden;
                        pnlINPC.Visibility = Visibility.Visible;
                        pnlINSYS.Visibility = Visibility.Hidden;
                        pnlINAT.Visibility = Visibility.Hidden;
                        pnlINPB.Visibility = Visibility.Hidden;
                        tbINLowPCValue.IsEnabled = true;
                        tbINHighPCValue.IsEnabled = false;
                        tbINLowPCValue.Text = "0";
                        tbINHighPCValue.Text = "-1";
                        break;
                    case "PC_RANGE":
                        pnlINKEY.Visibility = Visibility.Hidden;
                        pnlINCC.Visibility = Visibility.Hidden;
                        pnlINPC.Visibility = Visibility.Visible;
                        pnlINSYS.Visibility = Visibility.Hidden;
                        pnlINAT.Visibility = Visibility.Hidden;
                        pnlINPB.Visibility = Visibility.Hidden;
                        tbINLowPCValue.IsEnabled = true;
                        tbINHighPCValue.IsEnabled = true;
                        tbINLowPCValue.Text = "0";
                        tbINHighPCValue.Text = "127";
                        break;
                    case "SYS":
                        pnlINKEY.Visibility = Visibility.Hidden;
                        pnlINCC.Visibility = Visibility.Hidden;
                        pnlINPC.Visibility = Visibility.Hidden;
                        pnlINSYS.Visibility = Visibility.Visible;
                        pnlINAT.Visibility = Visibility.Hidden;
                        pnlINPB.Visibility = Visibility.Hidden;
                        break;
                    case "AT":
                        pnlINKEY.Visibility = Visibility.Hidden;
                        pnlINCC.Visibility = Visibility.Hidden;
                        pnlINPC.Visibility = Visibility.Hidden;
                        pnlINSYS.Visibility = Visibility.Hidden;
                        pnlINAT.Visibility = Visibility.Visible;
                        pnlINPB.Visibility = Visibility.Hidden;
                        tbINLowATValue.IsEnabled = true;
                        tbINHighATValue.IsEnabled = false;
                        tbINLowATValue.Text = "0";
                        tbINHighATValue.Text = "-1";
                        break;
                    case "AT_RANGE":
                        pnlINKEY.Visibility = Visibility.Hidden;
                        pnlINCC.Visibility = Visibility.Hidden;
                        pnlINPC.Visibility = Visibility.Hidden;
                        pnlINSYS.Visibility = Visibility.Hidden;
                        pnlINAT.Visibility = Visibility.Visible;
                        pnlINPB.Visibility = Visibility.Hidden;
                        tbINLowATValue.IsEnabled = true;
                        tbINHighATValue.IsEnabled = true;
                        tbINLowATValue.Text = "0";
                        tbINHighATValue.Text = "127";
                        break;
                    case "PB":
                        pnlINKEY.Visibility = Visibility.Hidden;
                        pnlINCC.Visibility = Visibility.Hidden;
                        pnlINPC.Visibility = Visibility.Hidden;
                        pnlINSYS.Visibility = Visibility.Hidden;
                        pnlINAT.Visibility = Visibility.Hidden;
                        pnlINPB.Visibility = Visibility.Visible;
                        cbINPBDirection.SelectedIndex = 0;
                        tbINLowPBValue.Text = "0";
                        tbINHighPBValue.Text = "8192";
                        break;
                }
            }
        }

        private void cbEventOutType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem item = (ComboBoxItem)e.AddedItems[0];
            if (item != null)
            {
                switch (item.Tag)
                {
                    case "KEY":
                        pnlOUTKEY.Visibility = Visibility.Visible;
                        pnlOUTCC.Visibility = Visibility.Hidden;
                        pnlOUTPC.Visibility = Visibility.Hidden;
                        pnlOUTSYS.Visibility = Visibility.Hidden;
                        pnlOUTAT.Visibility = Visibility.Hidden;
                        pnlOUTPB.Visibility = Visibility.Hidden;
                        tbOUTKey.IsEnabled = true;
                        tbOUTKey.Text = "0";
                        break;
                    case "KEY_RANGE":
                        pnlOUTKEY.Visibility = Visibility.Visible;
                        pnlOUTCC.Visibility = Visibility.Hidden;
                        pnlOUTPC.Visibility = Visibility.Hidden;
                        pnlOUTSYS.Visibility = Visibility.Hidden;
                        pnlOUTAT.Visibility = Visibility.Hidden;
                        pnlOUTPB.Visibility = Visibility.Hidden;
                        tbOUTKey.IsEnabled = false;
                        tbOUTKey.Text = "-1";
                        break;
                    case "CC":
                        pnlOUTKEY.Visibility = Visibility.Hidden;
                        pnlOUTCC.Visibility = Visibility.Visible;
                        pnlOUTPC.Visibility = Visibility.Hidden;
                        pnlOUTSYS.Visibility = Visibility.Hidden;
                        pnlOUTAT.Visibility = Visibility.Hidden;
                        pnlOUTPB.Visibility = Visibility.Hidden;
                        tbOUTCCValue.IsEnabled = true;
                        tbOUTCCValue.Text = "0";
                        break;
                    case "CC_RANGE":
                        pnlOUTKEY.Visibility = Visibility.Hidden;
                        pnlOUTCC.Visibility = Visibility.Visible;
                        pnlOUTPC.Visibility = Visibility.Hidden;
                        pnlOUTSYS.Visibility = Visibility.Hidden;
                        pnlOUTAT.Visibility = Visibility.Hidden;
                        pnlOUTPB.Visibility = Visibility.Hidden;
                        tbOUTCCValue.IsEnabled = false;
                        tbOUTCCValue.Text = "-1";
                        break;
                    case "PC":
                        pnlOUTKEY.Visibility = Visibility.Hidden;
                        pnlOUTCC.Visibility = Visibility.Hidden;
                        pnlOUTPC.Visibility = Visibility.Visible;
                        pnlOUTSYS.Visibility = Visibility.Hidden;
                        pnlOUTAT.Visibility = Visibility.Hidden;
                        pnlOUTPB.Visibility = Visibility.Hidden;
                        tbOUTPCValue.IsEnabled = true;
                        tbOUTPCValue.Text = "0";
                        break;
                    case "PC_RANGE":
                        pnlOUTKEY.Visibility = Visibility.Hidden;
                        pnlOUTCC.Visibility = Visibility.Hidden;
                        pnlOUTPC.Visibility = Visibility.Visible;
                        pnlOUTSYS.Visibility = Visibility.Hidden;
                        pnlOUTAT.Visibility = Visibility.Hidden;
                        pnlOUTPB.Visibility = Visibility.Hidden;
                        tbOUTPCValue.IsEnabled = false;
                        tbOUTPCValue.Text = "-1";
                        break;
                    case "SYS":
                        pnlOUTKEY.Visibility = Visibility.Hidden;
                        pnlOUTCC.Visibility = Visibility.Hidden;
                        pnlOUTPC.Visibility = Visibility.Hidden;
                        pnlOUTSYS.Visibility = Visibility.Visible;
                        pnlOUTAT.Visibility = Visibility.Hidden;
                        pnlOUTPB.Visibility = Visibility.Hidden;
                        break;
                    case "AT":
                        pnlOUTKEY.Visibility = Visibility.Hidden;
                        pnlOUTCC.Visibility = Visibility.Hidden;
                        pnlOUTPC.Visibility = Visibility.Hidden;
                        pnlOUTSYS.Visibility = Visibility.Hidden;
                        pnlOUTAT.Visibility = Visibility.Visible;
                        pnlOUTPB.Visibility = Visibility.Hidden;
                        tbOUTATValue.IsEnabled = true;
                        tbOUTATValue.Text = "0";
                        break;
                    case "AT_RANGE":
                        pnlOUTKEY.Visibility = Visibility.Hidden;
                        pnlOUTCC.Visibility = Visibility.Hidden;
                        pnlOUTPC.Visibility = Visibility.Hidden;
                        pnlOUTSYS.Visibility = Visibility.Hidden;
                        pnlOUTAT.Visibility = Visibility.Visible;
                        pnlOUTPB.Visibility = Visibility.Hidden;
                        tbOUTATValue.IsEnabled = false;
                        tbOUTATValue.Text = "-1";
                        break;
                    case "PB":
                        pnlOUTKEY.Visibility = Visibility.Hidden;
                        pnlOUTCC.Visibility = Visibility.Hidden;
                        pnlOUTPC.Visibility = Visibility.Hidden;
                        pnlOUTSYS.Visibility = Visibility.Hidden;
                        pnlOUTAT.Visibility = Visibility.Hidden;
                        pnlOUTPB.Visibility = Visibility.Visible;
                        cbOUTPBDirection.SelectedIndex = 0;
                        tbOUTLowPBValue.Text = "0";
                        tbOUTHighPBValue.Text = "8192";
                        break;
                }
            }
        }

        private void cbPBDirection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox cb = (ComboBox)sender;
            if (cb.Name.Equals("cbOUTPBDirection"))
            {
                if (cb.SelectedIndex == 0) 
                { tbOUTLowPBValue.Text = "0"; tbOUTHighPBValue.Text = "8192"; }
                else if (cb.SelectedIndex == 1)
                { tbOUTLowPBValue.Text = "-8192"; tbOUTHighPBValue.Text = "0"; }
                else
                { tbOUTLowPBValue.Text = "-8192"; tbOUTHighPBValue.Text = "8192"; }
            }
            else
            {
                if (cb.SelectedIndex == 0)
                { tbINLowPBValue.Text = "0"; tbINHighPBValue.Text = "8192"; }
                else if (cb.SelectedIndex == 1)
                { tbINLowPBValue.Text = "-8192"; tbINHighPBValue.Text = "0"; }
                else
                { tbINLowPBValue.Text = "-8192"; tbINHighPBValue.Text = "8192"; }
            }

        }
    }
}
