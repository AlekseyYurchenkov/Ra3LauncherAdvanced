using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Label = System.Windows.Forms.Label;

namespace Ra3LauncherAdvanced
{
    public partial class MainForm : Form
    {
        Manager manager = new Manager();
        Localizator localizator;
        HowToMultiplayerForm howToMultiplayerForm;
        public MainForm()
        {
            InitializeComponent();
            SetupFaqTable();
            List<Button> localizableButtons = new List<Button>
            {
                PlayButton,
                InstallGBPatchButton,
                MapsFolderButton,
                ModsFolderButton,
                modsUpdateButton,
            };
            List<System.Windows.Forms.Label> localizableLabels = new List<System.Windows.Forms.Label> 
            {
                FAQAboutLauncherLabel,
            };
            localizator = new Localizator(localizableButtons, localizableLabels);
        }

        private void SetupFaqTable()
        {
            faqTable.RowStyles.Clear();
            for (int i = 0; i < faqTable.RowCount; i++)
            {
                var control = faqTable.GetControlFromPosition(0, i);
                if (control != null)
                {
                    control.Visible = i % 2 == 0;
                    control.AutoSize = true;
                    if (i % 2 == 0)
                    {
                        control.MinimumSize = new Size(0, 32);
                        control.MaximumSize = new Size(999, 32);
                        control.Dock = DockStyle.Fill;
                        control.Click += new EventHandler(this.DropDownFaqPanel);
                    }

                }
                faqTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            faqTable.ColumnStyles.Clear();
            for (int i = 0; i < faqTable.ColumnCount; i++)
            {
                faqTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            }

        }
        private void AskToSetPath()
        {
            DialogResult dialogResult = MessageBox.Show(localizator.GetLocalizatedText("NoRa3PathText"),
                localizator.GetLocalizatedText("NoRa3PathName"),
                MessageBoxButtons.YesNo);
            if (dialogResult == DialogResult.Yes)
            {
                folderBrowserMainFolder.ShowDialog();
                if (folderBrowserMainFolder.SelectedPath != "")
                {
                    manager.SetGameMainFolderPath(folderBrowserMainFolder.SelectedPath);
                    MessageBox.Show(localizator.GetLocalizatedText("Ra3PathSelectedText"),
                    localizator.GetLocalizatedText("Ra3PathSelectedName"),
                    MessageBoxButtons.OK);
                }
            }
        }

        private void DropDownFaqPanel(object sender, EventArgs e)
        {
            int control_index = faqTable.GetPositionFromControl((Button)sender).Row;
            var control = faqTable.GetControlFromPosition(0, control_index+1);
            if (control != null)
            {
                control.Visible = !control.Visible;
            }

        }


        private void PlayButton_Click(object sender, EventArgs e)
        {
            if (!manager.LaunchGame())
            {
                AskToSetPath();
            }
        }

        private void modsListView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (!manager.LaunchGame(modsListView.SelectedIndices[0]))
            {
                AskToSetPath();
            }
        }

        private void InstallGBPatch_Click(object sender, EventArgs e)
        {
            if (manager.InstallGBPatch())
            {
                MessageBox.Show("If no errors were shown, then 4GB patch has been successfully installed!",
                "4GB installation result",
                MessageBoxButtons.OK);
            }
            else
            {
                AskToSetPath();
            }
        }

        private void UpdateModsListView()
        {
            modsListView.Items.Clear();
            List<Dictionary<string, string>> mods = manager.UpdateMods();
            foreach (Dictionary<string, string> mod in mods)
            {
                ListViewItem modItem = new ListViewItem(mod["name"]);
                modItem.ToolTipText = "Doubleclick name for start";
                modItem.SubItems.Add(mod["version"]);
                modsListView.Items.Add(modItem);
            }
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (tabControl1.SelectedIndex)
            {
                case 1:
                    UpdateModsListView();
                    break;
            }
        }

        private void modsUpdate_Click(object sender, EventArgs e)
        {
            UpdateModsListView();
        }

