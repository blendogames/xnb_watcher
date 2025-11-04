using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Runtime.InteropServices;

using XNAConverter;

using Newtonsoft.Json;

//TODO: allow DRAG files into window.

namespace xnb_watcher
{
    

    public partial class Form1 : Form
    {
        [DllImport("user32.dll")]
        public static extern int FlashWindow(IntPtr Hwnd, bool Revert);  

        const string PROFILE_FILE = "profiles.json";

        List<ProjectProfile> profiles;
        int lastSelectedIndex = -1;

        List<FileSystemWatcher> watchers;

        DateTime lastReadTime;

        public Form1()
        {
            InitializeComponent();

            watchers = new List<FileSystemWatcher>();

            this.FormClosed += MyClosedHandler;

            if (!LoadProfiles(out profiles))
            {
                listBox1.BackColor = Color.Pink;
                SetButtonsEnabled(false);
                AddLog("No profiles found. Please go to: File > Add new profile");
                return;
            }

            for (int i = 0; i < profiles.Count; i++)
            {
                comboBox1.Items.Add(profiles[i].profilename);
            }

            comboBox1.SelectedIndexChanged += new EventHandler(ComboBox1_SelectedIndexChanged);

            //Attempt to load the last-selected profile from previous session.
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.lastSelectedProfile))
            {
                int targetIndex = comboBox1.FindStringExact(Properties.Settings.Default.lastSelectedProfile);

                if (targetIndex >= 0)
                    comboBox1.SelectedIndex = targetIndex;
                else
                    comboBox1.SelectedIndex = 0;
            }
            else
            {
                //Default to first profile.
                comboBox1.SelectedIndex = 0;
            }

            dataGridView1.CellValueChanged += new DataGridViewCellEventHandler(dataGridView1_CellValueChanged);
            dataGridView1.CellEnter += new DataGridViewCellEventHandler(dataGridView1_CellEnter);


