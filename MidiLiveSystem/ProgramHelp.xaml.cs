using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// Logique d'interaction pour ProgramHelp.xaml
    /// </summary>
    public partial class ProgramHelp : Window
    {
        public ProgramHelp()
        {
            InitializeComponent();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void DonateButton_Click(object sender, RoutedEventArgs e)
        {
            string paypalEmailAddress = "guizmox@hotmail.com";
            string paypalDonateLink = $"https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business={paypalEmailAddress}&currency_code=EUR";

            var ps = new ProcessStartInfo(paypalDonateLink)
            {
                UseShellExecute = true,
                Verb = "open"
            };
            Process.Start(ps);
        }
    }
}
