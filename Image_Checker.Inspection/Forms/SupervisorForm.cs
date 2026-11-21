using Image_Checker.Inspection.Models;
using Image_Checker.Inspection.Services;
using Image_Checker.Utils;

namespace Image_Checker.Inspection.Forms;

/// <summary>
/// Supervisor screen. Scan the serial, then for each fixed side: show the side to the camera and press SPACE.
/// An OK result moves to the next step automatically. NG / CHECK waits for: retake, override or reject.
/// </summary>
public sealed class SupervisorForm : Form
{
    private readonly CameraService _camera = new();
    private readonly StepInspector _inspector = new();
    private readonly System.Windows.Forms.Timer _tmr = new() { Interval = 40 };

    private InspectionRecipe? _recipe;
    private StepResult?[] _results = Array.Empty<StepResult?>();
    private int[] _attempts = Array.Empty<int>();
    private int _step;
    private bool _partActive, _awaitingDecision, _busy, _loadingProduct;
    private string _serial = "";
    private string _finalText = "";

    private readonly ComboBox cmbProduct = new();
    private readonly TextBox txtSerial = new();
    private readonly Label lblCamera = new(), lblInstruction = new(), lblVerdict = new(),
                           lblDetail = new(), lblStatus = new();
    private readonly ListView lvSteps = new();
    private readonly PictureBox picPreview = new(), picCrop = new();
    private Button btnStart = null!, btnSetup = null!, btnCapture = null!, btnOverride = null!, btnReject = null!;

    public SupervisorForm()
    {
        BuildLayout();
        _tmr.Tick += OnTick;
        Shown += (_, _) => RefreshProducts(null);
    }

    // ══════════════════════════════════════════════════════════════
    //  LAYOUT
    // ══════════════════════════════════════════════════════════════

