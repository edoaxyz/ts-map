using System;
using System.Windows.Forms;

namespace TsMap.Canvas
{
    public partial class SetupForm : Form
    {
        public SetupForm()
        {
            InitializeComponent();
            folderBrowserDialog1.Description = "Please select the game directory\nE.g. D:/Games/steamapps/common/Euro Truck Simulator 2/";
            folderBrowserDialog1.ShowNewFolderButton = false;
            // Being bored of putting always my game directory for testing
            label1.Text = "Auto selected path (C:/Program Files (x86)/Steam/SteamApps/common/Euro Truck Simulator 2)";
            folderBrowserDialog1.SelectedPath = "C:/Program Files (x86)/Steam/SteamApps/common/Euro Truck Simulator 2";
            NextBtn.Enabled = true;
        }

        private void BrowseBtn_Click(object sender, EventArgs e)
        {
            var result = folderBrowserDialog1.ShowDialog();
            if (result == DialogResult.OK)
            {
                label1.Text = folderBrowserDialog1.SelectedPath;
                NextBtn.Enabled = true;
            }
        }

        private void NextBtn_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            new TsMapCanvas(this, folderBrowserDialog1.SelectedPath).Show();
            Hide();
        }
    }
}
