using MidiTools;
using System;
using System.Collections.Generic;
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
    /// Logique d'interaction pour PresetBrowser.xaml
    /// </summary>
    public partial class PresetBrowser : Window
    {
        private InstrumentData Instrument;
        public string[] SelectedPreset = new string[2] { "", "" };

        public PresetBrowser(InstrumentData instr)
        {
            InitializeComponent();

            Instrument = instr;
            PopulateHierarchyTree();
        }

        private void PopulateHierarchyTree()
        {
            tvPresets.Items.Clear();

            string sL1 = "";
            string sL2 = "";
            string sL3 = "";
            string sL4 = "";

            foreach (var g in Instrument.Categories)
            {

                TreeViewItem categoryItem = new TreeViewItem();
                categoryItem.Header = g.Category;
                categoryItem.Tag = "";

                if (g.Presets.Count > 0)
                {
                    foreach (var p in g.Presets)
                    {
                        TreeViewItem presetItem = new TreeViewItem();
                        presetItem.Header = p.PresetName;
                        presetItem.Tag = p.Id; // Stockez des données supplémentaires dans la propriété Tag si nécessaire
                        categoryItem.Items.Add(presetItem);
                    }
                }

                // Déterminez le niveau de la catégorie et ajoutez-la au bon parent
                switch (g.Level)
                {
                    case 1:
                        sL1 = g.Category;
                        sL2 = "";
                        sL3 = "";
                        sL4 = "";
                        tvPresets.Items.Add(categoryItem);
                        break;
                    case 2:
                        sL2 = g.Category;
                        sL3 = "";
                        sL4 = "";
                        FindParentNode(tvPresets, sL1).Items.Add(categoryItem);
                        break;
                    case 3:
                        sL3 = g.Category;
                        sL4 = "";
                        FindParentNode(tvPresets, sL2).Items.Add(categoryItem);
                        break;
                    case 4:
                        sL4 = g.Category;
                        FindParentNode(tvPresets, sL3).Items.Add(categoryItem);
                        break;
                    case 5:
                        FindParentNode(tvPresets, sL4).Items.Add(categoryItem);
                        break;
                        // Ajoutez d'autres cas selon votre hiérarchie
                }
            }
        }

        private TreeViewItem FindParentNode(ItemsControl parent, string categoryName)
        {
            foreach (var item in parent.Items)
            {
                TreeViewItem treeItem = item as TreeViewItem;
                if (treeItem.Header.ToString() == categoryName)
                {
                    return treeItem;
                }
                else
                {
                    var result = FindParentNode(treeItem, categoryName);
                    if (result != null)
                        return result;
                }
            }
            return null;
        }


        private void tvPresets_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TreeViewItem tvi = (TreeViewItem)e.NewValue;
            if (!tvi.Tag.ToString().Equals("")) //c'est une catégorie
            {
                string idx = tvi.Tag.ToString();
                ChangePreset(idx);
            }
        }

        private void ChangePreset(string idx)
        {
            if (idx.Length > 0)
            {
                MidiPreset mp = Instrument.GetPreset(idx);
                SelectedPreset = new string[2] { mp.PresetName, mp.TechName };
                Close();
            }
        }
    }
}
