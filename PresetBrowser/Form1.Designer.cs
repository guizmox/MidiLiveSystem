
namespace PresetBrowser
{
    partial class Form1
    {
        /// <summary>
        /// Variable nécessaire au concepteur.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Nettoyage des ressources utilisées.
        /// </summary>
        /// <param name="disposing">true si les ressources managées doivent être supprimées ; sinon, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Code généré par le Concepteur Windows Form

        /// <summary>
        /// Méthode requise pour la prise en charge du concepteur - ne modifiez pas
        /// le contenu de cette méthode avec l'éditeur de code.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.BtnOpenCubaseFile = new System.Windows.Forms.Button();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.tVPresets = new System.Windows.Forms.TreeView();
            this.lbProgram = new System.Windows.Forms.Label();
            this.cbMidiIN = new System.Windows.Forms.ComboBox();
            this.cbMidiOUT = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.rtbMonitor = new System.Windows.Forms.RichTextBox();
            this.ckSortByBank = new System.Windows.Forms.CheckBox();
            this.tbChannelMidiIN = new System.Windows.Forms.TextBox();
            this.tbChannelMidiOUT = new System.Windows.Forms.TextBox();
            this.pnlKeys = new System.Windows.Forms.Panel();
            this.label3 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.tbVelocity = new System.Windows.Forms.TextBox();
            this.btnOctaveMoins = new System.Windows.Forms.Button();
            this.btnOctavePlus = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.tbDevice = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.lbMidiInfo = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // BtnOpenCubaseFile
            // 
            this.BtnOpenCubaseFile.Location = new System.Drawing.Point(14, 14);
            this.BtnOpenCubaseFile.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.BtnOpenCubaseFile.Name = "BtnOpenCubaseFile";
            this.BtnOpenCubaseFile.Size = new System.Drawing.Size(154, 27);
            this.BtnOpenCubaseFile.TabIndex = 0;
            this.BtnOpenCubaseFile.Text = "Open Cubase Instr. File";
            this.BtnOpenCubaseFile.UseVisualStyleBackColor = true;
            this.BtnOpenCubaseFile.Click += new System.EventHandler(this.BtnOpenCubaseFile_Click);
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            // 
            // tVPresets
            // 
            this.tVPresets.Location = new System.Drawing.Point(14, 47);
            this.tVPresets.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.tVPresets.Name = "tVPresets";
            this.tVPresets.Size = new System.Drawing.Size(327, 528);
            this.tVPresets.TabIndex = 1;
            this.tVPresets.NodeMouseClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.tVPresets_NodeMouseClick);
            this.tVPresets.KeyUp += new System.Windows.Forms.KeyEventHandler(this.tVPresets_KeyUp);
            // 
            // lbProgram
            // 
            this.lbProgram.AutoSize = true;
            this.lbProgram.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lbProgram.Location = new System.Drawing.Point(349, 47);
            this.lbProgram.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lbProgram.Name = "lbProgram";
            this.lbProgram.Size = new System.Drawing.Size(128, 16);
            this.lbProgram.TabIndex = 2;
            this.lbProgram.Text = "PROGRAM DATA";
            // 
            // cbMidiIN
            // 
            this.cbMidiIN.FormattingEnabled = true;
            this.cbMidiIN.Location = new System.Drawing.Point(420, 82);
            this.cbMidiIN.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.cbMidiIN.Name = "cbMidiIN";
            this.cbMidiIN.Size = new System.Drawing.Size(194, 23);
            this.cbMidiIN.TabIndex = 3;
            this.cbMidiIN.SelectedIndexChanged += new System.EventHandler(this.cbMidiIN_SelectedIndexChanged);
            // 
            // cbMidiOUT
            // 
            this.cbMidiOUT.FormattingEnabled = true;
            this.cbMidiOUT.Location = new System.Drawing.Point(420, 113);
            this.cbMidiOUT.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.cbMidiOUT.Name = "cbMidiOUT";
            this.cbMidiOUT.Size = new System.Drawing.Size(194, 23);
            this.cbMidiOUT.TabIndex = 4;
            this.cbMidiOUT.SelectedIndexChanged += new System.EventHandler(this.cbMidiOUT_SelectedIndexChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(349, 85);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(47, 15);
            this.label1.TabIndex = 5;
            this.label1.Text = "MIDI IN";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(348, 117);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(58, 15);
            this.label2.TabIndex = 6;
            this.label2.Text = "MIDI OUT";
            // 
            // rtbMonitor
            // 
            this.rtbMonitor.Location = new System.Drawing.Point(420, 144);
            this.rtbMonitor.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.rtbMonitor.Name = "rtbMonitor";
            this.rtbMonitor.Size = new System.Drawing.Size(514, 431);
            this.rtbMonitor.TabIndex = 7;
            this.rtbMonitor.Text = "";
            // 
            // ckSortByBank
            // 
            this.ckSortByBank.AutoSize = true;
            this.ckSortByBank.Location = new System.Drawing.Point(175, 18);
            this.ckSortByBank.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.ckSortByBank.Name = "ckSortByBank";
            this.ckSortByBank.Size = new System.Drawing.Size(92, 19);
            this.ckSortByBank.TabIndex = 8;
            this.ckSortByBank.Text = "Sort by Bank";
            this.ckSortByBank.UseVisualStyleBackColor = true;
            this.ckSortByBank.CheckedChanged += new System.EventHandler(this.ckSortByBank_CheckedChanged);
            // 
            // tbChannelMidiIN
            // 
            this.tbChannelMidiIN.Location = new System.Drawing.Point(622, 82);
            this.tbChannelMidiIN.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.tbChannelMidiIN.MaxLength = 2;
            this.tbChannelMidiIN.Name = "tbChannelMidiIN";
            this.tbChannelMidiIN.Size = new System.Drawing.Size(58, 23);
            this.tbChannelMidiIN.TabIndex = 9;
            this.tbChannelMidiIN.Text = "1";
            this.tbChannelMidiIN.TextChanged += new System.EventHandler(this.tbChannelMidiIN_TextChanged);
            // 
            // tbChannelMidiOUT
            // 
            this.tbChannelMidiOUT.Location = new System.Drawing.Point(622, 113);
            this.tbChannelMidiOUT.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.tbChannelMidiOUT.MaxLength = 2;
            this.tbChannelMidiOUT.Name = "tbChannelMidiOUT";
            this.tbChannelMidiOUT.Size = new System.Drawing.Size(58, 23);
            this.tbChannelMidiOUT.TabIndex = 10;
            this.tbChannelMidiOUT.Text = "1";
            this.tbChannelMidiOUT.TextChanged += new System.EventHandler(this.tbChannelMidiOUT_TextChanged);
            // 
            // pnlKeys
            // 
            this.pnlKeys.Location = new System.Drawing.Point(186, 583);
            this.pnlKeys.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.pnlKeys.Name = "pnlKeys";
            this.pnlKeys.Size = new System.Drawing.Size(749, 130);
            this.pnlKeys.TabIndex = 11;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(14, 594);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(54, 15);
            this.label3.TabIndex = 12;
            this.label3.Text = "Velocity :";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(14, 617);
            this.label5.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(50, 15);
            this.label5.TabIndex = 14;
            this.label5.Text = "Octave :";
            // 
            // tbVelocity
            // 
            this.tbVelocity.Location = new System.Drawing.Point(77, 591);
            this.tbVelocity.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.tbVelocity.MaxLength = 3;
            this.tbVelocity.Name = "tbVelocity";
            this.tbVelocity.Size = new System.Drawing.Size(58, 23);
            this.tbVelocity.TabIndex = 16;
            this.tbVelocity.Text = "64";
            this.tbVelocity.TextChanged += new System.EventHandler(this.tbVelocity_TextChanged);
            // 
            // btnOctaveMoins
            // 
            this.btnOctaveMoins.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.btnOctaveMoins.Location = new System.Drawing.Point(77, 617);
            this.btnOctaveMoins.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.btnOctaveMoins.Name = "btnOctaveMoins";
            this.btnOctaveMoins.Size = new System.Drawing.Size(29, 45);
            this.btnOctaveMoins.TabIndex = 17;
            this.btnOctaveMoins.Text = "-";
            this.btnOctaveMoins.UseVisualStyleBackColor = true;
            this.btnOctaveMoins.Click += new System.EventHandler(this.btnOctaveMoins_Click);
            // 
            // btnOctavePlus
            // 
            this.btnOctavePlus.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.btnOctavePlus.Location = new System.Drawing.Point(106, 617);
            this.btnOctavePlus.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.btnOctavePlus.Name = "btnOctavePlus";
            this.btnOctavePlus.Size = new System.Drawing.Size(29, 45);
            this.btnOctavePlus.TabIndex = 18;
            this.btnOctavePlus.Text = "+";
            this.btnOctavePlus.UseVisualStyleBackColor = true;
            this.btnOctavePlus.Click += new System.EventHandler(this.btnOctavePlus_Click);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(348, 148);
            this.label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(60, 15);
            this.label4.TabIndex = 19;
            this.label4.Text = "MONITOR";
            // 
            // tbDevice
            // 
            this.tbDevice.AutoSize = true;
            this.tbDevice.Location = new System.Drawing.Point(768, 14);
            this.tbDevice.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.tbDevice.Name = "tbDevice";
            this.tbDevice.Size = new System.Drawing.Size(45, 15);
            this.tbDevice.TabIndex = 20;
            this.tbDevice.Text = "DEVICE";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(643, 717);
            this.label6.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(285, 15);
            this.label6.TabIndex = 21;
            this.label6.Text = "Guillaume Tristant (guizmox@hotmail.com) - 180224";
            // 
            // lbMidiInfo
            // 
            this.lbMidiInfo.AutoSize = true;
            this.lbMidiInfo.Location = new System.Drawing.Point(10, 714);
            this.lbMidiInfo.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lbMidiInfo.Name = "lbMidiInfo";
            this.lbMidiInfo.Size = new System.Drawing.Size(0, 15);
            this.lbMidiInfo.TabIndex = 22;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(948, 740);
            this.Controls.Add(this.lbMidiInfo);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.tbDevice);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.btnOctavePlus);
            this.Controls.Add(this.btnOctaveMoins);
            this.Controls.Add(this.tbVelocity);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.pnlKeys);
            this.Controls.Add(this.tbChannelMidiOUT);
            this.Controls.Add(this.tbChannelMidiIN);
            this.Controls.Add(this.ckSortByBank);
            this.Controls.Add(this.rtbMonitor);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.cbMidiOUT);
            this.Controls.Add(this.cbMidiIN);
            this.Controls.Add(this.lbProgram);
            this.Controls.Add(this.tVPresets);
            this.Controls.Add(this.BtnOpenCubaseFile);
            this.DoubleBuffered = true;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.MaximizeBox = false;
            this.Name = "Form1";
            this.Text = "MIDI Preset Tester";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Form1_FormClosed);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button BtnOpenCubaseFile;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.TreeView tVPresets;
        private System.Windows.Forms.Label lbProgram;
        private System.Windows.Forms.ComboBox cbMidiIN;
        private System.Windows.Forms.ComboBox cbMidiOUT;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.RichTextBox rtbMonitor;
        private System.Windows.Forms.CheckBox ckSortByBank;
        private System.Windows.Forms.TextBox tbChannelMidiIN;
        private System.Windows.Forms.TextBox tbChannelMidiOUT;
        private System.Windows.Forms.Panel pnlKeys;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox tbVelocity;
        private System.Windows.Forms.Button btnOctaveMoins;
        private System.Windows.Forms.Button btnOctavePlus;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label tbDevice;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label lbMidiInfo;
    }
}

