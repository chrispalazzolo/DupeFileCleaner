using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Collections.ObjectModel;

namespace DupeFileCleaner
{
    public partial class MainWindow : Window
    {
        IDictionary<string, List<string>> hashedFiles = new Dictionary<string, List<string>>();
        List<TreeViewItemModel> deleteFiles = new List<TreeViewItemModel>();
        BackgroundWorker scanBGWorker;

        int fileCt = 0;
        int dirScannedCt = 0;
        int filesScannedCt = 0;
        int dupsFoundCt = 0;
        int checkedBoxes = 0;
        
        public MainWindow()
        {
            InitializeComponent();
            txtSelectedFolder.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        private void Initialize()
        {
            hashedFiles.Clear();
            scanBGWorker = null;
            lblScanningNow.Content = "Choose a folder and press scan to start...";
            btnScan.IsEnabled = true;
            btnCancelScan.IsEnabled = false;
            tvMatches.ItemsSource = null;
            btnDeleteFiles.IsEnabled = false;
            deleteFiles.Clear();
            checkedBoxes = 0;
            lboxLogging.Items.Clear();
            btnSaveLog.IsEnabled = false;
            fileCt = 0;
            dirScannedCt = 0;
            lblDirScannedCt.Content = 0;
            filesScannedCt = 0;
            lblFilesScannedCt.Content = 0;
            dupsFoundCt = 0;
            lblDupFoundCt.Content = 0;
            prgbrScan.Value = 0;
        }
        private void btnFolderSelect_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog();
            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                txtSelectedFolder.Text = dialog.FolderName;
                Initialize();
            }
        }

        private void btnScan_Click(object sender, RoutedEventArgs e)
        {
            Initialize();
            lblScanningNow.Content = "Starting Scan...";
            btnCancelScan.IsEnabled = true;
            btnScan.IsEnabled = false;
            scanBGWorker = new BackgroundWorker();
            scanBGWorker.WorkerReportsProgress = true;
            scanBGWorker.WorkerSupportsCancellation = true;
            scanBGWorker.DoWork += Worker_Scan;
            scanBGWorker.ProgressChanged += Worker_ProgressChange;
            scanBGWorker.RunWorkerCompleted += ScanComplete;
            scanBGWorker.RunWorkerAsync(argument: txtSelectedFolder.Text);
        }

        private void Worker_Scan(object sender, DoWorkEventArgs e)
        {
            string startingDir = (string)e.Argument;

            UpdateLogUI(lboxLogging, "Starting Scan :: " + startingDir);
            GetFileCount(startingDir, e);
            ScanDirectory(startingDir, sender, e);
        }

        private void GetFileCount(string dir, DoWorkEventArgs e)
        {
            string path = dir;

            if (Directory.Exists(path))
            {
                if(scanBGWorker.CancellationPending != true)
                {
                    try
                    {
                        fileCt += Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Count();
                    }
                    catch
                    {
                        GetFileCountPerDirectory(path, e);
                    }
                }
                else
                {
                    e.Cancel = true;
                    return;
                }
            }
        }

