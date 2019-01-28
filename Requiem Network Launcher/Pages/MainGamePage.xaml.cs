﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Bleak;
using System.Diagnostics;
using System.Net.Http;
using System.Windows.Controls;
using System.Threading;
using System.IO;
using System.Net;
using Ionic.Zip;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using System.ServiceModel.Syndication;

namespace Requiem_Network_Launcher
{
    /// <summary>
    /// Interaction logic for MainGamePage.xaml
    /// </summary>
    public partial class MainGamePage : Page
    {
        private Injector _injector = new Injector();
        private Process _vindictus;
        private double _updateFileSize;
        private string _continueSign = "continue";
        private string _updatePath;
        private string _updateDownloadID;
        private string _versionTxtCheck = "yes";
        private string _currentVersionLocal;

        public MainGamePage()
        {
            InitializeComponent();
            CheckFilesPath();
            // display updating status notice
            ProgressDetails.Visibility = Visibility.Hidden;
            ProgressBar.Visibility = Visibility.Hidden;
        }

        #region Test RSS
        void RssNews()
        {
            try
            {
                XmlReaderSettings settings = new XmlReaderSettings();

                XmlUrlResolver resolver = new XmlUrlResolver();

                resolver.Credentials = new NetworkCredential("DumLauncher","Requiem2018-123");

                settings.XmlResolver = resolver;

                XmlReader reader = XmlReader.Create(@"http://requiemnetwork.com/forum/22-news-and-announcements.xml/", settings);

                SyndicationFeed feed = SyndicationFeed.Load(reader);

                foreach (SyndicationItem item in feed.Items)
                {
                    string description = item.Summary.Text;
                    // Get rid of the tags
                    description = Regex.Replace(description, @"<.+?>", String.Empty);

                    // Then decode the HTML entities
                    description = WebUtility.HtmlDecode(description);
                    Dispatcher.Invoke((Action)(() =>
                    {
                        RSSContent.Text += description;
                        RSSContent.Text += "======================================" + Environment.NewLine;
                    }));
                }
            }
            catch(Exception)
            {
            }
        }
        #endregion

        #region Start game function
        private async void StartGame()
        {
            var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;

            _vindictus = new Process();
            try
            {
                _vindictus.EnableRaisingEvents = true;
                _vindictus.Exited += _process_Exited;
                _vindictus.StartInfo.FileName = mainWindow.processPath;
                _vindictus.StartInfo.Arguments = " -lang zh-TW -token " + UserInfoRegistry.LoginToken; // if token is null -> server under maintenance error
                _vindictus.Start();
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show("There is already an instance of the game running...", "Error");
            }

            // inject winnsi.dll to Vindictus.exe 
            _injector.CreateRemoteThread(mainWindow.dllPath, _vindictus.Id);

            Dispatcher.Invoke((Action)(() =>
            {
                // disable start game button after the game start
                StartGameButton.Content = "PLAYING";
                StartGameButton.IsEnabled = false;
            }));

            // close the launcher
            await Task.Delay(2000);

            mainWindow.WindowState = WindowState.Minimized;
        }

