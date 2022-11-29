using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;

namespace NoteBlitz
{
    public class LinkButton : Control
    {
        #region Dependency Properties

        public string Path
        {
            get { return (string)GetValue(PathProperty); }
            set { SetValue(PathProperty, value); }
        }
        public static readonly DependencyProperty PathProperty =
            DependencyProperty.Register("Path", typeof(string), typeof(LinkButton), new PropertyMetadata(""));

        #endregion

        public event EventHandler LinkOpened;

        static LinkButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(LinkButton), new FrameworkPropertyMetadata(typeof(LinkButton)));
        }

        public override void OnApplyTemplate()
        {
            Image imgIcon = GetTemplateChild("imgIcon") as Image;
            TextBlock tbTitle = GetTemplateChild("tbTitle") as TextBlock;
            TextBlock tbSubtitle = GetTemplateChild("tbSubtitle") as TextBlock;

            try
            {
                imgIcon.Source = GetIcon.GetIcon.FromPath(Path);
                tbTitle.Text = System.IO.Path.GetFileName(Path);
            }
            catch { }

            //setup template
            tbSubtitle.Text = Path;
            ToolTip = Path;

            base.OnApplyTemplate();
        }

        //shell execute whatever path was clicked (works for files, folders, websites, etc.)
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                e.Handled = true;

                if (Keyboard.IsKeyDown(Key.LeftCtrl))
                {
                    //open in explorer instead
                    System.Diagnostics.Process.Start("explorer.exe", "/select, \"" + Path + "\"");

                    LinkOpened(this, EventArgs.Empty);
                    return;
                }

                ProcessStartInfo si = new ProcessStartInfo
                {
                    FileName = Path,
                    UseShellExecute = true
                };

                Process.Start(si);

                LinkOpened(this, EventArgs.Empty);
            }
        }
    }
}
