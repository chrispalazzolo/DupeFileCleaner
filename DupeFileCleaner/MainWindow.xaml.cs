﻿using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DupeFileCleaner
{
    public partial class MainWindow : Window
    {
        IDictionary<string, List<string>> hashedFiles = new Dictionary<string, List<string>>();

        int fileCt = 0;
        int dirScannedCt = 0;
        int filesScannedCt = 0;
        int dupsFoundCt = 0;
        
        public MainWindow()
        {
            InitializeComponent();
            txtSelectedFolder.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        private void Initialize()
        {
            hashedFiles.Clear();
            fileCt = 0;
            dirScannedCt = 0;
            filesScannedCt = 0;
            dupsFoundCt = 0;
            lblDirScannedCt.Content = 0;
            lblFilesScannedCt.Content = 0;
            lblDupFoundCt.Content = 0;
            lblScanningNow.Content = "";
            tvMatches.Items.Clear();
            prgbrScan.Value = 0;
            lboxLogging.Items.Clear();
            lblScanningNow.Content = "Choose a folder and press scan to start...";
        }
        private void btnFolderSelect_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog();
            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                txtSelectedFolder.Text = dialog.FolderName;
            }
        }

        private void btnScan_Click(object sender, RoutedEventArgs e)
        {
            Initialize();
            lblScanningNow.Content = "Starting Scan...";

            BackgroundWorker backgroundWorker = new BackgroundWorker();
            backgroundWorker.WorkerReportsProgress = true;
            backgroundWorker.WorkerSupportsCancellation = true;
            backgroundWorker.DoWork += Worker_Scan;
            backgroundWorker.ProgressChanged += Worker_ProgressChange;
            backgroundWorker.RunWorkerCompleted += ScanComplete;
            backgroundWorker.RunWorkerAsync(argument: txtSelectedFolder.Text);
        }

        private void Worker_Scan(object sender, DoWorkEventArgs e)
        {
            string path = (string)e.Argument;
            UpdateLogUI(lboxLogging, "Starting Scan :: " + path);
            GetFileCount(path);
            ScanDirectory(path, sender);
        }

        private void GetFileCount(string path)
        {
            if(Directory.Exists(path))
            {
                try
                {
                    fileCt += Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Count();
                }
                catch
                {
                    GetFileCountPerDirectory(path);
                }
            }
        }

        private void GetFileCountPerDirectory(string path)
        {
            if(Directory.Exists(path))
            {
                try
                {
                    string[] dirs = Directory.GetDirectories(path);
                    fileCt += Directory.EnumerateFiles(path).Count();

                    foreach (string dir in dirs)
                    {
                        GetFileCount(dir);
                    }
                }
                catch { }
            }
        }
        private void ScanDirectory(string path, object sender)
        {
            if (path != String.Empty && Directory.Exists(path))
            {
                dirScannedCt++;
                UpdateUI(lblDirScannedCt, dirScannedCt.ToString());
                UpdateUI(lblScanningNow, "Scanning Folder: " + path);
                UpdateLogUI(lboxLogging, "Scanning Folder :: " + path);

                try
                {
                    string[] foundDirs = Directory.GetDirectories(path);
                    string[] foundFiles = Directory.GetFiles(path);

                    if (foundFiles.Length > 0)
                    {
                        foreach (string foundFile in foundFiles)
                        {
                            UpdateUI(lblScanningNow, "Scanning File: " + foundFile);
                            UpdateLogUI(lboxLogging, "Scanning File :: " +  foundFile);
                            string hashKey = GetFileHash(foundFile);
                            
                            if(hashedFiles.ContainsKey(hashKey))
                            {
                                hashedFiles[hashKey].Add(foundFile);
                                dupsFoundCt++;
                                UpdateUI(lblDupFoundCt, dupsFoundCt.ToString());
                            }
                            else
                            {
                                hashedFiles.Add(hashKey, new List<string> { foundFile });
                            }

                            filesScannedCt++;
                            UpdateUI(lblFilesScannedCt, filesScannedCt.ToString());
                            double prcComplete = (double)filesScannedCt / (double)fileCt * 100;
                            (sender as BackgroundWorker).ReportProgress((int)prcComplete);
                        }
                    }

                    if (foundDirs.Length > 0)
                    {
                        foreach (string dir in foundDirs)
                        {
                            ScanDirectory(dir, sender);
                        }
                    }
                }
                catch(Exception)
                {
                    UpdateLogUI(lboxLogging, "Error :: Access Denied: " + path);
                }
            }
            else
            {
                UpdateLogUI(lboxLogging, "Error :: Invalid Path: " + path);
            }
        }

        private string GetFileHash(string path)
        {
            string hash = String.Empty;

            if(path != string.Empty)
            {
                using(MD5 md5 = MD5.Create())
                {
                    using(FileStream stream = File.OpenRead(path))
                    {
                        byte[] md5Hash = md5.ComputeHash(stream);
                        hash = BitConverter.ToString(md5Hash).Replace("-", "").ToLowerInvariant();
                    }
                }
            }
            else
            {
                UpdateLogUI(lboxLogging, "Error :: Can't hash file: " + path);
            }
            
            return hash;
        }

        private TreeViewItem GetTreeNode(string name, string header)
        {
            TreeViewItem newNode = new TreeViewItem();

            if(name != string.Empty && header != string.Empty)
            {
                newNode.Name = name;
                newNode.Header = header;
                /*Color color = new Color();
                color. = "#FFBDBDBD";
                newNode.Foreground = new SolidColorBrush(new Color("#FFBDBDBD"));*/
            }
            
            return newNode;
        }

        private void UpdateUI(ContentControl control, string value)
        {
            Dispatcher.Invoke(new Action(() => { control.Content = value; }));
        }

        private void UpdateLogUI(ListBox listBox, string value)
        {
            Dispatcher.Invoke(new Action(() => { listBox.Items.Add(value); }));
        }
        
        private void Worker_ProgressChange(object sender, ProgressChangedEventArgs e)
        {
            prgbrScan.Value = e.ProgressPercentage;
        }

        private void ScanComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            lblScanningNow.Content = "Scan Complete!";
            UpdateLogUI(lboxLogging, "Scan Complete :: Directories Scanned: " + dirScannedCt + " | Files Scanned: " + filesScannedCt + " | Duplicates Found: " + dupsFoundCt);
            FillTreeView();
        }

        private void FillTreeView()
        {
            if(hashedFiles.Count > 0)
            {
                foreach(KeyValuePair<string, List<string>> kvp in hashedFiles)
                {
                    if(kvp.Value.Count > 1)
                    {
                        string name = "pn" + kvp.Key + "0";
                        TreeViewItem parentNode = GetTreeNode(name, kvp.Value[0]);
                        
                        for(int i = 1; i < kvp.Value.Count; i++)
                        {
                            name = "sn" + kvp.Key + i;
                            TreeViewItem subNode = GetTreeNode(name, kvp.Value[i]);
                            parentNode.Items.Add(subNode);
                        }

                        tvMatches.Items.Add(parentNode);
                    } 
                }
            }
        }

        private void btnSaveLog_Click(object sender, RoutedEventArgs e)
        {
            if(lboxLogging.Items.Count > 0)
            {
                SaveFileDialog saveFile = new SaveFileDialog();
                saveFile.InitialDirectory = txtSelectedFolder.Text;

                if (saveFile.ShowDialog() == true)
                {
                    string[] logItems = new string[lboxLogging.Items.Count];

                    for (int i = 0; i < lboxLogging.Items.Count; i++)
                    {
                        logItems[i] = lboxLogging.Items[i].ToString();
                    }

                    File.WriteAllLines(saveFile.FileName, logItems.ToArray());
                }
            }
        }
    }
}