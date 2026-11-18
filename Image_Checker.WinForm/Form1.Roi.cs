using Image_Checker.Services;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Image_Checker.WinForm
{
	// The ROI (the cropped area the model was trained on) is stored ONLY in the "<model>.json" file next to
	// the model. If that file is missing, photos are given to the model whole and a model trained on a cropped
	// dataset flags good images as NG. This file loads the ROI and makes a missing one impossible to miss.
	public partial class Form1
	{
		// seams so the dialogs can be replaced in tests
		private Func<string, bool> _askLocateSidecar = DefaultAskLocateSidecar;
		private Func<string?> _browseForSidecar = DefaultBrowseForSidecar;
		private Func<string, bool> _confirmNoRoi = DefaultConfirmNoRoi;

		/// <summary>true when no crop is loaded (whole photos are used).</summary>
		private bool RoiMissing
		{
			get { return _roiRect.Width <= 0 || _roiRect.Height <= 0; }
		}

		/// <summary>
		/// Sets _roiRect and _trainingImageSize for the loaded model. If the .json is missing, offers to
		/// locate it and copies it next to the model so it is found from now on.
		/// </summary>
		private void LoadRoiForModel()
		{
			string sidecar = RoiSidecar.PathFor(_modelPath);
			RoiInfo info;
			bool found = RoiSidecar.TryRead(sidecar, out info);

			if (!found && _askLocateSidecar(sidecar))
			{
				string? chosen = _browseForSidecar();
				RoiInfo chosenInfo;
				if (chosen != null && RoiSidecar.TryRead(chosen, out chosenInfo) && chosenInfo.HasRoi)
				{
					try
					{
						File.Copy(chosen, sidecar, true);
						LogDiag("ROI settings copied next to the model: " + chosen + " -> " + sidecar);
					}
					catch (Exception ex)
					{
						LogDiag("Could not copy the ROI settings next to the model: " + ex.Message);
					}
					info = chosenInfo;
					found = true;
				}
				else if (chosen != null)
				{
					MessageBox.Show(
						"That file does not define an ROI (it has no RoiW / RoiH above zero).\n\n" +
						"Pick the .json that was saved with the model at training time.",
						"Not a model settings file", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				}
			}

			if (found && info.HasRoi)
			{
				_roiRect = info.ToRectangle();
				_trainingImageSize = new Size(info.ImageWidth > 0 ? info.ImageWidth : 224,
											  info.ImageHeight > 0 ? info.ImageHeight : 224);
			}
			else
			{
				_roiRect = Rectangle.Empty;
			}

			ShowRoiIndicator(found);
		}

		/// <summary>Always-visible ROI status next to the model name.</summary>
		private void ShowRoiIndicator(bool settingsFileFound)
		{
			if (RoiMissing)
			{
				lblModelInfo.Text = settingsFileFound
					? "⚠ ROI not set in the model's .json - photos are NOT cropped"
					: "⚠ Model .json not found - photos are NOT cropped";
				lblModelInfo.ForeColor = Color.Firebrick;
			}
			else
			{
				lblModelInfo.Text = $"✂ ROI {_roiRect.X},{_roiRect.Y}  {_roiRect.Width}x{_roiRect.Height}  |  model input {_trainingImageSize.Width}x{_trainingImageSize.Height}";
				lblModelInfo.ForeColor = Color.FromArgb(0, 120, 215);
			}
		}

		/// <summary>Asked before an action that would run with whole photos (monitoring, full retrain).</summary>
		private bool ConfirmRoiBeforeUse(string action)
		{
			return !RoiMissing || _confirmNoRoi(action);
		}

		private static bool DefaultAskLocateSidecar(string expectedPath)
		{
			return MessageBox.Show(
				"No settings file was found next to this model:\n\n" + expectedPath + "\n\n" +
				"That .json file stores the ROI - the cropped area the model was trained on. Without it the photos " +
				"are NOT cropped, and a model trained on a cropped dataset will wrongly flag good (OK) images as NG.\n\n" +
				"It is created at training time with the same name as the model, in the folder you trained in. " +
				"Locate it now?",
				"ROI settings not found", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
		}

		private static string? DefaultBrowseForSidecar()
		{
			using (var dialog = new OpenFileDialog
			{
				Filter = "Model settings (*.json)|*.json",
				Title = "Select the model's .json settings file"
			})
			{
				return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
			}
		}

		private static bool DefaultConfirmNoRoi(string action)
		{
			return MessageBox.Show(
				"No ROI is loaded for this model, so whole photos would be used to " + action + ".\n\n" +
				"If the model was trained on a cropped dataset (\"Apply ROI and Create Cropped Dataset\"), this will " +
				"make many OK images look like NG" +
				(action.StartsWith("retrain") ? ", and the retrained model would also be saved without an ROI" : "") + ".\n\n" +
				"Fix it first: put the model's .json next to the .zip and reload the model.\n\n" +
				"Continue anyway?",
				"No ROI loaded", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
				MessageBoxDefaultButton.Button2) == DialogResult.Yes;
		}
	}
}