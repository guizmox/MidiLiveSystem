using MidiTools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
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
using System.Windows.Shapes;

namespace MidiLiveSystem
{
    /// <summary>
    /// Logique d'interaction pour RecallButtons.xaml
    /// </summary>
    public partial class RecallButtons : Window
    {
        List<RoutingBox> Boxes;
        ProjectConfiguration Project;

        public RecallButtons(List<RoutingBox> boxes, ProjectConfiguration project)
        {
            Boxes = boxes;
            Project = project;
            InitializeComponent();
            InitPage();
            GetRecallFromProject();

        }

        private void InitPage()
        {
            cbRecallSet1.Items.Clear();
            cbRecallSet2.Items.Clear();
            cbRecallSet3.Items.Clear();
            cbRecallSet4.Items.Clear();
            cbRecallSet5.Items.Clear();
            cbRecallSet6.Items.Clear();
            cbRecallSet7.Items.Clear();
            cbRecallSet8.Items.Clear();

            foreach (var box in Boxes)
            {
                cbRecallSet1.Items.Add(new ComboBoxCustomItem() { Id = box.BoxGuid.ToString(), Description = box.BoxName + " - Preset : ", Value = "0" });
                cbRecallSet2.Items.Add(new ComboBoxCustomItem() { Id = box.BoxGuid.ToString(), Description = box.BoxName + " - Preset : ", Value = "0" });
                cbRecallSet3.Items.Add(new ComboBoxCustomItem() { Id = box.BoxGuid.ToString(), Description = box.BoxName + " - Preset : ", Value = "0" });
                cbRecallSet4.Items.Add(new ComboBoxCustomItem() { Id = box.BoxGuid.ToString(), Description = box.BoxName + " - Preset : ", Value = "0" });
                cbRecallSet5.Items.Add(new ComboBoxCustomItem() { Id = box.BoxGuid.ToString(), Description = box.BoxName + " - Preset : ", Value = "0" });
                cbRecallSet6.Items.Add(new ComboBoxCustomItem() { Id = box.BoxGuid.ToString(), Description = box.BoxName + " - Preset : ", Value = "0" });
                cbRecallSet7.Items.Add(new ComboBoxCustomItem() { Id = box.BoxGuid.ToString(), Description = box.BoxName + " - Preset : ", Value = "0" });
                cbRecallSet8.Items.Add(new ComboBoxCustomItem() { Id = box.BoxGuid.ToString(), Description = box.BoxName + " - Preset : ", Value = "0" });
            }

            cbRecallSet1.SelectedIndex = -1;
            cbRecallSet2.SelectedIndex = -1;
            cbRecallSet3.SelectedIndex = -1;
            cbRecallSet4.SelectedIndex = -1;
            cbRecallSet5.SelectedIndex = -1;
            cbRecallSet6.SelectedIndex = -1;
            cbRecallSet7.SelectedIndex = -1;
            cbRecallSet8.SelectedIndex = -1;
        }

        private void GetRecallFromProject()
        {
            foreach (var item in Project.RecallData)
            {
                ComboBox cbPreset = new ComboBox();
                switch (item.ButtonIndex)
                {
                    case 1:
                        cbPreset = cbRecallSet1;
                        break;
                    case 2:
                        cbPreset = cbRecallSet2;
                        break;
                    case 3:
                        cbPreset = cbRecallSet3;
                        break;
                    case 4:
                        cbPreset = cbRecallSet4;
                        break;
                    case 5:
                        cbPreset = cbRecallSet5;
                        break;
                    case 6:
                        cbPreset = cbRecallSet6;
                        break;
                    case 7:
                        cbPreset = cbRecallSet7;
                        break;
                    case 8:
                        cbPreset = cbRecallSet8;
                        break;
                }

                foreach (ComboBoxCustomItem cbitem in cbPreset.Items)
                {
                    string preset = "";

                    for (int i = 0; i < item.BoxGuids.Count; i++)
                    {
                        if (cbitem.Id.Equals(item.BoxGuids[i].ToString()))
                        {
                            preset = item.BoxPresets[i].ToString();
                            break;
                        }
                    }

                    if (preset.Length > 0)
                    {
                        cbitem.Value = preset;
                    }
                }
            }
        }

        private async void btnPreset_Click(object sender, RoutedEventArgs e)
        {
            Button btnPreset = (sender) as Button;

            await PresetChange(btnPreset);
        }

