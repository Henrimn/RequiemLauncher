﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using NLog;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Net.Http;
using System.Net;
using System.Threading;
using Requiem_Network_Launcher.Utils;

namespace Requiem_Network_Launcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        #region Global variables
        public string rootDirectory;
        public string dllPath;
        public string processPath;
        public string versionPath;
        public string updatePath;
        public string dropRateCalculatorPath;
        public string currentVersionLocal;
        public string launcherInfoPath;
        public string myDocumentsPath;
        private string _backgroundImagesPath;
        private string[] _backgroundImages;
        private ImageBrush _currentBackgroundImage;
        public bool waitingForRestart = false;
        public NotifyIcon _nIcon;
        //public DiscordRpcClient discordRpcClient = new DiscordRpcClient("576564180350533673");
        private static Logger log = NLog.LogManager.GetLogger("AppLog");
        private int _numberOfImages;
        private int _currentImageIndex;
        #endregion

        #region Constructor
        public MainWindow()
        {
            InitializeComponent();

            this.SourceInitialized += Window_SourceInitialized;
            NotifyIconSetup();

            // have to use new thread since download might block UI
            GetBackGroundImages();

            // timer for background changing
            System.Timers.Timer backgroundChangeTimer = new System.Timers.Timer(900000); // (ms) 900000 = 15 mins. 1800000 = 30 minutes
            backgroundChangeTimer.Elapsed += BackgroundChangeTimer_Elapsed;
            backgroundChangeTimer.Enabled = true;

            //SetupDiscordRpcClient();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // check for user info
            CheckUserInfo();

            launcherInfoPath = rootDirectory + "\\info.txt";
            try
            {
                File.WriteAllText(launcherInfoPath, "LauncherVersion=" + Assembly.GetExecutingAssembly().GetName().Version.ToString());
                MainFrame.Content = new LoginPage();
            }
            catch (ArgumentException e1)
            {
                System.Windows.MessageBox.Show(e1.Message);
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            log.Info("Logout button clicked.");
            Logout();
        }
        #endregion

        #region Check user info: DotNet version, hwid, user login info, file path
        private void CheckUserInfo()
        {
            log.Info("Checking user info...");
            CheckDotNetVersion.Get45PlusFromRegistry();

            if (CheckDotNetVersion.DotnetKey != "Henri")
            {
                System.Windows.MessageBox.Show("You are using .NET Framework version: " + CheckDotNetVersion.DotnetKey + ".\nPlease update to 4.6.1 or later to use the Launcher!",
                                                                                                ".NET Framework Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Process.Start("https://dotnet.microsoft.com/download/dotnet-framework-runtime");
                this.Close();
            }
            else if (CheckDotNetVersion.DotnetKey == "")
            {
                System.Windows.MessageBox.Show("You are using a very outdated .NET Framework version!\nPlease install version 4.6.1 or later to use the Launcher!",
                                                                                                ".NET Framework Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Process.Start("https://dotnet.microsoft.com/download/dotnet-framework-runtime");
                this.Close();
            }

            HardwareID.GetHardwareID();
            UserInfoRegistry.GetUserLoginInfo();

            // get current directory of the Launcher
            rootDirectory = System.AppDomain.CurrentDomain.BaseDirectory;

            // set path for winnsi.dll and Vindictus.exe, version.txt and LauncherUpdater.exe
            try
            {
                dllPath = System.IO.Path.Combine(rootDirectory, "winnsi.dll");
                processPath = System.IO.Path.Combine(rootDirectory, "Vindictus.exe");
                versionPath = System.IO.Path.Combine(rootDirectory, "Version.txt");
                dropRateCalculatorPath = System.IO.Path.Combine(rootDirectory, "DropRatesCalculator.exe");
            }
            catch (ArgumentException e)
            {
                System.Windows.MessageBox.Show(e.Message);
                log.Error(e.ToString());
            }
        }
        #endregion

        #region Setup system tray icon
        private void NotifyIconSetup()
        {
            _nIcon = new NotifyIcon();
            _nIcon.Icon = new System.Drawing.Icon(System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Resources/launcher_icon.ico")).Stream);
            _nIcon.Visible = true;
            _nIcon.DoubleClick += delegate (object sender, EventArgs e) { this.Show(); this.WindowState = WindowState.Normal; };
            _nIcon.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            _nIcon.ContextMenuStrip.Items.Add("Forum", null, this.Forum_Click);
            _nIcon.ContextMenuStrip.Items.Add("Drop Rates Calculator", null, this.DRC_Click);
            _nIcon.ContextMenuStrip.Items.Add("Dye System", null, this.DyeSite_Click);
            _nIcon.ContextMenuStrip.Items.Add("Logout", null, this.LogoutMenu_Click);
            _nIcon.ContextMenuStrip.Items.Add("Exit", null, this.MenuExit_Click);
            _nIcon.Text = "Requiem Network Launcher";
        }

        private void Forum_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("http://requiemnetwork.com");
        }

        private void DRC_Click(object sender, EventArgs e)
        {
            if (!File.Exists(dropRateCalculatorPath))
            {
                System.Windows.MessageBox.Show("Cannot find DropRateCalculator in the current folder. Make sure the launcher is in main game folder!",
                                                                                             "File Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                Process dropCalculator = new Process();
                try
                {
                    dropCalculator.EnableRaisingEvents = true;
                    dropCalculator.StartInfo.FileName = dropRateCalculatorPath;
                    dropCalculator.Start();
                }
                catch (Exception e1)
                {
                    System.Windows.MessageBox.Show(e1.Message, "Error");
                }
            }
        }

        private void DyeSite_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("http://requiemnetwork.com/dye");
        }

        private void MenuExit_Click(object sender, EventArgs e)
        {
            _nIcon.Visible = false;
            /*if (discordRpcClient.IsInitialized)
            {
                discordRpcClient.Dispose();
            }*/
            this.Close();
        }

        private void LogoutMenu_Click(object sender, EventArgs e)
        {
            Logout();
            this.Show();
            this.WindowState = WindowState.Normal;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                _nIcon.BalloonTipText = "Launcher has been minimized to system tray";
                _nIcon.BalloonTipTitle = "Requiem Network";
                _nIcon.ShowBalloonTip(1000);
                this.Hide();
            }
            base.OnStateChanged(e);
        }

        private void MetroWindow_Closed(object sender, EventArgs e)
        {
            log.Info("Closing launcher.\n");
            _nIcon.Visible = false;
            /*if(discordRpcClient.IsInitialized)
            {
                discordRpcClient.Dispose();
            }*/
            Environment.Exit(0);
        }

        #endregion

        #region Logout function
        private void Logout()
        {
            if (LogoutButton.Content.ToString().Contains("LOGOUT"))
            {
                UserInfoRegistry.ClearUserLoginInfo();

                MainFrame.Content = new LoginPage();

                Dispatcher.Invoke((Action)(() =>
                {
                    LogoutButton.Content = "LOGIN";
                }));
            }
        }
        #endregion

        #region Navigating Animation
        /// <summary>
        /// Custom navigation animation for navigation between pages
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainFrame_OnNavigating(object sender, NavigatingCancelEventArgs e)
        {

            var da = new DoubleAnimation();
            da.Duration = TimeSpan.FromSeconds(0.7);
            if (e.NavigationMode == NavigationMode.New)
            {
                da.From = 0;
                da.To = 1;
            }
            Dispatcher.Invoke((Action)(() =>
            {
                (e.Content as Page).BeginAnimation(OpacityProperty, da);
            }));
            if (e.NavigationMode == NavigationMode.Back)
            {
                e.Cancel = true;
            }
        }
        #endregion

        #region Background image handler
        private async void GetBackGroundImages()
        {
            // initialize path
            myDocumentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            _backgroundImagesPath = Path.Combine(Path.Combine(myDocumentsPath, "Requiem Network"), "Background Images");

            // create directory. If directory already exists, do noting.
            Directory.CreateDirectory(_backgroundImagesPath);

            log.Info("Get background images.");

            // <--------- get total number of images for background from server --------->
            HttpClient _client = new HttpClient();
            try
            {
                // read info from version.txt on the server
                var imagesCountString = await _client.GetStringAsync("http://requiemnetwork.com/launcher/background/count.txt");
                var imagesCountStringSplit = imagesCountString.Split(',');
                var numberOfImagesString = imagesCountStringSplit[0].Split('"')[3];
                _numberOfImages = Int32.Parse(numberOfImagesString);

                _backgroundImages = Directory.GetFiles(_backgroundImagesPath, "*.jpg");

                // if there is no image in the background images folder
                if (_backgroundImages.Length == 0)
                {
                    // <--------- set background image for mainwindow --------->
                    Random random = new Random();
                    // randomize an integer from 1
                    int randomIndex = random.Next(1, _numberOfImages + 1);
                    // set background image
                    _currentImageIndex = randomIndex;
                    _currentBackgroundImage = new ImageBrush();
                    _currentBackgroundImage.ImageSource = new BitmapImage(new Uri("http://requiemnetwork.com/launcher/background/background_0" + _currentImageIndex + ".jpg"));

                    Dispatcher.Invoke((Action)(() =>
                    {
                        this.Background = _currentBackgroundImage;
                    }));
                    
                    new Thread(delegate ()
                    {
                        using (WebClient client = new WebClient())
                        {
                            for (int i = 1; i <= _numberOfImages; i++)
                            {
                                client.DownloadFileAsync(new Uri("http://requiemnetwork.com/launcher/background/background_0" + i + ".jpg"),
                                                                            Path.Combine(_backgroundImagesPath, "background_0" + i + ".jpg"));

                                // WebClient does not support concurrent I/O so have to sleep thread until finish downloading
                                while (client.IsBusy)
                                {
                                    Thread.Sleep(1000);
                                }
                            }
                        }

                        // get all background images in folder after finish downloading all images
                        _backgroundImages = Directory.GetFiles(_backgroundImagesPath, "*.jpg");

                    }).Start();

                }
                else // if there is image
                {
                    if (_backgroundImages.Length != _numberOfImages) // if the number of image and count are different
                    {
                        // clear directory and all contents
                        Directory.Delete(_backgroundImagesPath, true);

                        // recreate directory
                        Directory.CreateDirectory(_backgroundImagesPath);

                        // <--------- set background image for mainwindow --------->
                        Random random = new Random();
                        // randomize an integer from 1
                        int randomIndex = random.Next(1, _numberOfImages + 1);
                        // set background image
                        _currentImageIndex = randomIndex;
                        _currentBackgroundImage = new ImageBrush();
                        _currentBackgroundImage.ImageSource = new BitmapImage(new Uri("http://requiemnetwork.com/launcher/background/background_0" + _currentImageIndex + ".jpg"));

                        Dispatcher.Invoke((Action)(() =>
                        {
                            this.Background = _currentBackgroundImage;
                        }));

                        new Thread(delegate ()
                        {
                            using (WebClient client = new WebClient())
                            {
                                for (int i = 1; i <= _numberOfImages; i++)
                                {
                                    client.DownloadFileAsync(new Uri("http://requiemnetwork.com/launcher/background/background_0" + i + ".jpg"),
                                                                                Path.Combine(_backgroundImagesPath, "background_0" + i + ".jpg"));

                                    // WebClient does not support concurrent I/O so have to sleep thread until finish downloading
                                    while (client.IsBusy)
                                    {
                                        Thread.Sleep(1000);
                                    }
                                }
                            }

                            // get all background images in folder after finish downloading all images
                            _backgroundImages = Directory.GetFiles(_backgroundImagesPath, "*.jpg");

                        }).Start();
                    }
                    else // if the number of images and count is the same
                    {
                        _backgroundImages = Directory.GetFiles(_backgroundImagesPath, "*.jpg");
                        // <--------- set background image for mainwindow --------->
                        Random random = new Random();
                        // randomize an integer from 1
                        int randomIndex = random.Next(1, _numberOfImages + 1);
                        string chosenOne = _backgroundImages[randomIndex];
                        // set background image
                        _currentImageIndex = randomIndex;
                        _currentBackgroundImage = new ImageBrush();
                        _currentBackgroundImage.ImageSource = new BitmapImage(new Uri(chosenOne, UriKind.Relative));

                        Dispatcher.Invoke((Action)(() =>
                        {
                            this.Background = _currentBackgroundImage;
                        }));
                    }
                }
            }
            catch (Exception e)
            {
                log.Error(e.ToString());
            }

        }

        private void SetBackgroundImage()
        {
            log.Info("Set/change background image(s).");

            // <--------- switch image randomly --------->
            Random random = new Random();

            // random image indexes
            int newImageIndex = random.Next(0, _numberOfImages);

            // avoid repeating images
            while (newImageIndex == _currentImageIndex)
            {
                newImageIndex = random.Next(0, _numberOfImages);
            }

            // set background image
            string newChosenOne = _backgroundImages[newImageIndex];
            ImageBrush newBackground = new ImageBrush();
            newBackground.ImageSource = new BitmapImage(new Uri(newChosenOne, UriKind.Relative));
            Dispatcher.Invoke((Action)(() =>
            {
                this.Background = newBackground;
            }));
            // set current image indicator for animation
            _currentBackgroundImage = newBackground;

            /* <--------- switch image in order --------->
            int newImageIndex;

            if (_currentImageIndex == (_numberOfImages-1)) // if reach the end of the list
            {
                newImageIndex = 0; // go back to first image
            }
            else 
            {
                newImageIndex = _currentImageIndex + 1; // go forward
            }*/

            /*
            // <--------- image fade animation for smoother switch --------->
            var fadeInAnimation = new DoubleAnimation(1d, TimeSpan.FromSeconds(0.7));
            var fadeOutAnimation = new DoubleAnimation(0d, TimeSpan.FromSeconds(0.7));

            fadeOutAnimation.Completed += (o, e) =>
            {
                Dispatcher.Invoke((Action)(() =>
                {
                    // set background image
                    string newChosenOne = _backgroundImages[newImageIndex];
                    ImageBrush newBackground = new ImageBrush();
                    newBackground.ImageSource = new BitmapImage(new Uri(newChosenOne, UriKind.Relative));
                    newBackground.BeginAnimation(Brush.OpacityProperty, fadeInAnimation);
                    this.Background = newBackground;
                    
                    // set current image indicator for animation
                    _currentBackgroundImage = newBackground;
                }));
            };

            Dispatcher.Invoke((Action)(() =>
            {
                _currentBackgroundImage.BeginAnimation(Brush.OpacityProperty, fadeOutAnimation);
            })); */

            // set current index
            _currentImageIndex = newImageIndex;
        }

        private void BackgroundChangeTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                SetBackgroundImage();
            }));
        }
        #endregion

        #region Setup discord rpc client
        /*
        public void SetupDiscordRpcClient()
        {
            log.Info("Setup drpc.");

            discordRpcClient.OnReady += (sender, e) =>
            {
                Console.WriteLine("Received Ready from user {0}", e.User.Username);
            };

            discordRpcClient.OnPresenceUpdate += (sender, e) =>
            {
                Console.WriteLine("Received Update! {0}", e.Presence);
            };

            discordRpcClient.Initialize();
        }*/
        #endregion

        #region Custom window resize - auto calculate ratio
        private double _aspectRatio;
        private bool? _adjustingHeight = null;

        internal enum SWP
        {
            NOMOVE = 0x0002
        }
        internal enum WM
        {
            WINDOWPOSCHANGING = 0x0046,
            EXITSIZEMOVE = 0x0232,
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public int flags;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(ref Win32Point pt);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point
        {
            public Int32 X;
            public Int32 Y;
        };

        public static Point GetMousePosition() // mouse position relative to screen
        {
            Win32Point w32Mouse = new Win32Point();
            GetCursorPos(ref w32Mouse);
            return new Point(w32Mouse.X, w32Mouse.Y);
        }


        private void Window_SourceInitialized(object sender, EventArgs ea)
        {
            HwndSource hwndSource = (HwndSource)HwndSource.FromVisual((Window)sender);
            hwndSource.AddHook(DragHook);

            _aspectRatio = this.Width / this.Height;
        }

        private IntPtr DragHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch ((WM)msg)
            {
                case WM.WINDOWPOSCHANGING:
                    {
                        WINDOWPOS pos = (WINDOWPOS)Marshal.PtrToStructure(lParam, typeof(WINDOWPOS));

                        if ((pos.flags & (int)SWP.NOMOVE) != 0)
                            return IntPtr.Zero;

                        Window wnd = (Window)HwndSource.FromHwnd(hwnd).RootVisual;
                        if (wnd == null)
                            return IntPtr.Zero;

                        // determine what dimension is changed by detecting the mouse position relative to the 
                        // window bounds. if gripped in the corner, either will work.
                        if (!_adjustingHeight.HasValue)
                        {
                            Point p = GetMousePosition();

                            double diffWidth = Math.Min(Math.Abs(p.X - pos.x), Math.Abs(p.X - pos.x - pos.cx));
                            double diffHeight = Math.Min(Math.Abs(p.Y - pos.y), Math.Abs(p.Y - pos.y - pos.cy));

                            _adjustingHeight = diffHeight > diffWidth;
                        }

                        if (_adjustingHeight.Value)
                            pos.cy = (int)(pos.cx / _aspectRatio); // adjusting height to width change
                        else
                            pos.cx = (int)(pos.cy * _aspectRatio); // adjusting width to heigth change

                        Marshal.StructureToPtr(pos, lParam, true);
                        handled = true;
                    }
                    break;
                case WM.EXITSIZEMOVE:
                    _adjustingHeight = null; // reset adjustment dimension and detect again next time window is resized
                    break;
            }

            return IntPtr.Zero;
        }
        #endregion
    }
}
