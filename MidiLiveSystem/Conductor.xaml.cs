using MidiTools;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MidiLiveSystem
{
    /// <summary>
    /// Logique d'interaction pour Conductor.xaml
    /// </summary>
    public partial class Conductor : Window
    {
        private readonly List<RoutingBox> Boxes = new();
        private readonly MidiRouting Routing = null;

        private readonly List<bool> _isMoving = new();
        private readonly List<Point> _buttonPosition = new();
        private readonly List<double> deltaX = new();
        private readonly List<double> deltaY = new();
        private readonly List<TranslateTransform> _currentTT = new();

        public Conductor(List<RoutingBox> boxes, MidiRouting routing)
        {
            InitializeComponent();

            Boxes = boxes;
            Routing = routing;

            Task.Run(() => InitStage());
        }

        public async Task InitStage()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                gdButtons.Children.Clear();
                _buttonPosition.Clear();
                _isMoving.Clear();
                deltaX.Clear();
                deltaY.Clear();
                _currentTT.Clear();

                for (int i = 0; i < Boxes.Count; i++)
                {
                    _buttonPosition.Add(new Point());
                    _isMoving.Add(false);
                    deltaX.Add(0);
                    deltaY.Add(0);
                    _currentTT.Add(new TranslateTransform());

                    InitializeRoutingBoxButton(Boxes[i], i);
                }
            });
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RoutingBoxButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            ((Button)sender).Background = Brushes.DarkOrange;

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
            ((Button)sender).Background = Brushes.DarkBlue;

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

            if (offsetX > 425)
            {
                offsetX = 425;
            }
            if (offsetX < -425)
            {
                offsetX = -425;
            }
            if (offsetY < -180)
            {
                offsetY = -180;
            }
            if (offsetY > 230)
            {
                offsetY = 230;
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

                ((Button)sender).Content = new TextBlock() { Foreground = Brushes.Yellow, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Text = NameButtonBox(Boxes[iIndex], iVol, iPan), FontSize = 8, TextWrapping = TextWrapping.Wrap };

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

        private static string NameButtonBox(RoutingBox box, int iVol, int iPan)
        {
            return string.Concat(box.BoxName, Environment.NewLine, box.PresetName, Environment.NewLine, "VOL=", iVol, " - PAN=", iPan);
        }

        private void SetBoxData(int iVol, int iPan, int iReverb, int iCutOff, int iAttack, RoutingBox box)
        {
            box.SetInitCC(new int[] { 7, iVol });
            box.SetInitCC(new int[] { 10, iPan });

            if (ckReverb.IsChecked.Value)
            {
                box.SetInitCC(new int[] { 91, iReverb });
            }

            if (ckAttack.IsChecked.Value)
            {
                box.SetInitCC(new int[] { 73, iAttack });
            }

            if (ckCutoff.IsChecked.Value)
            {
                box.SetInitCC(new int[] { 74, iCutOff });
            }
        }

        private void InitializeRoutingBoxButton(RoutingBox box, int iIndex)
        {
            int iVol = box.GetCCValue(7);
            int iPan = box.GetCCValue(10);

            if (iVol < 0) { iVol = 100; }
            if (iPan < 0) { iPan = 64; }

            var dropShadowEffect = new DropShadowEffect
            {
                Color = Colors.Gray,
                Direction = 0,
                ShadowDepth = 0,
                Opacity = 1,
                BlurRadius = 50
            };


            var rtbButton = new Button
            {
                Name = "btn_" + iIndex,
                Width = 120,
                Height = 40,
                Foreground = Brushes.Wheat,
                Content = new TextBlock() { Foreground = Brushes.Yellow, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Text = NameButtonBox(box, iVol, iPan), FontSize = 9, TextWrapping = TextWrapping.Wrap },
                Tag = box.BoxGuid.ToString(),
                VerticalContentAlignment = VerticalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = Brushes.DarkBlue,
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.DarkOrange,
                Effect = dropShadowEffect
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

        private static int[] TranscodePanVolumeToGrid(RoutingBox box)
        {
            MidiOptions opt = box.GetCurrentPreset().MidiOptions;

            //dimension = 768 / 768
            //margin 0 = 64 
            //
            int volPos = opt.CC_Volume_Value;
            int panPos = opt.CC_Pan_Value;

            double originalMaxV = 410.0;
            double destinationMaxV = 127.0;

            double ratioV = destinationMaxV / originalMaxV;
            volPos = Convert.ToInt32(((destinationMaxV - volPos) / ratioV) - 180);

            double originalMaxP = 430.0;
            double destinationMaxP = 64.0;

            double ratioP = destinationMaxP / originalMaxP;
            panPos = Convert.ToInt32(-(panPos - 64) / ratioP);

            return new int[2] { volPos, panPos };
        }

        private static int FromYToVolume(double dVertical)
        {
            double originalMax = 410.0;
            double destinationMax = 127.0;

            double ratio = destinationMax / originalMax;
            return Convert.ToInt32((destinationMax - (dVertical * ratio)) - 56);
        }

        private static int FromXToPan(double dHorizontal)
        {
            double originalMax = 430.0;
            double destinationMax = 64.0;

            double ratio = destinationMax / originalMax;
            int iConvert = Convert.ToInt32(-(dHorizontal * ratio) + 64);

            return iConvert;
        }

        private void ckAddLifeToProject_Click(object sender, RoutedEventArgs e)
        {
            CheckBox ck = (CheckBox)sender;

            Routing.AddLifeToProject(ck.IsChecked.Value);
        }
    }
}