        private async Task PresetChange(Button btnPreset)
        {
            ComboBox cbPreset = new ComboBox();

            await btnPreset.Dispatcher.InvokeAsync(() =>
            {
                switch (btnPreset.Name)
                {
                    case "btnRecall1":
                        cbPreset = cbRecallSet1;
                        break;
                    case "btnRecall2":
                        cbPreset = cbRecallSet2;
                        break;
                    case "btnRecall3":
                        cbPreset = cbRecallSet3;
                        break;
                    case "btnRecall4":
                        cbPreset = cbRecallSet4;
                        break;
                    case "btnRecall5":
                        cbPreset = cbRecallSet5;
                        break;
                    case "btnRecall6":
                        cbPreset = cbRecallSet6;
                        break;
                    case "btnRecall7":
                        cbPreset = cbRecallSet7;
                        break;
                    case "btnRecall8":
                        cbPreset = cbRecallSet8;
                        break;
                }

                switch (btnPreset.Name)
                {
                    case "btnRecall1":
                        btnRecall1.Background = Brushes.IndianRed;
                        btnRecall2.Background = Brushes.CadetBlue;
                        btnRecall3.Background = Brushes.CadetBlue;
                        btnRecall4.Background = Brushes.CadetBlue;
                        btnRecall5.Background = Brushes.CadetBlue;
                        btnRecall6.Background = Brushes.CadetBlue;
                        btnRecall7.Background = Brushes.CadetBlue;
                        btnRecall8.Background = Brushes.CadetBlue;
                        break;
                    case "btnRecall2":
                        btnRecall1.Background = Brushes.CadetBlue;
                        btnRecall2.Background = Brushes.IndianRed;
                        btnRecall3.Background = Brushes.CadetBlue;
                        btnRecall4.Background = Brushes.CadetBlue;
                        btnRecall5.Background = Brushes.CadetBlue;
                        btnRecall6.Background = Brushes.CadetBlue;
                        btnRecall7.Background = Brushes.CadetBlue;
                        btnRecall8.Background = Brushes.CadetBlue;
                        break;
                    case "btnRecall3":
                        btnRecall1.Background = Brushes.CadetBlue;
                        btnRecall2.Background = Brushes.CadetBlue;
                        btnRecall3.Background = Brushes.IndianRed;
                        btnRecall4.Background = Brushes.CadetBlue;
                        btnRecall5.Background = Brushes.CadetBlue;
                        btnRecall6.Background = Brushes.CadetBlue;
                        btnRecall7.Background = Brushes.CadetBlue;
                        btnRecall8.Background = Brushes.CadetBlue;
                        break;
                    case "btnRecall4":
                        btnRecall1.Background = Brushes.CadetBlue;
                        btnRecall2.Background = Brushes.CadetBlue;
                        btnRecall3.Background = Brushes.CadetBlue;
                        btnRecall4.Background = Brushes.IndianRed;
                        btnRecall5.Background = Brushes.CadetBlue;
                        btnRecall6.Background = Brushes.CadetBlue;
                        btnRecall7.Background = Brushes.CadetBlue;
                        btnRecall8.Background = Brushes.CadetBlue;
                        break;
                    case "btnRecall5":
                        btnRecall1.Background = Brushes.CadetBlue;
                        btnRecall2.Background = Brushes.CadetBlue;
                        btnRecall3.Background = Brushes.CadetBlue;
                        btnRecall4.Background = Brushes.CadetBlue;
                        btnRecall5.Background = Brushes.IndianRed;
                        btnRecall6.Background = Brushes.CadetBlue;
                        btnRecall7.Background = Brushes.CadetBlue;
                        btnRecall8.Background = Brushes.CadetBlue;
                        break;
                    case "btnRecall6":
                        btnRecall1.Background = Brushes.CadetBlue;
                        btnRecall2.Background = Brushes.CadetBlue;
                        btnRecall3.Background = Brushes.CadetBlue;
                        btnRecall4.Background = Brushes.CadetBlue;
                        btnRecall5.Background = Brushes.CadetBlue;
                        btnRecall6.Background = Brushes.IndianRed;
                        btnRecall7.Background = Brushes.CadetBlue;
                        btnRecall8.Background = Brushes.CadetBlue;
                        break;
                    case "btnRecall7":
                        btnRecall1.Background = Brushes.CadetBlue;
                        btnRecall2.Background = Brushes.CadetBlue;
                        btnRecall3.Background = Brushes.CadetBlue;
                        btnRecall4.Background = Brushes.CadetBlue;
                        btnRecall5.Background = Brushes.CadetBlue;
                        btnRecall6.Background = Brushes.CadetBlue;
                        btnRecall7.Background = Brushes.IndianRed;
                        btnRecall8.Background = Brushes.CadetBlue;
                        break;
                    case "btnRecall8":
                        btnRecall1.Background = Brushes.CadetBlue;
                        btnRecall2.Background = Brushes.CadetBlue;
                        btnRecall3.Background = Brushes.CadetBlue;
                        btnRecall4.Background = Brushes.CadetBlue;
                        btnRecall5.Background = Brushes.CadetBlue;
                        btnRecall6.Background = Brushes.CadetBlue;
                        btnRecall7.Background = Brushes.CadetBlue;
                        btnRecall8.Background = Brushes.IndianRed;
                        break;
                }
            });

            List<Task> tasks = new List<Task>();

            for (int i = 0; i < cbPreset.Items.Count; i++)
            {
                var item = (ComboBoxCustomItem)cbPreset.Items[i];
                tasks.Add(EventPool.AddTask(async () => await ProcessBox(item)));
            }

            await Task.WhenAll(tasks);

        }

