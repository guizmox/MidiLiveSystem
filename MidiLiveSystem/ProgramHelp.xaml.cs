using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MidiLiveSystem
{
    /// <summary>
    /// Logique d'interaction pour ProgramHelp.xaml
    /// </summary>
    public partial class ProgramHelp : Window
    {
        private int PageNumber = 1;
        public ProgramHelp()
        {
            InitializeComponent();
        }

        private void btnPreviousScreen_Click(object sender, RoutedEventArgs e)
        {
            if (PageNumber > 1)
            {
                PageNumber--;

                BitmapImage bitmapImage = new();
                bitmapImage.BeginInit();
                bitmapImage.UriSource = new Uri("assets/" + PageNumber + ".png", UriKind.Relative);
                bitmapImage.EndInit();

                imgHelp.Source = bitmapImage;
            }
        }

        private void btnNextScreen_Click(object sender, RoutedEventArgs e)
        {
            if (PageNumber < 3)
            {
                PageNumber++;

                BitmapImage bitmapImage = new();
                bitmapImage.BeginInit();
                bitmapImage.UriSource = new Uri("assets/" + PageNumber + ".png", UriKind.Relative);
                bitmapImage.EndInit();

                imgHelp.Source = bitmapImage;
            }
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

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
    }
}
