using System;
using System.Drawing;
using System.Windows.Forms;

namespace Image_Checker.WinForm
{
    /// <summary>
    /// Small modal dialog for configuring a one-class incremental
    /// (warm-start) fine-tune before it runs. Built entirely in code
    /// rather than the designer so it doesn't touch Form1.Designer.cs.
    /// </summary>
    public partial class FineTuneSettingsForm : Form
    {
        public int FineTuneEpochs { get; private set; }
        public float LearningRateMultiplier { get; private set; }

        private readonly NumericUpDown _numEpochs;
        private readonly NumericUpDown _numLrMultiplier;

        public FineTuneSettingsForm(int defaultEpochs = 5, float defaultLrMultiplier = 0.1f)
        {
            FineTuneEpochs = defaultEpochs;
            LearningRateMultiplier = defaultLrMultiplier;

            Text = "One-Class Fine-Tune Settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(380, 210);
            Font = new Font("Segoe UI", 9f);

            var lblEpochs = new Label
            {
                Text = "Fine-tune epochs:",
                Left = 20,
                Top = 22,
                Width = 180
            };
            _numEpochs = new NumericUpDown
            {
                Left = 210,
                Top = 20,
                Width = 140,
                Minimum = 1,
                Maximum = 200,
                Value = defaultEpochs
            };

            var lblLr = new Label
            {
                Text = "Learning-rate multiplier:",
                Left = 20,
                Top = 60,
                Width = 180
            };
            _numLrMultiplier = new NumericUpDown
            {
                Left = 210,
                Top = 58,
                Width = 140,
                Minimum = 0.01m,
                Maximum = 1.00m,
                Increment = 0.01m,
                DecimalPlaces = 2,
                Value = (decimal)defaultLrMultiplier
            };

            var lblHint = new Label
            {
                Text =
                    "More epochs / higher multiplier → learns the corrections faster,\n" +
                    "but risks drifting away from what the model already knew.\n\n" +
                    "Fewer epochs / lower multiplier → safer, gentler nudge, but may\n" +
                    "need a few correction rounds before it fully sticks.",
                Left = 20,
                Top = 96,
                Width = 340,
                Height = 75,
                ForeColor = Color.DimGray
            };

            var btnOk = new Button
            {
                Text = "Start Fine-Tune",
                Left = 130,
                Top = 175,
                Width = 120,
                DialogResult = DialogResult.OK
            };
            var btnCancel = new Button
            {
                Text = "Cancel",
                Left = 260,
                Top = 175,
                Width = 90,
                DialogResult = DialogResult.Cancel
            };

            btnOk.Click += (_, _) =>
            {
                FineTuneEpochs = (int)_numEpochs.Value;
                LearningRateMultiplier = (float)_numLrMultiplier.Value;
            };

            Controls.AddRange(new Control[]
            {
                lblEpochs, _numEpochs,
                lblLr, _numLrMultiplier,
                lblHint,
                btnOk, btnCancel
            });

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}