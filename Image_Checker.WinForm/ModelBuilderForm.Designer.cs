namespace Image_Checker.WinForm
{
    partial class ModelBuilderForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.lblTitle = new System.Windows.Forms.Label();
            this.groupBoxDataset = new System.Windows.Forms.GroupBox();
            this.lblDatasetInfo = new System.Windows.Forms.Label();
            this.txtDatasetPath = new System.Windows.Forms.TextBox();
            this.btnSelectDataset = new System.Windows.Forms.Button();
            this.lblDatasetPath = new System.Windows.Forms.Label();
            this.groupBoxOutput = new System.Windows.Forms.GroupBox();
            this.txtOutputPath = new System.Windows.Forms.TextBox();
            this.btnSelectOutput = new System.Windows.Forms.Button();
            this.lblOutputPath = new System.Windows.Forms.Label();
            this.groupBoxTraining = new System.Windows.Forms.GroupBox();
            this.lblCVFolds = new System.Windows.Forms.Label();
            this.numCVFolds = new System.Windows.Forms.NumericUpDown();
            this.lblTrials = new System.Windows.Forms.Label();
            this.numTrials = new System.Windows.Forms.NumericUpDown();
            this.groupBoxModels = new System.Windows.Forms.GroupBox();
            this.chkTransferLearning = new System.Windows.Forms.CheckBox();
            this.chkLightGBM = new System.Windows.Forms.CheckBox();
            this.chkFastTree = new System.Windows.Forms.CheckBox();
            this.chkLBFGS = new System.Windows.Forms.CheckBox();
            this.chkSDCA = new System.Windows.Forms.CheckBox();
            this.btnStartTraining = new System.Windows.Forms.Button();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.groupBoxLog = new System.Windows.Forms.GroupBox();
            this.txtTrainingLog = new System.Windows.Forms.RichTextBox();
            this.groupBoxDataset.SuspendLayout();
            this.groupBoxOutput.SuspendLayout();
            this.groupBoxTraining.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numCVFolds)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numTrials)).BeginInit();
            this.groupBoxModels.SuspendLayout();
            this.groupBoxLog.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblTitle
            // 
            this.lblTitle.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this.lblTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this.lblTitle.ForeColor = System.Drawing.Color.White;
            this.lblTitle.Location = new System.Drawing.Point(0, 0);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(1000, 60);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "🤖 ML Model Builder";
            this.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // groupBoxDataset
            // 
            this.groupBoxDataset.Controls.Add(this.lblDatasetInfo);
            this.groupBoxDataset.Controls.Add(this.txtDatasetPath);
            this.groupBoxDataset.Controls.Add(this.btnSelectDataset);
            this.groupBoxDataset.Controls.Add(this.lblDatasetPath);
            this.groupBoxDataset.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.groupBoxDataset.Location = new System.Drawing.Point(20, 80);
            this.groupBoxDataset.Name = "groupBoxDataset";
            this.groupBoxDataset.Size = new System.Drawing.Size(960, 150);
            this.groupBoxDataset.TabIndex = 1;
            this.groupBoxDataset.TabStop = false;
            this.groupBoxDataset.Text = "Step 1: Select Dataset Folder";
            // 
            // lblDatasetPath
            // 
            this.lblDatasetPath.AutoSize = true;
            this.lblDatasetPath.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblDatasetPath.Location = new System.Drawing.Point(15, 30);
            this.lblDatasetPath.Name = "lblDatasetPath";
            this.lblDatasetPath.Size = new System.Drawing.Size(348, 15);
            this.lblDatasetPath.TabIndex = 0;
            this.lblDatasetPath.Text = "Select folder containing 'OK' and 'NG' subfolders with training images:";
            // 
            // btnSelectDataset
            // 
            this.btnSelectDataset.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this.btnSelectDataset.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSelectDataset.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnSelectDataset.ForeColor = System.Drawing.Color.White;
            this.btnSelectDataset.Location = new System.Drawing.Point(15, 55);
            this.btnSelectDataset.Name = "btnSelectDataset";
            this.btnSelectDataset.Size = new System.Drawing.Size(150, 35);
            this.btnSelectDataset.TabIndex = 1;
            this.btnSelectDataset.Text = "📁 Browse...";
            this.btnSelectDataset.UseVisualStyleBackColor = false;
            this.btnSelectDataset.Click += new System.EventHandler(this.BtnSelectDataset_Click);
            // 
            // txtDatasetPath
            // 
            this.txtDatasetPath.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.txtDatasetPath.Location = new System.Drawing.Point(180, 58);
            this.txtDatasetPath.Name = "txtDatasetPath";
            this.txtDatasetPath.ReadOnly = true;
            this.txtDatasetPath.Size = new System.Drawing.Size(760, 23);
            this.txtDatasetPath.TabIndex = 2;
            // 
            // lblDatasetInfo
            // 
            this.lblDatasetInfo.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblDatasetInfo.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblDatasetInfo.Location = new System.Drawing.Point(15, 100);
            this.lblDatasetInfo.Name = "lblDatasetInfo";
            this.lblDatasetInfo.Size = new System.Drawing.Size(925, 40);
            this.lblDatasetInfo.TabIndex = 3;
            this.lblDatasetInfo.Text = "No dataset selected";
            this.lblDatasetInfo.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // groupBoxOutput
            // 
            this.groupBoxOutput.Controls.Add(this.txtOutputPath);
            this.groupBoxOutput.Controls.Add(this.btnSelectOutput);
            this.groupBoxOutput.Controls.Add(this.lblOutputPath);
            this.groupBoxOutput.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.groupBoxOutput.Location = new System.Drawing.Point(20, 250);
            this.groupBoxOutput.Name = "groupBoxOutput";
            this.groupBoxOutput.Size = new System.Drawing.Size(960, 100);
            this.groupBoxOutput.TabIndex = 2;
            this.groupBoxOutput.TabStop = false;
            this.groupBoxOutput.Text = "Step 2: Select Output Folder (Optional)";
            // 
            // lblOutputPath
            // 
            this.lblOutputPath.AutoSize = true;
            this.lblOutputPath.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblOutputPath.Location = new System.Drawing.Point(15, 30);
            this.lblOutputPath.Name = "lblOutputPath";
            this.lblOutputPath.Size = new System.Drawing.Size(273, 15);
            this.lblOutputPath.TabIndex = 0;
            this.lblOutputPath.Text = "Where to save the trained model (default: dataset folder):";
            // 
            // btnSelectOutput
            // 
            this.btnSelectOutput.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(76)))), ((int)(((byte)(175)))), ((int)(((byte)(80)))));
            this.btnSelectOutput.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSelectOutput.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnSelectOutput.ForeColor = System.Drawing.Color.White;
            this.btnSelectOutput.Location = new System.Drawing.Point(15, 55);
            this.btnSelectOutput.Name = "btnSelectOutput";
            this.btnSelectOutput.Size = new System.Drawing.Size(150, 35);
            this.btnSelectOutput.TabIndex = 1;
            this.btnSelectOutput.Text = "📂 Browse...";
            this.btnSelectOutput.UseVisualStyleBackColor = false;
            this.btnSelectOutput.Click += new System.EventHandler(this.BtnSelectOutput_Click);
            // 
            // txtOutputPath
            // 
            this.txtOutputPath.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.txtOutputPath.Location = new System.Drawing.Point(180, 58);
            this.txtOutputPath.Name = "txtOutputPath";
            this.txtOutputPath.ReadOnly = true;
            this.txtOutputPath.Size = new System.Drawing.Size(760, 23);
            this.txtOutputPath.TabIndex = 2;
            // 
            // groupBoxTraining
            // 
            this.groupBoxTraining.Controls.Add(this.groupBoxModels);
            this.groupBoxTraining.Controls.Add(this.numTrials);
            this.groupBoxTraining.Controls.Add(this.lblTrials);
            this.groupBoxTraining.Controls.Add(this.numCVFolds);
            this.groupBoxTraining.Controls.Add(this.lblCVFolds);
            this.groupBoxTraining.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.groupBoxTraining.Location = new System.Drawing.Point(20, 370);
            this.groupBoxTraining.Name = "groupBoxTraining";
            this.groupBoxTraining.Size = new System.Drawing.Size(960, 140);
            this.groupBoxTraining.TabIndex = 3;
            this.groupBoxTraining.TabStop = false;
            this.groupBoxTraining.Text = "Step 3: Configure Training Parameters";
            // 
            // lblCVFolds
            // 
            this.lblCVFolds.AutoSize = true;
            this.lblCVFolds.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblCVFolds.Location = new System.Drawing.Point(15, 35);
            this.lblCVFolds.Name = "lblCVFolds";
            this.lblCVFolds.Size = new System.Drawing.Size(190, 15);
            this.lblCVFolds.TabIndex = 0;
            this.lblCVFolds.Text = "Cross-Validation Folds (2-10):";
            // 
            // numCVFolds
            // 
            this.numCVFolds.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.numCVFolds.Location = new System.Drawing.Point(220, 32);
            this.numCVFolds.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            this.numCVFolds.Minimum = new decimal(new int[] { 2, 0, 0, 0 });
            this.numCVFolds.Name = "numCVFolds";
            this.numCVFolds.Size = new System.Drawing.Size(80, 25);
            this.numCVFolds.TabIndex = 1;
            this.numCVFolds.Value = new decimal(new int[] { 3, 0, 0, 0 });
            // 
            // lblTrials
            // 
            this.lblTrials.AutoSize = true;
            this.lblTrials.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblTrials.Location = new System.Drawing.Point(15, 70);
            this.lblTrials.Name = "lblTrials";
            this.lblTrials.Size = new System.Drawing.Size(195, 15);
            this.lblTrials.TabIndex = 2;
            this.lblTrials.Text = "Hyperparameter Tuning Trials (1-20):";
            // 
            // numTrials
            // 
            this.numTrials.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.numTrials.Location = new System.Drawing.Point(220, 67);
            this.numTrials.Maximum = new decimal(new int[] { 20, 0, 0, 0 });
            this.numTrials.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numTrials.Name = "numTrials";
            this.numTrials.Size = new System.Drawing.Size(80, 25);
            this.numTrials.TabIndex = 3;
            this.numTrials.Value = new decimal(new int[] { 5, 0, 0, 0 });
            // 
            // groupBoxModels
            // 
            this.groupBoxModels.Controls.Add(this.chkTransferLearning);
            this.groupBoxModels.Controls.Add(this.chkLightGBM);
            this.groupBoxModels.Controls.Add(this.chkFastTree);
            this.groupBoxModels.Controls.Add(this.chkLBFGS);
            this.groupBoxModels.Controls.Add(this.chkSDCA);
            this.groupBoxModels.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.groupBoxModels.Location = new System.Drawing.Point(350, 25);
            this.groupBoxModels.Name = "groupBoxModels";
            this.groupBoxModels.Size = new System.Drawing.Size(590, 100);
            this.groupBoxModels.TabIndex = 4;
            this.groupBoxModels.TabStop = false;
            this.groupBoxModels.Text = "Select Algorithms";
            // 
            // chkSDCA
            // 
            this.chkSDCA.AutoSize = true;
            this.chkSDCA.Checked = true;
            this.chkSDCA.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkSDCA.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.chkSDCA.Location = new System.Drawing.Point(15, 25);
            this.chkSDCA.Name = "chkSDCA";
            this.chkSDCA.Size = new System.Drawing.Size(159, 19);
            this.chkSDCA.TabIndex = 0;
            this.chkSDCA.Text = "SDCA (Fast, Baseline)";
            this.chkSDCA.UseVisualStyleBackColor = true;
            // 
            // chkLBFGS
            // 
            this.chkLBFGS.AutoSize = true;
            this.chkLBFGS.Checked = true;
            this.chkLBFGS.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkLBFGS.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.chkLBFGS.Location = new System.Drawing.Point(15, 50);
            this.chkLBFGS.Name = "chkLBFGS";
            this.chkLBFGS.Size = new System.Drawing.Size(169, 19);
            this.chkLBFGS.TabIndex = 1;
            this.chkLBFGS.Text = "L-BFGS (Accurate, Medium)";
            this.chkLBFGS.UseVisualStyleBackColor = true;
            // 
            // chkFastTree
            // 
            this.chkFastTree.AutoSize = true;
            this.chkFastTree.Checked = true;
            this.chkFastTree.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkFastTree.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.chkFastTree.Location = new System.Drawing.Point(220, 25);
            this.chkFastTree.Name = "chkFastTree";
            this.chkFastTree.Size = new System.Drawing.Size(177, 19);
            this.chkFastTree.TabIndex = 2;
            this.chkFastTree.Text = "FastTree (Fast, Tree-based)";
            this.chkFastTree.UseVisualStyleBackColor = true;
            // 
            // chkLightGBM
            // 
            this.chkLightGBM.AutoSize = true;
            this.chkLightGBM.Checked = true;
            this.chkLightGBM.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkLightGBM.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.chkLightGBM.Location = new System.Drawing.Point(220, 50);
            this.chkLightGBM.Name = "chkLightGBM";
            this.chkLightGBM.Size = new System.Drawing.Size(193, 19);
            this.chkLightGBM.TabIndex = 3;
            this.chkLightGBM.Text = "LightGBM (Best, Recommended)";
            this.chkLightGBM.UseVisualStyleBackColor = true;
            // 
            // chkTransferLearning
            // 
            this.chkTransferLearning.AutoSize = true;
            this.chkTransferLearning.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.chkTransferLearning.Location = new System.Drawing.Point(15, 75);
            this.chkTransferLearning.Name = "chkTransferLearning";
            this.chkTransferLearning.Size = new System.Drawing.Size(258, 19);
            this.chkTransferLearning.TabIndex = 4;
            this.chkTransferLearning.Text = "Transfer Learning - MobileNetV2 (Slow, RGB only)";
            this.chkTransferLearning.UseVisualStyleBackColor = true;
            // 
            // btnStartTraining
            // 
            this.btnStartTraining.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(156)))), ((int)(((byte)(39)))), ((int)(((byte)(176)))));
            this.btnStartTraining.Enabled = false;
            this.btnStartTraining.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnStartTraining.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.btnStartTraining.ForeColor = System.Drawing.Color.White;
            this.btnStartTraining.Location = new System.Drawing.Point(20, 530);
            this.btnStartTraining.Name = "btnStartTraining";
            this.btnStartTraining.Size = new System.Drawing.Size(960, 50);
            this.btnStartTraining.TabIndex = 4;
            this.btnStartTraining.Text = "🚀 Start Training";
            this.btnStartTraining.UseVisualStyleBackColor = false;
            this.btnStartTraining.Click += new System.EventHandler(this.BtnStartTraining_Click);
            // 
            // progressBar
            // 
            this.progressBar.Location = new System.Drawing.Point(20, 590);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(960, 25);
            this.progressBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.progressBar.TabIndex = 5;
            this.progressBar.Visible = false;
            // 
            // groupBoxLog
            // 
            this.groupBoxLog.Controls.Add(this.txtTrainingLog);
            this.groupBoxLog.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.groupBoxLog.Location = new System.Drawing.Point(20, 630);
            this.groupBoxLog.Name = "groupBoxLog";
            this.groupBoxLog.Size = new System.Drawing.Size(960, 250);
            this.groupBoxLog.TabIndex = 6;
            this.groupBoxLog.TabStop = false;
            this.groupBoxLog.Text = "Training Log";
            // 
            // txtTrainingLog
            // 
            this.txtTrainingLog.BackColor = System.Drawing.Color.Black;
            this.txtTrainingLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtTrainingLog.Font = new System.Drawing.Font("Consolas", 9F);
            this.txtTrainingLog.ForeColor = System.Drawing.Color.Lime;
            this.txtTrainingLog.Location = new System.Drawing.Point(3, 21);
            this.txtTrainingLog.Multiline = true;
            this.txtTrainingLog.Name = "txtTrainingLog";
            this.txtTrainingLog.ReadOnly = true;
            this.txtTrainingLog.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.txtTrainingLog.Size = new System.Drawing.Size(954, 226);
            this.txtTrainingLog.TabIndex = 0;
            // 
            // ModelBuilderForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(1000, 900);
            this.Controls.Add(this.groupBoxLog);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.btnStartTraining);
            this.Controls.Add(this.groupBoxTraining);
            this.Controls.Add(this.groupBoxOutput);
            this.Controls.Add(this.groupBoxDataset);
            this.Controls.Add(this.lblTitle);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "ModelBuilderForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "ML Model Builder - Image Checker";
            this.groupBoxDataset.ResumeLayout(false);
            this.groupBoxDataset.PerformLayout();
            this.groupBoxOutput.ResumeLayout(false);
            this.groupBoxOutput.PerformLayout();
            this.groupBoxTraining.ResumeLayout(false);
            this.groupBoxTraining.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numCVFolds)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numTrials)).EndInit();
            this.groupBoxModels.ResumeLayout(false);
            this.groupBoxModels.PerformLayout();
            this.groupBoxLog.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.GroupBox groupBoxDataset;
        private System.Windows.Forms.Label lblDatasetInfo;
        private System.Windows.Forms.TextBox txtDatasetPath;
        private System.Windows.Forms.Button btnSelectDataset;
        private System.Windows.Forms.Label lblDatasetPath;
        private System.Windows.Forms.GroupBox groupBoxOutput;
        private System.Windows.Forms.TextBox txtOutputPath;
        private System.Windows.Forms.Button btnSelectOutput;
        private System.Windows.Forms.Label lblOutputPath;
        private System.Windows.Forms.GroupBox groupBoxTraining;
        private System.Windows.Forms.NumericUpDown numTrials;
        private System.Windows.Forms.Label lblTrials;
        private System.Windows.Forms.NumericUpDown numCVFolds;
        private System.Windows.Forms.Label lblCVFolds;
        private System.Windows.Forms.GroupBox groupBoxModels;
        private System.Windows.Forms.CheckBox chkTransferLearning;
        private System.Windows.Forms.CheckBox chkLightGBM;
        private System.Windows.Forms.CheckBox chkFastTree;
        private System.Windows.Forms.CheckBox chkLBFGS;
        private System.Windows.Forms.CheckBox chkSDCA;
        private System.Windows.Forms.Button btnStartTraining;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.GroupBox groupBoxLog;
        private System.Windows.Forms.RichTextBox txtTrainingLog;
    }
}