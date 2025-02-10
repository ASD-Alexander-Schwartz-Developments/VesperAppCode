using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace VesperApp.Models
{
    [Serializable]
    internal class VesperOutputFolder
    {

        public const string DefaultVesperName = "VESPER";

        /// <summary>
        /// MyVesperData/UID/DATE_TIME/parsed/
        /// </summary>

        private string uid;
        private string name;
        public string root_folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + DefaultVesperFolder;
        public const string DefaultVesperFolder = "\\MyVesperData\\";
        public string subFolder = "";

        public VesperOutputFolder(string uid, string name = DefaultVesperName)
        {
            this.uid = uid;
            this.name = name;
            this.currentFolder = root_folder;
        }

        public string Name
        {
            get => this.name;
            set => this.name = value;
        }

        public string Id
        {
            get => this.uid;
        }

        public string currentFolder { get; internal set; }

        public override bool Equals(object? obj)
        {
            var objid = obj as VesperOutputFolder;

            if (objid == null)
            {
                // If it is null then it is not equal to this instance.
                return false;
            }

            // Instances are considered equal if the ReferenceId matches.
            return this.uid.Equals(objid.uid);
        }

        public override int GetHashCode()
        {
            return this.uid.GetHashCode();
        }

        public void CreateFolder(string root_path)
        {
            string newfolder = root_path + "\\" + this.uid;
            string newfiletag = newfolder + "\\.name";

            try
            {
                System.IO.DirectoryInfo infd = System.IO.Directory.CreateDirectory(newfolder);

                using (System.IO.FileStream fs = new System.IO.FileStream(newfiletag, System.IO.FileMode.Create))
                {
                    string text_to_write = this.uid + "=" + this.name;
                    byte[] data = System.Text.Encoding.ASCII.GetBytes(text_to_write);
                    fs.Write(data, 0, data.Length);
                    fs.Close();
                }

                System.IO.FileInfo FI = new System.IO.FileInfo(newfiletag);
                FI.Attributes = System.IO.FileAttributes.Hidden;
            }
            catch (Exception eee) { }
        }

        public void RemoveFolder(string root_path)
        {
            try
            {
                System.IO.Directory.Delete(root_path + "\\" + this.uid, true);
            }
            catch (Exception eee1) { }
        }
#if false
        public TreeView ListDirectory(TreeView treeView, string root)
        {
            treeView.Items.Clear();
            
            if (!root_folder.Equals(root))
                currentFolder = root;

            var stack = new Stack<object>();
            var rootDirectory = new DirectoryInfo(root);
            ItemCollection node = new ItemCollection(  (rootDirectory.Name) { Tag = rootDirectory };
            stack.Push(node);

            while (stack.Count > 0)
            {
                var currentNode = stack.Pop();
                var directoryInfo = (DirectoryInfo)currentNode.Tag;
                foreach (var directory in directoryInfo.GetDirectories())
                {
                    var childDirectoryNode = new TreeNode(directory.Name) { Tag = directory, Name = directory.Name };
                    currentNode.Nodes.Add(childDirectoryNode);
                    stack.Push(childDirectoryNode);
                }
            }

            treeView.Items.Add(node);
            return treeView;
        }

        public ListView DisplayFolder(string folderPath, ListView listView)
        {
            try
            {
                if (!System.IO.Directory.Exists(folderPath))
                    return listView;

                listView.Items.Clear();

                //if (System.IO.Directory.GetDirectories(folderPath).Length == 0)
                //    return;

                string[] files = System.IO.Directory.GetFiles(folderPath);
                FileInfo fi;

                for (int x = 0; x < files.Length; x++)
                {
                    fi = new FileInfo(files[x]);
                    if (!Regex.IsMatch(fi.Extension, "bin", RegexOptions.IgnoreCase))
                        continue;
                    ListViewItem item = new ListViewItem(fi.Name);
                    item.SubItems.Add(fi.LastWriteTime.ToString());
                    string type = getFileType(fi.Name);
                    item.SubItems.Add(type);

                    listView.Items.Add(item);
                }

                subFolder = folderPath;

                return listView;
                // add folder to file watcher to update list automatically.
                //this.fileSystemWatcher1.Path = folderPath;
            }
            catch (Exception e)
            {
                return listView;
            }
        }
#endif

        /// <summary>
        /// Finds and returns the logger's USB drive name.
        /// </summary>
        /// <returns>Mounted USB drive name</returns>
        public string GetDriveLetter()
        {
            
            return "";
        }

        public async Task<string> GetUSBData(string currentUid)
        {
            string drive = "";
            int count = 0;
            while (drive.Equals("") && count < 20)
            {
                drive = GetDriveLetter();
                count++;
                await Task.Delay(500);
            }

            if (!drive.Equals(""))
            {
                string targetDir, tempFile;
                string[] fnames;

                DateTime currentDate = DateTime.Now;

                if (this.uid == null)
                    this.uid = currentUid;

                fnames = Directory.GetFiles(drive);

                if (fnames.Length > 2)
                {
                    subFolder = DateTime.Now.ToString("dd_MM_yyyy-HH_mm_ss");
                    targetDir = root_folder + this.uid + "\\Data\\" + subFolder;
                }
                else
                {
                    return "NO DATA";
                }

                Directory.CreateDirectory(targetDir);
                Console.Write(targetDir + "\n");

                int padding = (int)(Math.Floor(Math.Log10(fnames.Length) + 1));
                string paddedFileName, newFile = "";
                await Task.Factory.StartNew(() =>
                {
                    double percent = 0, i = 0;

                    foreach (var file in fnames)
                    {
                        //if (!file.StartsWith("UID") && !file.StartsWith("config"))
                        {
                            paddedFileName = Path.GetFileName(file);
                            paddedFileName = paddedFileName.PadLeft(padding + 4, '0');
                            newFile = Path.Combine(targetDir, paddedFileName);
                            if (!File.Exists(newFile))
                                File.Copy(file, Path.Combine(newFile));
                        }

                        percent = Math.Ceiling((i++ / fnames.Length) * 100.0);
                        //Console.WriteLine("percent: " + percent + ", i: " + i + ", paddedFileName: " + paddedFileName);
                        /// TODO: Replace with event delegate MainForm.setStatusCustomMessage("Importing USB Data", Convert.ToInt32(percent));
                    }


                    tempFile = targetDir + "\\config.jso";
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);

                    tempFile = targetDir + "\\" + ("UID.txt").PadLeft(padding + 1, '0');
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);

                    /// TODO: Replace with event delegate MainForm.setStatusCustomMessage("Importing USB Data", 100);
                });

                return targetDir;// copied content to folder
            }


            return "";
        }


        public static VesperOutputFolder GetFromFolder(string path)
        {
            VesperOutputFolder vf = null;
            string filetag = path + "\\.name";

            try
            {
                using (System.IO.FileStream fs = new System.IO.FileStream(filetag, System.IO.FileMode.Open))
                {

                    byte[] data = new byte[fs.Length];
                    fs.Read(data, 0, data.Length);
                    fs.Close();
                    string idn = System.Text.Encoding.ASCII.GetString(data);

                    if (idn.Length > 0)
                    {
                        string[] pair = idn.Split(new char[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);

                        if (pair.Length == 2)
                        {
                            if (pair[0].Length > 0)
                            {
                                vf = new VesperOutputFolder(pair[0]);

                                if (pair[1].Length > 0)
                                {
                                    vf.Name = pair[1];
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception eee) { }

            return vf;
        }


        public string getFileType(string name)
        {
            char type = ' ';
            foreach (char c in name)
            {
                if (Char.IsLetter(c))
                {
                    type = c;
                    break;
                }
            }

            switch (type)
            {
                case 'G':
                    {
                        return "GPS";
                    }
                case 'U':
                    {
                        return "Audio";
                    }
                case 'E':
                    {
                        return "EEG";
                    }
                case 'S':
                    {
                        return "Special";
                    }
                case 'C':
                    {
                        return "Lepton";
                    }
                case 'A':
                    {
                        return "Acceleration";
                    }
                case 'I':
                    {
                        return "IMU10";
                    }
                case 'R':
                    {
                        return "Humidity";
                    }
                case 'F':
                    {
                        return "ADS1299";
                    }
                case 'L':
                    {
                        return "ALS3001D";
                    }
                default:
                    return "Unidentified";
            }
        }

        public void openInExplorer(string node)
        {
            if (currentFolder == null)
                Process.Start(root_folder);
            else
            {
                if (Directory.Exists(root_folder + node))
                    Process.Start(root_folder + node);
                else
                    Process.Start(currentFolder);
            }
        }

        public void openInExplorer()
        {
            if (currentFolder == null)
                Process.Start(root_folder);
            else
            {
                if (Directory.Exists(currentFolder + "\\Data\\"))
                    Process.Start(currentFolder + "\\Data\\");
                else
                    Process.Start(currentFolder);
            }
        }
#if false
        public void DeleteSelected(ListView listView, string path)
        {
            foreach (ListViewItem i in listView.CheckedItems)
            {
                if (File.Exists(path + i.Text))
                {
                    File.Delete(path + i.Text);
                    int idx = findListItemIdx(i.Text, listView);
                    if (idx >= 0)
                        listView.Items.RemoveAt(idx);
                    if (listView.CheckedItems.Count < 1)
                        break;
                }
            }
        }

        private int findListItemIdx(string name, ListView listView)
        {
            int idx = -1;
            for (int i = 0; i < listView.Items.Count; i++)
            {
                if (listView.Items[i].Text.Equals(name))
                {
                    idx = i;
                    break;
                }
            }
            return idx;
        }

#endif
    }









    [Serializable]
    class VesperOutputFolders
    {
        public const string DefaultVesperFolder = "\\MyVesperData";
        /// <summary>
        /// MyVesperData/UID/DATE_TIME/parsed/
        /// 
        /// </summary>

        private string root_path;
        private List<VesperOutputFolder> folders;


        public List<VesperOutputFolder> Folders
        {
            get { return this.folders; }
        }

        public VesperOutputFolders(string path)
        {
            this.root_path = path;
            RebuildList();
        }

        public VesperOutputFolders()
        {
            this.root_path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + DefaultVesperFolder;
            RebuildList();
        }

        public VesperOutputFolder Add(VesperOutputFolder vf)
        {
            bool isfound = false;
            VesperOutputFolder f = null;

            foreach (VesperOutputFolder fl in this.folders)
            {
                if (fl.Equals(vf) == true)
                {
                    isfound = true;
                    f = fl;
                    // fl.Name = vf.Name;
                    break;
                }
            }

            if (isfound == true)
            {
            }
            else
            {
                this.folders.Add(vf);
                vf.CreateFolder(this.root_path);
                f = vf;
            }

            return f;
        }

        public VesperOutputFolder Add(string uid)
        {
            VesperOutputFolder of = new VesperOutputFolder(uid);

            return this.Add(of);
        }

        public void Delete(VesperOutputFolder vf, bool remove_folder)
        {
            if (remove_folder == true)
            {
                vf.RemoveFolder(this.root_path);
            }
            this.folders.Remove(vf);
        }


        private void RebuildList()
        {
            List<string> dirs = null;
            if (System.IO.Directory.Exists(this.root_path) == false)
                System.IO.Directory.CreateDirectory(this.root_path);
            dirs = new List<string>(System.IO.Directory.EnumerateDirectories(this.root_path));

            this.folders = new List<VesperOutputFolder>();

            foreach (string dir in dirs)
            {
                if (System.IO.Directory.Exists(dir) == true)
                {
                    string fname = dir + "\\.name";

                    if (System.IO.File.Exists(fname) == true)
                    {
                        VesperOutputFolder vf = VesperOutputFolder.GetFromFolder(dir);

                        if (vf != null)
                        {
                            this.folders.Add(vf);
                        }
                    }
                }
            }
        }

        public static void Serialize(List<VesperOutputFolder> list, string filename)
        {
            using (System.IO.FileStream fs = new System.IO.FileStream(filename, System.IO.FileMode.CreateNew))
            {
                XmlSerializer xml = new XmlSerializer(typeof(VesperOutputFolders));
                xml.Serialize(fs, list);
                fs.Close();
            }
        }

        public static List<VesperOutputFolder> Deserialize(string filename)
        {
            List<VesperOutputFolder> list = new List<VesperOutputFolder>();

            try
            {
                using (System.IO.FileStream fs = new System.IO.FileStream(filename, System.IO.FileMode.CreateNew))
                {
                    XmlSerializer xml = new XmlSerializer(typeof(VesperOutputFolders));
                    xml.Deserialize(fs);
                    fs.Close();
                }
            }
            catch (Exception ee2) { }

            return list;
        }
    }

}
