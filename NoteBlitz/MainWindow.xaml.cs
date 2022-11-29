using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace NoteBlitz
{
    public class DataFile
    {
        public string Name { get; set; }
        public string Icon { get; set; }

        public string Document { get; set; }
        public double VerticalOffset { get; set; }
    }

    public class Data
    {
        public bool IsMaximized { get; set; }

        //position
        public double Top { get; set; }
        public double Left { get; set; }

        //size 
        public double Width { get; set; }
        public double Height { get; set; }

        public List<DataFile> files { get; set; }
        public int SelectedFile { get; set; }

        //public string Document { get; set; }

        public bool HotAlt { get; set; }
        public bool HotControl { get; set; }
        public bool HotShift { get; set; }

        public Key HotKey { get; set; }

        public bool HideWindowOnShortcutOpen { get; set; }
        public bool CheckForUpdates { get; set; }

        public Data()
        {
            HotControl = true;
            HotKey = Key.Space;

            HideWindowOnShortcutOpen = true;
            CheckForUpdates = true;
        }
    }

    public class UIFile : INotifyPropertyChanged
    {
        public DataFile File { get; set; }

        private string name;
        public string Name
        {
            get
            {
                return name;
            }

            set
            {
                name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Name"));
            }
        }

        public string Icon { get; set; }

        private bool editingName = false;
        public bool EditingName
        {
            get
            {
                return editingName;
            }

            set
            {
                editingName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("EditingName"));
            }
        }

        public UIFile(DataFile file)
        {
            File = file;
            Name = file.Name;
            Icon = file.Icon;
            EditingName = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public partial class MainWindow : Window
    {
        public string jsonFile = "data.json";
        bool Shown = false;

        public Data data { get; set; }
        public HotKey hotkey { get; set; }

        public ObservableCollection<UIFile> files;

        private Point _dragStartPoint;
        bool Initializing = false;
        bool Moving = false;

        #region SingleInstance
        static Mutex SingleAppMutex;

        private static bool CheckSingleInstance(string name)
        {
            try
            {
                //check if mutex already exists
                SingleAppMutex = Mutex.OpenExisting(name);

                //already opened
                return false;
            }
            catch
            {
                //mutex cannot be opened 
                //file is not opened
                try
                {
                    SingleAppMutex = new Mutex(true, name);

                    return true;
                }
                catch
                {
                    //cannot create mutex
                    //but mutex is not opened? 
                    return false;
                }
            }
        }
        #endregion

        public MainWindow()
        {
            Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;

            if (!CheckSingleInstance("NoteBlitz"))
            {
                Environment.Exit(0);
            }

            //setup application autostart 
            if (!Autostart.IsAutoStartEnabled("NoteBlitz", Environment.ProcessPath, "-hide"))
            {
                Autostart.SetAutoStart("NoteBlitz", Environment.ProcessPath, "-hide");
            }

            //when autostarted the path is screwed up
            System.IO.Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            this.ShowInTaskbar = false;

            //initialize window components
            InitializeComponent();

            //events
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            rtbMain.Loaded += RtbMain_Loaded;
            rtbMain.TextChanged += RtbMain_TextChanged;
            rtbMain.PreviewKeyDown += RtbMain_KeyDown;
            rtbMain.IsEnabled = false;
            RegisterPastingExtension();
        }

        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.Exception.Message + " " + e.Exception.StackTrace);
        }

        private void RtbMain_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space || e.Key == Key.Return || e.Key == Key.Tab)
            {
                ReplaceURLs();
            }
        }

        void RegisterPastingExtension()
        {
            System.Windows.DataObject.AddPastingHandler(rtbMain, rtbMain_Pasting);
        }

        private async void rtbMain_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            await Task.Delay(1000);
            AdornImages();
        }

        private async void RtbMain_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(1000);
            AdornImages();
        }

        private void RtbMain_TextChanged(object sender, TextChangedEventArgs e)
        {
            AdornImages();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //GlassUtils.EnableBlurBehind(this, false);

            //load data
            Load();

            //initialize files sidebar
            InitializeSidebar();

            //register hotkey
            InitHotkey();

            InitDragAndDrop();

            if (App.Hidden)
            {
                this.Visibility = Visibility.Hidden;
                this.WindowStyle = WindowStyle.SingleBorderWindow;
            }
            else
            {
                ShowWindow();
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            Save();
            e.Cancel = true;

            this.Visibility = Visibility.Hidden;
        }

        #region Update
        public static bool GetUpdateInfo(out string updateURL, out string updateVersion)
        {
            updateVersion = null;
            updateURL = null;

            try
            {
                string UpdateInfoURL = "https://api.github.com/repos/MrC0rrupted/NoteBlitz/releases/latest";

                using (var wc = new WebClient())
                {
                    wc.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/97.0.4692.99 Safari/537.36");
                    string json = wc.DownloadString(UpdateInfoURL);

                    Regex versionRegex = new Regex(@"\""tag_name\"":\s\""v\.(.+?)\""");
                    Regex downloadRegex = new Regex(@"\""browser_download_url\"":\s\""(.+?)\""");

                    var versionMatch = versionRegex.Match(json);
                    var downloadMatch = downloadRegex.Match(json);

                    if (versionMatch.Success && downloadMatch.Success)
                    {
                        updateVersion = versionMatch.Groups[1].Value;
                        updateURL = downloadMatch.Groups[1].Value;

                        return true;
                    }

                    return false;
                }
            }
            catch (Exception e)
            {
                return false;
            }
        }

        void CheckForUpdate()
        {
            Task.Factory.StartNew(() =>
            {
                string version, url;

                if (GetUpdateInfo(out url, out version))
                {
                    Version updateVersion = new Version(version);

                    Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version;

                    if (currentVersion.CompareTo(updateVersion) < 0)
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            //new version available
                            if (MessageBox.Show(this, "New version of NoteBlitz available: " + updateVersion.ToString() + "\nDo you want to update?", "Update available", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                            {
                                //Run the updater
                                ProcessStartInfo startInfo = new ProcessStartInfo();
                                startInfo.UseShellExecute = true;
                                startInfo.FileName = "Update.exe";

                                Process.Start(startInfo);

                                this.Close();
                                Environment.Exit(0);
                            }
                        }));
                    }
                }
            });
        }
        #endregion

        #region Window Utils
        private void ShowWindow()
        {
            Shown = true;
            this.ShowInTaskbar = true;

            if (WindowState == WindowState.Minimized)
            {
                SystemCommands.RestoreWindow(this);
            }

            //MessageBox.Show("top: " + data.Top + " ,left: " + data.Left + " ,width: " + data.Width + " ,height: " + data.Height);

            Top = data.Top;
            Left = data.Left;
            Width = data.Width;
            Height = data.Height;

            if (data.IsMaximized)
            {
                WindowState = WindowState.Maximized;
            }
            else
            {
                WindowState = WindowState.Normal;
            }

            this.Visibility = Visibility.Visible;

            if (data.CheckForUpdates)
            {
                CheckForUpdate();
            }
        }

        private void HideWindow()
        {
            this.Visibility = Visibility.Hidden;
            Save();
        }

        public void CenterOnScreen()
        {
            //get active screen 
            Point p = Mouse.GetPosition(null);
            WpfScreen activeScreen = WpfScreen.GetScreenFrom(p);

            Left = activeScreen.WorkingArea.Left + (activeScreen.WorkingArea.Width / 2) - (Width / 2);
            Top = activeScreen.WorkingArea.Top + (activeScreen.WorkingArea.Height / 2) - (Height / 2);
        }
        #endregion

        private async Task PresentFile(DataFile selectedFile)
        {
            rtbMain.IsEnabled = true;
            SetDocument(selectedFile.Document);

            await Task.Delay(1);
            rtbMain.ScrollToVerticalOffset(selectedFile.VerticalOffset);

            await Task.Delay(1000);
            AdornImages();
        }

        #region Drag and drop files 
        private void InitDragAndDrop()
        {
            AllowDrop = true;
            rtbMain.AllowDrop = true;
            rtbMain.PreviewDragEnter += RtbMain_PreviewDragEnter; ;
            rtbMain.PreviewDragOver += RtbMain_PreviewDragOver; ;
            rtbMain.PreviewDrop += RtbMain_PreviewDrop; ;
        }

        private async void RtbMain_PreviewDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];

                foreach (string file in files)
                {
                    string extension = System.IO.Path.GetExtension(file);

                    if (extension == ".png" || extension == ".jpg" || extension == ".jpeg")
                    {
                        rtbMain.PasteImageFiles(file);

                        await Task.Delay(100);

                        AdornImages();
                    }
                    else
                    {
                        AddLink(file);
                    }
                }
            }

            e.Handled = true;
        }

        private void RtbMain_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.All;
                e.Handled = true;
            }
        }

        private void RtbMain_PreviewDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.All;
                e.Handled = true;
            }
        }
        #endregion

        #region Link Buttons
        BlockUIContainer GetLinkContainer(string path)
        {
            LinkButton lb = new LinkButton
            {
                Path = path
            };

            lb.LinkOpened += Lb_LinkOpened;

            BlockUIContainer uiBlock = new BlockUIContainer(lb);
            uiBlock.LineStackingStrategy = LineStackingStrategy.MaxHeight;

            return uiBlock;
        }

        private void Lb_LinkOpened(object? sender, EventArgs e)
        {
            if (data.HideWindowOnShortcutOpen)
            {
                HideWindow();
            }
        }

        void AddLink(string path)
        {
            var block = GetLinkContainer(path);

            //insert into document
            if (rtbMain.CaretPosition.Paragraph == null)
            {
                rtbMain.Document.Blocks.Add(block);
            }
            else
            {
                if (rtbMain.CaretPosition.IsAtLineStartPosition)
                {
                    rtbMain.Document.Blocks.InsertBefore(rtbMain.CaretPosition.Paragraph, block);
                }
                else
                {
                    rtbMain.Document.Blocks.InsertAfter(rtbMain.CaretPosition.Paragraph, block);
                }

            }
        }
        #endregion

        #region Sidebar
        private T FindVisualParent<T>(DependencyObject child)
           where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null)
                return null;
            T parent = parentObject as T;
            if (parent != null)
                return parent;
            return FindVisualParent<T>(parentObject);
        }

        private void InitializeSidebar()
        {
            files = new ObservableCollection<UIFile>();

            foreach (var file in data.files)
            {
                files.Add(new UIFile(file));
            }

            lbFiles.SelectionChanged += LvFiles_SelectionChanged;

            lbFiles.Items.Clear();
            lbFiles.ItemsSource = files;
            lbFiles.SelectedIndex = data.SelectedFile;

            //drag and drop 
            var myResourceDictionary = new ResourceDictionary
            {
                Source = new Uri("/Themes/ColourfulDarkTheme.xaml", UriKind.RelativeOrAbsolute)
            };

            lbFiles.PreviewMouseMove += ListBox_PreviewMouseMove;
            var style = myResourceDictionary["lbItem"] as Style;
            style.Setters.Add(new Setter(HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            style.Setters.Add(new Setter(ListBoxItem.AllowDropProperty, true));
            style.Setters.Add(
                new EventSetter(
                    ListBoxItem.PreviewMouseLeftButtonDownEvent,
                    new MouseButtonEventHandler(ListBoxItem_PreviewMouseLeftButtonDown)));
            style.Setters.Add(
                    new EventSetter(
                        ListBoxItem.DropEvent,
                        new DragEventHandler(ListBoxItem_Drop)));
            lbFiles.ItemContainerStyle = style;
        }


        private void ListBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            Point point = e.GetPosition(null);
            Vector diff = _dragStartPoint - point;
            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                var lb = sender as ListBox;
                var lbi = FindVisualParent<ListBoxItem>(((DependencyObject)e.OriginalSource));
                if (lbi != null)
                {
                    DragDrop.DoDragDrop(lbi, lbi.DataContext, DragDropEffects.Move);
                }
            }
        }
        private void ListBoxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void ListBoxItem_Drop(object sender, DragEventArgs e)
        {
            if (sender is ListBoxItem)
            {
                var source = e.Data.GetData(typeof(UIFile)) as UIFile;
                var target = ((ListBoxItem)(sender)).DataContext as UIFile;

                int sourceIndex = lbFiles.Items.IndexOf(source);
                int targetIndex = lbFiles.Items.IndexOf(target);

                Move(source, sourceIndex, targetIndex);
            }
        }

        private void Move(UIFile source, int sourceIndex, int targetIndex)
        {
            if (sourceIndex < targetIndex)
            {
                files.Insert(targetIndex + 1, source);
                files.RemoveAt(sourceIndex);

                data.files.Insert(targetIndex + 1, source.File);
                data.files.RemoveAt(sourceIndex);
            }
            else
            {
                int removeIndex = sourceIndex + 1;
                if (files.Count + 1 > removeIndex)
                {
                    files.Insert(targetIndex, source);
                    files.RemoveAt(removeIndex);

                    data.files.Insert(targetIndex, source.File);
                    data.files.RemoveAt(removeIndex);
                }
            }

            lbFiles.SelectedIndex = targetIndex;

            Save();
        }

        private void LvFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.RemovedItems.Count != 0)
            {
                var previousSelectedFile = e.RemovedItems[0] as UIFile;

                //save document
                previousSelectedFile.File.Document = GetDocument();

                //save offset
                previousSelectedFile.File.VerticalOffset = rtbMain.VerticalOffset;
            }

            if (e.AddedItems.Count != 0)
            {
                var selectedFile = e.AddedItems[0] as UIFile;

                data.SelectedFile = files.IndexOf(selectedFile);

                PresentFile(selectedFile.File);
            }
        }
        #endregion

        #region Hotkey
        private bool InitHotkey()
        {
            if (hotkey != null)
            {
                hotkey.Enabled = false;
                hotkey.Dispose();
            }

            try
            {
                hotkey = new HotKey(System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle));
                hotkey.Modifiers |= (data.HotAlt ? HotKey.ModifierKeys.Alt : 0);
                hotkey.Modifiers |= (data.HotControl ? HotKey.ModifierKeys.Control : 0);
                hotkey.Modifiers |= (data.HotShift ? HotKey.ModifierKeys.Shift : 0);


                hotkey.Key = data.HotKey;
                hotkey.HotKeyPressed += Hotkey_HotKeyPressed;
                hotkey.Enabled = true;

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }

        private void Hotkey_HotKeyPressed(object? sender, HotKeyEventArgs e)
        {
            if (this.Visibility == Visibility.Visible && (WindowState == WindowState.Normal || WindowState == WindowState.Maximized))
            {
                HideWindow();
            }
            else
            {
                ShowWindow();
            }
        }
        #endregion

        #region MenuBar
        private void miSettings_Click(object sender, RoutedEventArgs e)
        {
            Settings settings = new Settings(data);

            settings.Owner = this;
            settings.ShowDialog();

            if (InitHotkey())
            {
                Save();
            }
        }

        private void miQuit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            Environment.Exit(0);
        }
        #endregion

        #region Adorn Images
        void AdornImage(Image image)
        {
            try
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(image);
                if (adornerLayer != null)
                {
                    bool found = false;
                    var adorners = adornerLayer.GetAdorners(image);

                    if (adorners != null)
                    {
                        foreach (var adorner in adorners)
                        {
                            if (adorner is ResizingAdorner)
                            {
                                found = true;
                                break;
                            }
                        }

                        if (found)
                        {
                            return;
                        }
                    }

                    adornerLayer.Add(new ResizingAdorner(image));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        void AdornImages()
        {
            foreach (var block in rtbMain.Document.Blocks)
            {
                if (block is Paragraph)
                {
                    Paragraph paragraph = (Paragraph)block;

                    if (paragraph.Inlines.Count > 0)
                    {
                        foreach (var inline in paragraph.Inlines)
                        {
                            if (inline is InlineUIContainer)
                            {
                                var uiblock = inline as InlineUIContainer;
                                if (uiblock.Child is Image)
                                {
                                    var image = uiblock.Child as Image;
                                    AdornImage(image);
                                }
                            }
                        }
                    }
                }
                else if (block is BlockUIContainer)
                {
                    var uiblock = block as BlockUIContainer;

                    if (uiblock.Child is Image)
                    {
                        var image = uiblock.Child as Image;
                        AdornImage(image);
                    }
                }
            }
        }
        #endregion

        #region Saving / Loading
        async void Load()
        {
            try
            {
                if (System.IO.File.Exists(jsonFile))
                {
                    string json = File.ReadAllText(jsonFile);
                    data = JsonConvert.DeserializeObject<Data>(json);

                    if (data == null)
                    {
                        throw new Exception("invalid format");
                    }

                    //if (!string.IsNullOrEmpty(data.Document))
                    //{
                    //    data.files = new List<DataFile>();
                    //    data.files.Add(new DataFile() { Document = data.Document, Icon = "res/edit.png", Name = "Legacy File", VerticalOffset = 0 });

                    //    data.Document = null;
                    //}

                    if (data.files.Count > 0)
                    {
                        await PresentFile(data.files[data.SelectedFile]);
                    }

                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("could not load data " + ex.Message, "NoteBlitz Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            //first ever launch
            Width = 1000;
            Height = 700;

            CenterOnScreen();

            ShowInTaskbar = true;
            Visibility = Visibility.Visible;

            data = new Data();

            data.Left = Left;
            data.Top = Top;
            data.Width = Width;
            data.Height = Height;

            data.files = new List<DataFile>() { new DataFile() { Name = "Notes", Icon = "res/edit.png" } };
            data.SelectedFile = 0;
        }

        void Save()
        {
            if (!Shown)
                return;

            if (WindowState == WindowState.Minimized)
            {
                data.Left = RestoreBounds.Left;
                data.Top = RestoreBounds.Top;
                data.Width = RestoreBounds.Width;
                data.Height = RestoreBounds.Height;
            }
            else
            {
                data.Left = Left;
                data.Top = Top;
                data.Width = Width;
                data.Height = Height;
            }

            data.IsMaximized = (WindowState == WindowState.Maximized);

            if (lbFiles.SelectedItem != null)
            {
                var selectedItem = lbFiles.SelectedItem as UIFile;

                selectedItem.File.Document = GetDocument();
                selectedItem.File.VerticalOffset = rtbMain.VerticalOffset;

                data.SelectedFile = lbFiles.SelectedIndex;
            }

            string json = JsonConvert.SerializeObject(data, Formatting.Indented);

            File.WriteAllText(jsonFile, json);
        }
        #endregion

        #region Document Management
        public string GetDocument()
        {
            string str = "";

            try
            {
                //replace ui elements with tokens
                var blocks = rtbMain.Document.Blocks.ToList();
                foreach (var block in blocks)
                {
                    if (block is BlockUIContainer)
                    {
                        if ((block as BlockUIContainer).Child is LinkButton)
                        {
                            var linkButton = (block as BlockUIContainer).Child as LinkButton;

                            Paragraph p = new Paragraph();
                            p.Inlines.Add(new Run("[LinkButton \"" + linkButton.Path + "\"]"));

                            rtbMain.Document.Blocks.InsertAfter(block, p);
                            rtbMain.Document.Blocks.Remove(block);
                        }
                    }
                }

                using (MemoryStream ms = new MemoryStream())
                {
                    var range = new TextRange(rtbMain.Document.ContentStart, rtbMain.Document.ContentEnd);
                    range.Save(ms, System.Windows.DataFormats.XamlPackage);
                    str = Convert.ToBase64String(ms.ToArray());
                }

                //restore document to remove tokens again
                rtbMain.Document.Blocks.Clear();

                foreach (var block in blocks)
                {
                    rtbMain.Document.Blocks.Add(block);
                }

            }
            catch { }

            return str;
        }

        public void SetDocument(string document)
        {
            if (string.IsNullOrEmpty(document))
            {
                rtbMain.Document.Blocks.Clear();
                return;
            }

            try
            {
                using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(document)))
                {
                    var range = new TextRange(rtbMain.Document.ContentStart, rtbMain.Document.ContentEnd);
                    range.Load(ms, System.Windows.DataFormats.XamlPackage);
                }

                //replace tokens with ui elements 
                var blocks = rtbMain.Document.Blocks.ToList();
                foreach (var block in blocks)
                {
                    if (block is Paragraph)
                    {
                        foreach (var run in (block as Paragraph).Inlines.ToList())
                        {
                            if (run is Run)
                            {
                                Regex linkButtonRegex = new Regex(@"\[LinkButton \""(.+)\""\]");
                                var linkButtonMatch = linkButtonRegex.Match((run as Run).Text);

                                if (linkButtonMatch.Success)
                                {
                                    var newBlock = GetLinkContainer(linkButtonMatch.Groups[1].Value);

                                    rtbMain.Document.Blocks.InsertAfter(block, newBlock);
                                    rtbMain.Document.Blocks.Remove(block);
                                    continue;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }
        #endregion

        private void miNewFile_Click(object sender, RoutedEventArgs e)
        {
            var newFile = new DataFile() { Name = "new file", Icon = "res/edit.png" };
            var newUIFile = new UIFile(newFile);

            data.files.Add(newFile);
            files.Add(newUIFile);

            lbFiles.SelectedItem = newUIFile;
        }

        private void miDelete_Click(object sender, RoutedEventArgs e)
        {
            var uiFile = (sender as FrameworkElement).DataContext as UIFile;

            files.Remove(uiFile);
            data.files.Remove(uiFile.File);

            if (data.files.Count == 0)
            {
                rtbMain.IsEnabled = false;
                rtbMain.Document.Blocks.Clear();
            }
        }

        private async void UIFileName_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var uiFile = (sender as FrameworkElement).DataContext as UIFile;
            if (data.SelectedFile == files.IndexOf(uiFile))
            {
                uiFile.EditingName = true;

                ListBoxItem myListViewItem = (ListBoxItem)(lbFiles.ItemContainerGenerator.ContainerFromItem(lbFiles.SelectedItem));

                // Getting the ContentPresenter of myListBoxItem
                ContentPresenter myContentPresenter = Utils.FindVisualChild<ContentPresenter>(myListViewItem);

                // Finding textBlock from the DataTemplate that is set on that ContentPresenter
                DataTemplate myDataTemplate = myContentPresenter.ContentTemplate;
                TextBox tbxName = (TextBox)myDataTemplate.FindName("tbxName", myContentPresenter);

                await Task.Delay(1);

                Keyboard.Focus(tbxName);
                tbxName.SelectAll();
            }
        }

        private void UIFileTextbox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            var uiFile = (sender as FrameworkElement).DataContext as UIFile;
            uiFile.Name = (sender as TextBox).Text;

            SetNewName(uiFile);
        }

        private void SetNewName(UIFile? uiFile)
        {
            uiFile.EditingName = false;
            uiFile.File.Name = uiFile.Name;

            Save();
        }

        private void tbxName_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var uiFile = (sender as FrameworkElement).DataContext as UIFile;

            if (e.Key == Key.Return || e.Key == Key.Escape)
            {
                Keyboard.ClearFocus();
            }
        }

        void ReplaceURLs()
        {
            while (TryReplaceURL()) ;
        }

        bool TryReplaceURL()
        {
            foreach (Block block in rtbMain.Document.Blocks)
            {
                if (block is Paragraph paragraph)
                {
                    foreach (Inline inline in paragraph.Inlines)
                    {
                        if (inline is Run run && run.Foreground != Brushes.Red)
                        {
                            Regex urlRegex = new Regex(@"(?i)\b((?:[a-z][\w-]+:(?:/{1,3}|[a-z0-9%])|www\d{0,3}[.]|[a-z0-9.\-]+[.][a-z]{2,4}/)(?:[^\s()<>]+|\(([^\s()<>]+|(\([^\s()<>]+\)))*\))+(?:\(([^\s()<>]+|(\([^\s()<>]+\)))*\)|[^\s`!()\[\]{};:'"".,<>?«»“”‘’]))");
                            Match m = urlRegex.Match(run.Text);

                            if (m.Success)
                            {
                                //cut out the matching text and replace it with an inline hyperlink 
                                string left = run.Text.Substring(0, m.Index);
                                string right = run.Text.Substring(m.Index + m.Length);

                                Run runLeft = new Run(left);
                                Hyperlink link = new Hyperlink() { NavigateUri = new Uri(m.Groups[0].Value) };
                                link.Inlines.Add(new Run(m.Groups[0].Value) { Cursor = Cursors.Hand });
                                Run runRight = new Run(right);

                                //insert new runs & link
                                paragraph.Inlines.InsertBefore(run, runLeft);
                                paragraph.Inlines.InsertAfter(runLeft, link);
                                paragraph.Inlines.InsertAfter(link, runRight);

                                //remove original run
                                paragraph.Inlines.Remove(run);

                                //url replaced start over
                                return true;
                            }
                        }
                    }
                }
            }

            //no urls found to be replaced
            return false;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            var hyperlink = (Hyperlink)sender;
            Process.Start(new ProcessStartInfo { FileName = hyperlink.NavigateUri.ToString(), UseShellExecute = true });

            if (data.HideWindowOnShortcutOpen)
            {
                HideWindow();
            }
        }
    }
}