    private void BuildLayout()
    {
        Text = "Inspection - Supervisor";
        Font = new Font("Segoe UI", 10f);
        MinimumSize = new Size(1200, 760);
        Size = new Size(1400, 860);
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(Ui.Outer)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildContent(), 0, 1);
        root.Controls.Add(BuildFooter(), 0, 2);
        Controls.Add(root);
    }

    private Control BuildHeader()
    {
        var t = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 8,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, Ui.Inner + 4)
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        cmbProduct.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbProduct.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        cmbProduct.SelectedIndexChanged += OnProductChanged;

        txtSerial.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        txtSerial.PlaceholderText = "Scan or type serial, then Enter";
        txtSerial.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            StartPart();
        };

        btnStart = Ui.Btn("Start new part", 140, (_, _) => StartPart());
        btnSetup = Ui.Btn("Setup (engineer)...", 170, BtnSetup_Click);

        lblCamera.AutoSize = true;
        lblCamera.Anchor = AnchorStyles.Right;
        lblCamera.Margin = new Padding(0, 0, Ui.Inner, 0);
        lblCamera.Text = "Camera: -";

        t.Controls.Add(HeaderLabel("Product"), 0, 0);
        t.Controls.Add(cmbProduct, 1, 0);
        t.Controls.Add(HeaderLabel("Part serial"), 2, 0);
        t.Controls.Add(txtSerial, 3, 0);
        t.Controls.Add(btnStart, 4, 0);
        t.Controls.Add(lblCamera, 6, 0);
        t.Controls.Add(btnSetup, 7, 0);
        return t;
    }

    private static Label HeaderLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(Ui.Inner, 0, Ui.Inner, 0)
    };

    private Control BuildContent()
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 390));

        // steps
        var grpSteps = new GroupBox
        {
            Text = "Steps",
            Dock = DockStyle.Fill,
            Padding = new Padding(Ui.Inner),
            Margin = new Padding(0, 0, Ui.Inner, 0)
        };
        lvSteps.Dock = DockStyle.Fill;
        lvSteps.View = View.Details;
        lvSteps.FullRowSelect = true;
        lvSteps.GridLines = true;
        lvSteps.MultiSelect = false;
        lvSteps.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        lvSteps.Font = new Font("Segoe UI", 12f);
        lvSteps.Columns.Add("#", 40);
        lvSteps.Columns.Add("Step", 120);
        lvSteps.Columns.Add("Result", 140);
        lvSteps.SelectedIndexChanged += (_, _) => lvSteps.SelectedItems.Clear();
        grpSteps.Controls.Add(lvSteps);

        // live camera
        var grpLive = new GroupBox
        {
            Text = "Live camera (green box = area that is checked)",
            Dock = DockStyle.Fill,
            Padding = new Padding(Ui.Inner),
            Margin = new Padding(0, 0, Ui.Inner, 0)
        };
        var tblLive = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        tblLive.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        tblLive.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        lblInstruction.Dock = DockStyle.Fill;
        lblInstruction.Font = new Font("Segoe UI", 15f, FontStyle.Bold);
        lblInstruction.ForeColor = Ui.Blue;
        lblInstruction.TextAlign = ContentAlignment.MiddleLeft;
        picPreview.Dock = DockStyle.Fill;
        picPreview.SizeMode = PictureBoxSizeMode.Zoom;
        picPreview.BackColor = Color.Black;
        picPreview.Paint += PreviewPaint;
        tblLive.Controls.Add(lblInstruction, 0, 0);
        tblLive.Controls.Add(picPreview, 0, 1);
        grpLive.Controls.Add(tblLive);

        // result
        var grpResult = new GroupBox
        {
            Text = "Last result",
            Dock = DockStyle.Fill,
            Padding = new Padding(Ui.Inner)
        };
        var tblRes = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        tblRes.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        tblRes.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tblRes.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tblRes.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        lblVerdict.Dock = DockStyle.Fill;
        lblVerdict.Font = new Font("Segoe UI", 30f, FontStyle.Bold);
        lblVerdict.ForeColor = Color.White;
        lblVerdict.TextAlign = ContentAlignment.MiddleCenter;
        lblDetail.AutoSize = true;
        lblDetail.MaximumSize = new Size(350, 0);
        lblDetail.Margin = new Padding(0, Ui.Inner, 0, Ui.Inner);
        picCrop.Dock = DockStyle.Fill;
        picCrop.SizeMode = PictureBoxSizeMode.Zoom;
        picCrop.BorderStyle = BorderStyle.FixedSingle;
        var lblCropCaption = new Label
        {
            Text = "What the model checked (cropped area)",
            AutoSize = true,
            ForeColor = Ui.Grey,
            Margin = new Padding(0, Ui.Inner, 0, 0)
        };
        tblRes.Controls.Add(lblVerdict, 0, 0);
        tblRes.Controls.Add(lblDetail, 0, 1);
        tblRes.Controls.Add(picCrop, 0, 2);
        tblRes.Controls.Add(lblCropCaption, 0, 3);
        grpResult.Controls.Add(tblRes);

        t.Controls.Add(grpSteps, 0, 0);
        t.Controls.Add(grpLive, 1, 0);
        t.Controls.Add(grpResult, 2, 0);
        ClearVerdict("READY", Ui.Grey);
        return t;
    }

    private Control BuildFooter()
    {
        var t = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, Ui.Inner + 4, 0, 0)
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        lblStatus.Dock = DockStyle.Fill;
        lblStatus.TextAlign = ContentAlignment.MiddleLeft;
        lblStatus.ForeColor = Ui.Grey;

        var flow = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        btnCapture = Ui.Btn("Capture && Check  (Space)", 250, async (_, _) => await CaptureAsync(), Ui.Blue);
        btnCapture.Height = 44;
        btnOverride = Ui.Btn("Override as OK...", 160, BtnOverride_Click);
        btnOverride.Height = 44;
        btnReject = Ui.Btn("Reject part", 130, BtnReject_Click, Ui.Red);
        btnReject.Height = 44;
        flow.Controls.Add(btnCapture);   // right-aligned: first added = rightmost
        flow.Controls.Add(btnOverride);
        flow.Controls.Add(btnReject);

        t.Controls.Add(lblStatus, 0, 0);
        t.Controls.Add(flow, 1, 0);
        return t;
    }

    // ══════════════════════════════════════════════════════════════
    //  PRODUCT / CAMERA
    // ══════════════════════════════════════════════════════════════

    private void RefreshProducts(string? select)
    {
        _loadingProduct = true;
        cmbProduct.Items.Clear();
        var products = RecipeStore.ListProducts();
        foreach (var p in products) cmbProduct.Items.Add(p);

        int idx = select == null ? -1 : products.FindIndex(p => p.Equals(select, StringComparison.OrdinalIgnoreCase));
        if (idx < 0 && products.Count > 0) idx = 0;
        if (idx >= 0) cmbProduct.SelectedIndex = idx;
        _loadingProduct = false;

        if (idx >= 0) LoadProduct(products[idx]);
        else
        {
            _recipe = null;
            ResetPart();
            SetStatus("No product yet. Press 'Setup (engineer)...' to create one and train the steps.", true);
            UpdateButtons();
        }
    }

    private void OnProductChanged(object? sender, EventArgs e)
    {
        if (_loadingProduct || cmbProduct.SelectedItem is not string name) return;
        if (_partActive && MessageBox.Show(this, "A part is in progress. Abandon it and switch product?",
                "Switch product", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            _loadingProduct = true;
            cmbProduct.SelectedItem = _recipe?.Name;
            _loadingProduct = false;
            return;
        }
        LoadProduct(name);
    }

    private async void LoadProduct(string name)
    {
        try
        {
            _tmr.Stop();
            _camera.Stop();

            var recipe = RecipeStore.Load(name);
            if (recipe == null)
            {
                SetStatus($"Product '{name}' has no recipe. Open Setup.", true);
                return;
            }

            _recipe = recipe;
            ResetPart();
            _busy = true;
            UpdateButtons();
            SetStatus("Loading the models...");

            var issues = await Task.Run(() => _inspector.Load(recipe));
            if (!_camera.Start(recipe.CameraIndex, recipe.FrameWidth, recipe.FrameHeight, out var camError))
                issues.Add("Camera: " + camError);

            _tmr.Start();
            _busy = false;
            UpdateButtons();
            SetStatus(issues.Count == 0
                ? "Ready. Scan the serial and press Enter."
                : string.Join("   |   ", issues), issues.Count > 0);
        }
        catch (Exception ex)
        {
            _busy = false;
            UpdateButtons();
            SetStatus("Could not load the product: " + ex.Message, true);
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var frame = _camera.GetFrameCopy();
        if (frame != null)
        {
            var old = picPreview.Image;
            picPreview.Image = frame;
            old?.Dispose();
        }

        bool ok = _camera.IsHealthy;
        lblCamera.Text = ok ? "Camera: live" : "Camera: NO IMAGE";
        lblCamera.ForeColor = ok ? Ui.Green : Ui.Red;
    }

    private void PreviewPaint(object? sender, PaintEventArgs e)
    {
        if (_recipe == null || picPreview.Image == null || _recipe.Steps.Count == 0) return;
        var step = _recipe.Steps[Math.Min(_step, _recipe.Steps.Count - 1)];
        if (!step.HasRoi) return;
        var r = Ui.ToScreen(picPreview, step.Roi);
        using var pen = new Pen(Color.Lime, 2f);
        e.Graphics.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
    }

    private void BtnSetup_Click(object? sender, EventArgs e)
    {
        string? current = _recipe?.Name;
        string? selected;
        _tmr.Stop();
        _camera.Stop();   // the setup screen needs the camera for itself

        using (var f = new StepSetupForm(current))
        {
            f.ShowDialog(this);
            selected = f.ProductName ?? current;
        }
        RefreshProducts(selected);
    }

    // ══════════════════════════════════════════════════════════════
    //  PART FLOW
    // ══════════════════════════════════════════════════════════════

    private bool StartPart(bool askConfirm = true)
    {
        if (_recipe == null || _busy) return false;

        if (_partActive && _step > 0 && askConfirm &&
            MessageBox.Show(this, "The current part is not finished. Abandon it and start a new one?",
                "New part", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return false;

        string serial = txtSerial.Text.Trim();
        if (serial.Length == 0)
        {
            SetStatus("Scan or type the part serial first.", true);
            txtSerial.Focus();
            return false;
        }

        var missing = _recipe.Steps.Where(s => !_inspector.HasStep(s.Number)).Select(s => s.Title).ToList();
        if (missing.Count > 0)
        {
            SetStatus("Not ready - no model for: " + string.Join(", ", missing) + ". Train them in Setup.", true);
            return false;
        }

        ReleaseResults();
        int n = _recipe.Steps.Count;
        _serial = serial;
        _results = new StepResult?[n];
        _attempts = new int[n];
        _step = 0;
        _partActive = true;
        _awaitingDecision = false;
        _finalText = "";

        ClearVerdict("READY", Ui.Grey);
        RefreshSteps();
        UpdateInstruction();
        UpdateButtons();
        SetStatus($"Part {serial} started.");
        return true;
    }

    private async Task CaptureAsync()
    {
        if (_busy || _recipe == null) return;
        if (!_partActive && !StartPart(false)) return;

        var step = _recipe.Steps[_step];
        var frame = _camera.GetFrameCopy();
        if (frame == null)
        {
            SetStatus("No camera image - check the USB camera.", true);
            return;
        }

        _busy = true;
        UpdateButtons();
        lblVerdict.Text = "CHECKING...";
        lblVerdict.BackColor = Ui.Grey;

        string dir = Path.Combine(RecipeStore.ResultsDir(_recipe.Name), "images",
            DateTime.Now.ToString("yyyyMMdd"), PathUtils.SanitizeFileName(_serial));
        string stamp = DateTime.Now.ToString("HHmmssfff");

        StepResult result;
        try { result = await Task.Run(() => _inspector.Check(step, frame, dir, stamp)); }
        finally { frame.Dispose(); }

        _busy = false;
        if (IsDisposed) return;
        ApplyResult(step, result);
        UpdateButtons();
    }

    private void ApplyResult(StepConfig step, StepResult r)
    {
        _attempts[_step]++;
        var old = _results[_step];
        _results[_step] = r;
        ShowVerdict(step, r);
        old?.Crop?.Dispose();   // after picCrop now shows the new crop

        if (!InspectionLog.Append(_recipe!.Name, _serial, step.Number, step.Name, r.Text, r.NgScore,
                r.FullImagePath, $"attempt {_attempts[_step]}; {r.Detail}"))
            SetStatus("Could not write inspection_log.csv (is it open in Excel?)", true);

        if (r.Verdict == StepVerdict.Ok)
        {
            System.Media.SystemSounds.Asterisk.Play();
            Advance();
        }
        else
        {
            System.Media.SystemSounds.Exclamation.Play();
            _awaitingDecision = true;
            RefreshSteps();
            UpdateInstruction();
        }
    }

    private void Advance()
    {
        _awaitingDecision = false;
        _step++;
        if (_step >= _recipe!.Steps.Count) { FinishPart(true); return; }
        RefreshSteps();
        UpdateInstruction();
        UpdateButtons();
    }

    private void BtnOverride_Click(object? sender, EventArgs e)
    {
        if (!_awaitingDecision || _recipe == null) return;
        var r = _results[_step];
        var step = _recipe.Steps[_step];
        if (r == null) return;

        if (MessageBox.Show(this,
                $"{step.Title}: the system says {r.Text}.\n\nMark this step as OK on your own responsibility?",
                "Supervisor override", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        r.Overridden = true;
        InspectionLog.Append(_recipe.Name, _serial, step.Number, step.Name, "OVERRIDE_OK",
            r.NgScore, r.FullImagePath, "supervisor override");
        Advance();
    }

    private void BtnReject_Click(object? sender, EventArgs e)
    {
        if (!_partActive) return;
        if (MessageBox.Show(this, $"Reject part {_serial}?", "Reject part",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        FinishPart(false);
    }

    private void FinishPart(bool pass)
    {
        bool anyOverride = _results.Any(x => x?.Overridden == true);
        string verdict = pass ? (anyOverride ? "FINAL_PASS_WITH_OVERRIDE" : "FINAL_PASS") : "FINAL_FAIL";
        InspectionLog.Append(_recipe!.Name, _serial, 0, "FINAL", verdict, 0f, "", "");

        _partActive = false;
        _awaitingDecision = false;
        _finalText = $"Part {_serial}: {(pass ? "PASSED" : "REJECTED")}. Scan the next serial.";

        lblVerdict.Text = pass ? "PART PASSED" : "PART REJECTED";
        lblVerdict.Font = new Font("Segoe UI", 22f, FontStyle.Bold);
        lblVerdict.BackColor = pass ? Ui.Green : Ui.Red;
        lblDetail.Text = string.Join("\n", _recipe.Steps.Select((s, i) =>
            $"{s.Title}: {(_results[i]?.Text ?? "not checked")}"));

        (pass ? System.Media.SystemSounds.Asterisk : System.Media.SystemSounds.Hand).Play();
        RefreshSteps();
        UpdateInstruction();
        UpdateButtons();
        txtSerial.SelectAll();
        txtSerial.Focus();
    }

    // ══════════════════════════════════════════════════════════════
    //  UI STATE
    // ══════════════════════════════════════════════════════════════

    private void ShowVerdict(StepConfig step, StepResult r)
    {
        lblVerdict.Font = new Font("Segoe UI", 30f, FontStyle.Bold);
        lblVerdict.Text = r.Verdict == StepVerdict.Error ? "ERROR" : $"{r.Text}  {r.Confidence:P0}";
        lblVerdict.BackColor = r.Verdict switch
        {
            StepVerdict.Ok => Ui.Green,
            StepVerdict.Ng => Ui.Red,
            StepVerdict.Uncertain => Ui.Orange,
            _ => Ui.Grey
        };
        lblDetail.Text = $"{step.Title}\n{r.Detail}\nChecked in {r.Ms:0} ms";
        picCrop.Image = r.Crop;
        RefreshSteps();
    }

    private void ClearVerdict(string text, Color color)
    {
        lblVerdict.Font = new Font("Segoe UI", 30f, FontStyle.Bold);
        lblVerdict.Text = text;
        lblVerdict.BackColor = color;
        lblDetail.Text = "";
        picCrop.Image = null;
    }

    private void ResetPart()
    {
        ReleaseResults();
        int n = _recipe?.Steps.Count ?? 0;
        _results = new StepResult?[n];
        _attempts = new int[n];
        _step = 0;
        _partActive = false;
        _awaitingDecision = false;
        _finalText = "";
        ClearVerdict("READY", Ui.Grey);
        RefreshSteps();
        UpdateInstruction();
    }

    private void ReleaseResults()
    {
        picCrop.Image = null;
        foreach (var r in _results) r?.Crop?.Dispose();
        _results = Array.Empty<StepResult?>();
    }

    private void RefreshSteps()
    {
        lvSteps.BeginUpdate();
        lvSteps.Items.Clear();
        if (_recipe != null)
        {
            for (int i = 0; i < _recipe.Steps.Count; i++)
            {
                var s = _recipe.Steps[i];
                var r = i < _results.Length ? _results[i] : null;
                string text = "";
                Color back = Color.White;

                if (r != null)
                {
                    text = r.Overridden ? "OK (override)" : $"{r.Text}  {r.Confidence:P0}";
                    back = r.Overridden || r.Verdict == StepVerdict.Ok ? Color.FromArgb(220, 240, 220)
                         : r.Verdict == StepVerdict.Ng ? Color.FromArgb(250, 220, 220)
                         : Color.FromArgb(255, 236, 200);
                }
                else if (i == _step && _partActive)
                {
                    text = "waiting...";
                    back = Color.FromArgb(220, 235, 252);
                }

                lvSteps.Items.Add(new ListViewItem(new[] { s.Number.ToString(), s.Name, text }) { BackColor = back });
            }
        }
        lvSteps.EndUpdate();
    }

    private void UpdateInstruction()
    {
        if (_recipe == null) { lblInstruction.Text = "No product selected. Press Setup to create one."; return; }
        if (!_partActive && _finalText.Length > 0) { lblInstruction.Text = _finalText; return; }
        if (!_partActive) { lblInstruction.Text = "Scan or type the part serial and press Enter."; return; }

        int n = _recipe.Steps.Count;
        var s = _recipe.Steps[_step];
        lblInstruction.Text = _awaitingDecision
            ? $"Step {_step + 1} of {n} ({s.Name}) is not OK: retake with SPACE, or Override / Reject."
            : $"Step {_step + 1} of {n}: {s.Name} - show this side to the camera, then press SPACE.";
    }

    private void UpdateButtons()
    {
        bool ready = _recipe != null && !_busy;
        btnCapture.Enabled = ready;
        btnStart.Enabled = ready;
        btnSetup.Enabled = !_busy;
        btnOverride.Enabled = _awaitingDecision && !_busy;
        btnReject.Enabled = _partActive && !_busy;
    }

    private void SetStatus(string text, bool warning = false)
    {
        lblStatus.Text = text;
        lblStatus.ForeColor = warning ? Ui.Red : Ui.Grey;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Space && ActiveControl is not TextBoxBase && ActiveControl is not ComboBox)
        {
            _ = CaptureAsync();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _tmr.Stop();
        _camera.Dispose();
        _inspector.Dispose();
        base.OnFormClosing(e);
    }
}