using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DupeFileCleaner
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        IDictionary<string, List<string>> hashedFiles = new Dictionary<string, List<string>>();
        
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
            dirScannedCt = 0;
            filesScannedCt = 0;
            dupsFoundCt = 0;
            lblDirScannedCt.Content = 0;
            lblFilesScannedCt.Content = 0;
            lblDupFoundCt.Content = 0;
            lblScanningNow.Content = "";
            tvMatches.Items.Clear();
            prgbrScan.Value = 0;
        }
        private void btnFolderSelect_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                txtSelectedFolder.Text = dialog.FolderName;
            }
        }

        private void btnScan_Click(object sender, RoutedEventArgs e)
        {
            Initialize();
            lblStatus.Content = "Scanning:";

            BackgroundWorker backgroundWorker = new BackgroundWorker();
            backgroundWorker.WorkerReportsProgress = true;
            backgroundWorker.WorkerSupportsCancellation = true;
            backgroundWorker.DoWork += Worker_Scan;
            backgroundWorker.ProgressChanged += UpdateUI;
            backgroundWorker.RunWorkerCompleted += ScanComplete;
            backgroundWorker.RunWorkerAsync(argument: txtSelectedFolder.Text);
        }

        private void Worker_Scan(object sender, DoWorkEventArgs e)
        {
            ScanDirectory((string)e.Argument);
        }
        private void ScanDirectory(string path)
        {
            if (path != String.Empty && Directory.Exists(path))
            {
                dirScannedCt++;
                this.Dispatcher.Invoke(new Action(() => { lblDirScannedCt.Content = dirScannedCt.ToString(); }));
                this.Dispatcher.Invoke(new Action(() => { lblScanningNow.Content = path; }));

                try
                {
                    string[] foundDirs = Directory.GetDirectories(path);
                    string[] foundFiles = Directory.GetFiles(path);

                    if (foundFiles.Length > 0)
                    {
                        foreach (string foundFile in foundFiles)
                        {
                            this.Dispatcher.Invoke(new Action(() => { lblScanningNow.Content = foundFile; }));
                            string hashKey = GetFileHash(foundFile);
                            
                            if(hashedFiles.ContainsKey(hashKey))
                            {
                                hashedFiles[hashKey].Add(foundFile);
                                dupsFoundCt++;
                                this.Dispatcher.Invoke(new Action(() => { lblDupFoundCt.Content = dupsFoundCt.ToString(); }));
                            }
                            else
                            {
                                hashedFiles.Add(hashKey, new List<string> { foundFile });
                            }

                            filesScannedCt++;
                            this.Dispatcher.Invoke(new Action(() => { lblFilesScannedCt.Content = filesScannedCt.ToString(); }));
                        }
                    }

                    if (foundDirs.Length > 0)
                    {
                        foreach (string dir in foundDirs)
                        {
                            ScanDirectory(dir);
                        }
                    }
                }
                catch(Exception exp)
                {
                    //txtblkStatus.Text += Environment.NewLine + "Access Denied: " + path;
                }
            }
            else
            {
                MessageBox.Show("Invalid File Path!");
            }
        }

        private void UpdateUI(object sender, ProgressChangedEventArgs e)
        {
            prgbrScan.Value = e.ProgressPercentage;
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
                MessageBox.Show("GetFileHash(): Invalid File Path!");
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
                newNode.Foreground = Brushes.White;
            }
            
            return newNode;
        }
        /*public static void ThreadSafe(Action action)
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Normal, new MethodInvoker(action,  ));
        }*/
        private void ScanComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            lblStatus.Content = "Scan Complete!";
            lblScanningNow.Content = "";
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
    }
}