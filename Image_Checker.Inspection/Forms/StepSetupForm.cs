using System.Drawing.Imaging;
using Image_Checker.Inspection.Models;
using Image_Checker.Inspection.Services;
using Image_Checker.Services;

namespace Image_Checker.Inspection.Forms;

/// <summary>
/// Engineer screen. Per product: choose the number of steps, then for each step
/// draw the ROI on the live camera, capture OK / NG images, train that step's model and test it.
/// </summary>
public sealed class StepSetupForm : Form
{
    private readonly CameraService _camera = new();
    private readonly System.Windows.Forms.Timer _tmr = new() { Interval = 40 };

    private InspectionRecipe? _recipe;
    private string _partId = NewPartId();
    private CancellationTokenSource? _cts;
    private bool _loading, _training, _dragging;
    private Point _dragStart;
    private Rectangle _roiBefore;

    private readonly ComboBox cmbProduct = new(), cmbResolution = new(), cmbStep = new();
    private readonly TextBox txtStepName = new(), txtLog = new();
    private readonly CheckBox chkLightGbm = new(), chkSdca = new(), chkLbfgs = new(), chkFastTree = new();
    private readonly Label lblCounts = new(), lblModel = new();
    private readonly PictureBox picPreview = new();
    private NumericUpDown nudSteps = null!, nudCamera = null!, nudTrials = null!, nudFolds = null!,
                          nudRoiX = null!, nudRoiY = null!, nudRoiW = null!, nudRoiH = null!;
    private Button btnLoadProduct = null!, btnConnect = null!, btnDataFolder = null!,
                   btnOkShot = null!, btnNgShot = null!, btnNewPart = null!, btnOpenFolder = null!,
                   btnTrain = null!, btnCancel = null!, btnTest = null!, btnSave = null!, btnClose = null!;

    /// <summary>The product that was being edited (so the supervisor screen can select it).</summary>
    public string? ProductName => _recipe?.Name;

    public StepSetupForm(string? product)
    {
        BuildLayout();
        _tmr.Tick += OnTick;
        Shown += (_, _) =>
        {
            cmbProduct.Items.AddRange(RecipeStore.ListProducts().Cast<object>().ToArray());
            if (product != null) { cmbProduct.Text = product; LoadOrCreateProduct(); }
            else Log("Type a product name and press 'Load / create product'. Data folder: " + RecipeStore.Root);
        };
    }

    private static string NewPartId() => "P" + DateTime.Now.ToString("yyMMddHHmmss");

    private static NumericUpDown Nud(int min, int max, int value) =>
        new() { Minimum = min, Maximum = max, Value = value, Width = 80 };

    // ══════════════════════════════════════════════════════════════
    //  LAYOUT
    // ══════════════════════════════════════════════════════════════

