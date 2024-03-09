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

        public PresetBrowser(InstrumentData instr, MidiPreset preset = null)
        {
            InitializeComponent();

            InstrumentPresets = instr;

            if (instr != null)
            {
                PopulateHierarchyTree(instr);
            }

            lblCaption.Content = "Preset Browser";

            if (preset != null && preset.PresetName.Length > 0)
            {
                tbLsb.Text = preset.Lsb.ToString();
                tbMsb.Text = preset.Msb.ToString();
                tbPrg.Text = preset.Prg.ToString();
                tbName.Text = preset.PresetName;
            }
        }

        private void tvPresets_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TreeViewItem tvi = (TreeViewItem)e.NewValue;
            if (tvi != null)
            {
                if (tvi.Tag.ToString().IndexOf("-") > -1) //c'est une catégorie
                {
                    string idx = tvi.Tag.ToString();
                    ChangePreset(idx);
                }
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void tbPrg_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (InstrumentPresets != null)
            {
                if (tbPrg != null && tbMsb != null & tbLsb != null)
                {
                    if (tbPrg.IsFocused || tbMsb.IsFocused || tbLsb.IsFocused)
                    {
                        FilterTreeViewByPrg(tbPrg.Text.Trim(), tbMsb.Text.Trim(), tbLsb.Text.Trim());
                    }
                }
            }
        }

        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterTreeViewByString(tbFilterTextBox.Text);
        }

        private void PopulateHierarchyTree(InstrumentData instr)
        {
            tvPresets.Items.Clear();

            string sL1 = "";
            string sL2 = "";
            string sL3 = "";
            string sL4 = "";

            TreeViewItem catFav = new TreeViewItem();
            catFav.Header = "- FAVOURITES -";
            catFav.Tag = "0";

            foreach (var g in instr.Categories)
            {
                foreach (var p in g.Presets)
                {
                    if (p.IsFavourite)
                    {
                        TreeViewItem presetItem = new TreeViewItem();
                        presetItem.Header = p.PresetName;
                        presetItem.Tag = p.Id;
                        catFav.Items.Add(presetItem);
                    }
                }
            }
            tvPresets.Items.Add(catFav);

            foreach (var g in instr.Categories)
            {

                TreeViewItem categoryItem = new TreeViewItem();
                categoryItem.Header = g.Category;
                categoryItem.Tag = g.IndexInFile.ToString();

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
                        sL1 = g.IndexInFile.ToString();
                        sL2 = "";
                        sL3 = "";
                        sL4 = "";
                        tvPresets.Items.Add(categoryItem);
                        break;
                    case 2:
                        sL2 = g.IndexInFile.ToString();
                        sL3 = "";
                        sL4 = "";
                        FindParentNode(tvPresets, sL1).Items.Add(categoryItem);
                        break;
                    case 3:
                        sL3 = g.IndexInFile.ToString();
                        sL4 = "";
                        FindParentNode(tvPresets, sL2).Items.Add(categoryItem);
                        break;
                    case 4:
                        sL4 = g.IndexInFile.ToString();
                        FindParentNode(tvPresets, sL3).Items.Add(categoryItem);
                        break;
                    case 5:
                        FindParentNode(tvPresets, sL4).Items.Add(categoryItem);
                        break;
                        // Ajoutez d'autres cas selon votre hiérarchie
                }
            }
        }

        private TreeViewItem FindParentNode(ItemsControl parent, string categoryIndexInFile)
        {
            foreach (var item in parent.Items)
            {
                TreeViewItem treeItem = item as TreeViewItem;
                if (treeItem.Tag.ToString() == categoryIndexInFile)
                {
                    return treeItem;
                }
                else
                {
                    var result = FindParentNode(treeItem, categoryIndexInFile);
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
                ckSetFavorite.IsChecked = mp.IsFavourite;
                OnPresetChanged?.Invoke(mp);
            }
        }

        private void FilterTreeViewByPrg(string sPrg, string sMsb, string sLsb)
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
                    if ((preset.Prg.ToString().Equals(sPrg, StringComparison.InvariantCultureIgnoreCase) || sPrg.Length == 0) &&
                        (preset.Msb.ToString().Equals(sMsb, StringComparison.InvariantCultureIgnoreCase) || sMsb.Length == 0) &&
                        (preset.Lsb.ToString().Equals(sLsb, StringComparison.InvariantCultureIgnoreCase) || sLsb.Length == 0))
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

        private void FilterTreeViewByString(string filterText)
        {
            if (InstrumentPresets != null)
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

        internal void GetPreset()
        {
            MidiPreset mp = new MidiPreset();

            mp.PresetName = tbName.Text.Trim();

            int iErrors = 0;

            try
            {
                mp.Msb = Convert.ToInt32(tbMsb.Text.Trim());
            }
            catch { mp.Msb = 0; iErrors++; }
            try
            {
                mp.Lsb = Convert.ToInt32(tbLsb.Text.Trim());
            }
            catch { mp.Lsb = 0; iErrors++; }
            try
            {
                mp.Prg = Convert.ToInt32(tbPrg.Text.Trim());
            }
            catch { mp.Prg = 0; iErrors++; }

            if (iErrors == 3)
            {
                mp.PresetName = "[ERROR]";
                MessageBox.Show("Unable to parse MSB/LSB/PRG values. Default value will be used.");
            }

            OnPresetChanged?.Invoke(mp);
        }

        private void ckSetFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (InstrumentPresets != null)
            {
                if (tvPresets.SelectedValue.ToString().IndexOf("-") > -1) //c'est une catégorie
                {
                    if (!((CheckBox)sender).IsChecked.Value)
                    {
                        string idx = tvPresets.SelectedValue.ToString();
                        MidiPreset p = InstrumentPresets.GetPreset(idx);
                        p.IsFavourite = false;
                    }
                    else
                    {
                        string idx = tvPresets.SelectedValue.ToString();
                        MidiPreset p = InstrumentPresets.GetPreset(idx);
                        p.IsFavourite = true;
                    }
                    PopulateHierarchyTree(InstrumentPresets);
                }
            }
        }
    }
}