        private async Task ProcessBox(ComboBoxCustomItem cb)
        {
            var box = Boxes.FirstOrDefault(b => b.BoxGuid.ToString().Equals(cb.Id));
            if (box != null)
            {
                int iPreset = 0;
                if (int.TryParse(cb.Value, out iPreset))
                {
                    if (iPreset > 0)
                    {
                        await box.ChangePreset(iPreset);
                    }
                }
            }
        }

        public void SaveRecallsToProject()
        {
            Project.RecallData.Clear();

            for (int i = 1; i <= 8; i++)
            {
                ComboBox cb = new ComboBox();
                TextBox txt = new TextBox();

                switch (i)
                {
                    case 1:
                        cb = cbRecallSet1;
                        txt.Text = "RECALL 1";
                        break;
                    case 2:
                        cb = cbRecallSet2;
                        txt.Text = "RECALL 2";
                        break;
                    case 3:
                        cb = cbRecallSet3;
                        txt.Text = "RECALL 3";
                        break;
                    case 4:
                        cb = cbRecallSet4;
                        txt.Text = "RECALL 4";
                        break;
                    case 5:
                        cb = cbRecallSet5;
                        txt.Text = "RECALL 5";
                        break;
                    case 6:
                        cb = cbRecallSet6;
                        txt.Text = "RECALL 6";
                        break;
                    case 7:
                        cb = cbRecallSet7;
                        txt.Text = "RECALL 7";
                        break;
                    case 8:
                        cb = cbRecallSet8;
                        txt.Text = "RECALL 8";
                        break;
                }

                List<Guid> boxguids = new List<Guid>();
                List<int> boxpresets = new List<int>();

                foreach (var item in cb.Items)
                {
                    ComboBoxCustomItem cbBox = (ComboBoxCustomItem)item;

                    var box = Boxes.FirstOrDefault(b => b.BoxGuid.ToString().Equals(cbBox.Id));
                    if (box != null)
                    {
                        int iPreset = 0;

                        if (int.TryParse(cbBox.Value, out iPreset))
                        {
                            boxguids.Add(box.BoxGuid);
                            boxpresets.Add(iPreset);
                        }
                    }
                }

                Project.RecallData.Add(new ProjectConfiguration.RecallConfiguration(boxguids, boxpresets, txt.Text, i));
            }
        }

        internal async Task SetButton(bool bNote, int iInitialValue, int iValue)
        {
            if (bNote)
            {
                if (iValue == iInitialValue) { await PresetChange(btnRecall1); }
                else if (iValue == iInitialValue + 1) { await PresetChange(btnRecall2); }
                else if (iValue == iInitialValue + 2) { await PresetChange(btnRecall3); }
                else if (iValue == iInitialValue + 3) { await PresetChange(btnRecall4); }
                else if (iValue == iInitialValue + 4) { await PresetChange(btnRecall5); }
                else if (iValue == iInitialValue + 5) { await PresetChange(btnRecall6); }
                else if (iValue == iInitialValue + 6) { await PresetChange(btnRecall7); }
                else if (iValue == iInitialValue + 7) { await PresetChange(btnRecall8); }
            }
            else
            {
                if (iValue == 0) { await PresetChange(btnRecall1); }
                else if (iValue == 1) { await PresetChange(btnRecall2); }
                else if (iValue == 2) { await PresetChange(btnRecall3); }
                else if (iValue == 3) { await PresetChange(btnRecall4); }
                else if (iValue == 4) { await PresetChange(btnRecall5); }
                else if (iValue == 5) { await PresetChange(btnRecall6); }
                else if (iValue == 6) { await PresetChange(btnRecall7); }
                else if (iValue == 7) { await PresetChange(btnRecall8); }
            }
        }

        private void btnSaveUIState_Click(object sender, RoutedEventArgs e)
        {
            string sName = ((Button)sender).Name;

            ComboBox cb = null;

            switch (sName)
            {
                case "btnSaveUIState1":
                    cb = cbRecallSet1;
                    break;
                case "btnSaveUIState2":
                    cb = cbRecallSet2;
                    break;
                case "btnSaveUIState3":
                    cb = cbRecallSet3;
                    break;
                case "btnSaveUIState4":
                    cb = cbRecallSet4;
                    break;
                case "btnSaveUIState5":
                    cb = cbRecallSet5;
                    break;
                case "btnSaveUIState6":
                    cb = cbRecallSet6;
                    break;
                case "btnSaveUIState7":
                    cb = cbRecallSet7;
                    break;
                case "btnSaveUIState8":
                    cb = cbRecallSet8;
                    break;
            }

            ComboBoxCustomItem cbi = null;

            int iBoxesCount = Boxes.Count;
            for (int i = 0; i < iBoxesCount; i++)
            {
                cbi = (ComboBoxCustomItem)cb.Items[i];
                cbi.Value = (Boxes[i].CurrentPreset + 1).ToString();
            }
        }

        internal async Task UpdateBoxes()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                InitPage();
                GetRecallFromProject();
            });
        }
    }
}
