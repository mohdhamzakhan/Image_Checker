namespace Image_Checker.WinForm
{
    partial class ModelBuilderForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            // ── Top-level controls ─────────────────────────────────────────────
            lblTitle = new Label();
            mainTabControl = new TabControl();

            // ── Tab pages ─────────────────────────────────────────────────────
            tabDataset = new TabPage();
            tabOutput = new TabPage();
            tabTraining = new TabPage();
            tabRoi = new TabPage();
            tabCnn = new TabPage();
            tabOneClass = new TabPage();
            tabLog = new TabPage();

            // ── Tab 1: Dataset ────────────────────────────────────────────────
            groupBoxDataset = new GroupBox();
            lblDatasetPath = new Label();
            btnSelectDataset = new Button();
            txtDatasetPath = new TextBox();
            lblDatasetInfo = new Label();

            // ── Tab 2: Output ─────────────────────────────────────────────────
            groupBoxOutput = new GroupBox();
            lblOutputPath = new Label();
            btnSelectOutput = new Button();
            txtOutputPath = new TextBox();

            // ── Tab 3: ML.NET Training ────────────────────────────────────────
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

            // ── Tab 4: ROI ────────────────────────────────────────────────────
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
            btnApplyRoi = new Button();
            txtImageIndex = new TextBox();
            btnGoToImage = new Button();

            // ── Tab 5: CNN ────────────────────────────────────────────────────
            groupBoxCnn = new GroupBox();
            grpCnnConfig = new GroupBox();
            lblCnnArch = new Label();
            cmbCnnArchitecture = new ComboBox();
            lblCnnEpochs = new Label();
            numCnnEpochs = new NumericUpDown();
            lblCnnBatch = new Label();
            numCnnBatch = new NumericUpDown();
            lblCnnLR = new Label();
            numCnnLR = new NumericUpDown();
            lblCnnEarlyStop = new Label();
            numCnnEarlyStop = new NumericUpDown();
            chkCnnAugment = new CheckBox();
            chkExportOnnx = new CheckBox();
            chkCnnUseGpu = new CheckBox();
            btnStartCnnTraining = new Button();
            grpOnnxTest = new GroupBox();
            lblOnnxModel = new Label();
            txtOnnxModelPath = new TextBox();
            btnBrowseOnnx = new Button();
            lblTestImage = new Label();
            btnBrowseTestImage = new Button();
            btnTestOnnx = new Button();
            picOnnxTest = new PictureBox();
            lblOnnxResult = new Label();

            // ── Tab 6: One-Class ──────────────────────────────────────────────
            groupBoxOneClass = new GroupBox();
            grpOneClassConfig = new GroupBox();
            lblOneClassDesc = new Label();
            lblOkFolder = new Label();
            txtOneClassOkFolder = new TextBox();
            btnBrowseOneClassOk = new Button();
            lblOneClassImgW = new Label();
            numOneClassImgW = new NumericUpDown();
            lblOneClassImgH = new Label();
            numOneClassImgH = new NumericUpDown();
            lblOneClassLatent = new Label();
            numOneClassLatent = new NumericUpDown();
            lblOneClassEpochs = new Label();
            numOneClassEpochs = new NumericUpDown();
            lblOneClassBatch = new Label();
            numOneClassBatch = new NumericUpDown();
            lblOneClassSensitivity = new Label();
            numOneClassSensitivity = new NumericUpDown();
            chkOneClassAugment = new CheckBox();
            chkOneClassGpu = new CheckBox();
            btnStartOneClass = new Button();
            grpOneClassTest = new GroupBox();
            lblOneClassModelPath = new Label();
            txtOneClassModelPath = new TextBox();
            btnBrowseOneClassModel = new Button();
            lblOneClassTestImage = new Label();
            btnBrowseOneClassTestImage = new Button();
            btnTestOneClass = new Button();
            picOneClassTest = new PictureBox();
            lblOneClassResult = new Label();
            lblOneClassThreshold = new Label();
            numOneClassThresholdAdj = new NumericUpDown();
            btnAdjustThreshold = new Button();
            // ── One-Class Target Resolution Previewer ──────────────────────────
            picOneClassPreview = new PictureBox();
            btnLoadOneClassPreview = new Button();
            lblOneClassPreviewSize = new Label();

            ((System.ComponentModel.ISupportInitialize)(picOneClassPreview)).BeginInit();

            lblOneClassPreviewSize.AutoSize = true;
            lblOneClassPreviewSize.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            lblOneClassPreviewSize.ForeColor = Color.FromArgb(0, 120, 215);
            lblOneClassPreviewSize.Location = new Point(780, 86);
            lblOneClassPreviewSize.Text = "Model View: N/A";

            btnLoadOneClassPreview.BackColor = Color.FromArgb(76, 175, 80);
            btnLoadOneClassPreview.FlatStyle = FlatStyle.Flat;
            btnLoadOneClassPreview.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            btnLoadOneClassPreview.ForeColor = Color.White;
            btnLoadOneClassPreview.Location = new Point(930, 82);
            btnLoadOneClassPreview.Size = new Size(140, 36);
            btnLoadOneClassPreview.Text = "Load Sample...";
            btnLoadOneClassPreview.UseVisualStyleBackColor = false;
            btnLoadOneClassPreview.Click += BtnLoadPreview_Click;

            // Set to Zoom so the aspect ratio of the resized bitmap is preserved on screen
            picOneClassPreview.BackColor = Color.FromArgb(30, 30, 30);
            picOneClassPreview.BorderStyle = BorderStyle.FixedSingle;
            picOneClassPreview.Location = new Point(780, 130);
            picOneClassPreview.Size = new Size(290, 290);
            picOneClassPreview.SizeMode = PictureBoxSizeMode.Zoom;

            // Expand the width of the config group to fit the new previewer on the right
            grpOneClassConfig.Size = new Size(1100, 440);


            // ── Tab 7: Log ────────────────────────────────────────────────────
            groupBoxLog = new GroupBox();
            txtTrainingLog = new RichTextBox();

            // ══════════════════════════════════════════════════════════════════
            //  BEGIN INIT
            // ══════════════════════════════════════════════════════════════════
            mainTabControl.SuspendLayout();
            tabDataset.SuspendLayout();
            tabOutput.SuspendLayout();
            tabTraining.SuspendLayout();
            tabRoi.SuspendLayout();
            tabCnn.SuspendLayout();
            tabOneClass.SuspendLayout();
            tabLog.SuspendLayout();

            groupBoxDataset.SuspendLayout();
            groupBoxOutput.SuspendLayout();
            groupBoxTraining.SuspendLayout();
            groupBoxModels.SuspendLayout();
            groupBoxRoi.SuspendLayout();
            groupBoxCnn.SuspendLayout();
            grpCnnConfig.SuspendLayout();
            grpOnnxTest.SuspendLayout();
            groupBoxOneClass.SuspendLayout();
            grpOneClassConfig.SuspendLayout();
            grpOneClassConfig.SuspendLayout();
            grpOneClassTest.SuspendLayout();
            groupBoxLog.SuspendLayout();

            foreach (var n in new NumericUpDown[] {
        numTrials, numCVFolds, numImageWidth, numImageHeight,
        numRoiX, numRoiY, numRoiW, numRoiH,
        numCnnEpochs, numCnnBatch, numCnnLR, numCnnEarlyStop,
        numOneClassImgW, numOneClassImgH, numOneClassLatent,
        numOneClassEpochs, numOneClassBatch, numOneClassSensitivity,
        numOneClassThresholdAdj })
                ((System.ComponentModel.ISupportInitialize)n).BeginInit();

            foreach (var p in new PictureBox[] {
        picRoiSource, picRoiCrop, picOnnxTest, picOneClassTest })
                ((System.ComponentModel.ISupportInitialize)p).BeginInit();

            SuspendLayout();

            // ══════════════════════════════════════════════════════════════════
            //  lblTitle
            // ══════════════════════════════════════════════════════════════════
            lblTitle.BackColor = Color.FromArgb(0, 120, 215);
            lblTitle.Dock = DockStyle.Top;
            lblTitle.Font = new Font("Microsoft Sans Serif", 16F, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Height = 60;
            lblTitle.Text = "🤖  ML Model Builder";
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;

            // ══════════════════════════════════════════════════════════════════
            //  mainTabControl
            // ══════════════════════════════════════════════════════════════════
            mainTabControl.Dock = DockStyle.Fill;
            mainTabControl.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold);
            mainTabControl.ItemSize = new Size(160, 36);
            mainTabControl.SizeMode = TabSizeMode.Fixed;
            mainTabControl.Padding = new Point(12, 6);
            mainTabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
            mainTabControl.DrawItem += MainTabControl_DrawItem;

            mainTabControl.TabPages.AddRange(new TabPage[] {
        tabDataset, tabOutput, tabTraining,
        tabRoi, tabCnn, tabOneClass, tabLog });

            // ══════════════════════════════════════════════════════════════════
            //  TAB 1 — DATASET
            // ══════════════════════════════════════════════════════════════════
            tabDataset.Text = "📁  Dataset";
            tabDataset.BackColor = Color.White;
            tabDataset.Padding = new Padding(12);

            lblDatasetPath.AutoSize = true;
            lblDatasetPath.Font = new Font("Microsoft Sans Serif", 9F);
            lblDatasetPath.Location = new Point(16, 40);
            lblDatasetPath.Text = "Select folder containing class subfolders (OK, NG, etc.):";

            btnSelectDataset.BackColor = Color.FromArgb(0, 120, 215);
            btnSelectDataset.FlatStyle = FlatStyle.Flat;
            btnSelectDataset.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            btnSelectDataset.ForeColor = Color.White;
            btnSelectDataset.Location = new Point(16, 75);
            btnSelectDataset.Size = new Size(160, 44);
            btnSelectDataset.Text = "📁  Browse…";
            btnSelectDataset.UseVisualStyleBackColor = false;
            btnSelectDataset.Click += BtnSelectDataset_Click;

            txtDatasetPath.Font = new Font("Microsoft Sans Serif", 9F);
            txtDatasetPath.Location = new Point(190, 83);
            txtDatasetPath.ReadOnly = true;
            txtDatasetPath.Size = new Size(900, 28);

            lblDatasetInfo.BorderStyle = BorderStyle.FixedSingle;
            lblDatasetInfo.Font = new Font("Microsoft Sans Serif", 9F);
            lblDatasetInfo.Location = new Point(16, 140);
            lblDatasetInfo.Size = new Size(1076, 56);
            lblDatasetInfo.Text = "No dataset selected";
            lblDatasetInfo.TextAlign = ContentAlignment.MiddleCenter;

            groupBoxDataset.Controls.AddRange(new Control[] {
        lblDatasetPath, btnSelectDataset, txtDatasetPath, lblDatasetInfo });
            groupBoxDataset.Dock = DockStyle.Fill;
            groupBoxDataset.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold);
            groupBoxDataset.Padding = new Padding(12);
            groupBoxDataset.Text = "Step 1 — Select Dataset Folder";

            tabDataset.Controls.Add(groupBoxDataset);

            // ══════════════════════════════════════════════════════════════════
            //  TAB 2 — OUTPUT
            // ══════════════════════════════════════════════════════════════════
            tabOutput.Text = "💾  Output";
            tabOutput.BackColor = Color.White;

            lblOutputPath.AutoSize = true;
            lblOutputPath.Font = new Font("Microsoft Sans Serif", 9F);
            lblOutputPath.Location = new Point(16, 40);
            lblOutputPath.Text = "Where to save the trained model (default: dataset folder):";

            btnSelectOutput.BackColor = Color.FromArgb(76, 175, 80);
            btnSelectOutput.FlatStyle = FlatStyle.Flat;
            btnSelectOutput.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            btnSelectOutput.ForeColor = Color.White;
            btnSelectOutput.Location = new Point(16, 75);
            btnSelectOutput.Size = new Size(160, 44);
            btnSelectOutput.Text = "📂  Browse…";
            btnSelectOutput.UseVisualStyleBackColor = false;
            btnSelectOutput.Click += BtnSelectOutput_Click;

            txtOutputPath.Font = new Font("Microsoft Sans Serif", 9F);
            txtOutputPath.Location = new Point(190, 83);
            txtOutputPath.ReadOnly = true;
            txtOutputPath.Size = new Size(900, 28);

            groupBoxOutput.Controls.AddRange(new Control[] {
        lblOutputPath, btnSelectOutput, txtOutputPath });
            groupBoxOutput.Dock = DockStyle.Fill;
            groupBoxOutput.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold);
            groupBoxOutput.Padding = new Padding(12);
            groupBoxOutput.Text = "Step 2 — Select Output Folder (Optional)";

            tabOutput.Controls.Add(groupBoxOutput);

            // ══════════════════════════════════════════════════════════════════
            //  TAB 3 — ML.NET TRAINING
            // ══════════════════════════════════════════════════════════════════
            tabTraining.Text = "🚀  ML.NET Train";
            tabTraining.BackColor = Color.White;

            // Algorithms group
            chkSDCA.AutoSize = true;
            chkSDCA.Checked = true;
            chkSDCA.Font = new Font("Microsoft Sans Serif", 9F);
            chkSDCA.Location = new Point(16, 40);
            chkSDCA.Text = "SDCA (Fast, Baseline)";

            chkLBFGS.AutoSize = true;
            chkLBFGS.Checked = true;
            chkLBFGS.Font = new Font("Microsoft Sans Serif", 9F);
            chkLBFGS.Location = new Point(16, 80);
            chkLBFGS.Text = "L-BFGS (Accurate, Medium)";

            chkFastTree.AutoSize = true;
            chkFastTree.Checked = true;
            chkFastTree.Font = new Font("Microsoft Sans Serif", 9F);
            chkFastTree.Location = new Point(280, 40);
            chkFastTree.Text = "FastTree (Fast, Tree-based)";

            chkLightGBM.AutoSize = true;
            chkLightGBM.Checked = true;
            chkLightGBM.Font = new Font("Microsoft Sans Serif", 9F);
            chkLightGBM.Location = new Point(280, 80);
            chkLightGBM.Text = "LightGBM (Best, Recommended)";

            chkTransferLearning.AutoSize = true;
            chkTransferLearning.Font = new Font("Microsoft Sans Serif", 9F);
            chkTransferLearning.Location = new Point(16, 120);
            chkTransferLearning.Text = "Transfer Learning — MobileNetV2 (Slow, RGB only)";

            groupBoxModels.Controls.AddRange(new Control[] {
        chkSDCA, chkLBFGS, chkFastTree, chkLightGBM, chkTransferLearning });
            groupBoxModels.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            groupBoxModels.Location = new Point(16, 36);
            groupBoxModels.Size = new Size(680, 170);
            groupBoxModels.Text = "Algorithms";

            // CV / Trials / Image size
            lblCVFolds.AutoSize = true;
            lblCVFolds.Font = new Font("Microsoft Sans Serif", 9F);
            lblCVFolds.Location = new Point(16, 240);
            lblCVFolds.Text = "Cross-Validation Folds (2–10):";

            numCVFolds.Font = new Font("Microsoft Sans Serif", 9F);
            numCVFolds.Location = new Point(290, 236);
            numCVFolds.Minimum = 2; numCVFolds.Maximum = 10; numCVFolds.Value = 3;
            numCVFolds.Size = new Size(80, 28);

            lblTrials.AutoSize = true;
            lblTrials.Font = new Font("Microsoft Sans Serif", 9F);
            lblTrials.Location = new Point(16, 290);
            lblTrials.Text = "Hyperparameter Tuning Trials (1–20):";

            numTrials.Font = new Font("Microsoft Sans Serif", 9F);
            numTrials.Location = new Point(340, 286);
            numTrials.Minimum = 1; numTrials.Maximum = 20; numTrials.Value = 5;
            numTrials.Size = new Size(80, 28);

            lblImageSize.AutoSize = true;
            lblImageSize.Font = new Font("Microsoft Sans Serif", 9F);
            lblImageSize.Location = new Point(16, 340);
            lblImageSize.Text = "Target Image Size  W × H:";

            numImageWidth.Font = new Font("Microsoft Sans Serif", 9F);
            numImageWidth.Location = new Point(240, 336);
            numImageWidth.Minimum = 32; numImageWidth.Maximum = 1024; numImageWidth.Value = 224;
            numImageWidth.Size = new Size(80, 28);

            numImageHeight.Font = new Font("Microsoft Sans Serif", 9F);
            numImageHeight.Location = new Point(330, 336);
            numImageHeight.Minimum = 32; numImageHeight.Maximum = 1024; numImageHeight.Value = 224;
            numImageHeight.Size = new Size(80, 28);

            // Buttons
            btnStartTraining.BackColor = Color.FromArgb(156, 39, 176);
            btnStartTraining.Enabled = false;
            btnStartTraining.FlatStyle = FlatStyle.Flat;
            btnStartTraining.Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold);
            btnStartTraining.ForeColor = Color.White;
            btnStartTraining.Location = new Point(16, 400);
            btnStartTraining.Size = new Size(500, 48);
            btnStartTraining.Text = "🚀  Start ML.NET Training";
            btnStartTraining.UseVisualStyleBackColor = false;
            btnStartTraining.Click += BtnStartTraining_Click;

            btnStopTraining.BackColor = Color.FromArgb(220, 53, 69);
            btnStopTraining.Enabled = false;
            btnStopTraining.FlatStyle = FlatStyle.Flat;
            btnStopTraining.Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold);
            btnStopTraining.ForeColor = Color.White;
            btnStopTraining.Location = new Point(530, 400);
            btnStopTraining.Size = new Size(200, 48);
            btnStopTraining.Text = "🛑  Stop";
            btnStopTraining.UseVisualStyleBackColor = false;
            btnStopTraining.Click += BtnStopTraining_Click;

            progressBar.Location = new Point(16, 470);
            progressBar.Size = new Size(1076, 20);
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.Visible = false;

            groupBoxTraining.Controls.AddRange(new Control[] {
        groupBoxModels,
        lblCVFolds, numCVFolds, lblTrials, numTrials,
        lblImageSize, numImageWidth, numImageHeight,
        btnStartTraining, btnStopTraining, progressBar });
            groupBoxTraining.Dock = DockStyle.Fill;
            groupBoxTraining.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold);
            groupBoxTraining.Padding = new Padding(12);
            groupBoxTraining.Text = "Step 3 — Configure & Run ML.NET Training";

            tabTraining.Controls.Add(groupBoxTraining);

            // ══════════════════════════════════════════════════════════════════
            //  TAB 4 — ROI
            // ══════════════════════════════════════════════════════════════════
            tabRoi.Text = "✂️  ROI";
            tabRoi.BackColor = Color.White;

            picRoiSource.BorderStyle = BorderStyle.FixedSingle;
            picRoiSource.Location = new Point(16, 40);
            picRoiSource.Size = new Size(480, 300);
            picRoiSource.SizeMode = PictureBoxSizeMode.Zoom;
            picRoiSource.Paint += picRoiSource_Paint;

            picRoiCrop.BorderStyle = BorderStyle.FixedSingle;
            picRoiCrop.Location = new Point(516, 40);
            picRoiCrop.Size = new Size(300, 300);
            picRoiCrop.SizeMode = PictureBoxSizeMode.Zoom;

            lblRoiInfo.Font = new Font("Microsoft Sans Serif", 9F);
            lblRoiInfo.Location = new Point(836, 40);
            lblRoiInfo.Size = new Size(260, 120);
            lblRoiInfo.Text = "Shows the area used for training.\nSelect dataset first, then click Preview ROI.";

            btnPreviewRoi.BackColor = Color.FromArgb(0, 120, 215);
            btnPreviewRoi.FlatStyle = FlatStyle.Flat;
            btnPreviewRoi.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            btnPreviewRoi.ForeColor = Color.White;
            btnPreviewRoi.Location = new Point(836, 172);
            btnPreviewRoi.Size = new Size(160, 36);
            btnPreviewRoi.Text = "Preview ROI";
            btnPreviewRoi.UseVisualStyleBackColor = false;
            btnPreviewRoi.Click += btnPreviewRoi_Click;

            // ROI spinners with labels
            var lblRoiX = new Label
            {
                Text = "X:",
                AutoSize = true,
                Location = new Point(16, 370),
                Font = new Font("Microsoft Sans Serif", 9F)
            };
            numRoiX.Font = new Font("Microsoft Sans Serif", 9F);
            numRoiX.Location = new Point(40, 366);
            numRoiX.Maximum = 1920; numRoiX.Value = 220;
            numRoiX.Size = new Size(80, 28);
            numRoiX.ValueChanged += RoiValueChanged;

            var lblRoiY = new Label
            {
                Text = "Y:",
                AutoSize = true,
                Location = new Point(140, 370),
                Font = new Font("Microsoft Sans Serif", 9F)
            };
            numRoiY.Font = new Font("Microsoft Sans Serif", 9F);
            numRoiY.Location = new Point(164, 366);
            numRoiY.Maximum = 1080; numRoiY.Value = 140;
            numRoiY.Size = new Size(80, 28);
            numRoiY.ValueChanged += RoiValueChanged;

            var lblRoiW = new Label
            {
                Text = "W:",
                AutoSize = true,
                Location = new Point(264, 370),
                Font = new Font("Microsoft Sans Serif", 9F)
            };
            numRoiW.Font = new Font("Microsoft Sans Serif", 9F);
            numRoiW.Location = new Point(295, 366);
            numRoiW.Minimum = 10; numRoiW.Maximum = 1920; numRoiW.Value = 200;
            numRoiW.Size = new Size(80, 28);
            numRoiW.ValueChanged += RoiValueChanged;

            var lblRoiH = new Label
            {
                Text = "H:",
                AutoSize = true,
                Location = new Point(390, 370),
                Font = new Font("Microsoft Sans Serif", 9F)
            };
            numRoiH.Font = new Font("Microsoft Sans Serif", 9F);
            numRoiH.Location = new Point(416, 366);
            numRoiH.Minimum = 10; numRoiH.Maximum = 1080; numRoiH.Value = 200;
            numRoiH.Size = new Size(80, 28);
            numRoiH.ValueChanged += RoiValueChanged;

            btnRoiPrev.BackColor = Color.FromArgb(76, 175, 80);
            btnRoiPrev.FlatStyle = FlatStyle.Flat;
            btnRoiPrev.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            btnRoiPrev.Location = new Point(516, 366);
            btnRoiPrev.Size = new Size(80, 36);
            btnRoiPrev.Text = "<<";
            btnRoiPrev.UseVisualStyleBackColor = false;

            btnRoiNext.BackColor = SystemColors.Highlight;
            btnRoiNext.FlatStyle = FlatStyle.Flat;
            btnRoiNext.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            btnRoiNext.Location = new Point(612, 366);
            btnRoiNext.Size = new Size(80, 36);
            btnRoiNext.Text = ">>";
            btnRoiNext.UseVisualStyleBackColor = false;

            var lblGoto = new Label
            {
                Text = "Go to #:",
                AutoSize = true,
                Location = new Point(720, 370),
                Font = new Font("Microsoft Sans Serif", 9F)
            };
            txtImageIndex.Location = new Point(800, 366);
            txtImageIndex.Size = new Size(80, 28);
            txtImageIndex.Font = new Font("Microsoft Sans Serif", 9F);

            btnGoToImage.Text = "Go";
            btnGoToImage.Location = new Point(896, 363);
            btnGoToImage.Size = new Size(60, 34);
            btnGoToImage.FlatStyle = FlatStyle.Flat;
            btnGoToImage.Click += BtnGoToImage_Click;

            btnApplyRoi.BackColor = Color.FromArgb(46, 125, 50);
            btnApplyRoi.FlatStyle = FlatStyle.Flat;
            btnApplyRoi.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold);
            btnApplyRoi.ForeColor = Color.White;
            btnApplyRoi.Location = new Point(16, 430);
            btnApplyRoi.Size = new Size(500, 48);
            btnApplyRoi.Text = "🖼️  Apply ROI and Create Cropped Dataset";
            btnApplyRoi.UseVisualStyleBackColor = false;
            btnApplyRoi.Click += btnApplyRoi_Click;

            groupBoxRoi.Controls.AddRange(new Control[] {
        picRoiSource, picRoiCrop, lblRoiInfo, btnPreviewRoi,
        lblRoiX, numRoiX, lblRoiY, numRoiY,
        lblRoiW, numRoiW, lblRoiH, numRoiH,
        btnRoiPrev, btnRoiNext,
        lblGoto, txtImageIndex, btnGoToImage,
        btnApplyRoi });
            groupBoxRoi.Dock = DockStyle.Fill;
            groupBoxRoi.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold);
            groupBoxRoi.Padding = new Padding(12);
            groupBoxRoi.Text = "Step 4 — ROI Preview & Crop";

            tabRoi.Controls.Add(groupBoxRoi);

            // ══════════════════════════════════════════════════════════════════
            //  TAB 5 — CNN
            // ══════════════════════════════════════════════════════════════════
            tabCnn.Text = "🧠  CNN Train";
            tabCnn.BackColor = Color.White;

            // ── CNN Config group (Spaced to 50px per row) ───────────────────────
            lblCnnArch.AutoSize = true;
            lblCnnArch.Font = new Font("Microsoft Sans Serif", 9F);
            lblCnnArch.Location = new Point(16, 40);
            lblCnnArch.Text = "Architecture:";

            cmbCnnArchitecture.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbCnnArchitecture.Font = new Font("Microsoft Sans Serif", 9F);
            cmbCnnArchitecture.Location = new Point(160, 36);
            cmbCnnArchitecture.Size = new Size(200, 30);

            lblCnnEpochs.AutoSize = true;
            lblCnnEpochs.Font = new Font("Microsoft Sans Serif", 9F);
            lblCnnEpochs.Location = new Point(16, 90);
            lblCnnEpochs.Text = "Epochs:";

            numCnnEpochs.Font = new Font("Microsoft Sans Serif", 9F);
            numCnnEpochs.Location = new Point(160, 86);
            numCnnEpochs.Minimum = 1; numCnnEpochs.Maximum = 500; numCnnEpochs.Value = 20;
            numCnnEpochs.Size = new Size(100, 28);

            lblCnnBatch.AutoSize = true;
            lblCnnBatch.Font = new Font("Microsoft Sans Serif", 9F);
            lblCnnBatch.Location = new Point(16, 140);
            lblCnnBatch.Text = "Batch Size:";

            numCnnBatch.Font = new Font("Microsoft Sans Serif", 9F);
            numCnnBatch.Location = new Point(160, 136);
            numCnnBatch.Minimum = 1; numCnnBatch.Maximum = 256; numCnnBatch.Value = 16;
            numCnnBatch.Size = new Size(100, 28);

            lblCnnLR.AutoSize = true;
            lblCnnLR.Font = new Font("Microsoft Sans Serif", 9F);
            lblCnnLR.Location = new Point(16, 190);
            lblCnnLR.Text = "Learning Rate:";

            numCnnLR.DecimalPlaces = 5;
            numCnnLR.Font = new Font("Microsoft Sans Serif", 9F);
            numCnnLR.Increment = new decimal(new int[] { 1, 0, 0, 327680 });
            numCnnLR.Location = new Point(160, 186);
            numCnnLR.Maximum = new decimal(new int[] { 5, 0, 0, 65536 });
            numCnnLR.Minimum = new decimal(new int[] { 1, 0, 0, 327680 });
            numCnnLR.Value = new decimal(new int[] { 1, 0, 0, 196608 });
            numCnnLR.Size = new Size(130, 28);

            lblCnnEarlyStop.AutoSize = true;
            lblCnnEarlyStop.Font = new Font("Microsoft Sans Serif", 9F);
            lblCnnEarlyStop.Location = new Point(16, 240);
            lblCnnEarlyStop.Text = "Early Stop (epochs):";

            numCnnEarlyStop.Font = new Font("Microsoft Sans Serif", 9F);
            numCnnEarlyStop.Location = new Point(190, 236);
            numCnnEarlyStop.Minimum = 1; numCnnEarlyStop.Value = 5;
            numCnnEarlyStop.Size = new Size(100, 28);

            chkCnnAugment.AutoSize = true;
            chkCnnAugment.Checked = true;
            chkCnnAugment.Font = new Font("Microsoft Sans Serif", 9F);
            chkCnnAugment.Location = new Point(16, 290);
            chkCnnAugment.Text = "Data Augmentation (flip + brightness)";

            chkExportOnnx.AutoSize = true;
            chkExportOnnx.Checked = true;
            chkExportOnnx.Font = new Font("Microsoft Sans Serif", 9F);
            chkExportOnnx.Location = new Point(16, 330);
            chkExportOnnx.Text = "Export to ONNX after training";

            chkCnnUseGpu.AutoSize = true;
            chkCnnUseGpu.Font = new Font("Microsoft Sans Serif", 9F);
            chkCnnUseGpu.Location = new Point(16, 370);
            chkCnnUseGpu.Text = "Use GPU / CUDA";

            btnStartCnnTraining.BackColor = Color.FromArgb(0, 122, 204);
            btnStartCnnTraining.FlatStyle = FlatStyle.Flat;
            btnStartCnnTraining.Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold);
            btnStartCnnTraining.ForeColor = Color.White;
            btnStartCnnTraining.Location = new Point(16, 420);
            btnStartCnnTraining.Size = new Size(340, 48);
            btnStartCnnTraining.Text = "🧠  Train CNN";
            btnStartCnnTraining.UseVisualStyleBackColor = false;

            grpCnnConfig.Controls.AddRange(new Control[] {
        lblCnnArch, cmbCnnArchitecture,
        lblCnnEpochs, numCnnEpochs,
        lblCnnBatch, numCnnBatch,
        lblCnnLR, numCnnLR,
        lblCnnEarlyStop, numCnnEarlyStop,
        chkCnnAugment, chkExportOnnx, chkCnnUseGpu,
        btnStartCnnTraining });
            grpCnnConfig.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold);
            grpCnnConfig.Location = new Point(12, 40);
            grpCnnConfig.Size = new Size(420, 500);
            grpCnnConfig.Text = "CNN Configuration";

            // ── ONNX Test group ────────────────────────────────────────────────
            lblOnnxModel.AutoSize = true;
            lblOnnxModel.Font = new Font("Microsoft Sans Serif", 9F);
            lblOnnxModel.Location = new Point(16, 40);
            lblOnnxModel.Text = "Model (.bin):";

            txtOnnxModelPath.BackColor = Color.WhiteSmoke;
            txtOnnxModelPath.Font = new Font("Microsoft Sans Serif", 9F);
            txtOnnxModelPath.Location = new Point(120, 36);
            txtOnnxModelPath.ReadOnly = true;
            txtOnnxModelPath.Size = new Size(420, 28);

            btnBrowseOnnx.BackColor = Color.FromArgb(76, 175, 80);
            btnBrowseOnnx.FlatStyle = FlatStyle.Flat;
            btnBrowseOnnx.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            btnBrowseOnnx.ForeColor = Color.White;
            btnBrowseOnnx.Location = new Point(556, 32);
            btnBrowseOnnx.Size = new Size(110, 36);
            btnBrowseOnnx.Text = "Browse…";
            btnBrowseOnnx.UseVisualStyleBackColor = false;

            lblTestImage.AutoSize = true;
            lblTestImage.Font = new Font("Microsoft Sans Serif", 9F);
            lblTestImage.Location = new Point(16, 100);
            lblTestImage.Text = "Test Image:";

            btnBrowseTestImage.BackColor = Color.FromArgb(0, 120, 215);
            btnBrowseTestImage.FlatStyle = FlatStyle.Flat;
            btnBrowseTestImage.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            btnBrowseTestImage.ForeColor = Color.White;
            btnBrowseTestImage.Location = new Point(120, 95);
            btnBrowseTestImage.Size = new Size(160, 36);
            btnBrowseTestImage.Text = "Browse image…";
            btnBrowseTestImage.UseVisualStyleBackColor = false;

            btnTestOnnx.BackColor = Color.FromArgb(0, 153, 76);
            btnTestOnnx.FlatStyle = FlatStyle.Flat;
            btnTestOnnx.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold);
            btnTestOnnx.ForeColor = Color.White;
            btnTestOnnx.Location = new Point(296, 95);
            btnTestOnnx.Size = new Size(200, 36);
            btnTestOnnx.Text = "▶  Run Inference";
            btnTestOnnx.UseVisualStyleBackColor = false;

            picOnnxTest.BackColor = Color.FromArgb(30, 30, 30);
            picOnnxTest.BorderStyle = BorderStyle.FixedSingle;
            picOnnxTest.Location = new Point(16, 160);
            picOnnxTest.Size = new Size(260, 260);
            picOnnxTest.SizeMode = PictureBoxSizeMode.Zoom;

            lblOnnxResult.BackColor = Color.FromArgb(20, 20, 20);
            lblOnnxResult.BorderStyle = BorderStyle.FixedSingle;
            lblOnnxResult.Font = new Font("Consolas", 9.5F);
            lblOnnxResult.ForeColor = Color.LightGreen;
            lblOnnxResult.Location = new Point(292, 160);
            lblOnnxResult.Size = new Size(376, 260);
            lblOnnxResult.Text = "Load a model and test image,\nthen click Run Inference.";

            grpOnnxTest.Controls.AddRange(new Control[] {
        lblOnnxModel, txtOnnxModelPath, btnBrowseOnnx,
        lblTestImage, btnBrowseTestImage, btnTestOnnx,
        picOnnxTest, lblOnnxResult });
            grpOnnxTest.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold);
            grpOnnxTest.Location = new Point(448, 40);
            grpOnnxTest.Size = new Size(700, 500);
            grpOnnxTest.Text = "CNN / ONNX Quick Test";

            groupBoxCnn.Controls.AddRange(new Control[] { grpCnnConfig, grpOnnxTest });
            groupBoxCnn.Dock = DockStyle.Fill;
            groupBoxCnn.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold);
            groupBoxCnn.Padding = new Padding(8);
            groupBoxCnn.Text = "Step 5 — CNN Training & Inference  (TorchSharp — no Python required)";

            tabCnn.Controls.Add(groupBoxCnn);

            // ══════════════════════════════════════════════════════════════════
            //  TAB 6 — ONE-CLASS
            // ══════════════════════════════════════════════════════════════════
            tabOneClass.Text = "🔵  One-Class";
            tabOneClass.BackColor = Color.White;

            // ── CONFIG GROUP (Left Side - Width 700) ──────────────────────────
            lblOneClassDesc.Font = new Font("Microsoft Sans Serif", 9F);
            lblOneClassDesc.Location = new Point(16, 35);
            lblOneClassDesc.Size = new Size(660, 36);
            lblOneClassDesc.ForeColor = Color.FromArgb(0, 80, 160);
            lblOneClassDesc.Text = "ℹ️  Train on OK images only. The autoencoder learns what OK looks like. Anything that reconstructs poorly is flagged as NG (anomaly detection).";

            // OK Folder Selection
            lblOkFolder.AutoSize = true;
            lblOkFolder.Font = new Font("Microsoft Sans Serif", 9F);
            lblOkFolder.Location = new Point(16, 82);
            lblOkFolder.Text = "OK Images Folder:";

            txtOneClassOkFolder.Font = new Font("Microsoft Sans Serif", 9F);
            txtOneClassOkFolder.Location = new Point(140, 78);
            txtOneClassOkFolder.ReadOnly = true;
            txtOneClassOkFolder.Size = new Size(410, 28);

            btnBrowseOneClassOk.BackColor = Color.FromArgb(0, 120, 215);
            btnBrowseOneClassOk.FlatStyle = FlatStyle.Flat;
            btnBrowseOneClassOk.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            btnBrowseOneClassOk.ForeColor = Color.White;
            btnBrowseOneClassOk.Location = new Point(560, 74);
            btnBrowseOneClassOk.Size = new Size(120, 36);
            btnBrowseOneClassOk.Text = "📁 Browse…";
            btnBrowseOneClassOk.UseVisualStyleBackColor = false;

            // -- Parameter Column 1 --
            lblOneClassImgW.AutoSize = true;
            lblOneClassImgW.Font = new Font("Microsoft Sans Serif", 9F);
            lblOneClassImgW.Location = new Point(16, 134);
            lblOneClassImgW.Text = "Image Width:";

            numOneClassImgW.Font = new Font("Microsoft Sans Serif", 9F);
            numOneClassImgW.Location = new Point(130, 130);
            numOneClassImgW.Minimum = 32; numOneClassImgW.Maximum = 512;
            numOneClassImgW.Increment = 32; numOneClassImgW.Value = 128;
            numOneClassImgW.Size = new Size(90, 28);

            lblOneClassImgH.AutoSize = true;
            lblOneClassImgH.Font = new Font("Microsoft Sans Serif", 9F);
            lblOneClassImgH.Location = new Point(16, 174);
            lblOneClassImgH.Text = "Image Height:";

            numOneClassImgH.Font = new Font("Microsoft Sans Serif", 9F);
            numOneClassImgH.Location = new Point(130, 170);
            numOneClassImgH.Minimum = 32; numOneClassImgH.Maximum = 512;
            numOneClassImgH.Increment = 32; numOneClassImgH.Value = 128;
            numOneClassImgH.Size = new Size(90, 28);

            lblOneClassBatch.AutoSize = true;
            lblOneClassBatch.Font = new Font("Microsoft Sans Serif", 9F);
            lblOneClassBatch.Location = new Point(16, 214);
            lblOneClassBatch.Text = "Batch Size:";

            numOneClassBatch.Font = new Font("Microsoft Sans Serif", 9F);
            numOneClassBatch.Location = new Point(130, 210);
            numOneClassBatch.Minimum = 4; numOneClassBatch.Maximum = 64;
            numOneClassBatch.Increment = 4; numOneClassBatch.Value = 16;
            numOneClassBatch.Size = new Size(90, 28);

            // -- Parameter Column 2 --
            lblOneClassLatent.AutoSize = true;
            lblOneClassLatent.Font = new Font("Microsoft Sans Serif", 9F);
            lblOneClassLatent.Location = new Point(240, 134);
            lblOneClassLatent.Text = "Latent Dims:";

            numOneClassLatent.Font = new Font("Microsoft Sans Serif", 9F);
            numOneClassLatent.Location = new Point(380, 130);
            numOneClassLatent.Minimum = 8; numOneClassLatent.Maximum = 256;
            numOneClassLatent.Increment = 8; numOneClassLatent.Value = 64;
            numOneClassLatent.Size = new Size(90, 28);

            lblOneClassEpochs.AutoSize = true;
            lblOneClassEpochs.Font = new Font("Microsoft Sans Serif", 9F);
            lblOneClassEpochs.Location = new Point(240, 174);
            lblOneClassEpochs.Text = "Epochs:";

            numOneClassEpochs.Font = new Font("Microsoft Sans Serif", 9F);
            numOneClassEpochs.Location = new Point(380, 170);
            numOneClassEpochs.Minimum = 5; numOneClassEpochs.Maximum = 200;
            numOneClassEpochs.Value = 30;
            numOneClassEpochs.Size = new Size(90, 28);

            lblOneClassSensitivity.AutoSize = true;
            lblOneClassSensitivity.Font = new Font("Microsoft Sans Serif", 9F);
            lblOneClassSensitivity.Location = new Point(240, 214);
            lblOneClassSensitivity.Text = "Sensitivity ×0.1:";

            numOneClassSensitivity.Font = new Font("Microsoft Sans Serif", 9F);
            numOneClassSensitivity.Location = new Point(380, 210);
            numOneClassSensitivity.Minimum = 5; numOneClassSensitivity.Maximum = 50;
            numOneClassSensitivity.Value = 20;
            numOneClassSensitivity.Size = new Size(90, 28);

            var tipSens = new ToolTip { AutoPopDelay = 8000 };
            tipSens.SetToolTip(numOneClassSensitivity,
                "Threshold = mean + (value×0.1) × std\n" +
                "10 = 1.0 (strict)   20 = 2.0 (balanced)   30 = 3.0 (lenient)");

            // -- Previewer Area (Right side of Config) --
            lblOneClassPreviewSize = new Label();
            lblOneClassPreviewSize.AutoSize = true;
            lblOneClassPreviewSize.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            lblOneClassPreviewSize.ForeColor = Color.FromArgb(0, 120, 215);
            lblOneClassPreviewSize.Location = new Point(480, 134);
            lblOneClassPreviewSize.Text = "Model View: N/A";

            btnLoadOneClassPreview = new Button();
            btnLoadOneClassPreview.BackColor = Color.FromArgb(76, 175, 80);
            btnLoadOneClassPreview.FlatStyle = FlatStyle.Flat;
            btnLoadOneClassPreview.Font = new Font("Microsoft Sans Serif", 8.5F, FontStyle.Bold);
            btnLoadOneClassPreview.ForeColor = Color.White;
            btnLoadOneClassPreview.Location = new Point(480, 160);
            btnLoadOneClassPreview.Size = new Size(200, 32);
            btnLoadOneClassPreview.Text = "Load Sample Image...";
            btnLoadOneClassPreview.UseVisualStyleBackColor = false;
            // Wire up event if not already done in the code-behind
            btnLoadOneClassPreview.Click += BtnLoadPreview_Click; 

            picOneClassPreview = new PictureBox();
            ((System.ComponentModel.ISupportInitialize)(picOneClassPreview)).BeginInit();
            picOneClassPreview.BackColor = Color.FromArgb(30, 30, 30);
            picOneClassPreview.BorderStyle = BorderStyle.FixedSingle;
            picOneClassPreview.Location = new Point(480, 200);
            picOneClassPreview.Size = new Size(200, 200);
            picOneClassPreview.SizeMode = PictureBoxSizeMode.Zoom;

            // Wire up the live refresh
            numOneClassImgW.ValueChanged += PreviewSize_ValueChanged;
            numOneClassImgH.ValueChanged += PreviewSize_ValueChanged;

            // -- Lower Controls & Start Button --
            chkOneClassAugment.AutoSize = true;
            chkOneClassAugment.Checked = true;
            chkOneClassAugment.Font = new Font("Microsoft Sans Serif", 9F);
            chkOneClassAugment.Location = new Point(16, 260);
            chkOneClassAugment.Text = "Data Augmentation (flip + brightness jitter)";

            chkOneClassGpu.AutoSize = true;
            chkOneClassGpu.Font = new Font("Microsoft Sans Serif", 9F);
            chkOneClassGpu.Location = new Point(16, 290);
            chkOneClassGpu.Text = "Use GPU / CUDA";

            btnStartOneClass.BackColor = Color.FromArgb(0, 122, 204);
            btnStartOneClass.FlatStyle = FlatStyle.Flat;
            btnStartOneClass.Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold);
            btnStartOneClass.ForeColor = Color.White;
            btnStartOneClass.Location = new Point(16, 330);
            btnStartOneClass.Size = new Size(424, 70);
            btnStartOneClass.Text = "🔵  Train One-Class Model";
            btnStartOneClass.UseVisualStyleBackColor = false;

            grpOneClassConfig.Controls.AddRange(new Control[] {
                lblOneClassDesc, lblOkFolder, txtOneClassOkFolder, btnBrowseOneClassOk,
                lblOneClassImgW, numOneClassImgW, lblOneClassImgH, numOneClassImgH,
                lblOneClassBatch, numOneClassBatch, lblOneClassLatent, numOneClassLatent,
                lblOneClassEpochs, numOneClassEpochs, lblOneClassSensitivity, numOneClassSensitivity,
                lblOneClassPreviewSize, btnLoadOneClassPreview, picOneClassPreview,
                chkOneClassAugment, chkOneClassGpu, btnStartOneClass
            });
            grpOneClassConfig.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold);
            grpOneClassConfig.Location = new Point(12, 40);
            grpOneClassConfig.Size = new Size(700, 420);
            grpOneClassConfig.Text = "One-Class Configuration";

            // ── TEST GROUP (Right Side - Width 520) ───────────────────────────
            lblOneClassModelPath.AutoSize = true;
            lblOneClassModelPath.Font = new Font("Microsoft Sans Serif", 9F);
            lblOneClassModelPath.Location = new Point(16, 40);
            lblOneClassModelPath.Text = "Model (.bin):";

            txtOneClassModelPath.BackColor = Color.WhiteSmoke;
            txtOneClassModelPath.Font = new Font("Microsoft Sans Serif", 9F);
            txtOneClassModelPath.Location = new Point(110, 36);
            txtOneClassModelPath.ReadOnly = true;
            txtOneClassModelPath.Size = new Size(280, 28);

            btnBrowseOneClassModel.BackColor = Color.FromArgb(76, 175, 80);
            btnBrowseOneClassModel.FlatStyle = FlatStyle.Flat;
            btnBrowseOneClassModel.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            btnBrowseOneClassModel.ForeColor = Color.White;
            btnBrowseOneClassModel.Location = new Point(400, 32);
            btnBrowseOneClassModel.Size = new Size(100, 36);
            btnBrowseOneClassModel.Text = "Browse…";
            btnBrowseOneClassModel.UseVisualStyleBackColor = false;

            lblOneClassTestImage.AutoSize = true;
            lblOneClassTestImage.Font = new Font("Microsoft Sans Serif", 9F);
            lblOneClassTestImage.Location = new Point(16, 84);
            lblOneClassTestImage.Text = "Test Image:";

            btnBrowseOneClassTestImage.BackColor = Color.FromArgb(0, 120, 215);
            btnBrowseOneClassTestImage.FlatStyle = FlatStyle.Flat;
            btnBrowseOneClassTestImage.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            btnBrowseOneClassTestImage.ForeColor = Color.White;
            btnBrowseOneClassTestImage.Location = new Point(110, 80);
            btnBrowseOneClassTestImage.Size = new Size(140, 36);
            btnBrowseOneClassTestImage.Text = "Browse img…";
            btnBrowseOneClassTestImage.UseVisualStyleBackColor = false;

            btnTestOneClass.BackColor = Color.FromArgb(0, 153, 76);
            btnTestOneClass.FlatStyle = FlatStyle.Flat;
            btnTestOneClass.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold);
            btnTestOneClass.ForeColor = Color.White;
            btnTestOneClass.Location = new Point(260, 80);
            btnTestOneClass.Size = new Size(240, 36);
            btnTestOneClass.Text = "▶  Run Inference";
            btnTestOneClass.UseVisualStyleBackColor = false;

            lblOneClassThreshold.AutoSize = true;
            lblOneClassThreshold.Font = new Font("Microsoft Sans Serif", 9F);
            lblOneClassThreshold.Location = new Point(16, 134);
            lblOneClassThreshold.Text = "Adjust thresh:";

            numOneClassThresholdAdj.Font = new Font("Microsoft Sans Serif", 9F);
            numOneClassThresholdAdj.Location = new Point(110, 130);
            numOneClassThresholdAdj.DecimalPlaces = 6;
            numOneClassThresholdAdj.Increment = new decimal(new int[] { 1, 0, 0, 327680 });
            numOneClassThresholdAdj.Minimum = new decimal(new int[] { 1, 0, 0, 393216 });
            numOneClassThresholdAdj.Maximum = new decimal(new int[] { 1, 0, 0, 0 });
            numOneClassThresholdAdj.Value = new decimal(new int[] { 1, 0, 0, 131072 });
            numOneClassThresholdAdj.Size = new Size(140, 28);

            btnAdjustThreshold.BackColor = Color.FromArgb(255, 152, 0);
            btnAdjustThreshold.FlatStyle = FlatStyle.Flat;
            btnAdjustThreshold.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
            btnAdjustThreshold.ForeColor = Color.White;
            btnAdjustThreshold.Location = new Point(260, 126);
            btnAdjustThreshold.Size = new Size(100, 36);
            btnAdjustThreshold.Text = "Apply";
            btnAdjustThreshold.UseVisualStyleBackColor = false;

            picOneClassTest.BackColor = Color.FromArgb(30, 30, 30);
            picOneClassTest.BorderStyle = BorderStyle.FixedSingle;
            picOneClassTest.Location = new Point(16, 180);
            picOneClassTest.Size = new Size(220, 220);
            picOneClassTest.SizeMode = PictureBoxSizeMode.Zoom;

            lblOneClassResult.BackColor = Color.FromArgb(20, 20, 20);
            lblOneClassResult.BorderStyle = BorderStyle.FixedSingle;
            lblOneClassResult.Font = new Font("Consolas", 9.5F);
            lblOneClassResult.ForeColor = Color.LightGreen;
            lblOneClassResult.Location = new Point(252, 180);
            lblOneClassResult.Size = new Size(248, 220);
            lblOneClassResult.Text = "Load a one-class model\nand test image, then\nclick Run Inference.";

            grpOneClassTest.Controls.AddRange(new Control[] {
                lblOneClassModelPath, txtOneClassModelPath, btnBrowseOneClassModel,
                lblOneClassTestImage, btnBrowseOneClassTestImage, btnTestOneClass,
                lblOneClassThreshold, numOneClassThresholdAdj, btnAdjustThreshold,
                picOneClassTest, lblOneClassResult
            });
            grpOneClassTest.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold);
            // Placed neatly to the right of the Config group
            grpOneClassTest.Location = new Point(724, 40);
            grpOneClassTest.Size = new Size(520, 420);
            grpOneClassTest.Text = "One-Class Quick Test";

            // ── MAIN WRAPPER ──────────────────────────────────────────────────
            groupBoxOneClass.Controls.AddRange(new Control[] { grpOneClassConfig, grpOneClassTest });
            groupBoxOneClass.Dock = DockStyle.Fill;
            groupBoxOneClass.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold);
            groupBoxOneClass.Padding = new Padding(8);
            groupBoxOneClass.Text = "Step 6 — One-Class Model (OK-Only / Anomaly Detection)";

            tabOneClass.Controls.Add(groupBoxOneClass);

            ((System.ComponentModel.ISupportInitialize)(picOneClassPreview)).EndInit();

            // ══════════════════════════════════════════════════════════════════
            //  TAB 7 — LOG
            // ══════════════════════════════════════════════════════════════════
            tabLog.Text = "📋  Log";
            tabLog.BackColor = Color.Black;

            txtTrainingLog.BackColor = Color.Black;
            txtTrainingLog.Dock = DockStyle.Fill;
            txtTrainingLog.Font = new Font("Consolas", 9F);
            txtTrainingLog.ForeColor = Color.Lime;
            txtTrainingLog.ReadOnly = true;
            txtTrainingLog.ScrollBars = RichTextBoxScrollBars.Vertical;
            txtTrainingLog.BorderStyle = BorderStyle.None;

            groupBoxLog.Controls.Add(txtTrainingLog);
            groupBoxLog.Dock = DockStyle.Fill;
            groupBoxLog.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold);
            groupBoxLog.ForeColor = Color.Lime;
            groupBoxLog.Text = "Training Log";

            tabLog.Controls.Add(groupBoxLog);

            // ══════════════════════════════════════════════════════════════════
            //  FORM
            // ══════════════════════════════════════════════════════════════════
            AutoScaleDimensions = new SizeF(10F, 22F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(1280, 820);
            Controls.Add(mainTabControl);
            Controls.Add(lblTitle);
            Font = new Font("Microsoft Sans Serif", 9F);
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(1100, 700);
            Name = "ModelBuilderForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "ML Model Builder — Image Checker";

            // ── ResumeLayout ──────────────────────────────────────────────────
            foreach (var n in new NumericUpDown[] {
        numTrials, numCVFolds, numImageWidth, numImageHeight,
        numRoiX, numRoiY, numRoiW, numRoiH,
        numCnnEpochs, numCnnBatch, numCnnLR, numCnnEarlyStop,
        numOneClassImgW, numOneClassImgH, numOneClassLatent,
        numOneClassEpochs, numOneClassBatch, numOneClassSensitivity,
        numOneClassThresholdAdj })
                ((System.ComponentModel.ISupportInitialize)n).EndInit();

            foreach (var p in new PictureBox[] {
        picRoiSource, picRoiCrop, picOnnxTest, picOneClassTest })
                ((System.ComponentModel.ISupportInitialize)p).EndInit();

            groupBoxDataset.ResumeLayout(false);
            groupBoxDataset.PerformLayout();
            groupBoxOutput.ResumeLayout(false);
            groupBoxOutput.PerformLayout();
            groupBoxTraining.ResumeLayout(false);
            groupBoxTraining.PerformLayout();
            groupBoxModels.ResumeLayout(false);
            groupBoxModels.PerformLayout();
            groupBoxRoi.ResumeLayout(false);
            grpCnnConfig.ResumeLayout(false);
            grpCnnConfig.PerformLayout();
            grpOnnxTest.ResumeLayout(false);
            grpOnnxTest.PerformLayout();
            groupBoxCnn.ResumeLayout(false);
            grpOneClassConfig.ResumeLayout(false);
            grpOneClassConfig.PerformLayout();
            grpOneClassTest.ResumeLayout(false);
            grpOneClassTest.PerformLayout();
            groupBoxOneClass.ResumeLayout(false);
            groupBoxLog.ResumeLayout(false);

            tabDataset.ResumeLayout(false);
            tabOutput.ResumeLayout(false);
            tabTraining.ResumeLayout(false);
            tabRoi.ResumeLayout(false);
            tabCnn.ResumeLayout(false);
            tabOneClass.ResumeLayout(false);
            tabLog.ResumeLayout(false);

            mainTabControl.ResumeLayout(false);
            ResumeLayout(false);
        }

        // ── Custom tab drawing — coloured active tab ───────────────────────────
        private void MainTabControl_DrawItem(object? sender, DrawItemEventArgs e)
        {
            var tab = mainTabControl.TabPages[e.Index];
            var rect = mainTabControl.GetTabRect(e.Index);

            bool selected = e.Index == mainTabControl.SelectedIndex;

            using var bgBrush = new SolidBrush(selected
              ? Color.FromArgb(0, 120, 215)
              : Color.FromArgb(240, 240, 240));
            e.Graphics.FillRectangle(bgBrush, rect);

            using var textBrush = new SolidBrush(selected ? Color.White : Color.FromArgb(60, 60, 60));
            var fmt = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            e.Graphics.DrawString(tab.Text,
              new Font("Microsoft Sans Serif", 9F, selected ? FontStyle.Bold : FontStyle.Regular),
              textBrush, rect, fmt);
        }

        #endregion

        #endregion

        // ── Control declarations ───────────────────────────────────────────────
        private Label lblTitle;
        private TabControl mainTabControl;
        private TabPage tabDataset, tabOutput, tabTraining,
                            tabRoi, tabCnn, tabOneClass, tabLog;

        // Dataset
        private GroupBox groupBoxDataset;
        private Label lblDatasetPath;
        private Button btnSelectDataset;
        private TextBox txtDatasetPath;
        private Label lblDatasetInfo;

        // Output
        private GroupBox groupBoxOutput;
        private Label lblOutputPath;
        private Button btnSelectOutput;
        private TextBox txtOutputPath;

        // ML.NET Training
        private GroupBox groupBoxTraining;
        private GroupBox groupBoxModels;
        private CheckBox chkTransferLearning, chkLightGBM, chkFastTree, chkLBFGS, chkSDCA;
        private NumericUpDown numTrials, numCVFolds, numImageWidth, numImageHeight;
        private Label lblTrials, lblCVFolds, lblImageSize;
        private Button btnStartTraining, btnStopTraining;
        private ProgressBar progressBar;

        // ROI
        private GroupBox groupBoxRoi;
        private PictureBox picRoiSource, picRoiCrop;
        private Button btnPreviewRoi, btnRoiNext, btnRoiPrev, btnApplyRoi, btnGoToImage;
        private Label lblRoiInfo;
        private NumericUpDown numRoiX, numRoiY, numRoiW, numRoiH;
        private TextBox txtImageIndex;

        // CNN
        private GroupBox groupBoxCnn, grpCnnConfig, grpOnnxTest;
        private Label lblCnnArch, lblCnnEpochs, lblCnnBatch, lblCnnLR, lblCnnEarlyStop;
        private ComboBox cmbCnnArchitecture;
        private NumericUpDown numCnnEpochs, numCnnBatch, numCnnLR, numCnnEarlyStop;
        private CheckBox chkCnnAugment, chkExportOnnx, chkCnnUseGpu;
        private Button btnStartCnnTraining;
        private Label lblOnnxModel, lblTestImage, lblOnnxResult;
        private TextBox txtOnnxModelPath;
        private Button btnBrowseOnnx, btnBrowseTestImage, btnTestOnnx;
        private PictureBox picOnnxTest;

        // One-Class
        private GroupBox groupBoxOneClass, grpOneClassConfig, grpOneClassTest;
        private Label lblOneClassDesc, lblOkFolder, lblOneClassImgW, lblOneClassImgH;
        private Label lblOneClassLatent, lblOneClassEpochs, lblOneClassBatch;
        private Label lblOneClassSensitivity, lblOneClassModelPath;
        private Label lblOneClassTestImage, lblOneClassResult, lblOneClassThreshold;
        private TextBox txtOneClassOkFolder, txtOneClassModelPath;
        private Button btnBrowseOneClassOk, btnStartOneClass;
        private Button btnBrowseOneClassModel, btnBrowseOneClassTestImage;
        private Button btnTestOneClass, btnAdjustThreshold;
        private NumericUpDown numOneClassImgW, numOneClassImgH, numOneClassLatent;
        private NumericUpDown numOneClassEpochs, numOneClassBatch, numOneClassSensitivity;
        private NumericUpDown numOneClassThresholdAdj;
        private CheckBox chkOneClassAugment, chkOneClassGpu;
        private PictureBox picOneClassTest;
        // Add to your declarations at the bottom:
        private PictureBox picOneClassPreview;
        private Button btnLoadOneClassPreview;
        private Label lblOneClassPreviewSize;

        // Log
        private GroupBox groupBoxLog;
        private RichTextBox txtTrainingLog;
    }
}