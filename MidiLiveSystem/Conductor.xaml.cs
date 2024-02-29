using MidiTools;
using System;
using System.Collections.Generic;
using System.Reflection;
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
    /// Logique d'interaction pour Conductor.xaml
    /// </summary>
    public partial class Conductor : Window
    {
        private List<RoutingBox> Boxes = new List<RoutingBox>();

        private List<bool> _isMoving = new List<bool>();
        private List<Point> _buttonPosition = new List<Point>();
        private List<double> deltaX = new List<double>();
        private List<double> deltaY = new List<double>();
        private List<TranslateTransform> _currentTT = new List<TranslateTransform>();

        public Conductor(List<RoutingBox> boxes)
        {
            InitializeComponent();

            Boxes = boxes;

            for (int i = 0; i < boxes.Count; i++)
            {
                _buttonPosition.Add(new Point());
                _isMoving.Add(false);
                deltaX.Add(0);
                deltaY.Add(0);
                _currentTT.Add(new TranslateTransform());

                InitializeRoutingBoxButton(boxes[i], i);
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RoutingBoxButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            int iIndex = Convert.ToInt32(((Button)sender).Name.Split('_')[1]);

            if (_buttonPosition == null)
                _buttonPosition[iIndex] = ((Button)sender).TransformToAncestor(gdButtons).Transform(new Point(0, 0));
            var mousePosition = Mouse.GetPosition(gdButtons);
            deltaX[iIndex] = mousePosition.X - _buttonPosition[iIndex].X;
            deltaY[iIndex] = mousePosition.Y - _buttonPosition[iIndex].Y;
            _isMoving[iIndex] = true;

        }

        private void RoutingBoxButton_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            int iIndex = Convert.ToInt32(((Button)sender).Name.Split('_')[1]);

            _currentTT[iIndex] = ((Button)sender).RenderTransform as TranslateTransform;
            _isMoving[iIndex] = false;
        }

        private void RoutingBoxButton_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            int iIndex = Convert.ToInt32(((Button)sender).Name.Split('_')[1]);

            if (!_isMoving[iIndex]) return;

            var mousePoint = Mouse.GetPosition(gdButtons);

            var offsetX = (_currentTT == null ? _buttonPosition[iIndex].X : _buttonPosition[iIndex].X - _currentTT[iIndex].X) + deltaX[iIndex] - mousePoint.X;
            var offsetY = (_currentTT == null ? _buttonPosition[iIndex].Y : _buttonPosition[iIndex].Y - _currentTT[iIndex].Y) + deltaY[iIndex] - mousePoint.Y;

            if (offsetX > 330)
            {
                offsetX = 330;
            }
            if (offsetX < -330)
            {
                offsetX = -330;
            }
            if (offsetY < -130)
            {
                offsetY = -130;
            }
            if (offsetY > 170)
            {
                offsetY = 170;
            }

            ((Button)sender).RenderTransform = new TranslateTransform(-offsetX, -offsetY);

            Dispatcher.Invoke(() =>
            {
                int iPan = FromXToPan(offsetX);
                int iVol = FromYToVolume(offsetY);
                int iCutOff = 127 - iVol;
                int iReverb = 127 - iVol;
                int iAttackConverter = Convert.ToInt32((512 / (iVol + 1)) - 4);
                int iAttack = iAttackConverter > 64 ? 64 : iAttackConverter;

                SetBoxData(iVol, iPan, iReverb, iCutOff, iAttack, Boxes[iIndex]);

                ((Button)sender).Content = new TextBlock() { Foreground = Brushes.Yellow, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Text = NameButtonBox(Boxes[iIndex], iVol, iPan), FontSize = 9, TextWrapping = TextWrapping.Wrap };

                string text = string.Concat("VOL=", iVol, " - PAN=", iPan);

                if (ckReverb.IsChecked.Value)
                {
                    text = string.Concat(text, " - REV=", iReverb);
                }
                if (ckCutoff.IsChecked.Value) 
                {
                    text = string.Concat(text, " - CUTOFF=", iCutOff);
                }
                if (ckAttack.IsChecked.Value) 
                {
                    text = string.Concat(text, " - ATTACK=", iAttack);
                }

                lblDataInfo.Content = text;
            });
        }

        private string NameButtonBox(RoutingBox box, int iVol, int iPan)
        {
            return string.Concat(box.BoxName, Environment.NewLine, "VOL=", iVol, " - PAN=", iPan);
        }

        private void SetBoxData(int iVol, int iPan, int iReverb, int iCutOff, int iAttack, RoutingBox box)
        {
            foreach (var item in box.cbCCDefaultValues.Items)
            {
                ComboBoxCustomItem cb = (ComboBoxCustomItem)item;

                switch (cb.Id)
                {
                    case "tbCC_Chorus":
                        break;
                    case "tbCC_Pan":
                        cb.Value = iPan.ToString();
                        break;
                    case "tbCC_Volume":
                        cb.Value = iVol.ToString();
                        break;
                    case "tbCC_Attack":
                        if (ckAttack.IsChecked.Value)
                        { cb.Value = iAttack.ToString(); }
                        break;
                    case "tbCC_Decay":
                        break;
                    case "tbCC_Release":
                        break;
                    case "tbCC_Reverb":
                        if (ckReverb.IsChecked.Value)
                        { cb.Value = iReverb.ToString(); }
                        break;
                    case "tbCC_Timbre":
                        break;
                    case "tbCC_CutOff":
                        if (ckCutoff.IsChecked.Value)
                        { cb.Value = iCutOff.ToString(); }
                        break;
                }
            }
        }

        private void InitializeRoutingBoxButton(RoutingBox box, int iIndex)
        {
            int iVol = 100;
            int iPan = 64;

            foreach (var item in box.cbCCDefaultValues.Items)
            {
                ComboBoxCustomItem cb = (ComboBoxCustomItem)item;

                switch (cb.Id)
                {
                    case "tbCC_Pan":
                        iPan = Convert.ToInt32(cb.Value);
                        break;
                    case "tbCC_Volume":
                        iVol = Convert.ToInt32(cb.Value);
                        break;
                }
            }
            
            var rtbButton = new Button
            {
                Name = "btn_" + iIndex,
                Width = 120,
                Height = 40,
                Foreground = Brushes.Wheat,
                Content = new TextBlock() { Foreground = Brushes.Yellow, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Text = NameButtonBox(box, iVol, iPan), FontSize = 9, TextWrapping = TextWrapping.Wrap },
                Tag = box.BoxGuid.ToString(),
                VerticalContentAlignment = VerticalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            rtbButton.PreviewMouseDown += RoutingBoxButton_PreviewMouseDown;
            rtbButton.PreviewMouseUp += RoutingBoxButton_PreviewMouseUp;
            rtbButton.PreviewMouseMove += RoutingBoxButton_PreviewMouseMove;

            int[] iPos = TranscodePanVolumeToGrid(box);

            //rtbButton.Margin = new Thickness(iPos[0], iPos[1], 0, 0); // Position en pixels (X=100, Y=200)
            //rtbButton.RenderTransform = new TranslateTransform(-iPos[1], -iPos[0]);
            rtbButton.RenderTransform = new TranslateTransform(-iPos[1], -iPos[0]);

            _currentTT[iIndex] = rtbButton.RenderTransform as TranslateTransform;
            _isMoving[iIndex] = false;

            gdButtons.Children.Add(rtbButton);
        }

        private int[] TranscodePanVolumeToGrid(RoutingBox box)
        {
            MidiOptions opt = box.GetOptions();

            //dimension = 768 / 768
            //margin 0 = 64 
            //
            int volPos = opt.CC_Volume_Value;
            int panPos = opt.CC_Pan_Value;

            double originalMaxV = 300.0;
            double destinationMaxV = 127.0;

            double ratioV = destinationMaxV / originalMaxV;
            volPos = Convert.ToInt32(((destinationMaxV - volPos) / ratioV) - 130);

            double originalMaxP = 330.0;
            double destinationMaxP = 64.0;

            double ratioP = destinationMaxP / originalMaxP;
            panPos = Convert.ToInt32(-(panPos - 64) / ratioP);

            return new int[2] { volPos, panPos };
        }

        private int FromYToVolume(double dVertical)
        {
            double originalMax = 300.0;
            double destinationMax = 127.0;

            double ratio = destinationMax / originalMax;
            return Convert.ToInt32((destinationMax - (dVertical * ratio)) - 55);
        }

        private int FromXToPan(double dHorizontal)
        {
            double originalMax = 330.0;
            double destinationMax = 64.0;

            double ratio = destinationMax / originalMax;
            int iConvert = Convert.ToInt32(-(dHorizontal * ratio) + 64);

            return iConvert;
        }
    }
}
