using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NoteBlitz
{
    public static class Utils
    {
        public static childItem FindVisualChild<childItem>(DependencyObject obj)
    where childItem : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is childItem)
                {
                    return (childItem)child;
                }
                else
                {
                    childItem childOfChild = FindVisualChild<childItem>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }

        public static BitmapImage LoadImageFromByteArray(byte[] data, int decodeWidth = -1)
        {
            if (data == null || data.Length == 0)
            {
                return null;
            }

            MemoryStream ms = new MemoryStream(data);
            BitmapImage src = new BitmapImage();
            src.BeginInit();
            src.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            src.CacheOption = BitmapCacheOption.OnLoad;
            src.StreamSource = ms;

            if (decodeWidth != -1)
            {
                src.DecodePixelWidth = decodeWidth;
            }

            src.EndInit();

            if (src.CanFreeze)
                src.Freeze();

            return src;
        }

        public static byte[] SaveImageToByteArray(BitmapSource img)
        {
            if (img == null)
            {
                return new byte[0];
            }

            MemoryStream ms = new MemoryStream();
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Interlace = PngInterlaceOption.On;
            encoder.Frames.Add(BitmapFrame.Create(img));
            encoder.Save(ms);
            return ms.ToArray();
        }

        public static BitmapSource DecoupleBitmap(BitmapSource bmp)
        {
            int height = bmp.PixelHeight;
            int width = bmp.PixelWidth;
            int stride = width * ((bmp.Format.BitsPerPixel + 7) / 8);

            byte[] bits = new byte[height * stride];
            bmp.CopyPixels(bits, stride, 0);

            WriteableBitmap DecoupledBitmap = new WriteableBitmap(width, height, bmp.DpiX, bmp.DpiY, bmp.Format, bmp.Palette);
            DecoupledBitmap.WritePixels(new Int32Rect(0, 0, width, height), bits, stride, 0);

            if (DecoupledBitmap.CanFreeze)
                DecoupledBitmap.Freeze();

            return (BitmapSource)DecoupledBitmap;
        }

        public static void SaveBitmapImage(BitmapSource image, string path)
        {
            try
            {
                FileStream stream = new FileStream(path, FileMode.Create);
                //PngBitmapEncoder encoder = new PngBitmapEncoder();
                //encoder.Interlace = PngInterlaceOption.On;
                JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                encoder.Save(stream);
                stream.Close();
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public static BitmapSource LoadBitmapImage(string path, int DecodePixelWidth = -1)
        {
            if (!System.IO.File.Exists(path))
                return null;

            try
            {
                MemoryStream ms = new MemoryStream();
                FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
                ms.SetLength(stream.Length);
                stream.Read(ms.GetBuffer(), 0, (int)stream.Length);

                ms.Flush();
                stream.Close();

                BitmapImage src = new BitmapImage();
                src.BeginInit();
                src.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;

                if (DecodePixelWidth != -1)
                    src.DecodePixelWidth = DecodePixelWidth;

                src.CacheOption = BitmapCacheOption.OnLoad;
                src.StreamSource = ms;
                src.EndInit();

                if (src.CanFreeze)
                    src.Freeze();

                return DecoupleBitmap(src);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static void PasteImageFiles(this RichTextBox rtb, string file)
        {
            // Assuming you have one file that you care about, pass it off to whatever
            // handling code you have defined.
            try
            {
                BitmapImage bitmap = new BitmapImage(new Uri(file));
                Image image = new Image();
                image.Width = bitmap.PixelWidth;
                image.Height = bitmap.PixelHeight;
                image.Source = bitmap;
                image.Stretch = Stretch.Uniform;

                BlockUIContainer container = new BlockUIContainer(image);

                if (rtb.CaretPosition.Paragraph == null)
                {
                    rtb.Document.Blocks.Add(container);
                }
                else
                {
                    if (rtb.CaretPosition.IsAtLineStartPosition)
                    {
                        rtb.Document.Blocks.InsertBefore(rtb.CaretPosition.Paragraph, container);
                    }
                    else
                    {
                        rtb.Document.Blocks.InsertAfter(rtb.CaretPosition.Paragraph, container);
                    }

                }
            }
            catch (Exception)
            {
                Debug.WriteLine("\"file\" was not an image");
            }

        }
    }


}