        private void MapsFolderButton_Click(object sender, EventArgs e)
        {
            manager.OpenMapsFolder();
        }

        private void ModsFolderButton_Click(object sender, EventArgs e)
        {
            manager.OpenModsFolder();
        }

        private void HowToMultiplayerButton_Click(object sender, EventArgs e)
        {
            if (howToMultiplayerForm == null)
            {
                howToMultiplayerForm = new HowToMultiplayerForm();
                howToMultiplayerForm.Show();
            }
        }

        private void faqTable_Paint(object sender, PaintEventArgs e)
        {

        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }

        private void MainButton_MouseEnter(object sender, EventArgs e)
        {
            var buttonSender = sender as Button;
            buttonSender.BackgroundImage = Properties.Resources.Ra3ButtonHover;
            buttonSender.Refresh();

        }
        private void MainButton_MouseLeave(object sender, EventArgs e)
        {
            var buttonSender = sender as Button;
            buttonSender.BackgroundImage = Properties.Resources.Ra3Button;
            buttonSender.Refresh();

        }

        private void FAQAboutLauncherLabel_Click(object sender, EventArgs e)
        {

        }
    }

    public partial class Manager
    {
        private const string ergcPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Electronic Arts\Electronic Arts\Red Alert 3\ergc";
        private static string gameMainFolderPath = string.Empty;
        private static string modsFolderPath = string.Empty;
        private List<Dictionary<string, string>> mods = new List<Dictionary<string, string>>();
        private bool isErgcKeysFixed = false;
        public Manager()
        {
            gameMainFolderPath = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Electronic Arts\Electronic Arts\Red Alert 3",
            "Install Dir", "");
            modsFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\Red Alert 3\Mods\";
            FixLongLoading();
            FixRegistry();
            if (Properties.Settings.Default.syncLanguage)
            {
                SetLanguageFromGame();
            }
            UpdateMods();
            CheckAndFixSkirmish();
        }

        private void FixLongLoading()
        {
            string hostFilePath = "C:\\Windows\\System32\\drivers\\etc\\hosts";
            string[] hostFileText = File.ReadAllLines(hostFilePath);
            foreach (string line in hostFileText)
            {
                if (line.Trim() == "127.0.0.1 files.ea.com")
                {
                    return;
                }

            }
            string[] hostFileNew = new string[hostFileText.Length + 1];
            hostFileText.CopyTo(hostFileNew, 0);
            hostFileNew[hostFileText.Length] = "127.0.0.1 files.ea.com";
            File.WriteAllLines(hostFilePath, hostFileNew);
        }