        private void _process_Exited(object sender, EventArgs e)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                // re-enable start game button after the game is closed
                StartGameButton.Content = "START GAME";
                StartGameButton.IsEnabled = true;
                StartGameButton.Foreground = new SolidColorBrush(Colors.Black);
            }));
        }
        #endregion

        #region Update game function
        private async void Update()
        {
            var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;

            // update ui for downloading
            Dispatcher.Invoke((Action)(() =>
            {
                ShowHideDownloadInfo(DownloadInfoBox.Height, "1line");
                DownloadInfoBox.Text = "Preparing update...";
                DownloadInfoBox.Foreground = new SolidColorBrush(Colors.LawnGreen);
            }));

            if (_versionTxtCheck == "not found")
            {
                HttpClient _client = new HttpClient();

                // get download links from server
                var updateDownload = await _client.GetStringAsync("http://requiemnetwork.com/launcher/update_02.txt");
                var updateDownloadSplit = updateDownload.Split(',');

                // download information for people who update their game regularly
                var updateDowndloadLink = updateDownloadSplit[0].Split('"');

                _updateDownloadID = updateDowndloadLink[3];
                GetDownloadFileSize(updateDowndloadLink[5]);
            }
            else if (_versionTxtCheck == "yes")
            {

                HttpClient _client = new HttpClient();

                // get download links from server
                var updateDownload = await _client.GetStringAsync("http://requiemnetwork.com/launcher/update_01.txt");
                var updateDownloadSplit = updateDownload.Split(',');

                // download information for people who update their game regularly
                var updateDowndloadOld = updateDownloadSplit[0].Split('"');

                // download information for people who has not updated their game for a while
                var updateDowndloadNew = updateDownloadSplit[1].Split('"');

                // get download link based on their game's current version
                if (_currentVersionLocal == updateDowndloadOld[1]) // if player has the latest patch
                {
                    _updateDownloadID = updateDowndloadOld[3];
                    GetDownloadFileSize(updateDowndloadOld[5]);
                }
                else // if player has an outdate patch 
                {
                    _updateDownloadID = updateDowndloadNew[3];
                    GetDownloadFileSize(updateDowndloadNew[5]);
                }
            }

            // create temporary zip file from download
            _updatePath = System.IO.Path.Combine(mainWindow.rootDirectory, "UpdateTemporary.zip");
            //_updatePath = @"D:\test\UpdateTemp.zip";


            // download update (zip) 
            try
            {
                using (CookieAwareWebClient webClient = new CookieAwareWebClient())
                {
                    if (_updateFileSize <= 100000000) // if file is < 100 MB -> no virus scan page -> can download directly
                    {
                        // download the update
                        var uri = new Uri("https://drive.google.com/uc?export=download&id=" + _updateDownloadID);
                        webClient.DownloadProgressChanged += WebClient_DownloadProgressChanged;
                        webClient.DownloadFileCompleted += WebClient_DownloadFileCompleted;
                        await webClient.DownloadFileTaskAsync(uri, _updatePath);
                    }
                    else // more than 100 MB -> have to bypass virus scanning page
                    {
                        // sometimes google drive returns an NID cookie instead of a download warning cookie at first attempt
                        // it will works in the second attempt
                        for (int i = 0; i < 2; i++)
                        {
                            // download page content
                            string DownloadString = await webClient.DownloadStringTaskAsync("https://drive.google.com/uc?export=download&id=" + _updateDownloadID);

                            // get confirm code from page content
                            Match match = Regex.Match(DownloadString, @"confirm=([0-9A-Za-z]+)");

                            if (_continueSign == "stop")
                            {
                                break;

                            }
                            else
                            {
                                // construct new download link with confirm code
                                string updateDownloadLinkNew = "https://drive.google.com/uc?export=download&" + match.Value + "&id=" + _updateDownloadID;
                                var uri = new Uri(updateDownloadLinkNew);
                                webClient.DownloadProgressChanged += WebClient_DownloadProgressChanged;
                                webClient.DownloadFileCompleted += WebClient_DownloadFileCompleted;
                                await webClient.DownloadFileTaskAsync(uri, _updatePath);

                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show(e.Message, "Error");
            }
        }

        private void WebClient_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;

            // get the size of the downloaded file
            long length = new System.IO.FileInfo(_updatePath).Length;

            if (length < 60000) // if the downloaded file is a web page content (usually has size smaller than 60KB)
            {
                // delete the file and re-send the request
                File.Delete(_updatePath);
            }
            else // if the file is valid, extract it
            {
                _continueSign = "stop";

                Dispatcher.Invoke((Action)(() =>
                {
                    ShowHideDownloadInfo(DownloadInfoBox.Height, "1line");
                    // display updating status notice
                    DownloadInfoBox.Text = "Extracting files...";
                }));

                // perform unzip in a new thread to prevent UI from freezing
                new Thread(delegate ()
                {
                    // extract update to main game folder and overwrite all existing files
                    using (ZipFile zip = ZipFile.Read(_updatePath))
                    {
                        zip.ExtractProgress += Zip_ExtractProgress;
                        //zip.ExtractAll(@"D:\test\", ExtractExistingFileAction.OverwriteSilently);
                        zip.ExtractAll(mainWindow.rootDirectory, ExtractExistingFileAction.OverwriteSilently);
                    }

                    // delete the temporary zip file after finish extracting
                    File.Delete(_updatePath);

                    Dispatcher.Invoke((Action)(() =>
                    {
                        // display updating status notice
                        ProgressDetails.Visibility = Visibility.Hidden;
                        ProgressBar.Visibility = Visibility.Hidden;
                    }));

                    _versionTxtCheck = "yes";

                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        CheckGameVersion();
                    }));

                }).Start();
            }
        }

        private void Zip_ExtractProgress(object sender, ExtractProgressEventArgs e)
        {
            if (e.TotalBytesToTransfer > 0)
            {
                Dispatcher.Invoke((Action)(() =>
                {
                    // display updating status notice
                    ProgressBar.Value = Convert.ToInt32(100 * e.BytesTransferred / e.TotalBytesToTransfer);
                    ProgressDetails.Text = "Extracting: " + e.CurrentEntry.FileName;
                }));
            }
        }

        private void WebClient_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            double total = ((double)e.BytesReceived / (double)_updateFileSize) * 100;
            if (_continueSign == "continue")
            {
                Dispatcher.Invoke((Action)(() =>
                {
                    // display updating status notice
                    ProgressDetails.Visibility = Visibility.Visible;
                    ProgressBar.Visibility = Visibility.Visible;
                    ProgressBar.Value = total;
                    ProgressDetails.Text = FileSizeType(e.BytesReceived) + " / " + FileSizeType(_updateFileSize);

                    ShowHideDownloadInfo(DownloadInfoBox.Height, "1line");
                    DownloadInfoBox.Text = "Dowloading update... " + total.ToString("F2") + "%";
                    
                }));
            }
        }

        private void GetDownloadFileSize(string fileSize)
        {
            _updateFileSize = Convert.ToInt64(fileSize);
        }

        private string FileSizeType(double bytes)
        {
            string fileSizeType;
            if ((bytes / 1024d / 1024d / 1024d) > 1)
            {
                fileSizeType = string.Format("{0} GB", (bytes / 1024d / 1024d / 1024d).ToString("0.00"));
            }
            else
            {
                fileSizeType = string.Format("{0} MB", (bytes / 1024d / 1024d).ToString("0.00"));
            }

            return fileSizeType;
        }
        #endregion

        #region Check game version function
        private async void CheckGameVersion()
        {
            var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;

            // update small version info at bottom left corner
            Dispatcher.Invoke((Action)(() =>
            {
                ShowHideDownloadInfo(DownloadInfoBox.Height, "1line");
                DownloadInfoBox.Text = "Checking game version...";
                DownloadInfoBox.Foreground = new SolidColorBrush(Colors.LawnGreen);

                // disable start game button when check for game version
                StartGameButton.Content = "UPDATING";
                StartGameButton.IsEnabled = false;
                StartGameButton.Foreground = new SolidColorBrush(Colors.Silver);
            }));

            // read info from version.txt file in main game folder
            var versionTextLocal = System.IO.File.ReadAllText(mainWindow.versionPath);
            var versionTextLocalSplit = versionTextLocal.Split(',');
            var currentVersionLocal = versionTextLocalSplit[0].Split('"')[3];
            var currentVersionDate = versionTextLocalSplit[1].Split('"')[3];

            // update small version info at bottom left corner
            Dispatcher.Invoke((Action)(() =>
            {
                VersionDisplayLabel.Content = "Version: " + currentVersionLocal + " - Release Date: " + currentVersionDate;
            }));

            // work with .net framework 4.5 and above
            HttpClient _client = new HttpClient();

            try
            {
                // read info from version.txt on the server
                var versionTextServer = await _client.GetStringAsync("http://requiemnetwork.com/launcher/version.txt");
                var versionTextServerSplit = versionTextServer.Split(',');
                var currentVersionServer = versionTextServerSplit[0].Split('"')[3];
                Console.WriteLine("current version on server:" + currentVersionServer);

                // check if player has updated their game yet or not
                if (currentVersionLocal != currentVersionServer)
                {
                    // store variable so that it can be accessed outside of async method
                    _currentVersionLocal = currentVersionLocal;
                    _continueSign = "continue";
                    Update();
                }
                else
                {
                    // display notice
                    Dispatcher.Invoke((Action)(() =>
                    {
                        ShowHideDownloadInfo(DownloadInfoBox.Height, "1line");
                        DownloadInfoBox.Text = "Your game is up-to-date!";
                        DownloadInfoBox.Foreground = new SolidColorBrush(Colors.LawnGreen);

                        // re-enable start game button for player
                        StartGameButton.Content = "START GAME";
                        StartGameButton.IsEnabled = true;
                        StartGameButton.Foreground = new SolidColorBrush(Colors.Black);

                    }));

                }
            }
            catch (Exception e)
            {
                if (e is HttpRequestException)
                {
                    System.Windows.MessageBox.Show(e.Message, "Connection error");

                    Dispatcher.Invoke((Action)(() =>
                    {
                        ShowHideDownloadInfo(DownloadInfoBox.Height, "3lines");
                        DownloadInfoBox.Text = "Cannot connect to server!\nPlease check your network connection first.\nContact staff for more help.";
                        DownloadInfoBox.Foreground = new SolidColorBrush(Colors.Red);
                    }));
                }
                else
                {
                    System.Windows.MessageBox.Show(e.Message, "Error");

                    Dispatcher.Invoke((Action)(() =>
                    {
                        ShowHideDownloadInfo(DownloadInfoBox.Height, "3lines");
                        DownloadInfoBox.Text = e.Message;
                        DownloadInfoBox.Foreground = new SolidColorBrush(Colors.Red);
                    }));
                }
            }

        }
        #endregion

        #region Check file path function
        private void CheckFilesPath()
        {
            var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;

            var ngclientFilePath = System.IO.Path.Combine(mainWindow.rootDirectory, "NGClient.aes");

            /*_dllPath = @"C:\Requiem\ko-KR\winnsi.dll";
            _processPath = @"C:\Requiem\ko-KR\Vindictus.exe";
            _versionPath = @"D:\test\version.txt";*/

            if (!File.Exists(mainWindow.versionPath))
            {
                _versionTxtCheck = "not found";
                Update();
            }
            else if (!File.Exists(mainWindow.dllPath))
            {
                _versionTxtCheck = "not found";
                System.Windows.MessageBox.Show("Cannot find winnsi.dll. Please make sure launcher is in your main game folder!", "Missing files", MessageBoxButton.OK, MessageBoxImage.Error);
                // update small version info at bottom left corner
                Dispatcher.Invoke((Action)(() =>
                {
                    ShowHideDownloadInfo(DownloadInfoBox.Height, "2lines");
                    DownloadInfoBox.Text = "Missing files.\nContact staff for more help.";
                    DownloadInfoBox.Foreground = new SolidColorBrush(Colors.Red);

                    StartGameButton.IsEnabled = false;
                    StartGameButton.Foreground = new SolidColorBrush(Colors.Silver);

                }));
            }
            else if (!File.Exists(mainWindow.processPath))
            {
                _versionTxtCheck = "not found";
                System.Windows.MessageBox.Show("Cannot find winnsi.dll. Please make sure launcher is in your main game folder!", "Missing files", MessageBoxButton.OK, MessageBoxImage.Error);
                // update small version info at bottom left corner
                Dispatcher.Invoke((Action)(() =>
                {
                    ShowHideDownloadInfo(DownloadInfoBox.Height, "2lines");
                    DownloadInfoBox.Text = "Missing files.\nContact staff for more help.";
                    DownloadInfoBox.Foreground = new SolidColorBrush(Colors.Red);

                    StartGameButton.IsEnabled = false;
                    StartGameButton.Foreground = new SolidColorBrush(Colors.Silver);
                }));
            }
            else if (!File.Exists(ngclientFilePath))
            {
                ShowHideDownloadInfo(DownloadInfoBox.Height, "2lines");
                System.Windows.MessageBox.Show("Cannot find winnsi.dll. Please make sure launcher is in your main game folder!", "Missing files", MessageBoxButton.OK, MessageBoxImage.Error);
                // update small version info at bottom left corner
                Dispatcher.Invoke((Action)(() =>
                {
                    DownloadInfoBox.Text = "Missing files.\nContact staff for more help.";
                    DownloadInfoBox.Foreground = new SolidColorBrush(Colors.Red);

                    StartGameButton.IsEnabled = false;
                    StartGameButton.Foreground = new SolidColorBrush(Colors.Silver);
                }));
            }
            else
            {
                CheckGameVersion();
            }
        }
        #endregion

        #region DownloadInfoBox animation
        private void ShowHideDownloadInfo(double from, string signal)
        {
            var da = new DoubleAnimation();
            da.Duration = TimeSpan.FromSeconds(0.5);
            da.From = from;
            if (signal == "1line")
            {
                if (from != 37)
                {
                    da.To = 37;
                    Dispatcher.Invoke((Action)(() =>
                    {
                        DownloadInfoBox.BeginAnimation(HeightProperty, da);
                    }));
                }
            }
            else if (signal == "2lines")
            {
                if (from != 70)
                {
                    da.To = 70;
                    Dispatcher.Invoke((Action)(() =>
                    {
                        DownloadInfoBox.BeginAnimation(HeightProperty, da);
                    }));
                }
            }
            else if (signal == "3lines")
            {
                if (from != 105)
                {
                    da.To = 105;
                    Dispatcher.Invoke((Action)(() =>
                    {
                        DownloadInfoBox.BeginAnimation(HeightProperty, da);
                    }));
                }
            }
            else
            {
                da.To = 0;
                Dispatcher.Invoke((Action)(() =>
                {
                    DownloadInfoBox.BeginAnimation(HeightProperty, da);
                }));
            }
        }
        #endregion

        #region All button click events of Main Game Page
        private void StartGameButton_Click(object sender, RoutedEventArgs e)
        {
            ShowHideDownloadInfo(DownloadInfoBox.Height, "reset");
            StartGame();
        }

        private void NewsButton_Click(object sender, RoutedEventArgs e)
        {
            MenuItemSetTransitions(0);
            RssNews();
        }

        private void PatchNotesButton_Click(object sender, RoutedEventArgs e)
        {
            MenuItemSetTransitions(1);
        }

        private void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            MenuItemSetTransitions(2);
            CheckGameVersion();
        }

        private void DropRateCalculatorButton_Click(object sender, RoutedEventArgs e)
        {
            MenuItemSetTransitions(3);
            var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;

            if (!File.Exists(mainWindow.dropRateCalculatorPath))
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
                    dropCalculator.Exited += DropCalculator_Exited; ;
                    dropCalculator.StartInfo.FileName = mainWindow.dropRateCalculatorPath;
                    dropCalculator.Start();
                }
                catch (Exception)
                {
                    System.Windows.MessageBox.Show("There is already an instance of the game running...", "Error");
                }
            }
            
        }

        private void DyeSystemButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://requiemnetwork.com/dye");
        }

        private void WebsiteButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://requiemnetwork.com");
        }
        
        private void DropCalculator_Exited(object sender, EventArgs e)
        {
            MenuItemSetTransitions(0);
        }
        #endregion

        #region Transition for menu of items in Main Game Page
        private void MenuItemSetTransitions(int itemIndex)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                NewsButton.Content = "NEWS";
                PatchNotesButton.Content = "PATCH NOTES";
                CheckForUpdatesButton.Content = "CHECK FOR UPDATES";
                DropRateCalculatorButton.Content = "CALCULATOR";
                DyeSystemButton.Content = "DYE SYSTEM";
                WebsiteButton.Content = "WEBSITE";

                NewsButton.Width = 380;
                PatchNotesButton.Width = 380;
                CheckForUpdatesButton.Width = 380;
                DropRateCalculatorButton.Width = 380;
                DyeSystemButton.Width = 380;
                WebsiteButton.Width = 380;

                NewsButton.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#7FFFFFFF");
                PatchNotesButton.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#7FFFFFFF");
                CheckForUpdatesButton.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#7FFFFFFF");
                DropRateCalculatorButton.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#7FFFFFFF");
                DyeSystemButton.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#7FFFFFFF");
                WebsiteButton.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#7FFFFFFF");
            }));

            switch (itemIndex)
            {
                case 1:
                    Dispatcher.Invoke((Action)(() =>
                    {
                        PatchNotesButton.Content = ">PATCH NOTES";
                        PatchNotesButton.Width = 415;
                        PatchNotesButton.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#BFFFFFFF");
                    }));
                    break;

                case 2:
                    Dispatcher.Invoke((Action)(() =>
                    {
                        CheckForUpdatesButton.Content = ">CHECK FOR UPDATES";
                        CheckForUpdatesButton.Width = 415;
                        CheckForUpdatesButton.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#BFFFFFFF");
                    }));
                    break;

                case 3:
                    Dispatcher.Invoke((Action)(() =>
                    {
                        DropRateCalculatorButton.Content = ">CALCULATOR";
                        DropRateCalculatorButton.Width = 415;
                        DropRateCalculatorButton.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#BFFFFFFF");
                    }));
                    break;

                /*case 4:
                    Dispatcher.Invoke((Action)(() =>
                    {
                        DyeSystemButton.Content = ">DYE SYSTEM";
                        DyeSystemButton.Width = 415;
                        DyeSystemButton.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#BFFFFFFF");
                    }));
                    break;

                case 5:
                    Dispatcher.Invoke((Action)(() =>
                    {
                        WebsiteButton.Content = ">WEBSITE";
                        WebsiteButton.Width = 415;
                        WebsiteButton.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#BFFFFFFF");
                    }));
                    break; */

                default:
                    Dispatcher.Invoke((Action)(() =>
                    {
                        NewsButton.Content = ">NEWS";
                        NewsButton.Width = 415;
                        NewsButton.Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#BFFFFFFF");
                    }));
                    break;
            }
        }
        #endregion
    }
}