        private void GetFileCountPerDirectory(string path, DoWorkEventArgs e)
        {
            if(Directory.Exists(path))
            {
                try
                {
                    string[] dirs = Directory.GetDirectories(path);
                    fileCt += Directory.EnumerateFiles(path).Count();

                    foreach (string dir in dirs)
                    {
                        GetFileCount(dir, e);
                    }
                }
                catch { }
            }
        }
        private void ScanDirectory(string path, object sender, DoWorkEventArgs doWorkEvtArgs)
        {
            if (path != String.Empty && Directory.Exists(path))
            {
                dirScannedCt++;
                UpdateUI(lblDirScannedCt, dirScannedCt.ToString());
                UpdateUI(lblScanningNow, "Scanning Folder: " + path);
                UpdateLogUI(lboxLogging, "Scanning Folder :: " + path);

                try
                {   
                    ScanFiles(Directory.GetFiles(path), sender, doWorkEvtArgs);

                    string[] foundDirs = Directory.GetDirectories(path);

                    if (foundDirs.Length > 0)
                    {
                        foreach (string dir in foundDirs)
                        {
                            if(scanBGWorker.CancellationPending != true)
                            {
                                ScanDirectory(dir, sender, doWorkEvtArgs);
                            }
                            else
                            {
                                return;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    UpdateLogUI(lboxLogging, "Error :: Access Denied: " + path);
                }
            }
            else
            {
                UpdateLogUI(lboxLogging, "Error :: Invalid Path: " + path);
            }
        }

        private void ScanFiles(string[] files, object sender, DoWorkEventArgs doWorkEvtArgs)
        {
            if (files.Length > 0)
            {
                foreach (string foundFile in files)
                {
                    if(scanBGWorker.CancellationPending != true)
                    {
                        UpdateUI(lblScanningNow, "Scanning File: " + foundFile);
                        UpdateLogUI(lboxLogging, "Scanning File :: " + foundFile);

                        string hashKey = GetFileHash(foundFile);

                        if (hashedFiles.ContainsKey(hashKey))
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
                    else
                    {
                        doWorkEvtArgs.Cancel = true;
                        return;
                    }
                }
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
            if(!e.Cancelled)
            {
                lblScanningNow.Content = "Scan Complete!";
                UpdateLogUI(lboxLogging, "Scan Complete :: Directories Scanned: " + dirScannedCt + " | Files Scanned: " + filesScannedCt + " | Duplicates Found: " + dupsFoundCt);
                btnCancelScan.IsEnabled = false;
                btnScan.IsEnabled = true;
                btnSaveLog.IsEnabled = true;
            }
            else
            {
                CancelScan();  
            }

            FillTreeView();
        }

        private void FillTreeView()
        {
            if(hashedFiles.Count > 0)
            {
                tvMatches.ItemsSource = null;
                checkedBoxes = 0;
                btnDeleteFiles.IsEnabled = false;

                ObservableCollection<TreeViewItemModel> items = new ObservableCollection<TreeViewItemModel>();
                
                foreach(KeyValuePair<string, List<string>> kvp in hashedFiles)
                {
                    if(kvp.Value.Count > 1)
                    {
                        string name = kvp.Key;
                        TreeViewItemModel parentNode = new TreeViewItemModel() { Name = name, Header = kvp.Value[0], Children = new ObservableCollection<TreeViewItemModel>() };
                        
                        for(int i = 1; i < kvp.Value.Count; i++)
                        {
                            name = kvp.Key;
                            parentNode.Children.Add(new TreeViewItemModel() { Name = name, Header = kvp.Value[i] });
                            
                        }

                        items.Add(parentNode);
                    }
                }

                tvMatches.ItemsSource = items;
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            btnDeleteFiles.IsEnabled = true;
            deleteFiles.Add((TreeViewItemModel)(e.Source as CheckBox).DataContext);
            checkedBoxes++;
        }
        private void CheckBox_UnChecked(object sender, RoutedEventArgs e)
        {
            deleteFiles.Remove((TreeViewItemModel)(e.Source as CheckBox).DataContext);
            checkedBoxes--;

            if(checkedBoxes <= 0)
            {
                btnDeleteFiles.IsEnabled = false;
            }
        }
        private void btnSaveLog_Click(object sender, RoutedEventArgs e)
        {
            if(lboxLogging.Items.Count > 0)
            {
                SaveFileDialog saveFile = new SaveFileDialog();
                saveFile.InitialDirectory = txtSelectedFolder.Text;
                saveFile.Filter = "Text file (*.txt)|*.txt";
                saveFile.DefaultExt = ".txt";

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

        private void btnCancelScan_Click(object sender, RoutedEventArgs e)
        {
            scanBGWorker.CancelAsync();
        }

        private void CancelScan()
        {
            UpdateUI(lblScanningNow, "Scan Canceled!");
            UpdateLogUI(lboxLogging, "Scan Canceled!");
            btnCancelScan.IsEnabled = false;
            btnScan.IsEnabled = true;
        }

        private void btnDeleteFiles_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("Delete selected files?", "Delete Files", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                if(deleteFiles.Count > 0)
                {
                    foreach (TreeViewItemModel item in deleteFiles)
                    {
                        string file = item.Header;
                        try
                        {
                            File.Delete(file);
                            hashedFiles[item.Name].Remove(file);
                            lboxLogging.Items.Add("Deleting File :: " + file);
                        }
                        catch
                        {
                            lboxLogging.Items.Add("Error :: Can't delete file: " + file);
                        }
                    }

                    FillTreeView();
                }
            }
            else
            {
                return;
            }
        }
    }
}