        private void FixRegistry()
        {
            Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node", true).CreateSubKey(@"Electronic Arts\Electronic Arts\Red Alert 3\ergc");
            if (!CheckErgcKeys())
            {
                FixErgcKeys();
            }
            SetConstantRegistryValues();
        }
        private void SetConstantRegistryValues()
        {
            string keyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Electronic Arts\Electronic Arts\Red Alert 3";
            Registry.SetValue(keyPath, "DisplayName", "Command & Conquer™ Red Alert™ 3");
            Registry.SetValue(keyPath, "Patch URL", "http://www.ea.com/redalert");
            Registry.SetValue(keyPath, "ProductName", "Command & Conquer™ Red Alert™ 3");
            Registry.SetValue(keyPath, "ProfileFolderName", "Profiles");
            Registry.SetValue(keyPath, "Registration", "Software\\Electronic Arts\\Electronic Arts\\Red Alert 3\\ergc");
            Registry.SetValue(keyPath, "ReplayFolderName", "Replays");
            Registry.SetValue(keyPath, "SaveFolderName", "SaveGames");
            Registry.SetValue(keyPath, "ScreenshotsFolderName", "Screenshots");
            Registry.SetValue(keyPath, "Suppression Exe", "");
            Registry.SetValue(keyPath, "UseLocalUserMap", "0", RegistryValueKind.DWord);
            Registry.SetValue(keyPath, "UseLocalUserMaps", "0", RegistryValueKind.DWord);
            Registry.SetValue(keyPath, "UserDataLeafName", "Red Alert 3");
            Registry.SetValue(keyPath+"\\1.0", "DisplayName", "Red Alert 3");
        }
        private bool CheckErgcKeys()
        {;
            string ergcValue = (string)Registry.GetValue(ergcPath, "(Default)", "");
            if (ergcValue == null)
            {
                return false;
            }
            if (ergcValue == "" || ergcValue.Split(char.Parse("-")).Length != 5 || ergcValue.Length != 24)
            {
                return false;
            }
            ergcValue = (string)Registry.GetValue(ergcPath, "", "");
            if (ergcValue == "" || ergcValue.Split(char.Parse("-")).Length != 5 || ergcValue.Length != 24)
            {
                return false;
            }
            return true;
        }
        private void FixErgcKeys()
        {
            Random random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            string newErgcKey = "";
            for (int i=0; i < 5; i++)
            {
                string part = new string(Enumerable.Repeat(chars, 4)
                .Select(s => s[random.Next(s.Length)]).ToArray());
                newErgcKey += part;
                newErgcKey += i < 4 ? "-" : "";
            }
            Registry.SetValue(ergcPath, "", newErgcKey);
            Registry.SetValue(ergcPath, "(Default)", newErgcKey);
            isErgcKeysFixed = true;
        }
        private void SetLanguageFromGame()
        {
            Properties.Settings.Default.language = (string)Registry.GetValue("HKEY_CURRENT_USER\\SOFTWARE\\Electronic Arts\\Electronic Arts\\Red Alert 3", "Language", "english");
            Properties.Settings.Default.Save();
        }
        private void CheckAndFixSkirmish()
        {
            string profilesFolderPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\Red Alert 3\\Profiles";
            if (!Directory.Exists(profilesFolderPath))
            {
                return;
            }
            foreach (string profileFolder in Directory.GetDirectories(profilesFolderPath))
            {
                if (!File.Exists(profileFolder + "\\Skirmish.ini"))
                {
                    continue;
                }
                string[] data = File.ReadAllLines(profileFolder + "\\Skirmish.ini");
                if (data.Length == 0)
                {
                    continue;
                }
                string[] splittedLine = data[0].Split(char.Parse(";"));
                string playerCode = splittedLine[splittedLine.Length - 2].Split(char.Parse(":"))[0];
                if (playerCode != "S=X")
                {
                    continue;
                }
                string fixedPlayerCode = $"S=H{profileFolder.Replace($"{profilesFolderPath}\\", "")},0,0,FT,-1,7,-1,-1,0,1,-1,:X:X:X:X:X:";
                splittedLine[splittedLine.Length - 2] = fixedPlayerCode;
                string restoredLine = "";
                foreach (string line in splittedLine)
                {
                    if (line == splittedLine[splittedLine.Length-1])
                    {
                        continue;
                    }
                    restoredLine += $"{line};";
                }
                data[0] = restoredLine;
                File.WriteAllLines(profileFolder + "\\Skirmish.ini", data);
            }

        }
        public bool LaunchGame(int modIndex = -1)
        {
            if (IsGameMainFolderPathValid())
            {
                CheckAndFixSkirmish();
                string startParams = modIndex < 0 ? string.Empty : $"-modconfig \"{mods[modIndex]["path"]}\"";
                var proc = Process.Start(gameMainFolderPath + @"\Ra3.exe", startParams);
                return true;
            }
            return false;
        }

