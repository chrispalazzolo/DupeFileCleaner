using System.IO;
using System.Security.Cryptography;
using System.Windows;

namespace DupeFileCleaner
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        IDictionary<string, string> hashedFiles = new Dictionary<string, string>();
        IDictionary<string, List<string>> matches = new Dictionary<string, List<string>>();
        public MainWindow()
        {
            InitializeComponent();
            txtSelectedFolder.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
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
            String path = txtSelectedFolder.Text;
            ScanDirectory(path);
        }
        private void ScanDirectory(string path)
        {
            if (path != String.Empty && Directory.Exists(path))
            {
                string[] subDirectories = new string[] {};
                try
                {
                    subDirectories = Directory.GetDirectories(path);
                    string[] foundFiles = Directory.GetFiles(path);

                    if (subDirectories.Length > 0)
                    {
                        foreach (string subDirectory in subDirectories)
                        {
                            ScanDirectory(subDirectory);
                        }
                    }

                    if (foundFiles.Length > 0)
                    {
                        foreach (string foundFile in foundFiles)
                        {
                            string hashKey = GetFileHash(foundFile);

                            if(hashedFiles.ContainsKey(hashKey))
                            {
                                if(matches.ContainsKey(hashKey))
                                {
                                    matches[hashKey].Add(foundFile);
                                }
                                else
                                {
                                    matches.Add(hashKey, new List<string> {hashedFiles[hashKey], foundFile});
                                }
                            }
                            else
                            {
                                hashedFiles.Add(hashKey, foundFile);
                            }
                        }
                    }
                }
                catch
                {
                    MessageBox.Show("Access Denied: " + path);
                }
            }
            else
            {
                MessageBox.Show("Invalid File Path!");
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
                MessageBox.Show("GetFileHash(): Invalid File Path!");
            }
            
            return hash;
        }
    }
}