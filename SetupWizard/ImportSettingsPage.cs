﻿/*
Copyright (c) 2016, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;

using MatterHackers.Agg.UI;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.Localizations;
using System.IO;
using MatterHackers.Agg;
using System.Collections.Generic;

namespace MatterHackers.MatterControl
{
	public class SelectPartsOfPrinterToImport : WizardPage
	{
		static string importMessage = "Select what you would like to merge into your current profile.".Localize();

		string settingsFilePath;
		PrinterSettings settingsToImport;
		int selectedMaterial = -1;
		int selectedQuality = -1;

		PrinterSettingsLayer destinationLayer;
		string sectionName;

		private bool isMergeIntoUserLayer = false;


		public SelectPartsOfPrinterToImport(string settingsFilePath, PrinterSettingsLayer destinationLayer, string sectionName = null) :
			base(unlocalizedTextForTitle: "Import Wizard")
		{
			this.isMergeIntoUserLayer = destinationLayer == ActiveSliceSettings.Instance.UserLayer;
			this.destinationLayer = destinationLayer;
			this.sectionName = sectionName;

			settingsToImport = PrinterSettings.LoadFile(settingsFilePath);

			this.headerLabel.Text = "Select What to Import".Localize();

			this.settingsFilePath = settingsFilePath;

			var scrollWindow = new ScrollableWidget()
			{
				AutoScroll = true,
				HAnchor = HAnchor.ParentLeftRight,
				VAnchor = VAnchor.ParentBottomTop,
			};
			scrollWindow.ScrollArea.HAnchor = HAnchor.ParentLeftRight;
			contentRow.AddChild(scrollWindow);

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.ParentLeftRight,
			};
			scrollWindow.AddChild(container);

			if (isMergeIntoUserLayer)
			{
				container.AddChild(new WrappedTextWidget(importMessage, 10, textColor: ActiveTheme.Instance.PrimaryTextColor));
			}

			// add in the check boxes to select what to import
			container.AddChild(new TextWidget("Main Settings:")
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				Margin = new BorderDouble(0, 3, 0, isMergeIntoUserLayer ? 10 : 0),
			});

			var mainProfileRadioButton = new RadioButton("Printer Profile")
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				Margin = new BorderDouble(5, 0),
				HAnchor = HAnchor.ParentLeft,
				Checked = true,
			};
			container.AddChild(mainProfileRadioButton);

			if (settingsToImport.QualityLayers.Count > 0)
			{
				container.AddChild(new TextWidget("Quality Presets:")
				{
					TextColor = ActiveTheme.Instance.PrimaryTextColor,
					Margin = new BorderDouble(0, 3, 0, 15),
				});

				int buttonIndex = 0;
				foreach (var qualitySetting in settingsToImport.QualityLayers)
				{
					RadioButton qualityButton = new RadioButton(qualitySetting.Name)
					{
						TextColor = ActiveTheme.Instance.PrimaryTextColor,
						Margin = new BorderDouble(5, 0, 0, 0),
						HAnchor = HAnchor.ParentLeft,
					};
					container.AddChild(qualityButton);

					int localButtonIndex = buttonIndex;
					qualityButton.CheckedStateChanged += (s, e) =>
					{
						if (qualityButton.Checked)
						{
							selectedQuality = localButtonIndex;
						}
						else
						{
							selectedQuality = -1;
						}
					};

					buttonIndex++;
				}
			}

			if (settingsToImport.MaterialLayers.Count > 0)
			{
				container.AddChild(new TextWidget("Material Presets:")
				{
					TextColor = ActiveTheme.Instance.PrimaryTextColor,
					Margin = new BorderDouble(0, 3, 0, 15),
				});

				int buttonIndex = 0;
				foreach (var materialSetting in settingsToImport.MaterialLayers)
				{
					RadioButton materialButton = new RadioButton(materialSetting.Name)
					{
						TextColor = ActiveTheme.Instance.PrimaryTextColor,
						Margin = new BorderDouble(5, 0),
						HAnchor = HAnchor.ParentLeft,
					};

					container.AddChild(materialButton);

					int localButtonIndex = buttonIndex;
					materialButton.CheckedStateChanged += (s, e) =>
					{
						if (materialButton.Checked)
						{
							selectedMaterial = localButtonIndex;
						}
						else
						{
							selectedMaterial = -1;
						}
					};

					buttonIndex++;
				}
			}

			var mergeButtonTitle = this.isMergeIntoUserLayer ? "Merge".Localize() : "Import".Localize();
			var mergeButton = textImageButtonFactory.Generate( mergeButtonTitle);
			mergeButton.Name = "Merge Profile";
			mergeButton.Click += (s,e) => UiThread.RunOnIdle(Merge);
			footerRow.AddChild(mergeButton);

			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);

			if(settingsToImport.QualityLayers.Count == 0 && settingsToImport.MaterialLayers.Count == 0)
			{
				// Only main setting so don't ask what to merge just do it.
				UiThread.RunOnIdle(Merge);
			}
		}

		static string importPrinterSuccessMessage = "Settings have been merged into your current printer.".Localize();

		HashSet<string> skipKeys = new HashSet<string>
		{
			"layer_name",
			"layer_id",
		};

		void Merge()
		{
			var activeSettings = ActiveSliceSettings.Instance;

			var layerCascade = new List<PrinterSettingsLayer>
			{
				ActiveSliceSettings.Instance.OemLayer,
				ActiveSliceSettings.Instance.BaseLayer,
				destinationLayer,
			};

			PrinterSettingsLayer layerToImport = settingsToImport.BaseLayer;
			if (selectedMaterial > -1)
			{
				var material = settingsToImport.MaterialLayers[selectedMaterial];

				foreach(var item in material)
				{
					if (!skipKeys.Contains(item.Key))
					{
						destinationLayer[item.Key] = item.Value;
					}
				}

				if (!isMergeIntoUserLayer && material.ContainsKey("layer_name"))
				{
					destinationLayer["layer_name"] = material["layer_name"];
				}
			}
			else if (selectedQuality > -1)
			{
				var quality = settingsToImport.QualityLayers[selectedQuality];

				foreach (var item in quality)
				{
					if (!skipKeys.Contains(item.Key))
					{
						destinationLayer[item.Key] = item.Value;
					}
				}

				if (!isMergeIntoUserLayer && quality.ContainsKey("layer_name"))
				{
					destinationLayer["layer_name"] = quality["layer_name"];
				}
			}
			else
			{
				foreach (var item in layerToImport)
				{
					// Compare the value to import to the layer cascade value and only set if different
					string currentValue = activeSettings.GetValue(item.Key, layerCascade).Trim();
					string importValue = settingsToImport.GetValue(item.Key, layerCascade).Trim();
					if (currentValue != item.Value)
					{
						destinationLayer[item.Key] = item.Value;
					}
				}
			}

			activeSettings.SaveChanges();

			UiThread.RunOnIdle(ApplicationController.Instance.ReloadAdvancedControlsPanel);

			string successMessage = importPrinterSuccessMessage.FormatWith(Path.GetFileNameWithoutExtension(settingsFilePath));
			if (!isMergeIntoUserLayer)
			{
				string sourceName = isMergeIntoUserLayer ? Path.GetFileNameWithoutExtension(settingsFilePath) : destinationLayer["layer_name"];
				successMessage = ImportSettingsPage.importSettingSuccessMessage.FormatWith(sourceName, sectionName);
			}

			WizardWindow.ChangeToPage(new ImportSucceeded(successMessage)
			{
				WizardWindow = this.WizardWindow,
			});
		}
	}

	public class ImportSucceeded : WizardPage
	{
		public ImportSucceeded(string successMessage) :
			base("Done", "Import Wizard")
		{
			this.headerLabel.Text = "Import Successful".Localize();

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.ParentLeftRight,
			};
			contentRow.AddChild(container);

			var successMessageWidget = new WrappedTextWidget(successMessage, 10, textColor: ActiveTheme.Instance.PrimaryTextColor);
			container.AddChild(successMessageWidget);

			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);
		}
	}

	public class ImportSettingsPage : WizardPage
	{
		RadioButton newPrinterButton;
		RadioButton mergeButton;
		RadioButton newQualityPresetButton;
		RadioButton newMaterialPresetButton;

		public ImportSettingsPage() :
			base("Cancel", "Import Wizard")
		{
			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.ParentLeftRight,
			};
			contentRow.AddChild(container);

			if (true)
			{
				container.AddChild(new TextWidget("Merge Into:")
				{
					TextColor = ActiveTheme.Instance.PrimaryTextColor,
					Margin = new BorderDouble(0, 0, 0, 5),
				});

				// merge into current settings
				mergeButton = new RadioButton("Current".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				mergeButton.Checked = true;
				container.AddChild(mergeButton);

				container.AddChild(new TextWidget("Create New:")
				{
					TextColor = ActiveTheme.Instance.PrimaryTextColor,
					Margin = new BorderDouble(0, 0, 0, 15),
				});

				// add new profile
				newPrinterButton = new RadioButton("Printer".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				container.AddChild(newPrinterButton);

				// add as quality preset
				newQualityPresetButton = new RadioButton("Quality preset".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				container.AddChild(newQualityPresetButton);

				// add as material preset
				newMaterialPresetButton = new RadioButton("Material preset".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				container.AddChild(newMaterialPresetButton);
			}
			else
			{
				// add new profile
				newPrinterButton = new RadioButton("Import as new printer profile".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				newPrinterButton.Checked = true;
				container.AddChild(newPrinterButton);

				container.AddChild(
					CreateDetailInfo("Add a new printer profile to your list of available printers.\nThis will not change your current settings.")
					);

				// merge into current settings
				mergeButton = new RadioButton("Merge into current printer profile".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				container.AddChild(mergeButton);

				container.AddChild(
					CreateDetailInfo("Merge settings and presets (if any) into your current profile. \nYou will still be able to revert to the factory settings at any time.")
					);

				// add as quality preset
				newQualityPresetButton = new RadioButton("Import settings as new QUALITY preset".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				container.AddChild(newQualityPresetButton);

				container.AddChild(
					CreateDetailInfo("Add new quality preset with the settings from this import.")
					);

				// add as material preset
				newMaterialPresetButton = new RadioButton("Import settings as new MATERIAL preset".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
				container.AddChild(newMaterialPresetButton);

				container.AddChild(
					CreateDetailInfo("Add new material preset with the settings from this import.")
					);
			}


			var importButton = textImageButtonFactory.Generate("Choose File".Localize());
			importButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				FileDialog.OpenFileDialog(
						new OpenFileDialogParams("settings files|*.ini;*.printer;*.slice"),
						(dialogParams) =>
						{
							if (!string.IsNullOrEmpty(dialogParams.FileName))
							{
								ImportSettingsFile(dialogParams.FileName);
							}
						});
			});

			importButton.Visible = true;
			cancelButton.Visible = true;

			//Add buttons to buttonContainer
			footerRow.AddChild(importButton);
			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);
		}

		private GuiWidget CreateDetailInfo(string detailText)
		{
			var wrappedText = new WrappedTextWidget(detailText, 5)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
			};

			var container = new GuiWidget(HAnchor.ParentLeftRight, VAnchor.FitToChildren)
			{
				Margin = new BorderDouble(25, 15, 5, 5),
			};

			container.AddChild(wrappedText);

			return container;
		}

		static string importPrinterSuccessMessage = "You have successfully imported a new printer profile. You can find '{0}' in your list of available printers.".Localize();
		internal static string importSettingSuccessMessage = "You have successfully imported a new {1} setting. You can find '{0}' in your list of {1} settings.".Localize();

		private void ImportSettingsFile(string settingsFilePath)
		{
			if(newPrinterButton.Checked)
			{
				ProfileManager.ImportFromExisting(settingsFilePath);
				WizardWindow.ChangeToPage(new ImportSucceeded(importPrinterSuccessMessage.FormatWith(Path.GetFileNameWithoutExtension(settingsFilePath)))
				{
					WizardWindow = this.WizardWindow,
				});
			}
			else if(mergeButton.Checked)
			{
				MergeSettings(settingsFilePath);
			}
			else if(newQualityPresetButton.Checked)
			{
				ImportToPreset(settingsFilePath);
			}
			else if(newMaterialPresetButton.Checked)
			{
				ImportToPreset(settingsFilePath);
			}
		}

		private void ImportToPreset(string settingsFilePath)
		{
			if (!string.IsNullOrEmpty(settingsFilePath) && File.Exists(settingsFilePath))
			{
				PrinterSettingsLayer newLayer;

				string sectionName = (newMaterialPresetButton.Checked) ? "Material".Localize() : "Quality".Localize();

				string importType = Path.GetExtension(settingsFilePath).ToLower();
				switch (importType)
				{
					case ".printer":
						newLayer = new PrinterSettingsLayer();
						newLayer["layer_name"] = Path.GetFileNameWithoutExtension(settingsFilePath);

						if (newQualityPresetButton.Checked)
						{
							ActiveSliceSettings.Instance.QualityLayers.Add(newLayer);

						}
						else
						{
							// newMaterialPresetButton.Checked
							ActiveSliceSettings.Instance.MaterialLayers.Add(newLayer);
						}

						// open a wizard to ask what to import to the preset
						WizardWindow.ChangeToPage(new SelectPartsOfPrinterToImport(settingsFilePath, newLayer, sectionName));

						break;

					case ".slice": // legacy presets file extension
					case ".ini":
						var settingsToImport = PrinterSettingsLayer.LoadFromIni(settingsFilePath);
						string layerHeight;

						bool isSlic3r = importType == ".slice" || settingsToImport.TryGetValue(SettingsKey.layer_height, out layerHeight);
						if (isSlic3r)
						{
							newLayer = new PrinterSettingsLayer();
							newLayer.Name = Path.GetFileNameWithoutExtension(settingsFilePath);

							// Only be the base and oem layers (not the user, quality or material layer)
							var baseAndOEMCascade = new List<PrinterSettingsLayer>
							{
								ActiveSliceSettings.Instance.OemLayer,
								ActiveSliceSettings.Instance.BaseLayer
							};

							foreach (var item in settingsToImport)
							{
								string currentValue = ActiveSliceSettings.Instance.GetValue(item.Key, baseAndOEMCascade).Trim();
								// Compare the value to import to the layer cascade value and only set if different
								if (currentValue != item.Value)
								{
									newLayer[item.Key] = item.Value;
								}
							}

							if (newMaterialPresetButton.Checked)
							{
								ActiveSliceSettings.Instance.MaterialLayers.Add(newLayer);
							}
							else
							{
								ActiveSliceSettings.Instance.QualityLayers.Add(newLayer);
							}

							ActiveSliceSettings.Instance.SaveChanges();

							WizardWindow.ChangeToPage(new ImportSucceeded(importSettingSuccessMessage.FormatWith(Path.GetFileNameWithoutExtension(settingsFilePath), sectionName))
							{
								WizardWindow = this.WizardWindow,
							});
						}
						else
						{
							// looks like a cura file
							throw new NotImplementedException("need to import from 'cure.ini' files");
						}
						break;

					default:
						// Did not figure out what this file is, let the user know we don't understand it
						StyledMessageBox.ShowMessageBox(null, "Oops! Unable to recognize settings file '{0}'.".Localize().FormatWith(Path.GetFileName(settingsFilePath)), "Unable to Import".Localize());
						break;
				}

			}
			Invalidate();
		}

		private void MergeSettings(string settingsFilePath)
		{
			if (!string.IsNullOrEmpty(settingsFilePath) && File.Exists(settingsFilePath))
			{
				string importType = Path.GetExtension(settingsFilePath).ToLower();
				switch (importType)
				{
					case ".printer":
						WizardWindow.ChangeToPage(new SelectPartsOfPrinterToImport(settingsFilePath, ActiveSliceSettings.Instance.UserLayer));
						break;

					case ".slice": // old presets format
					case ".ini":
						var settingsToImport = PrinterSettingsLayer.LoadFromIni(settingsFilePath);
						string layerHeight;

						bool isSlic3r = settingsToImport.TryGetValue(SettingsKey.layer_height, out layerHeight);
						if (isSlic3r)
						{
							var activeSettings = ActiveSliceSettings.Instance;

							foreach (var item in settingsToImport)
							{
								// Compare the value to import to the layer cascade value and only set if different
								string currentValue = activeSettings.GetValue(item.Key, null).Trim();
								if (currentValue != item.Value)
								{
									activeSettings.UserLayer[item.Key] = item.Value;
								}
							}

							activeSettings.SaveChanges();

							UiThread.RunOnIdle(ApplicationController.Instance.ReloadAdvancedControlsPanel);
						}
						else
						{
							// looks like a cura file
							throw new NotImplementedException("need to import from 'cure.ini' files");
						}
						WizardWindow.Close();
						break;

					default:
						WizardWindow.Close();
						// Did not figure out what this file is, let the user know we don't understand it
						StyledMessageBox.ShowMessageBox(null, "Oops! Unable to recognize settings file '{0}'.".Localize().FormatWith(Path.GetFileName(settingsFilePath)), "Unable to Import".Localize());
						break;
				}

			}
			Invalidate();
		}
	}
}