        public bool InstallGBPatch()
        {
            if (IsGameMainFolderPathValid())
            {
                var proc = Process.Start(".\\Resources\\FourGbPatchByNtCore.exe", $"\"{gameMainFolderPath}\\Data\\ra3_1.12.game\"");
                return true;
            }
            return false;
        }
        public bool IsGameMainFolderPathValid()
        {
            return File.Exists(gameMainFolderPath + @"\Ra3.exe");
        }
        public string GetGameMainFolderPath()
        {
            return gameMainFolderPath;
        }
        public void SetGameMainFolderPath(string newPath)
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Electronic Arts\Electronic Arts\Red Alert 3",
                "Install Dir", newPath);
            gameMainFolderPath = newPath;
        }
        public List<Dictionary<string, string>> UpdateMods()
        {
            List<Dictionary<string, string>> modsNew = new List<Dictionary<string, string>>();
            //Checking if does Mods folder exist
            if (Directory.Exists(modsFolderPath))
            {
                foreach (string currentFolder in Directory.GetDirectories(modsFolderPath))
                {
                    foreach (string file in Directory.GetFiles(currentFolder, "*.skudef", SearchOption.AllDirectories))
                    {
                        string[] modFullName = file.Replace($"{currentFolder}\\", "").Replace(".skudef", "").Split(char.Parse("_"));
                        modsNew.Add(new Dictionary<string, string> {
                            {"path", file},
                            {"name", modFullName[0]},
                            {"version", modFullName[1]},
                        });
                    }
                }
            }
            mods = modsNew;
            return modsNew;
        }
        public void OpenModsFolder()
        {
            if (!Directory.Exists(modsFolderPath))
            {
                Directory.CreateDirectory(modsFolderPath);
            }
            var proc = Process.Start("explorer.exe", modsFolderPath);
        }
        public void OpenMapsFolder()
        {
            string mapsFolderPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\Red Alert 3\\Maps";
            if (!Directory.Exists(mapsFolderPath))
            {
                Directory.CreateDirectory(mapsFolderPath);
            }
            Process.Start("explorer.exe", mapsFolderPath);
        }

    }

    public partial class Localizator
    {
        private List<Button> Buttons = new List<Button>();
        private List<Label> Labels = new List<Label>();
        private Dictionary<string, string> textData = new Dictionary<string, string>();

        public Localizator(List<Button> localizableButtons, List<Label> localizableLabels)
        {
            Buttons = localizableButtons;
            Labels = localizableLabels;
            textData = GetTextData();
            UpdateLocalizableControls();
        }

        public string GetLocalizatedText(string key)
        {
            key = key.ToLower();
            string langKey = "_"+Properties.Settings.Default.language.ToLower().Replace("_english", "");
            if (textData.ContainsKey(key + langKey))
            {
                return textData[key + langKey];
            }
            else if (textData.ContainsKey(key))
            {
                return textData[key];
            }
            return key;
        }
        public void UpdateLocalizableControls()
        {
            foreach (Button button in Buttons)
            {
                button.Text = GetLocalizatedText(button.Name);
                button.Refresh();
            }
            foreach (Label label in Labels)
            {
                label.Text = GetLocalizatedText(label.Name);
                label.Refresh();
            }
        }
        private Dictionary<string, string> GetTextData()
        {
            Dictionary<string, string> rawTextData = new Dictionary<string, string>();
            string[] localizationFile = File.ReadAllLines("Resources\\localization.ini");
            string langKey = "";
            foreach (string line in localizationFile)
            {
                if (line.Length == 0)
                {
                    continue;
                }
                string trimmedLine = line.Trim();
                if (trimmedLine[0] == char.Parse("#"))
                {
                    continue;
                }
                if (trimmedLine[0] == char.Parse("[") && trimmedLine[trimmedLine.Length - 1] == char.Parse("]"))
                {
                    langKey = "_"+trimmedLine.Substring(1, trimmedLine.Length - 2).ToLower().Replace("_english", "");
                    continue;
                }
                string[] splitedLine = line.Split('=');
                if (splitedLine.Length > 1)
                {
                    string key = splitedLine[0].Trim().ToLower()+langKey;
                    if (!rawTextData.ContainsKey(key)){
                        rawTextData.Add(key, splitedLine[1].Trim().Replace(@"\n", "\n"));
                    } else
                    {
                        Debug.WriteLine($"Key {key} was already added.");
                    }
                }
            }
            return rawTextData;
        }
    }
}
