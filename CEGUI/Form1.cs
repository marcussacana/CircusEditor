using CircusEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CEGUI {
    public partial class Form1 : Form {
        public Form1() {
            InitializeComponent();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e) {
            openFileDialog1.ShowDialog();
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e) {
            saveFileDialog1.ShowDialog();
        }

        MesEditor Editor;
        private void openFileDialog1_FileOk(object sender, CancelEventArgs e) {
            Editor = new MesEditor(File.ReadAllBytes(openFileDialog1.FileName));
            string[] Strs = Editor.Import();
            listBox1.Items.Clear();
            foreach (string Str in Strs)
                listBox1.Items.Add(Str);
        }

        private void saveFileDialog1_FileOk(object sender, CancelEventArgs e) {
            List<string> Strings = new List<string>();
            foreach (object obj in listBox1.Items)
                Strings.Add(obj.ToString());
            byte[] Script = Editor.Export(Strings.ToArray());
            File.WriteAllBytes(saveFileDialog1.FileName, Script);
            MessageBox.Show("File saved.", "CEGUI", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e) {
            try {
                textBox1.Text = listBox1.Items[listBox1.SelectedIndex].ToString();
                Text = "id: " + listBox1.SelectedIndex;
            } catch { }
        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e) {
            if (e.KeyChar == '\n' || e.KeyChar == '\r') {
                try {
                    listBox1.Items[listBox1.SelectedIndex] = textBox1.Text;
                } catch {

                }
            }
        }
    }
}
