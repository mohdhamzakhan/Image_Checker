using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Image_Checker.WinForm
{
    public partial class CorrectionsManagerForm : Form
    {
        private readonly string _basePath;
        private readonly string _correctionsPath;
        private List<CorrectionEntry> _corrections;
        private List<CorrectionEntry> _filteredCorrections;
        private CorrectionEntry _selectedCorrection;
        private bool _hasChanges = false;

        public CorrectionsManagerForm(string basePath)
        {
            _basePath = basePath;
            _correctionsPath = Path.Combine(basePath, "corrections.csv");
           

            InitializeComponent();

            // Set initial combo box selection

            LoadCorrections();
            UpdateStats();
            cboFilterLabel.SelectedIndex = 0;
        }

        #region Data Loading

        private void LoadCorrections()
        {
            _corrections = new List<CorrectionEntry>();

            if (!File.Exists(_correctionsPath))
            {
                gridCorrections.DataSource = null;
                return;
            }

            try
            {
                var lines = File.ReadAllLines(_correctionsPath).Skip(1); // Skip header

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = line.Split(',');
                    if (parts.Length < 5)
                        continue;

                    var entry = new CorrectionEntry
                    {
                        Timestamp = parts[0].Trim('"'),
                        ImagePath = parts[1].Trim('"'),
                        FileName = Path.GetFileName(parts[1].Trim('"')),
                        OriginalLabel = parts[2].Trim('"'),
                        Confidence = float.Parse(parts[3].Trim('"')),
                        CorrectedLabel = parts[4].Trim('"')
                    };

                    _corrections.Add(entry);
                }

                _filteredCorrections = new List<CorrectionEntry>(_corrections);
                gridCorrections.DataSource = _filteredCorrections;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading corrections:\n{ex.Message}",
                    "Load Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ApplyFilters()
        {
            var searchText = txtSearch.Text.Trim().ToLower();
            var filterLabel = cboFilterLabel.SelectedItem?.ToString();

            _filteredCorrections = _corrections.Where(c =>
            {
                var matchesSearch = string.IsNullOrEmpty(searchText) ||
                                  c.FileName.ToLower().Contains(searchText);

                var matchesFilter = filterLabel == "All" ||
                                  string.IsNullOrEmpty(filterLabel) ||
                                  c.CorrectedLabel == filterLabel;

                return matchesSearch && matchesFilter;
            }).ToList();

            gridCorrections.DataSource = null;
            gridCorrections.DataSource = _filteredCorrections;
            UpdateStats();
        }

        private void UpdateStats()
        {
            int total = _corrections.Count;
            int ok = _corrections.Count(c => c.CorrectedLabel == "OK");
            int ng = _corrections.Count(c => c.CorrectedLabel == "NG");
            int filtered = _filteredCorrections?.Count ?? 0;

            lblStats.Text = $"📊 Total Corrections: {total} | OK: {ok} | NG: {ng}";

            if (filtered < total)
            {
                lblStats.Text += $" | Showing: {filtered}";
            }
        }

        #endregion

        #region Grid Events

        private void GridCorrections_SelectionChanged(object sender, EventArgs e)
        {
            if (gridCorrections.SelectedRows.Count == 0)
            {
                ClearPreview();
                return;
            }

            var row = gridCorrections.SelectedRows[0];
            _selectedCorrection = row.DataBoundItem as CorrectionEntry;

            if (_selectedCorrection == null)
            {
                ClearPreview();
                return;
            }

            // Load image preview
            if (File.Exists(_selectedCorrection.ImagePath))
            {
                try
                {
                    picturePreview.Image?.Dispose();
                    picturePreview.Image = Image.FromFile(_selectedCorrection.ImagePath);

                    lblImageInfo.Text = $"📷 {_selectedCorrection.FileName} | " +
                                      $"Original: {_selectedCorrection.OriginalLabel} ({_selectedCorrection.Confidence:P2}) → " +
                                      $"Corrected: {_selectedCorrection.CorrectedLabel}";

                    // Enable edit controls
                    cboEditLabel.Enabled = true;
                    cboEditLabel.SelectedItem = _selectedCorrection.CorrectedLabel;
                    btnDelete.Enabled = true;
                }
                catch (Exception ex)
                {
                    lblImageInfo.Text = $"❌ Error loading image: {ex.Message}";
                    picturePreview.Image = null;
                }
            }
            else
            {
                lblImageInfo.Text = $"⚠️ Image file not found: {_selectedCorrection.ImagePath}";
                picturePreview.Image = null;
            }
        }

        private void GridCorrections_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            // Delete button column
            if (gridCorrections.Columns[e.ColumnIndex].Name == "colDelete")
            {
                var entry = _filteredCorrections[e.RowIndex];
                DeleteCorrection(entry);
            }
        }

        private void GridCorrections_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            if (gridCorrections.Columns[e.ColumnIndex].Name == "colCorrectedLabel")
            {
                _hasChanges = true;
                btnSave.Enabled = true;
            }
        }

        #endregion

        #region Button Click Handlers

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (_selectedCorrection == null)
                return;

            var newLabel = cboEditLabel.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(newLabel))
            {
                MessageBox.Show("Please select a label.", "Invalid Input",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_selectedCorrection.CorrectedLabel == newLabel)
            {
                MessageBox.Show("Label is the same. No change needed.", "No Change",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Update in memory
            _selectedCorrection.CorrectedLabel = newLabel;
            _hasChanges = true;

            // Save to file
            SaveCorrectionsToFile();

            // Refresh grid
            gridCorrections.Refresh();
            UpdateStats();

            MessageBox.Show($"✅ Label updated to: {newLabel}", "Success",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (_selectedCorrection == null)
                return;

            DeleteCorrection(_selectedCorrection);
        }

        private void DeleteCorrection(CorrectionEntry entry)
        {
            var result = MessageBox.Show(
                $"Delete this correction?\n\n" +
                $"File: {entry.FileName}\n" +
                $"Label: {entry.CorrectedLabel}",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            // Remove from lists
            _corrections.Remove(entry);
            _filteredCorrections.Remove(entry);
            _hasChanges = true;

            // Save to file
            SaveCorrectionsToFile();

            // Refresh grid
            gridCorrections.DataSource = null;
            gridCorrections.DataSource = _filteredCorrections;
            UpdateStats();

            MessageBox.Show("✅ Correction deleted", "Success",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnDeleteAll_Click(object sender, EventArgs e)
        {
            if (_corrections.Count == 0)
            {
                MessageBox.Show("No corrections to delete.", "Nothing to Delete",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show(
                $"⚠️ DELETE ALL CORRECTIONS?\n\n" +
                $"This will permanently delete all {_corrections.Count} corrections.\n" +
                $"This action cannot be undone!\n\n" +
                $"Continue?",
                "Confirm Delete All",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            // Backup before deleting
            var backupPath = Path.Combine(_basePath, $"corrections_backup_{DateTime.Now:yyyyMMddHHmmss}.csv");
            try
            {
                File.Copy(_correctionsPath, backupPath, true);
            }
            catch { }

            // Clear all
            _corrections.Clear();
            _filteredCorrections.Clear();
            _hasChanges = true;

            // Save empty file
            SaveCorrectionsToFile();

            // Refresh UI
            gridCorrections.DataSource = null;
            ClearPreview();
            UpdateStats();

            MessageBox.Show(
                $"✅ All corrections deleted\n\n" +
                $"Backup saved: {Path.GetFileName(backupPath)}",
                "Deleted",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            LoadCorrections();
            UpdateStats();
            //ApplyFilters();
            MessageBox.Show("✅ Corrections reloaded", "Refreshed",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                FileName = $"corrections_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                DefaultExt = "csv"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    File.Copy(_correctionsPath, dialog.FileName, true);
                    MessageBox.Show($"✅ Exported to:\n{dialog.FileName}", "Export Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed:\n{ex.Message}", "Export Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        #endregion

        #region Filter Events

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            //ApplyFilters();
        }

        private void CboFilterLabel_SelectedIndexChanged(object sender, EventArgs e)
        {
            ApplyFilters();
        }

        #endregion

        #region Helper Methods

        private void SaveCorrectionsToFile()
        {
            try
            {
                var lines = new List<string>
                {
                    "Timestamp,ImagePath,OriginalLabel,Confidence,CorrectedLabel"
                };

                lines.AddRange(_corrections.Select(c =>
                    $"{c.Timestamp},{c.ImagePath},{c.OriginalLabel},{c.Confidence},{c.CorrectedLabel}"));

                File.WriteAllLines(_correctionsPath, lines);
                _hasChanges = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving corrections:\n{ex.Message}",
                    "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ClearPreview()
        {
            picturePreview.Image?.Dispose();
            picturePreview.Image = null;
            lblImageInfo.Text = "Select a correction to preview";
            cboEditLabel.Enabled = false;
            btnSave.Enabled = false;
            btnDelete.Enabled = false;
            _selectedCorrection = null;
        }

        #endregion

        #region Form Events

        private void CorrectionsManagerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_hasChanges)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Save before closing?",
                    "Unsaved Changes",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    SaveCorrectionsToFile();
                }
                else if (result == DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }

            picturePreview.Image?.Dispose();
        }

        #endregion
    }

    #region Data Model

    /// <summary>
    /// Represents a single correction entry
    /// </summary>
    public class CorrectionEntry
    {
        public string Timestamp { get; set; }
        public string ImagePath { get; set; }
        public string FileName { get; set; }
        public string OriginalLabel { get; set; }
        public float Confidence { get; set; }
        public string CorrectedLabel { get; set; }

        public string ConfidenceDisplay => $"{Confidence:P2}";
    }

    #endregion
}