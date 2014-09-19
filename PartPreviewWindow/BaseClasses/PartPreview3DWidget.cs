﻿/*
Copyright (c) 2014, Lars Brubaker
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
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
    public class PartPreview3DWidget : PartPreviewWidget
    {
        protected bool autoRotateEnabled = false;
        public MeshViewerWidget meshViewerWidget;
        event EventHandler unregisterEvents;

        protected ViewControls3D viewControls3D;

        public PartPreview3DWidget()
        {
            SliceSettingsWidget.PartPreviewSettingsChanged.RegisterEvent(RecreateBedAndPartPosition, ref unregisterEvents);
            ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent(RecreateBedAndPartPosition, ref unregisterEvents);
        }

        void RecreateBedAndPartPosition(object sender, EventArgs e)
        {
            double buildHeight = ActiveSliceSettings.Instance.BuildHeight;

            UiThread.RunOnIdle((state) =>
            {
                meshViewerWidget.CreatePrintBed(
                    new Vector3(ActiveSliceSettings.Instance.BedSize, buildHeight),
                    ActiveSliceSettings.Instance.BedCenter,
                    ActiveSliceSettings.Instance.BedShape);
                PutOemImageOnBed();
            });
        }

        protected void PutOemImageOnBed()
        {
            // this is to add an image to the bed
            string imagePathAndFile = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "OEMSettings", "bedimage.png");
            if (autoRotateEnabled && File.Exists(imagePathAndFile))
            {
                ImageBuffer wattermarkImage = new ImageBuffer();
                ImageIO.LoadImageData(imagePathAndFile, wattermarkImage);

                ImageBuffer bedImage = meshViewerWidget.BedImage;
                Graphics2D bedGraphics = bedImage.NewGraphics2D();
                bedGraphics.Render(wattermarkImage,
                    new Vector2((bedImage.Width - wattermarkImage.Width) / 2, (bedImage.Height - wattermarkImage.Height) / 2));
            }
        }
        
        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        protected static SolidSlider InsertUiForSlider(FlowLayoutWidget wordOptionContainer, string header, double min = 0, double max = .5)
        {
            double scrollBarWidth = 100;
            TextWidget spacingText = new TextWidget(header, textColor: ActiveTheme.Instance.PrimaryTextColor);
            spacingText.Margin = new BorderDouble(10, 3, 3, 5);
            spacingText.HAnchor = HAnchor.ParentLeft;
            wordOptionContainer.AddChild(spacingText);
            SolidSlider namedSlider = new SolidSlider(new Vector2(), scrollBarWidth, 0, 1);
            namedSlider.Minimum = min;
            namedSlider.Maximum = max;
            namedSlider.Margin = new BorderDouble(3, 5, 3, 3);
            namedSlider.HAnchor = HAnchor.ParentCenter;
            namedSlider.View.BackgroundColor = new RGBA_Bytes();
            wordOptionContainer.AddChild(namedSlider);

            return namedSlider;
        }
    }
}
