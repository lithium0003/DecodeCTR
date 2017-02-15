using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DecodeCTR
{
    public partial class Form1 : Form
    {
        CancellationTokenSource cts = new CancellationTokenSource();

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            CryptCTR.password = "";
        }

        private void Log(string str)
        {
            textBox_log.Text += (str + "\r\n");
        }

        private void Log(string format, params object[] args)
        {
            Log(string.Format(format, args));
        }

        private async Task DecodeFile(IEnumerable<string> encfiles)
        {
            foreach(var file in encfiles)
            {
                if (!File.Exists(file))
                {
                    Log("file not found : {0}", file);
                    continue;
                }
                var decfilepath = Path.GetDirectoryName(file);

                var encfilename = Path.GetFileName(file);
                var enckey = Path.GetFileNameWithoutExtension(encfilename);
                if (!Regex.IsMatch(encfilename, ".*?\\.[a-z0-9]{8}\\.enc$"))
                {
                    Log("filename decode error : {0}", file);
                    continue;
                }
                var decfilename = Path.GetFileNameWithoutExtension(enckey);

                var decfile = Path.Combine(decfilepath, decfilename);
                if (File.Exists(decfile))
                {
                    Log("Exists : {0}", decfile);
                    continue;
                }

                using (var efile = File.OpenRead(file))
                using (var dfile = File.OpenWrite(decfile))
                using (var cfile = new CryptCTR.AES256CTR_CryptStream(efile, enckey))
                {
                    try
                    {
                        await cfile.CopyToAsync(dfile, 81920, cts.Token);
                    }
                    catch(Exception ex)
                    {
                        Log("Decode Error : {0}->{1} {2}", file, decfile, ex.Message);
                        continue;
                    }
                }
                Log("OK : {0}->{1}", file, decfile);
            }
        }

        private async Task DecodeFolder(IEnumerable<string> encfolder)
        {
            foreach (var folder in encfolder)
            {
                if (!Directory.Exists(folder))
                {
                    Log("folder not found : {0}", folder);
                    continue;
                }
                // subitems
                var subfiles = Directory.GetFiles(folder);
                var subdirs = Directory.GetDirectories(folder);

                await DecodeFile(subfiles);
                await DecodeFolder(subdirs);
            }
        }

        private async Task EncodeFile(IEnumerable<string> plainfiles)
        {
            foreach (var file in plainfiles)
            {
                if (!File.Exists(file))
                {
                    Log("file not found : {0}", file);
                    continue;
                }
                var encfilepath = Path.GetDirectoryName(file);

                var plainfilename = Path.GetFileName(file);
                var enckey = plainfilename + "." + Path.GetFileNameWithoutExtension(Path.GetRandomFileName());
                var encfilename = enckey + ".enc";

                var encfile = Path.Combine(encfilepath, encfilename);
                if (File.Exists(encfile))
                {
                    Log("Exists : {0}", encfile);
                    continue;
                }

                using (var pfile = File.OpenRead(file))
                using (var efile = File.OpenWrite(encfile))
                using (var cfile = new CryptCTR.AES256CTR_CryptStream(pfile, enckey))
                {
                    try
                    {
                        await cfile.CopyToAsync(efile, 81920, cts.Token);
                    }
                    catch (Exception ex)
                    {
                        Log("Encode Error : {0}->{1} {2}", file, encfile, ex.Message);
                        continue;
                    }
                }
                Log("OK : {0}->{1}", file, encfile);
            }
        }

        private async Task EncodeFolder(IEnumerable<string> plainfolder)
        {
            foreach (var folder in plainfolder)
            {
                if (!Directory.Exists(folder))
                {
                    Log("folder not found : {0}", folder);
                    continue;
                }
                // subitems
                var subfiles = Directory.GetFiles(folder);
                var subdirs = Directory.GetDirectories(folder);

                await EncodeFile(subfiles);
                await EncodeFolder(subdirs);
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            textBox1.PasswordChar = (checkBox1.Checked) ? '*' : '\0';
        }

        private void textBox1_Leave(object sender, EventArgs e)
        {
            CryptCTR.password = textBox1.Text;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            cts.Cancel();
        }

        private async void button_DecFile_Click(object sender, EventArgs e)
        {
            openFileDialog1.Title = "select to decrypt";
            if (openFileDialog1.ShowDialog() != DialogResult.OK) return;

            await DecodeFile(openFileDialog1.FileNames);
            Log("Done.");
        }

        private async void button_DecFolder_Click(object sender, EventArgs e)
        {
            folderBrowserDialog1.Description = "select to decrypt";
            if (folderBrowserDialog1.ShowDialog() != DialogResult.OK) return;

            await DecodeFolder(new string[] { folderBrowserDialog1.SelectedPath });
            Log("Done.");
        }

        private async void button_EncFile_Click(object sender, EventArgs e)
        {
            openFileDialog1.Title = "select to encrypt";
            if (openFileDialog1.ShowDialog() != DialogResult.OK) return;

            await EncodeFile(openFileDialog1.FileNames);
            Log("Done.");
        }

        private async void button_EncFolder_Click(object sender, EventArgs e)
        {
            folderBrowserDialog1.Description = "select to encrypt";
            if (folderBrowserDialog1.ShowDialog() != DialogResult.OK) return;

            await EncodeFolder(new string[] { folderBrowserDialog1.SelectedPath });
            Log("Done.");
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private async void Form1_DragDrop(object sender, DragEventArgs e)
        {
            string[] fileNames = (string[])e.Data.GetData(DataFormats.FileDrop, false);

            var decodes = fileNames.Where(item => Regex.IsMatch(Path.GetFileName(item), ".*?\\.[a-z0-9]{8}\\.enc$"));
            var encodes = fileNames.Except(decodes);

            await DecodeFile(decodes.Where(item => File.Exists(item)));
            await DecodeFolder(decodes.Where(item => Directory.Exists(item)));
            await EncodeFile(encodes.Where(item => File.Exists(item)));
            await EncodeFolder(encodes.Where(item => Directory.Exists(item)));
            Log("Done.");
        }
    }
}
