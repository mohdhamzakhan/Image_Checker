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
            lblTitle = new Label();
            groupBoxOutput = new GroupBox();
            txtOutputPath = new TextBox();
            btnSelectOutput = new Button();
            lblOutputPath = new Label();
            groupBoxTraining = new GroupBox();
            groupBoxModels = new GroupBox();
            chkTransferLearning = new CheckBox();
            chkLightGBM = new CheckBox();
            chkFastTree = new CheckBox();
            chkLBFGS = new CheckBox();
            chkSDCA = new CheckBox();
            numTrials = new NumericUpDown();
            lblTrials = new Label();
            numCVFolds = new NumericUpDown();
            lblCVFolds = new Label();
            lblImageSize = new Label();
            numImageWidth = new NumericUpDown();
            numImageHeight = new NumericUpDown();
            btnStartTraining = new Button();
            btnStopTraining = new Button();
            progressBar = new ProgressBar();
            groupBoxLog = new GroupBox();
            txtTrainingLog = new RichTextBox();
            groupBoxRoi = new GroupBox();
            btnRoiNext = new Button();
            btnRoiPrev = new Button();
            picRoiSource = new PictureBox();
            picRoiCrop = new PictureBox();
            btnPreviewRoi = new Button();
            lblRoiInfo = new Label();
            numRoiX = new NumericUpDown();
            numRoiY = new NumericUpDown();
            numRoiW = new NumericUpDown();
            numRoiH = new NumericUpDown();
            lblDatasetPath = new Label();
            btnSelectDataset = new Button();
            txtDatasetPath = new TextBox();
            lblDatasetInfo = new Label();
            groupBoxDataset = new GroupBox();
            btnApplyRoi = new Button();
            groupBoxOutput.SuspendLayout();
            groupBoxTraining.SuspendLayout();
            groupBoxModels.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numTrials).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numCVFolds).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numImageWidth).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numImageHeight).BeginInit();
            groupBoxLog.SuspendLayout();
            groupBoxRoi.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)picRoiSource).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picRoiCrop).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numRoiX).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numRoiY).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numRoiW).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numRoiH).BeginInit();
            groupBoxDataset.SuspendLayout();
            SuspendLayout();
            // 
            // lblTitle
            // 
            lblTitle.BackColor = Color.FromArgb(0, 120, 215);
            lblTitle.Dock = DockStyle.Top;
            lblTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(0, 0);
            lblTitle.Margin = new Padding(4, 0, 4, 0);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(1891, 100);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "🤖 ML Model Builder";
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // groupBoxOutput
            // 
            groupBoxOutput.Controls.Add(txtOutputPath);
            groupBoxOutput.Controls.Add(btnSelectOutput);
            groupBoxOutput.Controls.Add(lblOutputPath);
            groupBoxOutput.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            groupBoxOutput.Location = new Point(29, 417);
            groupBoxOutput.Margin = new Padding(4, 5, 4, 5);
            groupBoxOutput.Name = "groupBoxOutput";
            groupBoxOutput.Padding = new Padding(4, 5, 4, 5);
            groupBoxOutput.Size = new Size(1371, 167);
            groupBoxOutput.TabIndex = 2;
            groupBoxOutput.TabStop = false;
            groupBoxOutput.Text = "Step 2: Select Output Folder (Optional)";
            // 
            // txtOutputPath
            // 
            txtOutputPath.Font = new Font("Segoe UI", 9F);
            txtOutputPath.Location = new Point(257, 97);
            txtOutputPath.Margin = new Padding(4, 5, 4, 5);
            txtOutputPath.Name = "txtOutputPath";
            txtOutputPath.ReadOnly = true;
            txtOutputPath.Size = new Size(1084, 31);
            txtOutputPath.TabIndex = 2;
            // 
            // btnSelectOutput
            // 
            btnSelectOutput.BackColor = Color.FromArgb(76, 175, 80);
            btnSelectOutput.FlatStyle = FlatStyle.Flat;
            btnSelectOutput.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnSelectOutput.ForeColor = Color.White;
            btnSelectOutput.Location = new Point(21, 92);
            btnSelectOutput.Margin = new Padding(4, 5, 4, 5);
            btnSelectOutput.Name = "btnSelectOutput";
            btnSelectOutput.Size = new Size(214, 58);
            btnSelectOutput.TabIndex = 1;
            btnSelectOutput.Text = "📂 Browse...";
            btnSelectOutput.UseVisualStyleBackColor = false;
            btnSelectOutput.Click += BtnSelectOutput_Click;
            // 
            // lblOutputPath
            // 
            lblOutputPath.AutoSize = true;
            lblOutputPath.Font = new Font("Segoe UI", 9F);
            lblOutputPath.Location = new Point(21, 50);
            lblOutputPath.Margin = new Padding(4, 0, 4, 0);
            lblOutputPath.Name = "lblOutputPath";
            lblOutputPath.Size = new Size(464, 25);
            lblOutputPath.TabIndex = 0;
            lblOutputPath.Text = "Where to save the trained model (default: dataset folder):";
            // 
            // groupBoxTraining
            // 
            groupBoxTraining.Controls.Add(groupBoxModels);
            groupBoxTraining.Controls.Add(numTrials);
            groupBoxTraining.Controls.Add(lblTrials);
            groupBoxTraining.Controls.Add(numCVFolds);
            groupBoxTraining.Controls.Add(lblCVFolds);
            groupBoxTraining.Controls.Add(lblImageSize);
            groupBoxTraining.Controls.Add(numImageWidth);
            groupBoxTraining.Controls.Add(numImageHeight);
            groupBoxTraining.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            groupBoxTraining.Location = new Point(29, 617);
            groupBoxTraining.Margin = new Padding(4, 5, 4, 5);
            groupBoxTraining.Name = "groupBoxTraining";
            groupBoxTraining.Padding = new Padding(4, 5, 4, 5);
            groupBoxTraining.Size = new Size(1371, 233);
            groupBoxTraining.TabIndex = 3;
            groupBoxTraining.TabStop = false;
            groupBoxTraining.Text = "Step 3: Configure Training Parameters";
            // 
            // groupBoxModels
            // 
            groupBoxModels.Controls.Add(chkTransferLearning);
            groupBoxModels.Controls.Add(chkLightGBM);
            groupBoxModels.Controls.Add(chkFastTree);
            groupBoxModels.Controls.Add(chkLBFGS);
            groupBoxModels.Controls.Add(chkSDCA);
            groupBoxModels.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            groupBoxModels.Location = new Point(500, 42);
            groupBoxModels.Margin = new Padding(4, 5, 4, 5);
            groupBoxModels.Name = "groupBoxModels";
            groupBoxModels.Padding = new Padding(4, 5, 4, 5);
            groupBoxModels.Size = new Size(843, 167);
            groupBoxModels.TabIndex = 4;
            groupBoxModels.TabStop = false;
            groupBoxModels.Text = "Select Algorithms";
            // 
            // chkTransferLearning
            // 
            chkTransferLearning.AutoSize = true;
            chkTransferLearning.Font = new Font("Segoe UI", 9F);
            chkTransferLearning.Location = new Point(21, 125);
            chkTransferLearning.Margin = new Padding(4, 5, 4, 5);
            chkTransferLearning.Name = "chkTransferLearning";
            chkTransferLearning.Size = new Size(426, 29);
            chkTransferLearning.TabIndex = 4;
            chkTransferLearning.Text = "Transfer Learning - MobileNetV2 (Slow, RGB only)";
            chkTransferLearning.UseVisualStyleBackColor = true;
            // 
            // chkLightGBM
            // 
            chkLightGBM.AutoSize = true;
            chkLightGBM.Checked = true;
            chkLightGBM.CheckState = CheckState.Checked;
            chkLightGBM.Font = new Font("Segoe UI", 9F);
            chkLightGBM.Location = new Point(314, 83);
            chkLightGBM.Margin = new Padding(4, 5, 4, 5);
            chkLightGBM.Name = "chkLightGBM";
            chkLightGBM.Size = new Size(292, 29);
            chkLightGBM.TabIndex = 3;
            chkLightGBM.Text = "LightGBM (Best, Recommended)";
            chkLightGBM.UseVisualStyleBackColor = true;
            // 
            // chkFastTree
            // 
            chkFastTree.AutoSize = true;
            chkFastTree.Checked = true;
            chkFastTree.CheckState = CheckState.Checked;
            chkFastTree.Font = new Font("Segoe UI", 9F);
            chkFastTree.Location = new Point(314, 42);
            chkFastTree.Margin = new Padding(4, 5, 4, 5);
            chkFastTree.Name = "chkFastTree";
            chkFastTree.Size = new Size(241, 29);
            chkFastTree.TabIndex = 2;
            chkFastTree.Text = "FastTree (Fast, Tree-based)";
            chkFastTree.UseVisualStyleBackColor = true;
            // 
            // chkLBFGS
            // 
            chkLBFGS.AutoSize = true;
            chkLBFGS.Checked = true;
            chkLBFGS.CheckState = CheckState.Checked;
            chkLBFGS.Font = new Font("Segoe UI", 9F);
            chkLBFGS.Location = new Point(21, 83);
            chkLBFGS.Margin = new Padding(4, 5, 4, 5);
            chkLBFGS.Name = "chkLBFGS";
            chkLBFGS.Size = new Size(252, 29);
            chkLBFGS.TabIndex = 1;
            chkLBFGS.Text = "L-BFGS (Accurate, Medium)";
            chkLBFGS.UseVisualStyleBackColor = true;
            // 
            // chkSDCA
            // 
            chkSDCA.AutoSize = true;
            chkSDCA.Checked = true;
            chkSDCA.CheckState = CheckState.Checked;
            chkSDCA.Font = new Font("Segoe UI", 9F);
            chkSDCA.Location = new Point(21, 42);
            chkSDCA.Margin = new Padding(4, 5, 4, 5);
            chkSDCA.Name = "chkSDCA";
            chkSDCA.Size = new Size(202, 29);
            chkSDCA.TabIndex = 0;
            chkSDCA.Text = "SDCA (Fast, Baseline)";
            chkSDCA.UseVisualStyleBackColor = true;
            // 
            // numTrials
            // 
            numTrials.Font = new Font("Segoe UI", 10F);
            numTrials.Location = new Point(314, 112);
            numTrials.Margin = new Padding(4, 5, 4, 5);
            numTrials.Maximum = new decimal(new int[] { 20, 0, 0, 0 });
            numTrials.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numTrials.Name = "numTrials";
            numTrials.Size = new Size(114, 34);
            numTrials.TabIndex = 3;
            numTrials.Value = new decimal(new int[] { 5, 0, 0, 0 });
            // 
            // lblTrials
            // 
            lblTrials.AutoSize = true;
            lblTrials.Font = new Font("Segoe UI", 9F);
            lblTrials.Location = new Point(21, 117);
            lblTrials.Margin = new Padding(4, 0, 4, 0);
            lblTrials.Name = "lblTrials";
            lblTrials.Size = new Size(299, 25);
            lblTrials.TabIndex = 2;
            lblTrials.Text = "Hyperparameter Tuning Trials (1-20):";
            // 
            // numCVFolds
            // 
            numCVFolds.Font = new Font("Segoe UI", 10F);
            numCVFolds.Location = new Point(314, 53);
            numCVFolds.Margin = new Padding(4, 5, 4, 5);
            numCVFolds.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            numCVFolds.Minimum = new decimal(new int[] { 2, 0, 0, 0 });
            numCVFolds.Name = "numCVFolds";
            numCVFolds.Size = new Size(114, 34);
            numCVFolds.TabIndex = 1;
            numCVFolds.Value = new decimal(new int[] { 3, 0, 0, 0 });
            // 
            // lblCVFolds
            // 
            lblCVFolds.AutoSize = true;
            lblCVFolds.Font = new Font("Segoe UI", 9F);
            lblCVFolds.Location = new Point(21, 58);
            lblCVFolds.Margin = new Padding(4, 0, 4, 0);
            lblCVFolds.Name = "lblCVFolds";
            lblCVFolds.Size = new Size(245, 25);
            lblCVFolds.TabIndex = 0;
            lblCVFolds.Text = "Cross-Validation Folds (2-10):";
            // 
            // lblImageSize
            // 
            lblImageSize.AutoSize = true;
            lblImageSize.Font = new Font("Segoe UI", 9F);
            lblImageSize.Location = new Point(21, 175);
            lblImageSize.Margin = new Padding(4, 0, 4, 0);
            lblImageSize.Name = "lblImageSize";
            lblImageSize.Size = new Size(211, 25);
            lblImageSize.TabIndex = 5;
            lblImageSize.Text = "Target image size (W * H)";
            // 
            // numImageWidth
            // 
            numImageWidth.Font = new Font("Segoe UI", 10F);
            numImageWidth.Location = new Point(295, 167);
            numImageWidth.Margin = new Padding(4, 5, 4, 5);
            numImageWidth.Maximum = new decimal(new int[] { 1024, 0, 0, 0 });
            numImageWidth.Minimum = new decimal(new int[] { 32, 0, 0, 0 });
            numImageWidth.Name = "numImageWidth";
            numImageWidth.Size = new Size(86, 34);
            numImageWidth.TabIndex = 6;
            numImageWidth.Value = new decimal(new int[] { 224, 0, 0, 0 });
            // 
            // numImageHeight
            // 
            numImageHeight.Font = new Font("Segoe UI", 10F);
            numImageHeight.Location = new Point(395, 167);
            numImageHeight.Margin = new Padding(4, 5, 4, 5);
            numImageHeight.Maximum = new decimal(new int[] { 1024, 0, 0, 0 });
            numImageHeight.Minimum = new decimal(new int[] { 32, 0, 0, 0 });
            numImageHeight.Name = "numImageHeight";
            numImageHeight.Size = new Size(90, 34);
            numImageHeight.TabIndex = 7;
            numImageHeight.Value = new decimal(new int[] { 224, 0, 0, 0 });
            // 
            // btnStartTraining
            // 
            btnStartTraining.BackColor = Color.FromArgb(156, 39, 176);
            btnStartTraining.Enabled = false;
            btnStartTraining.FlatStyle = FlatStyle.Flat;
            btnStartTraining.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            btnStartTraining.ForeColor = Color.White;
            btnStartTraining.Location = new Point(29, 883);
            btnStartTraining.Margin = new Padding(4, 5, 4, 5);
            btnStartTraining.Name = "btnStartTraining";
            btnStartTraining.Size = new Size(1086, 83);
            btnStartTraining.TabIndex = 4;
            btnStartTraining.Text = "🚀 Start Training";
            btnStartTraining.UseVisualStyleBackColor = false;
            btnStartTraining.Click += BtnStartTraining_Click;
            // 
            // btnStopTraining
            // 
            btnStopTraining.BackColor = Color.FromArgb(220, 53, 69);
            btnStopTraining.Enabled = false;
            btnStopTraining.FlatStyle = FlatStyle.Flat;
            btnStopTraining.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            btnStopTraining.ForeColor = Color.White;
            btnStopTraining.Location = new Point(1143, 883);
            btnStopTraining.Margin = new Padding(4, 5, 4, 5);
            btnStopTraining.Name = "btnStopTraining";
            btnStopTraining.Size = new Size(257, 83);
            btnStopTraining.TabIndex = 5;
            btnStopTraining.Text = "\U0001f6d1 Stop";
            btnStopTraining.UseVisualStyleBackColor = false;
            btnStopTraining.Click += BtnStopTraining_Click;
            // 
            // progressBar
            // 
            progressBar.Location = new Point(29, 983);
            progressBar.Margin = new Padding(4, 5, 4, 5);
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(1371, 42);
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.TabIndex = 6;
            progressBar.Visible = false;
            // 
            // groupBoxLog
            // 
            groupBoxLog.Controls.Add(txtTrainingLog);
            groupBoxLog.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            groupBoxLog.Location = new Point(29, 1050);
            groupBoxLog.Margin = new Padding(4, 5, 4, 5);
            groupBoxLog.Name = "groupBoxLog";
            groupBoxLog.Padding = new Padding(4, 5, 4, 5);
            groupBoxLog.Size = new Size(1371, 417);
            groupBoxLog.TabIndex = 7;
            groupBoxLog.TabStop = false;
            groupBoxLog.Text = "Training Log";
            // 
            // txtTrainingLog
            // 
            txtTrainingLog.BackColor = Color.Black;
            txtTrainingLog.Dock = DockStyle.Fill;
            txtTrainingLog.Font = new Font("Consolas", 9F);
            txtTrainingLog.ForeColor = Color.Lime;
            txtTrainingLog.Location = new Point(4, 32);
            txtTrainingLog.Margin = new Padding(4, 5, 4, 5);
            txtTrainingLog.Name = "txtTrainingLog";
            txtTrainingLog.ReadOnly = true;
            txtTrainingLog.ScrollBars = RichTextBoxScrollBars.Vertical;
            txtTrainingLog.Size = new Size(1363, 380);
            txtTrainingLog.TabIndex = 0;
            txtTrainingLog.Text = "";
            // 
            // groupBoxRoi
            // 
            groupBoxRoi.Controls.Add(btnRoiNext);
            groupBoxRoi.Controls.Add(btnRoiPrev);
            groupBoxRoi.Controls.Add(picRoiSource);
            groupBoxRoi.Controls.Add(picRoiCrop);
            groupBoxRoi.Controls.Add(btnPreviewRoi);
            groupBoxRoi.Controls.Add(lblRoiInfo);
            groupBoxRoi.Controls.Add(numRoiX);
            groupBoxRoi.Controls.Add(numRoiY);
            groupBoxRoi.Controls.Add(numRoiW);
            groupBoxRoi.Controls.Add(numRoiH);
            groupBoxRoi.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            groupBoxRoi.Location = new Point(1418, 123);
            groupBoxRoi.Name = "groupBoxRoi";
            groupBoxRoi.Size = new Size(462, 727);
            groupBoxRoi.TabIndex = 8;
            groupBoxRoi.TabStop = false;
            groupBoxRoi.Text = "Step 4 - ROI Preview";
            // 
            // btnRoiNext
            // 
            btnRoiNext.BackColor = SystemColors.Highlight;
            btnRoiNext.FlatStyle = FlatStyle.Flat;
            btnRoiNext.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnRoiNext.Location = new Point(141, 487);
            btnRoiNext.Name = "btnRoiNext";
            btnRoiNext.Size = new Size(70, 53);
            btnRoiNext.TabIndex = 10;
            btnRoiNext.Text = ">>";
            btnRoiNext.UseVisualStyleBackColor = false;
            // 
            // btnRoiPrev
            // 
            btnRoiPrev.BackColor = Color.FromArgb(76, 175, 80);
            btnRoiPrev.FlatStyle = FlatStyle.Flat;
            btnRoiPrev.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnRoiPrev.Location = new Point(24, 487);
            btnRoiPrev.Name = "btnRoiPrev";
            btnRoiPrev.Size = new Size(70, 53);
            btnRoiPrev.TabIndex = 9;
            btnRoiPrev.Text = "<<";
            btnRoiPrev.UseVisualStyleBackColor = false;
            // 
            // picRoiSource
            // 
            picRoiSource.BorderStyle = BorderStyle.FixedSingle;
            picRoiSource.Location = new Point(15, 62);
            picRoiSource.Name = "picRoiSource";
            picRoiSource.Size = new Size(320, 180);
            picRoiSource.SizeMode = PictureBoxSizeMode.Zoom;
            picRoiSource.TabIndex = 0;
            picRoiSource.TabStop = false;
            picRoiSource.Paint += picRoiSource_Paint;
            // 
            // picRoiCrop
            // 
            picRoiCrop.BorderStyle = BorderStyle.FixedSingle;
            picRoiCrop.Location = new Point(15, 264);
            picRoiCrop.Name = "picRoiCrop";
            picRoiCrop.Size = new Size(200, 200);
            picRoiCrop.SizeMode = PictureBoxSizeMode.Zoom;
            picRoiCrop.TabIndex = 1;
            picRoiCrop.TabStop = false;
            // 
            // btnPreviewRoi
            // 
            btnPreviewRoi.BackColor = Color.FromArgb(0, 120, 215);
            btnPreviewRoi.FlatStyle = FlatStyle.Flat;
            btnPreviewRoi.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnPreviewRoi.ForeColor = Color.White;
            btnPreviewRoi.Location = new Point(221, 292);
            btnPreviewRoi.Name = "btnPreviewRoi";
            btnPreviewRoi.Size = new Size(150, 35);
            btnPreviewRoi.TabIndex = 0;
            btnPreviewRoi.Text = "Preview ROI";
            btnPreviewRoi.UseVisualStyleBackColor = false;
            btnPreviewRoi.Click += btnPreviewRoi_Click;
            // 
            // lblRoiInfo
            // 
            lblRoiInfo.Font = new Font("Segoe UI", 9F);
            lblRoiInfo.Location = new Point(230, 330);
            lblRoiInfo.Name = "lblRoiInfo";
            lblRoiInfo.Size = new Size(204, 134);
            lblRoiInfo.TabIndex = 2;
            lblRoiInfo.Text = "Shows the area used for training.\r\nSelect dataset first, then click Preview ROI.";
            // 
            // numRoiX
            // 
            numRoiX.Font = new Font("Segoe UI", 9F);
            numRoiX.Location = new Point(24, 552);
            numRoiX.Maximum = new decimal(new int[] { 640, 0, 0, 0 });
            numRoiX.Name = "numRoiX";
            numRoiX.Size = new Size(70, 31);
            numRoiX.TabIndex = 5;
            numRoiX.Value = new decimal(new int[] { 220, 0, 0, 0 });
            numRoiX.ValueChanged += RoiValueChanged;
            // 
            // numRoiY
            // 
            numRoiY.Font = new Font("Segoe UI", 9F);
            numRoiY.Location = new Point(24, 594);
            numRoiY.Maximum = new decimal(new int[] { 480, 0, 0, 0 });
            numRoiY.Name = "numRoiY";
            numRoiY.Size = new Size(70, 31);
            numRoiY.TabIndex = 6;
            numRoiY.Value = new decimal(new int[] { 140, 0, 0, 0 });
            numRoiY.ValueChanged += RoiValueChanged;
            // 
            // numRoiW
            // 
            numRoiW.Font = new Font("Segoe UI", 9F);
            numRoiW.Location = new Point(141, 552);
            numRoiW.Maximum = new decimal(new int[] { 640, 0, 0, 0 });
            numRoiW.Minimum = new decimal(new int[] { 10, 0, 0, 0 });
            numRoiW.Name = "numRoiW";
            numRoiW.Size = new Size(70, 31);
            numRoiW.TabIndex = 7;
            numRoiW.Value = new decimal(new int[] { 200, 0, 0, 0 });
            numRoiW.ValueChanged += RoiValueChanged;
            // 
            // numRoiH
            // 
            numRoiH.Font = new Font("Segoe UI", 9F);
            numRoiH.Location = new Point(141, 589);
            numRoiH.Maximum = new decimal(new int[] { 480, 0, 0, 0 });
            numRoiH.Minimum = new decimal(new int[] { 10, 0, 0, 0 });
            numRoiH.Name = "numRoiH";
            numRoiH.Size = new Size(70, 31);
            numRoiH.TabIndex = 8;
            numRoiH.Value = new decimal(new int[] { 200, 0, 0, 0 });
            numRoiH.ValueChanged += RoiValueChanged;
            // 
            // lblDatasetPath
            // 
            lblDatasetPath.AutoSize = true;
            lblDatasetPath.Font = new Font("Segoe UI", 9F);
            lblDatasetPath.Location = new Point(21, 50);
            lblDatasetPath.Margin = new Padding(4, 0, 4, 0);
            lblDatasetPath.Name = "lblDatasetPath";
            lblDatasetPath.Size = new Size(566, 25);
            lblDatasetPath.TabIndex = 0;
            lblDatasetPath.Text = "Select folder containing 'OK' and 'NG' subfolders with training images:";
            // 
            // btnSelectDataset
            // 
            btnSelectDataset.BackColor = Color.FromArgb(0, 120, 215);
            btnSelectDataset.FlatStyle = FlatStyle.Flat;
            btnSelectDataset.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnSelectDataset.ForeColor = Color.White;
            btnSelectDataset.Location = new Point(21, 92);
            btnSelectDataset.Margin = new Padding(4, 5, 4, 5);
            btnSelectDataset.Name = "btnSelectDataset";
            btnSelectDataset.Size = new Size(214, 58);
            btnSelectDataset.TabIndex = 1;
            btnSelectDataset.Text = "📁 Browse...";
            btnSelectDataset.UseVisualStyleBackColor = false;
            btnSelectDataset.Click += BtnSelectDataset_Click;
            // 
            // txtDatasetPath
            // 
            txtDatasetPath.Font = new Font("Segoe UI", 9F);
            txtDatasetPath.Location = new Point(257, 97);
            txtDatasetPath.Margin = new Padding(4, 5, 4, 5);
            txtDatasetPath.Name = "txtDatasetPath";
            txtDatasetPath.ReadOnly = true;
            txtDatasetPath.Size = new Size(1084, 31);
            txtDatasetPath.TabIndex = 2;
            // 
            // lblDatasetInfo
            // 
            lblDatasetInfo.BorderStyle = BorderStyle.FixedSingle;
            lblDatasetInfo.Font = new Font("Segoe UI", 9F);
            lblDatasetInfo.Location = new Point(21, 167);
            lblDatasetInfo.Margin = new Padding(4, 0, 4, 0);
            lblDatasetInfo.Name = "lblDatasetInfo";
            lblDatasetInfo.Size = new Size(1321, 65);
            lblDatasetInfo.TabIndex = 3;
            lblDatasetInfo.Text = "No dataset selected";
            lblDatasetInfo.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // groupBoxDataset
            // 
            groupBoxDataset.Controls.Add(lblDatasetInfo);
            groupBoxDataset.Controls.Add(txtDatasetPath);
            groupBoxDataset.Controls.Add(btnSelectDataset);
            groupBoxDataset.Controls.Add(lblDatasetPath);
            groupBoxDataset.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            groupBoxDataset.Location = new Point(29, 133);
            groupBoxDataset.Margin = new Padding(4, 5, 4, 5);
            groupBoxDataset.Name = "groupBoxDataset";
            groupBoxDataset.Padding = new Padding(4, 5, 4, 5);
            groupBoxDataset.Size = new Size(1371, 250);
            groupBoxDataset.TabIndex = 1;
            groupBoxDataset.TabStop = false;
            groupBoxDataset.Text = "Step 1: Select Dataset Folder";
            // 
            // btnApplyRoi
            // 
            btnApplyRoi.BackColor = Color.FromArgb(46, 125, 50);
            btnApplyRoi.FlatStyle = FlatStyle.Flat;
            btnApplyRoi.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnApplyRoi.ForeColor = Color.White;
            btnApplyRoi.Location = new Point(1418, 858);
            btnApplyRoi.Margin = new Padding(4, 5, 4, 5);
            btnApplyRoi.Name = "btnApplyRoi";
            btnApplyRoi.Size = new Size(462, 67);
            btnApplyRoi.TabIndex = 19;
            btnApplyRoi.Text = "🖼️ Apply ROI and Create Cropped Dataset";
            btnApplyRoi.UseVisualStyleBackColor = false;
            btnApplyRoi.Click += btnApplyRoi_Click;
            // 
            // ModelBuilderForm
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(1891, 1500);
            Controls.Add(btnApplyRoi);
            Controls.Add(groupBoxRoi);
            Controls.Add(groupBoxLog);
            Controls.Add(progressBar);
            Controls.Add(btnStopTraining);
            Controls.Add(btnStartTraining);
            Controls.Add(groupBoxTraining);
            Controls.Add(groupBoxOutput);
            Controls.Add(groupBoxDataset);
            Controls.Add(lblTitle);
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Margin = new Padding(4, 5, 4, 5);
            MaximizeBox = false;
            Name = "ModelBuilderForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "ML Model Builder - Image Checker";
            groupBoxOutput.ResumeLayout(false);
            groupBoxOutput.PerformLayout();
            groupBoxTraining.ResumeLayout(false);
            groupBoxTraining.PerformLayout();
            groupBoxModels.ResumeLayout(false);
            groupBoxModels.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numTrials).EndInit();
            ((System.ComponentModel.ISupportInitialize)numCVFolds).EndInit();
            ((System.ComponentModel.ISupportInitialize)numImageWidth).EndInit();
            ((System.ComponentModel.ISupportInitialize)numImageHeight).EndInit();
            groupBoxLog.ResumeLayout(false);
            groupBoxRoi.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)picRoiSource).EndInit();
            ((System.ComponentModel.ISupportInitialize)picRoiCrop).EndInit();
            ((System.ComponentModel.ISupportInitialize)numRoiX).EndInit();
            ((System.ComponentModel.ISupportInitialize)numRoiY).EndInit();
            ((System.ComponentModel.ISupportInitialize)numRoiW).EndInit();
            ((System.ComponentModel.ISupportInitialize)numRoiH).EndInit();
            groupBoxDataset.ResumeLayout(false);
            groupBoxDataset.PerformLayout();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label lblTitle;
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
        private System.Windows.Forms.Button btnStopTraining;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.GroupBox groupBoxLog;
        private System.Windows.Forms.RichTextBox txtTrainingLog;
        private System.Windows.Forms.Label lblImageSize;
        private System.Windows.Forms.NumericUpDown numImageWidth;
        private System.Windows.Forms.NumericUpDown numImageHeight;
        private System.Windows.Forms.GroupBox groupBoxRoi;
        private System.Windows.Forms.PictureBox picRoiSource;
        private System.Windows.Forms.PictureBox picRoiCrop;
        private System.Windows.Forms.Button btnPreviewRoi;
        private System.Windows.Forms.Label lblRoiInfo;
        private System.Windows.Forms.NumericUpDown numRoiX;
        private System.Windows.Forms.NumericUpDown numRoiY;
        private System.Windows.Forms.NumericUpDown numRoiW;
        private System.Windows.Forms.NumericUpDown numRoiH;
        private Label lblDatasetPath;
        private Button btnSelectDataset;
        private TextBox txtDatasetPath;
        private Label lblDatasetInfo;
        private GroupBox groupBoxDataset;
        private Button btnRoiNext;
        private Button btnRoiPrev;
        private Button btnApplyRoi;
    }
}