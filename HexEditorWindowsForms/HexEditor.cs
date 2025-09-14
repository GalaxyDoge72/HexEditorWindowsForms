using System;
using System.IO;
using System.Windows.Forms;

namespace HexEditorWindowsForms
{
    public partial class HexEditor : Form
    {
        private HexEditorControl hexEditorControl;

        public HexEditor()
        {
            InitializeComponent();
            hexEditorControl = new HexEditorControl();
            hexEditorControl.Dock = DockStyle.Fill;
            this.Controls.Add(hexEditorControl);
            hexEditorControl.BringToFront();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        byte[] fileData = File.ReadAllBytes(openFileDialog.FileName);
                        hexEditorControl.LoadFile(fileData);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error opening file: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        byte[] fileData = hexEditorControl.GetFileData();
                        File.WriteAllBytes(saveFileDialog.FileName, fileData);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error saving file: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }
}