            this.AllowDrop = true;
            this.DragEnter += new DragEventHandler(Form1_DragEnter);
            this.DragDrop += new DragEventHandler(Form1_DragDrop);
        }

        void dataGridView1_CellEnter(object sender, DataGridViewCellEventArgs e)
        {
            //Default the dropdown to WATCH.
            DataGridViewComboBoxCell cell = (DataGridViewComboBoxCell)dataGridView1.Rows[e.RowIndex].Cells[0];

            if (cell.Value == null)
                cell.Value = "Watch";
            else if (string.IsNullOrWhiteSpace(cell.Value.ToString()))
                cell.Value = "Watch";
        }

        void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        //Drag file into window.
        void Form1_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var path = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];
            }

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);            

            AddLog("- Dragged in {0} items -", files.Length.ToString());

            for (int i = 0; i < files.Length; i++)
            {
                AddLog(Path.GetFileName(files[i]));
            }
            AddLog(string.Empty);

            for (int i = 0; i < files.Length; i++)
            {
                string extension = Path.GetExtension(files[i]);

                if (string.IsNullOrWhiteSpace(extension))
                {
                    //Probably a folder?
                    if (Directory.Exists(files[i]))
                    {
                        string[] allFiles = Directory.GetFiles(files[i], "*", SearchOption.TopDirectoryOnly);

                        List<string> candidates = new List<string>();
                        for (int k = 0; k < allFiles.Length; k++)
                        {
                            FileInfo newFile = new FileInfo(allFiles[k]);
                            if (IsAcceptableFile(newFile))
                            {
                                candidates.Add(allFiles[k]);
                            }
                        }

                        for (int k = 0; k < candidates.Count; k++)
                        {
                            DoXNBCompile(candidates[k], true, false);
                        }
                    }
                    else
                    {
                        AddLog("Failed to parse: {0}", files[i]);
                    }
                }
                else
                {
                    DoXNBCompile(files[i], false, false);
                }
            }
        }

        void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            //Delete any empty rows.
            //for (int i = dataGridView1.Rows.Count; i >= 0; i--)
            //{
            //    if (i >= dataGridView1.Rows.Count - 1)
            //        continue;
            //
            //    if (dataGridView1.Rows[i].Cells[1].Value == null)
            //    {
            //        dataGridView1.Rows.RemoveAt(i);
            //        continue;
            //    }
            //
            //    if (string.IsNullOrWhiteSpace(dataGridView1.Rows[i].Cells[1].Value.ToString()))
            //    {
            //        dataGridView1.Rows.RemoveAt(i);
            //        continue;
            //    }
            //}

            RestartWatchFolders(true);

            dataGridView1.ClearSelection();
        }

        private void RestartWatchFolders(bool saveChanges)
        {
            if (saveChanges)
            {
                //First we save the datagrid changes to the current profile.
                List<ProfileFolder> folderList = new List<ProfileFolder>();

                for (int i = 0; i < dataGridView1.Rows.Count - 1; i++)
                {
                    if (dataGridView1.Rows[i].Cells[1].Value == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(dataGridView1.Rows[i].Cells[1].Value.ToString()))
                    {
                        continue;
                    }

                    ProfileFolder newBuild = new ProfileFolder();
                    newBuild.operation = dataGridView1.Rows[i].Cells[0].Value.ToString();
                    newBuild.path = dataGridView1.Rows[i].Cells[1].Value.ToString();
                    folderList.Add(newBuild);
                }

                profiles[comboBox1.SelectedIndex].folders = folderList.ToArray();
            }


            //Reset the watchers. Do a sanity check.
            int errors = 0;
            watchers.Clear();
            for (int i = 0; i < dataGridView1.Rows.Count - 1; i++)
            {
                if (dataGridView1.Rows[i].Cells[1].Value == null)
                {
                    continue;
                }

                string foldername = dataGridView1.Rows[i].Cells[1].Value.ToString();

                if (!Directory.Exists(foldername))
                {
                    AddLog("Error: folder doesn't exist: '{0}'", foldername);
                    listBox1.BackColor = Color.Pink;
                    errors++;
                }
            }

            if (errors > 0)
                return;

            
            for (int i = 0; i < dataGridView1.Rows.Count - 1; i++)
            {
                if (dataGridView1.Rows[i].Cells[1].Value == null)
                {
                    continue;
                }

                if (dataGridView1.Rows[i].Cells[0].Value.ToString() == "Ignore") //don't watch the Ignore folders.
                {
                    AddLog("Ignoring: {0}", dataGridView1.Rows[i].Cells[1].Value.ToString());
                    continue;
                }

                string foldername = dataGridView1.Rows[i].Cells[1].Value.ToString();
                StartWatching(foldername);
            }

            AddLog(string.Empty);
        }

        //This gets called when a profile is selected.
        private void ComboBox1_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            if (lastSelectedIndex == comboBox1.SelectedIndex)
                return; //If select same profile, then ignore.

            watchers.Clear();
            listBox1.BackColor = Color.White;
            AddLog("------- Profile: {0} -------", profiles[comboBox1.SelectedIndex].profilename);

            lastSelectedIndex = comboBox1.SelectedIndex;

            dataGridView1.Rows.Clear(); //clear out datagrid.
            dataGridView1.Refresh();

            if (profiles[comboBox1.SelectedIndex].folders == null)
                return;

            if (profiles[comboBox1.SelectedIndex].folders.Length <= 0)
                return;

            //Populate the datagridview.
            for (int i = 0; i < profiles[comboBox1.SelectedIndex].folders.Length; i++)
            {
                DataGridViewRow row = new DataGridViewRow();
                row.CreateCells(dataGridView1);

                DataGridViewComboBoxCell cell = (DataGridViewComboBoxCell)row.Cells[0];
                cell.Value = profiles[comboBox1.SelectedIndex].folders[i].operation; //Set operation dropdown.

                row.Cells[1].Value = profiles[comboBox1.SelectedIndex].folders[i].path; //Set folder.
                dataGridView1.Rows.Add(row);
            }

            //Start watching.
            RestartWatchFolders(false);
        }

        private bool LoadProfiles(out List<ProjectProfile> output)
        {
            output = new List<ProjectProfile>();

            if (!File.Exists(PROFILE_FILE))
            {
                return false;
            }

            string fileContents = GetFileContents(PROFILE_FILE);

            if (string.IsNullOrWhiteSpace(fileContents))
            {
                return false;
            }

            try
            {
                output = JsonConvert.DeserializeObject<List<ProjectProfile>>(fileContents);
            }
            catch (Exception e)
            {
                AddLog("Error: can't parse {0}. Error: {1}", PROFILE_FILE, e.Message);
                return false;
            }

            if (output.Count <= 0)
            {
                return false; //Empty.
            }

            return true;
        }

        private void StartWatching(string folderpath)
        {
            try
            {
                FileSystemWatcher newWatcher = new FileSystemWatcher();
                newWatcher.Path = folderpath;
                newWatcher.IncludeSubdirectories = true;
                newWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
                newWatcher.Filter = "*"; //FileSystemWatcher doesn't support multiple filters, so we'll do the filtering via the DoXNBCompile function.
                newWatcher.Changed += new FileSystemEventHandler(OnChanged);
                newWatcher.Created += new FileSystemEventHandler(OnChanged);
                newWatcher.Deleted += new FileSystemEventHandler(OnChanged);
                newWatcher.Renamed += new RenamedEventHandler(OnRenamed);
                newWatcher.EnableRaisingEvents = true; //Activate it.

                watchers.Add(newWatcher);
                AddLog("Watching: {0}", folderpath);
                listBox1.BackColor = Color.GreenYellow;
            }
            catch (Exception err)
            {
                AddLog("Failed to watch {0}. Error: {1}", folderpath, err.Message);
                listBox1.BackColor = Color.Pink;
            }
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            DoXNBCompile(e.FullPath, true);
        }       

        private void OnRenamed(object source, RenamedEventArgs e)
        {
            DoXNBCompile(e.FullPath, true);
        }

        private bool IsAcceptableFile(FileInfo file)
        {
            //See if this is a " - copy" file. Ignore them.
            if (file.Name.Contains(" - Copy."))
                return false;

            string extension = file.Extension.ToLowerInvariant();

            string[] acceptableExtensions =
            {
                ".fbx",
                ".png",
                ".jpg",
                ".tga",
                ".spritefont",
                ".wmv",
                ".bmp",
                ".dds",                
                ".fx",
            };

            for (int i = 0; i < acceptableExtensions.Length; i++)
            {
                if (extension == acceptableExtensions[i])
                    return true;
            }

            return false;
        }

        private bool IsInIgnoredFolder(FileInfo file)
        {
            string fileFolder = file.DirectoryName.ToLowerInvariant();
            
            //compare to all ignored folders.

            for (int i = 0; i < profiles[lastSelectedIndex].folders.Length; i++)
            {
                if (string.Compare(profiles[lastSelectedIndex].folders[i].operation, "Watch", StringComparison.InvariantCultureIgnoreCase) == 0)
                    continue;

                string ignoredFolder = profiles[lastSelectedIndex].folders[i].path.ToLowerInvariant();

                if (string.Compare(ignoredFolder, fileFolder, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void DoXNBCompile(string fullpath, bool doFBXCheck, bool doIgnoreCheck = true)
        {
            FileInfo file = new FileInfo(fullpath);

            if (!file.Exists || !IsAcceptableFile(file) || (IsInIgnoredFolder(file) && doIgnoreCheck))
            {
                return;
            }

            string outputFolder = file.DirectoryName;

            if (doFBXCheck && file.Extension.ToLowerInvariant() != ".fbx")
            {
                //Do the FBX texture workaround.
                //See if there is an FBX in this folder.                
                string[] fbxFiles = Directory.GetFiles(outputFolder, "*.fbx");
                if (fbxFiles.Length > 0)
                {
                    //Yes. There is one or more FBXs in this folder.

                    //Just recompile all the FBXs. TODO: figure out how to recompile only the affected FBX, and not all of them.
                    for (int i = 0; i < fbxFiles.Length; i++)
                    {
                        DoXNBCompile(fbxFiles[i], false);
                    }

                    return;
                }
            }



            //Hack to fix the C# filewatcher bug. This prevents it from spamming multiple times in one frame.
            DateTime lastWriteTime = File.GetLastWriteTime(file.FullName);
            long delta = lastWriteTime.Ticks - lastReadTime.Ticks;
            if (delta < 5000000) //500ms (10000 ticks = 1 millisecond)
                return;

            lastReadTime = lastWriteTime;



            //Found a file. Process it.            
            List<string> errorArray = new List<string>();
            List<string> outputFiles = new List<string>();
            bool buildStatus = false;

            try
            {
                XNBBuilder packager = new XNBBuilder("Windows", "HiDef", false);
                //packager.BuildAudioAsSongs = audioBox.Checked;
                packager.PackageContent(new string[] { fullpath }, outputFolder, false, outputFolder, out errorArray, out outputFiles, out buildStatus);
            }
            catch (Exception err)
            {
                AddLogInvoke(err.Message);
                buildStatus = false;
            }

            if (!buildStatus)
            {
                //Fail.
                listBox1.BackColor = Color.Pink;
                AddLogInvoke("Failed to export: {0}", file.Name);
                if (errorArray.Count > 0)
                {
                    for (int i = 0; i < errorArray.Count; i++)
                    {
                        AddLogInvoke(errorArray[i]);
                    }
                }
                AddLogInvoke("************* E R R O R *************");

                //Flash the taskbar icon.
                MethodInvoker mi = delegate() { FlashWindow(this.Handle, false); };
                this.Invoke(mi);
            }
            else
            {
                //Success.
                listBox1.BackColor = Color.GreenYellow;
                for (int i = 0; i < outputFiles.Count; i++)
                {
                    string filename = Path.GetFileName(outputFiles[i]);
                    AddLogInvoke(filename);
                }
                AddLogInvoke("- Export done -");
            }

            //Clean up 
            try
            {
                //Do some cleanup.
                string[] xmlFiles = Directory.GetFiles(Environment.CurrentDirectory, "*.xml");
                for (int i = 0; i < xmlFiles.Length; i++)
                {
                    if (File.Exists(xmlFiles[i]))
                    {
                        File.Delete(xmlFiles[i]);
                    }
                }
            }
            catch
            {
            }

            AddLogInvoke(string.Empty);

        }

        private bool FileExistsInSubfolder(string rootpath, string filename)
        {
            if (File.Exists(Path.Combine(rootpath, filename)))
                return true;

            foreach (string subDir in Directory.GetDirectories(rootpath, "*", SearchOption.AllDirectories))
            {
                if (File.Exists(Path.Combine(subDir, filename)))
                    return true;
            }

            return false;
        }

        private void SetButtonsEnabled(bool value)
        {
            dataGridView1.Enabled = value;
            comboBox1.Enabled = value;

            dataGridView1.RowsDefaultCellStyle.BackColor = value ? Color.White : Color.LightGray;
        }

        private string GetFileContents(string filepath)
        {
            string output = "";

            try
            {
                using (FileStream stream = File.Open(filepath, FileMode.Open, FileAccess.Read))
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        //dump file contents into a string.
                        output = reader.ReadToEnd();
                    }
                }
            }
            catch (Exception e)
            {
                AddLog("Error: can't read \n{0}. Error: {1}", filepath, e.Message);
                listBox1.BackColor = Color.Pink;
                return string.Empty;
            }

            return output;
        }

        private void AddLogInvoke(string text, params string[] args)
        {
            MethodInvoker mi = delegate() { AddLog(text, args); };
            this.Invoke(mi);
        }

        private void AddLog(string text, params string[] args)
        {
            listBox1.Items.Add(string.Format(text, args));

            int nItems = (int)(listBox1.Height / listBox1.ItemHeight);
            listBox1.TopIndex = listBox1.Items.Count - nItems;

            this.Update();
            this.Refresh();
        }

        private void addNewProfileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Add new profile.
            watchers.Clear();
            listBox1.BackColor = Color.White;

            string promptValue = Prompt.ShowDialog("What is the name of the new profile?", "New profile");
            promptValue = promptValue.Trim();

            if (string.IsNullOrWhiteSpace(promptValue))
            {
                listBox1.BackColor = Color.Pink;
                AddLog("Error: can't use empty profile name.");
                return;
            }

            //Check if it already exists.
            for (int i = 0; i < comboBox1.Items.Count; i++)
            {
                string comboboxText = (comboBox1.Items[i]).ToString();

                if (string.Compare(comboboxText, promptValue, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    listBox1.BackColor = Color.Pink;
                    AddLog("Error: profile '{0}' already exists.", promptValue);
                    return;
                }
            }

            ProjectProfile newProfile = new ProjectProfile();
            newProfile.profilename = promptValue;            
            profiles.Add(newProfile);

            comboBox1.Items.Add(promptValue);
            comboBox1.SelectedIndex = comboBox1.FindStringExact(promptValue);

            AddLog(string.Empty);
            AddLog("Added new profile: {0}", promptValue);
            listBox1.BackColor = Color.White;
            SetButtonsEnabled(true);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        protected void MyClosedHandler(object sender, EventArgs e)
        {
            SaveSettings();
        }

        private void SaveSettings()
        {
            try
            {
                using (StreamWriter file = File.CreateText(PROFILE_FILE))
                {
                    string output = JsonConvert.SerializeObject(profiles, Formatting.Indented);
                    file.Write(output);
                }

                if (comboBox1.SelectedIndex >= 0)
                {
                    Properties.Settings.Default.lastSelectedProfile = comboBox1.Items[comboBox1.SelectedIndex].ToString();
                    Properties.Settings.Default.Save();
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(string.Format("Error: failed to save {0}\n\n{1}", PROFILE_FILE, err.Message), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Blendo XNB Watcher\nby Brendon Chung\nXNB code from https://github.com/dnasdw/xnbbuilder\nJune 2020\n\nWhenever you modify/create a texture or model, Blendo XNB Watcher will automatically convert it to XNB format. It is an automated process and requires no action from you.\n\nTo manually convert assets, drag files/folders into the main window.", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void renameCurrentProfileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listBox1.BackColor = Color.White;
            if (comboBox1.SelectedIndex < 0)
            {
                AddLog("Error: can't rename, no profile selected.");
                return;
            }

            string currentProfilename = (comboBox1.Items[comboBox1.SelectedIndex]).ToString();

            string promptValue = Prompt.ShowDialog(string.Format("What would you like to rename '{0}' to?", currentProfilename), "Rename profile");
            promptValue = promptValue.Trim();

            if (string.IsNullOrWhiteSpace(promptValue))
            {
                listBox1.BackColor = Color.Pink;
                AddLog("Error: can't use empty profile name. Rename cancelled.");
                return;
            }

            //Check if it already exists.
            for (int i = 0; i < comboBox1.Items.Count; i++)
            {
                string comboboxText = (comboBox1.Items[i]).ToString();

                if (string.Compare(comboboxText, promptValue, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    listBox1.BackColor = Color.Pink;
                    AddLog(string.Format("Error: profile '{0}' already exists.", promptValue));
                    return;
                }
            }

            //Ok, all looks good.
            profiles[comboBox1.SelectedIndex].profilename = promptValue;
            comboBox1.Items[comboBox1.SelectedIndex] = promptValue;
            AddLog(string.Format("Profile '{0}' changed to: '{1}'", currentProfilename, promptValue));
        }

        private void deleteCurrentProfileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listBox1.BackColor = Color.White;

            if (comboBox1.SelectedIndex < 0)
            {
                listBox1.BackColor = Color.Pink;
                AddLog("Error: can't delete, no profile selected.");
                return;
            }

            string profilename = (comboBox1.Items[comboBox1.SelectedIndex]).ToString();

            profiles.RemoveAt(comboBox1.SelectedIndex);
            comboBox1.Items.RemoveAt(comboBox1.SelectedIndex);

            if (comboBox1.Items.Count > 0)
                comboBox1.SelectedIndex = 0;

            AddLog(string.Format("Deleted profile: {0}", profilename));

            if (profiles.Count <= 0)
            {
                SetButtonsEnabled(false);

                //Clear out the data grid.
                dataGridView1.Rows.Clear();
            }
        }

        private void copyToClipboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string output = string.Empty;

            foreach (object item in listBox1.Items)
                output += item.ToString() + "\r\n";

            if (string.IsNullOrWhiteSpace(output))
            {
                AddLog(string.Empty);
                AddLog("No log found.");
                return;
            }

            Clipboard.SetText(output);

            AddLog(string.Empty);
            AddLog("Copied entire log to clipboard.");
        }

        private void clearToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
        }

    }
}
