using MaterialDesignColors.ColorManipulation;
using MidiTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Linq;

namespace MidiLiveSystem
{
    /// <summary>
    /// Logique d'interaction pour PresetBrowser.xaml
    /// </summary>
    public partial class PresetBrowser : Window
    {

        public delegate void BrowserPresetChanged(MidiPreset mp);
        public event BrowserPresetChanged OnPresetChanged;

        private InstrumentData InstrumentPresets;

        public PresetBrowser(InstrumentData instr)
        {
            InitializeComponent();

            InstrumentPresets = instr;

            if (instr != null)
            {
                PopulateHierarchyTree(instr);
            }

            lblCaption.Content = "Preset Browser";
        }

        private void tvPresets_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TreeViewItem tvi = (TreeViewItem)e.NewValue;
            if (tvi != null)
            {
                if (!tvi.Tag.ToString().Equals("")) //c'est une catégorie
                {
                    string idx = tvi.Tag.ToString();
                    ChangePreset(idx);
                }
            }
        }

        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterTreeView(tbFilterTextBox.Text);
        }

        private void PopulateHierarchyTree(InstrumentData instr)
        {
            tvPresets.Items.Clear();

            string sL1 = "";
            string sL2 = "";
            string sL3 = "";
            string sL4 = "";

            foreach (var g in instr.Categories)
            {

                TreeViewItem categoryItem = new TreeViewItem();
                categoryItem.Header = g.Category;
                categoryItem.Tag = "";

                //if (g.Category.Equals("Orchestral Woodwinds"))
                //{

                //}

                if (g.Presets.Count > 0)
                {
                    foreach (var p in g.Presets)
                    {
                        TreeViewItem presetItem = new TreeViewItem();
                        presetItem.Header = p.PresetName;
                        presetItem.Tag = p.Id;
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

        private void ChangePreset(string idx)
        {
            if (idx.Length > 0)
            {
                MidiPreset mp = InstrumentPresets.GetPreset(idx);
                lblPresetInfo.Content = Path.GetFileName(InstrumentPresets.CubaseFile);
                tbMsb.Text = mp.Msb.ToString();
                tbLsb.Text = mp.Lsb.ToString();
                tbPrg.Text = mp.Prg.ToString();
                tbName.Text = mp.PresetName.ToString();
                OnPresetChanged?.Invoke(mp);
            }
        }

        private void FilterTreeView(string filterText)
        {
            if (string.IsNullOrWhiteSpace(filterText))
            {
                PopulateHierarchyTree(InstrumentPresets);
            }
            else
            {
                var filter = new InstrumentData();
                filter.CubaseFile = InstrumentPresets.CubaseFile;
                filter.Device = InstrumentPresets.Device;
                foreach (PresetHierarchy category in InstrumentPresets.Categories)
                {
                    PresetHierarchy cat = new PresetHierarchy();
                    cat.IndexInFile = category.IndexInFile;
                    cat.Category = category.Category;
                    cat.Level = 1;
                    cat.Raw = category.Raw;
                    cat.Presets = new List<MidiPreset>();

                    foreach (var preset in category.Presets)
                    {
                        if (preset.PresetName.Contains(filterText.Trim(), StringComparison.InvariantCultureIgnoreCase))
                        {
                            MidiPreset p = new MidiPreset();
                            p.PresetName = preset.PresetName;
                            p.Prg = preset.Prg;
                            p.Lsb = preset.Lsb;
                            p.Msb = preset.Msb;
                            p.InstrumentGroup = preset.InstrumentGroup;
                            p.Channel = preset.Channel;
                   
                            cat.Presets.Add(p);
                        }
                    }

                    if (cat.Presets.Count > 0) 
                    {
                        filter.Categories.Add(cat);
                    }
                }
                PopulateHierarchyTree(filter);
            }
        }
    }
}
