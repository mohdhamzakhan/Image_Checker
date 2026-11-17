using Image_Checker.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Image_Checker.WinForm
{
    // "Why is the model's answer different from the folder?"
    //  - every monitored mismatch gets a one-line reason (grid column "Why" + monitor.log)
    //  - double-click any grid row to open the full explanation window
    public partial class Form1
    {
        private TrainingStats? _trainingStats;
        private bool _explainWired;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!_explainWired)
            {
                _explainWired = true;
                grid.CellDoubleClick += Grid_CellDoubleClick;
            }
        }

        private void Grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            string? path = grid.Rows[e.RowIndex].Cells["ImagePath"].Value?.ToString();
            var result = _results.FirstOrDefault(r => r.ImagePath == path);
            ExplainImage(path, result?.Subfolder);
        }

        /// <summary>Opens the explanation window for one image. Works while monitoring keeps running.</summary>
        private void ExplainImage(string? imagePath, string? folderLabel)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                MessageBox.Show("The image file no longer exists.", "Why?",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_activeModelType != ModelType.MlNet || string.IsNullOrEmpty(_modelPath) || !File.Exists(_modelPath))
            {
                MessageBox.Show(
                    "The explanation is available for ML.NET models (.zip). Load one from Settings first.",
                    "Why?", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // The window gets its OWN model instance: the main one is used by monitoring and is not thread-safe.
            string modelPath = _modelPath;
            var dialog = new ExplainForm(imagePath, folderLabel, _roiRect, _basePath,
                () => new PredictorScorer(new Predictor(modelPath), true));
            dialog.Show(this);   // modeless: monitoring continues while it is open
        }

        /// <summary>
        /// Starts reading a sample of the training images in the background so the first mismatch can
        /// already say "this crop is blurrier than usual for OK images".
        /// </summary>
        private void WarmTrainingStats()
        {
            string basePath = _basePath;
            Rectangle roi = _roiRect;
            if (string.IsNullOrEmpty(basePath)) return;

            Task.Run(() =>
            {
                try { _trainingStats = TrainingStatsBuilder.GetCached(basePath, roi); }
                catch (Exception ex) { LogDiag("TRAINING STATS ERROR: " + ex.Message); }
            });
        }

        /// <summary>
        /// Quick explanation for a mismatch found by monitoring. Runs on the UI thread with the main
        /// model (one extra prediction, only for mismatches). <paramref name="processedPath"/> is the
        /// ROI crop that was just predicted and still exists on disk.
        /// </summary>
        private Explanation? QuickExplain(string processedPath, string originalPath, string folderLabel)
        {
            if (_activeModelType != ModelType.MlNet || _predictor == null) return null;

            ScoreResult score = new PredictorScorer(_predictor, false).Score(processedPath);

            ImageStats? stats = null;
            try
            {
                using (Bitmap crop = ExplainImaging.LoadCopy(processedPath))
                    stats = ExplainImaging.ComputeStats(crop);
            }
            catch { /* statistics are optional */ }

            int width = 0, height = 0;
            try
            {
                using (Image img = Image.FromFile(originalPath))
                {
                    width = img.Width;
                    height = img.Height;
                }
            }
            catch { }

            TrainingStats? training = _trainingStats;
            return MismatchExplainer.Explain(new ExplainInput
            {
                FolderLabel = folderLabel,
                KnownLabels = training == null ? null : training.Labels.ToList(),
                Score = score,
                Stats = stats,
                Training = training,
                OriginalWidth = width,
                OriginalHeight = height,
                RoiWidth = _roiRect.Width,
                RoiHeight = _roiRect.Height
            });
        }
    }
}