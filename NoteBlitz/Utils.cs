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
