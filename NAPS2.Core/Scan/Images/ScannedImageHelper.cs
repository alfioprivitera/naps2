﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using NAPS2.Scan.Images.Transforms;

namespace NAPS2.Scan.Images
{
    internal static class ScannedImageHelper
    {
        public static void GetSmallestBitmap(Bitmap sourceImage, ScanBitDepth bitDepth, bool highQuality, int quality, out Bitmap bitmap, out MemoryStream encodedBitmap, out ImageFormat imageFormat)
        {
            // Defaults for out arguments
            bitmap = null;
            encodedBitmap = null;
            imageFormat = ImageFormat.Png;

            // Store the image in as little space as possible
            if (bitDepth == ScanBitDepth.BlackWhite)
            {
                // Store as a 1-bit bitmap
                // This is lossless and takes up minimal storage (best of both worlds), so highQuality is irrelevant
                bitmap = (Bitmap)BitmapHelper.CopyToBpp(sourceImage, 1).Clone();
                // Note that if a black and white image comes from native WIA, bitDepth is unknown,
                // so the image will be png-encoded below instead of using a 1-bit bitmap
            }
            else if (highQuality)
            {
                // Store as PNG
                // Lossless, but some images (color/grayscale) take up lots of storage
                encodedBitmap = EncodePng(sourceImage);
            }
            else
            {
                // Store as PNG/JPEG depending on which is smaller
                var pngEncoded = EncodePng(sourceImage);
                var jpegEncoded = EncodeJpeg(sourceImage, quality);
                if (pngEncoded.Length <= jpegEncoded.Length)
                {
                    // Probably a black and white image (from native WIA, so bitDepth is unknown), which PNG compresses well vs. JPEG
                    encodedBitmap = pngEncoded;
                    jpegEncoded.Dispose();
                }
                else
                {
                    // Probably a color or grayscale image, which JPEG compresses well vs. PNG
                    encodedBitmap = jpegEncoded;
                    pngEncoded.Dispose();
                    imageFormat = ImageFormat.Jpeg;
                }
            }
        }

        private static MemoryStream EncodePng(Bitmap bitmap)
        {
            var encoded = new MemoryStream();
            bitmap.Save(encoded, ImageFormat.Png);
            return encoded;
        }

        private static MemoryStream EncodeJpeg(Bitmap bitmap, int quality)
        {
            var encoded = new MemoryStream();
            if (quality == -1)
            {
                bitmap.Save(encoded, ImageFormat.Jpeg);
            }
            else
            {
                quality = Math.Max(Math.Min(quality, 100), 0);
                var encoder = ImageCodecInfo.GetImageEncoders().First(x => x.FormatID == ImageFormat.Jpeg.Guid);
                var encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                bitmap.Save(encoded, encoder, encoderParams);
            }
            return encoded;
        }

        public static Bitmap PostProcessStep1(Image output, ScanProfile profile)
        {
            double scaleFactor = 1;
            if (!profile.UseNativeUI)
            {
                scaleFactor = profile.AfterScanScale.ToIntScaleFactor();
            }
            var result = ImageScaleHelper.ScaleImage(output, scaleFactor);

            if (!profile.UseNativeUI && profile.ForcePageSize)
            {
                float width = output.Width / output.HorizontalResolution;
                float height = output.Height / output.VerticalResolution;
                if (float.IsNaN(width) || float.IsNaN(height))
                {
                    width = output.Width;
                    height = output.Height;
                }
                PageDimensions pageDimensions = profile.PageSize.PageDimensions() ?? profile.CustomPageSize;
                if (pageDimensions.Width > pageDimensions.Height && width < height)
                {
                    // Flip dimensions
                    result.SetResolution((float)(output.Width / pageDimensions.HeightInInches()), (float)(output.Height / pageDimensions.WidthInInches()));
                }
                else
                {
                    result.SetResolution((float)(output.Width / pageDimensions.WidthInInches()), (float)(output.Height / pageDimensions.HeightInInches()));
                }
            }

            return result;
        }

        public static void PostProcessStep2(ScannedImage image, ScanProfile profile)
        {
            if (!profile.UseNativeUI && profile.BrightnessContrastAfterScan)
            {
                if (profile.Brightness != 0)
                {
                    image.AddTransform(new BrightnessTransform { Brightness = profile.Brightness });
                }
                if (profile.Contrast != 0)
                {
                    image.AddTransform(new TrueContrastTransform { Contrast = profile.Contrast });
                }
            }
        }
    }
}