    private void BuildLayout()
    {
        Text = "Inspection setup - one model per step";
        Font = new Font("Segoe UI", 9f);
        MinimumSize = new Size(1200, 780);
        Size = new Size(1360, 880);
        StartPosition = FormStartPosition.CenterParent;
        KeyPreview = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(Ui.Outer)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // content: camera | settings
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));

        var grpLive = new GroupBox
        {
            Text = "Live camera  -  drag on the picture to draw the ROI of the selected step",
            Dock = DockStyle.Fill,
            Padding = new Padding(Ui.Inner),
            Margin = new Padding(0, 0, Ui.Inner, 0)
        };
        picPreview.Dock = DockStyle.Fill;
        picPreview.SizeMode = PictureBoxSizeMode.Zoom;
        picPreview.BackColor = Color.Black;
        picPreview.Paint += PreviewPaint;
        picPreview.MouseDown += PreviewMouseDown;
        picPreview.MouseMove += PreviewMouseMove;
        picPreview.MouseUp += PreviewMouseUp;
        grpLive.Controls.Add(picPreview);

        var right = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var stack = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        stack.Controls.Add(BuildProductGroup());
        stack.Controls.Add(BuildStepGroup());
        stack.Controls.Add(BuildCaptureGroup());
        stack.Controls.Add(BuildTrainGroup());
        right.Controls.Add(stack);

        content.Controls.Add(grpLive, 0, 0);
        content.Controls.Add(right, 1, 0);

        // log
        var grpLog = new GroupBox
        {
            Text = "Log",
            Dock = DockStyle.Fill,
            Padding = new Padding(Ui.Inner),
            Margin = new Padding(0, Ui.Inner, 0, 0)
        };
        txtLog.Dock = DockStyle.Fill;
        txtLog.Multiline = true;
        txtLog.ReadOnly = true;
        txtLog.ScrollBars = ScrollBars.Vertical;
        txtLog.Font = new Font("Consolas", 9f);
        grpLog.Controls.Add(txtLog);

        // footer (right aligned)
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, Ui.Inner, 0, 0)
        };
        btnClose = Ui.Btn("Close", 100, (_, _) => Close());
        btnSave = Ui.Btn("Save", 100, (_, _) => { SaveRecipe(); Log("Saved."); }, Ui.Blue);
        footer.Controls.Add(btnClose);
        footer.Controls.Add(btnSave);

        root.Controls.Add(content, 0, 0);
        root.Controls.Add(grpLog, 0, 1);
        root.Controls.Add(footer, 0, 2);
        Controls.Add(root);
    }

    private Control BuildProductGroup()
    {
        var g = Ui.Group("1. Product and camera", out var t);

        cmbProduct.DropDownStyle = ComboBoxStyle.DropDown;   // pick or type a new name
        Ui.Row(t, "Product", cmbProduct);

        nudSteps = Nud(1, 12, 5);
        nudSteps.ValueChanged += (_, _) =>
        {
            if (_loading || _recipe == null) return;
            _recipe.SetStepCount((int)nudSteps.Value);
            FillSteps();
        };
        Ui.Row(t, "Number of steps", nudSteps);

        nudCamera = Nud(0, 9, 0);
        Ui.Row(t, "Camera index", nudCamera);

        cmbResolution.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbResolution.Items.AddRange(new object[] { "640x480", "1280x720", "1920x1080" });
        cmbResolution.SelectedIndex = 1;
        Ui.Row(t, "Resolution", cmbResolution);

        btnLoadProduct = Ui.Btn("Load / create product", 160, (_, _) => LoadOrCreateProduct());
        btnConnect = Ui.Btn("Connect camera", 130, (_, _) => ConnectCamera());
        btnDataFolder = Ui.Btn("Data folder...", 110, (_, _) => ChooseDataFolder());
        var flow = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        flow.Controls.AddRange(new Control[] { btnLoadProduct, btnConnect, btnDataFolder });
        Ui.Span(t, flow);
        return g;
    }

    private Control BuildStepGroup()
    {
        var g = Ui.Group("2. Step (one fixed side of the product)", out var t);

        cmbStep.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbStep.SelectedIndexChanged += (_, _) => ShowStep();
        Ui.Row(t, "Step", cmbStep);

        txtStepName.TextChanged += (_, _) =>
        {
            var s = CurrentStep();
            if (!_loading && s != null) s.Name = txtStepName.Text.Trim();
        };
        Ui.Row(t, "Name", txtStepName);

        nudRoiX = Nud(0, 10000, 0);
        nudRoiY = Nud(0, 10000, 0);
        nudRoiW = Nud(0, 10000, 0);
        nudRoiH = Nud(0, 10000, 0);
        foreach (var n in new[] { nudRoiX, nudRoiY, nudRoiW, nudRoiH })
            n.ValueChanged += (_, _) => ReadRoiFromControls();

        var roiFlow = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        AddRoiCell(roiFlow, "X", nudRoiX);
        AddRoiCell(roiFlow, "Y", nudRoiY);
        AddRoiCell(roiFlow, "W", nudRoiW);
        AddRoiCell(roiFlow, "H", nudRoiH);
        Ui.Row(t, "ROI (pixels)", roiFlow);

        Ui.Row(t, "Trained model", lblModel);

        var hint = new Label
        {
            Text = "Changing the ROI after capturing images makes the old images inconsistent. Recapture and retrain.",
            AutoSize = true,
            MaximumSize = new Size(440, 0),
            ForeColor = Ui.Grey
        };
        Ui.Span(t, hint);
        return g;
    }

    private static void AddRoiCell(FlowLayoutPanel f, string label, NumericUpDown n)
    {
        f.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(Ui.Inner, 6, 2, 0) });
        n.Margin = new Padding(0, 2, 0, 0);
        f.Controls.Add(n);
    }

    private Control BuildCaptureGroup()
    {
        var g = Ui.Group("3. Capture training images (cropped to the ROI)", out var t);

        lblCounts.AutoSize = true;
        lblCounts.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        Ui.Span(t, lblCounts);

        btnOkShot = Ui.Btn("Capture OK  (F1)", 130, (_, _) => Capture("OK"), Ui.Green);
        btnNgShot = Ui.Btn("Capture NG  (F2)", 130, (_, _) => Capture("NG"), Ui.Red);
        btnNewPart = Ui.Btn("New part", 90, (_, _) => { _partId = NewPartId(); Log("New part id: " + _partId); });
        btnOpenFolder = Ui.Btn("Open folder", 100, (_, _) => OpenDatasetFolder());
        var flow = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        flow.Controls.AddRange(new Control[] { btnOkShot, btnNgShot, btnNewPart, btnOpenFolder });
        Ui.Span(t, flow);

        var hint = new Label
        {
            Text = "Capture each physical part in several positions. Press 'New part' when you change to another " +
                   "part, so the test split never contains the same part as the training split.",
            AutoSize = true,
            MaximumSize = new Size(440, 0),
            ForeColor = Ui.Grey
        };
        Ui.Span(t, hint);
        return g;
    }

    private Control BuildTrainGroup()
    {
        var g = Ui.Group("4. Train and test this step", out var t);

        chkLightGbm.Text = "LightGBM"; chkLightGbm.Checked = true; chkLightGbm.AutoSize = true;
        chkSdca.Text = "SDCA"; chkSdca.Checked = true; chkSdca.AutoSize = true;
        chkLbfgs.Text = "LBFGS"; chkLbfgs.AutoSize = true;
        chkFastTree.Text = "FastTree (slow)"; chkFastTree.AutoSize = true;
        var algos = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        algos.Controls.AddRange(new Control[] { chkLightGbm, chkSdca, chkLbfgs, chkFastTree });
        Ui.Row(t, "Algorithms", algos);

        nudTrials = Nud(1, 30, 5);
        Ui.Row(t, "Tuning trials", nudTrials);
        nudFolds = Nud(2, 10, 3);
        Ui.Row(t, "CV folds", nudFolds);

        btnTrain = Ui.Btn("Train step model", 140, BtnTrain_Click, Ui.Blue);
        btnCancel = Ui.Btn("Cancel", 80, (_, _) => _cts?.Cancel());
        btnCancel.Enabled = false;
        btnTest = Ui.Btn("Test on live image", 140, BtnTest_Click);
        var flow = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        flow.Controls.AddRange(new Control[] { btnTrain, btnCancel, btnTest });
        Ui.Span(t, flow);
        return g;
    }

    // ══════════════════════════════════════════════════════════════
    //  PRODUCT / STEP
    // ══════════════════════════════════════════════════════════════

    private StepConfig? CurrentStep() =>
        _recipe != null && cmbStep.SelectedIndex >= 0 && cmbStep.SelectedIndex < _recipe.Steps.Count
            ? _recipe.Steps[cmbStep.SelectedIndex]
            : null;

    private void LoadOrCreateProduct()
    {
        string name = cmbProduct.Text.Trim();
        if (name.Length == 0) { Warn("Type a product name first."); return; }

        var r = RecipeStore.Load(name);
        if (r == null)
        {
            r = new InspectionRecipe { Name = name };
            r.SetStepCount((int)nudSteps.Value);
            RecipeStore.Save(r);
            Log($"Created product '{name}' with {r.Steps.Count} steps.");
        }
        else Log($"Loaded product '{name}'.");

        _loading = true;
        _recipe = r;
        nudSteps.Value = Math.Clamp(r.Steps.Count, (int)nudSteps.Minimum, (int)nudSteps.Maximum);
        nudCamera.Value = Math.Clamp(r.CameraIndex, 0, 9);
        string res = $"{r.FrameWidth}x{r.FrameHeight}";
        int ri = cmbResolution.Items.IndexOf(res);
        cmbResolution.SelectedIndex = ri >= 0 ? ri : 1;
        _loading = false;
        FillSteps();
    }

    private void FillSteps()
    {
        if (_recipe == null) return;
        int keep = cmbStep.SelectedIndex;
        _loading = true;
        cmbStep.Items.Clear();
        foreach (var s in _recipe.Steps) cmbStep.Items.Add($"Step {s.Number}");
        cmbStep.SelectedIndex = keep >= 0 && keep < cmbStep.Items.Count ? keep : 0;
        _loading = false;
        ShowStep();
    }

    private void ShowStep()
    {
        var s = CurrentStep();
        if (s == null) return;
        _loading = true;
        txtStepName.Text = s.Name;
        nudRoiX.Value = Math.Min(s.RoiX, (int)nudRoiX.Maximum);
        nudRoiY.Value = Math.Min(s.RoiY, (int)nudRoiY.Maximum);
        nudRoiW.Value = Math.Min(s.RoiW, (int)nudRoiW.Maximum);
        nudRoiH.Value = Math.Min(s.RoiH, (int)nudRoiH.Maximum);
        lblModel.Text = s.HasModel ? Path.GetFileName(s.ModelPath) : "(not trained yet)";
        lblModel.ForeColor = s.HasModel ? Ui.Green : Ui.Grey;
        _loading = false;
        UpdateCounts();
        picPreview.Invalidate();
    }

    private void ReadRoiFromControls()
    {
        var s = CurrentStep();
        if (_loading || s == null) return;
        s.Roi = new Rectangle((int)nudRoiX.Value, (int)nudRoiY.Value, (int)nudRoiW.Value, (int)nudRoiH.Value);
        picPreview.Invalidate();
    }

    private void ReadSettings()
    {
        if (_recipe == null) return;
        _recipe.CameraIndex = (int)nudCamera.Value;
        var parts = cmbResolution.Text.Split('x');
        if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
        {
            _recipe.FrameWidth = w;
            _recipe.FrameHeight = h;
        }
    }

    private void SaveRecipe()
    {
        if (_recipe == null) return;
        ReadSettings();
        try { RecipeStore.Save(_recipe); }
        catch (Exception ex) { Log("Could not save the recipe: " + ex.Message); }
    }

    private void ConnectCamera()
    {
        if (_recipe == null) { Warn("Load or create a product first."); return; }
        ReadSettings();
        _tmr.Stop();
        if (_camera.Start(_recipe.CameraIndex, _recipe.FrameWidth, _recipe.FrameHeight, out var error))
        {
            _tmr.Start();
            Log($"Camera {_recipe.CameraIndex} connected ({_recipe.FrameWidth}x{_recipe.FrameHeight}).");
        }
        else Log("Camera: " + error);
    }

    private void ChooseDataFolder()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Folder that holds all products, datasets, models and results",
            SelectedPath = RecipeStore.Root,
            UseDescriptionForTitle = true
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        RecipeStore.SetRoot(dlg.SelectedPath);
        cmbProduct.Items.Clear();
        cmbProduct.Items.AddRange(RecipeStore.ListProducts().Cast<object>().ToArray());
        Log("Data folder: " + RecipeStore.Root);
    }

    // ══════════════════════════════════════════════════════════════
    //  PREVIEW + ROI DRAWING
    // ══════════════════════════════════════════════════════════════

    private void OnTick(object? sender, EventArgs e)
    {
        var frame = _camera.GetFrameCopy();
        if (frame == null) return;
        var old = picPreview.Image;
        picPreview.Image = frame;
        old?.Dispose();
    }

    private void PreviewPaint(object? sender, PaintEventArgs e)
    {
        if (picPreview.Image == null)
        {
            TextRenderer.DrawText(e.Graphics, "Press 'Connect camera'", Font, picPreview.ClientRectangle,
                Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }
        var s = CurrentStep();
        if (s == null || !s.HasRoi) return;
        var r = Ui.ToScreen(picPreview, s.Roi);
        using var pen = new Pen(Color.Lime, 2f);
        e.Graphics.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
    }

    private void PreviewMouseDown(object? sender, MouseEventArgs e)
    {
        var s = CurrentStep();
        if (s == null || picPreview.Image == null || e.Button != MouseButtons.Left) return;
        _dragging = true;
        _dragStart = Ui.ToImage(picPreview, e.Location);
        _roiBefore = s.Roi;
    }

    private void PreviewMouseMove(object? sender, MouseEventArgs e)
    {
        var s = CurrentStep();
        if (!_dragging || s == null) return;
        var p = Ui.ToImage(picPreview, e.Location);
        SetRoi(s, new Rectangle(Math.Min(_dragStart.X, p.X), Math.Min(_dragStart.Y, p.Y),
                                Math.Abs(p.X - _dragStart.X), Math.Abs(p.Y - _dragStart.Y)));
    }

    private void PreviewMouseUp(object? sender, MouseEventArgs e)
    {
        var s = CurrentStep();
        if (!_dragging || s == null) return;
        _dragging = false;
        if (s.RoiW < 32 || s.RoiH < 32) SetRoi(s, _roiBefore);   // a click, not a box
        else Log($"{s.Title}: ROI {s.RoiX},{s.RoiY}  {s.RoiW}x{s.RoiH}");
    }

    private void SetRoi(StepConfig s, Rectangle r)
    {
        s.Roi = r;
        _loading = true;
        nudRoiX.Value = Math.Min(r.X, (int)nudRoiX.Maximum);
        nudRoiY.Value = Math.Min(r.Y, (int)nudRoiY.Maximum);
        nudRoiW.Value = Math.Min(r.Width, (int)nudRoiW.Maximum);
        nudRoiH.Value = Math.Min(r.Height, (int)nudRoiH.Maximum);
        _loading = false;
        picPreview.Invalidate();
    }

    // ══════════════════════════════════════════════════════════════
    //  CAPTURE
    // ══════════════════════════════════════════════════════════════

    private void Capture(string label)
    {
        var step = CurrentStep();
        if (step == null || _recipe == null) { Warn("Load or create a product first."); return; }
        if (!step.HasRoi) { Warn("Draw the ROI on the live picture first (drag with the mouse)."); return; }

        using var frame = _camera.GetFrameCopy();
        if (frame == null) { Warn("No camera image. Press 'Connect camera'."); return; }

        try
        {
            using var crop = ExplainImaging.CropToRoi(frame, step.Roi);
            string dir = Path.Combine(RecipeStore.StepDatasetDir(_recipe.Name, step.Number), label);
            Directory.CreateDirectory(dir);
            // text before "___" is the part id: ModelTrainer keeps one part on one side of the train/test split
            string file = $"{_partId}___S{step.Number}__{DateTime.Now:HHmmssfff}.png";
            crop.Save(Path.Combine(dir, file), ImageFormat.Png);
            UpdateCounts();
        }
        catch (Exception ex)
        {
            Log("Capture failed: " + ex.Message);
        }
    }

    private int CountImages(string label)
    {
        var s = CurrentStep();
        if (s == null || _recipe == null) return 0;
        string dir = Path.Combine(RecipeStore.StepDatasetDir(_recipe.Name, s.Number), label);
        return Directory.Exists(dir) ? Directory.GetFiles(dir).Length : 0;
    }

    private void UpdateCounts()
    {
        var s = CurrentStep();
        lblCounts.Text = s == null ? "" : $"{s.Title}:   OK {CountImages("OK")}   |   NG {CountImages("NG")}";
    }

    private void OpenDatasetFolder()
    {
        var s = CurrentStep();
        if (s == null || _recipe == null) return;
        string dir = RecipeStore.StepDatasetDir(_recipe.Name, s.Number);
        Directory.CreateDirectory(dir);
        System.Diagnostics.Process.Start("explorer.exe", dir);
    }

    // ══════════════════════════════════════════════════════════════
    //  TRAIN / TEST
    // ══════════════════════════════════════════════════════════════

    private async void BtnTrain_Click(object? sender, EventArgs e)
    {
        var step = CurrentStep();
        if (step == null || _recipe == null) { Warn("Load or create a product first."); return; }
        if (!step.HasRoi) { Warn("Draw the ROI of this step first."); return; }

        int ok = CountImages("OK"), ng = CountImages("NG");
        if (ok == 0 || ng == 0) { Warn("Capture images for both OK and NG first."); return; }
        if (!chkLightGbm.Checked && !chkSdca.Checked && !chkLbfgs.Checked && !chkFastTree.Checked)
        { Warn("Select at least one algorithm."); return; }
        if ((ok < 20 || ng < 20) && MessageBox.Show(this,
                $"Only {ok} OK and {ng} NG images. At least 20-30 of each is recommended.\n\nTrain anyway?",
                "Few images", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        SaveRecipe();
        string dataset = RecipeStore.StepDatasetDir(_recipe.Name, step.Number);
        var options = new TrainOptions(chkSdca.Checked, chkLbfgs.Checked, chkFastTree.Checked,
                                       chkLightGbm.Checked, (int)nudTrials.Value, (int)nudFolds.Value);
        var roi = step.Roi;

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        SetTraining(true);
        Log($"=== Training {step.Title} ({ok} OK / {ng} NG) ===");

        try
        {
            string model = await Task.Run(() => StepTrainer.Train(dataset, roi, options, Log, ct), ct);
            step.ModelPath = model;
            SaveRecipe();
            ShowStep();
            Log($"=== {step.Title}: model ready -> {Path.GetFileName(model)} ===");
            MessageBox.Show(this, $"{step.Title}: model trained.\n\n{Path.GetFileName(model)}",
                "Training done", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException) { Log("Training cancelled."); }
        catch (Exception ex)
        {
            Log("Training failed: " + ex.GetBaseException().Message);
            MessageBox.Show(this, ex.GetBaseException().Message, "Training failed",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetTraining(false);
        }
    }

    private async void BtnTest_Click(object? sender, EventArgs e)
    {
        var step = CurrentStep();
        if (step == null) return;
        if (!step.HasModel) { Warn("Train this step first."); return; }

        var frame = _camera.GetFrameCopy();
        if (frame == null) { Warn("No camera image. Press 'Connect camera'."); return; }

        btnTest.Enabled = false;
        try
        {
            var result = await Task.Run(() =>
            {
                using var inspector = new StepInspector();
                if (!inspector.LoadStep(step, out var error)) throw new InvalidOperationException(error);
                return inspector.Check(step, frame,
                    Path.Combine(Path.GetTempPath(), "ic_inspection_test"), DateTime.Now.ToString("HHmmssfff"));
            });
            Log($"TEST {step.Title}: {result.Text}  -  {result.Detail}  ({result.Ms:0} ms)");
            result.Crop?.Dispose();
        }
        catch (Exception ex)
        {
            Log("Test failed: " + ex.Message);
        }
        finally
        {
            frame.Dispose();
            btnTest.Enabled = true;
        }
    }

    private void SetTraining(bool on)
    {
        _training = on;
        btnTrain.Enabled = !on;
        btnCancel.Enabled = on;
        btnOkShot.Enabled = btnNgShot.Enabled = !on;
        btnLoadProduct.Enabled = !on;
        btnTest.Enabled = !on;
        cmbStep.Enabled = !on;
    }

    // ══════════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════════

    private void Log(string message)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action(() => Log(message))); } catch (InvalidOperationException) { }
            return;
        }
        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private void Warn(string text) =>
        MessageBox.Show(this, text, "Setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F1) { Capture("OK"); return true; }
        if (keyData == Keys.F2) { Capture("NG"); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_training)
        {
            if (MessageBox.Show(this, "Training is running. Cancel it and close?", "Setup",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }
            _cts?.Cancel();
        }

        _tmr.Stop();
        _camera.Dispose();
        SaveRecipe();
        base.OnFormClosing(e);
    }
}