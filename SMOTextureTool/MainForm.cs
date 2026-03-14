using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SMOTextureTool
{
    public partial class MainForm : Form
    {
        private string _currentFilePath = "";
        private byte[] _currentFileData = null;
        private List<TextureInfo> _textures = new();
        private readonly Dictionary<int, string> _replacementFiles = new();

        private TableLayoutPanel rootLayout;
        private TableLayoutPanel topLayout;
        private Label lblTitle;
        private Button btnOpenFile;
        private TextBox txtFilePath;
        private Button btnExtractAll;
        private Button btnSelectFolder;
        private Button btnSaveSmo;
        private Button btnClearAll;
        private Label lblStatus;
        private FlowLayoutPanel panelTextures;

        public MainForm()
        {
            InitializeComponent();
            BuildUi();
        }

        private void BuildUi()
        {
            SuspendLayout();

            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            Text = "SMO Texture Tool";
            Width = 1500;
            Height = 980;
            MinimumSize = new Size(1120, 760);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(245, 247, 250);

            rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(12),
                BackColor = BackColor
            };
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Controls.Add(rootLayout);

            BuildTopPanel();
            rootLayout.Controls.Add(topLayout, 0, 0);

            panelTextures = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0, 10, 0, 0),
                BackColor = Color.FromArgb(245, 247, 250)
            };
            rootLayout.Controls.Add(panelTextures, 0, 1);

            Resize += (s, e) => UpdateRowWidths();

            ResumeLayout();
        }

        private void BuildTopPanel()
        {
            topLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 4,
                RowCount = 4,
                BackColor = Color.White,
                Padding = new Padding(14),
                Margin = new Padding(0, 0, 0, 10)
            };

            topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F));
            topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0F));
            topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0F));

            topLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            topLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            topLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            topLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            lblTitle = new Label
            {
                Text = "SMO Texture Tool",
                AutoSize = true,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(35, 35, 40),
                Margin = new Padding(0, 0, 0, 10)
            };
            topLayout.Controls.Add(lblTitle, 0, 0);
            topLayout.SetColumnSpan(lblTitle, 4);

            btnOpenFile = new Button
            {
                Text = "Выбрать",
                Dock = DockStyle.Fill,
                Height = 38,
                Margin = new Padding(0, 0, 10, 10)
            };
            btnOpenFile.Click += BtnOpenFile_Click;
            topLayout.Controls.Add(btnOpenFile, 0, 1);

            txtFilePath = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 0, 10)
            };
            topLayout.Controls.Add(txtFilePath, 1, 1);
            topLayout.SetColumnSpan(txtFilePath, 3);

            TableLayoutPanel buttonsRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 10)
            };
            buttonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            buttonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            buttonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            buttonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            btnExtractAll = new Button
            {
                Text = "Извлечь все текстуры",
                Dock = DockStyle.Fill,
                Height = 64,
                Margin = new Padding(0, 0, 8, 0)
            };
            btnExtractAll.Click += BtnExtractAll_Click;
            buttonsRow.Controls.Add(btnExtractAll, 0, 0);

            btnSelectFolder = new Button
            {
                Text = "Папка с заменами",
                Dock = DockStyle.Fill,
                Height = 64,
                Margin = new Padding(0, 0, 8, 0)
            };
            btnSelectFolder.Click += BtnSelectFolder_Click;
            buttonsRow.Controls.Add(btnSelectFolder, 1, 0);

            btnSaveSmo = new Button
            {
                Text = "Сохранить новый SMO",
                Dock = DockStyle.Fill,
                Height = 64,
                Margin = new Padding(0, 0, 8, 0)
            };
            btnSaveSmo.Click += BtnSaveSmo_Click;
            buttonsRow.Controls.Add(btnSaveSmo, 2, 0);

            btnClearAll = new Button
            {
                Text = "Сбросить всё",
                Dock = DockStyle.Fill,
                Height = 64,
                Margin = new Padding(0)
            };
            btnClearAll.Click += BtnClearAll_Click;
            buttonsRow.Controls.Add(btnClearAll, 3, 0);

            topLayout.Controls.Add(buttonsRow, 0, 2);
            topLayout.SetColumnSpan(buttonsRow, 4);

            lblStatus = new Label
            {
                Text = "Файл не открыт",
                Dock = DockStyle.Fill,
                AutoSize = true,
                ForeColor = Color.FromArgb(90, 95, 105),
                TextAlign = ContentAlignment.MiddleLeft
            };
            topLayout.Controls.Add(lblStatus, 0, 3);
            topLayout.SetColumnSpan(lblStatus, 4);
        }

        private void BtnOpenFile_Click(object sender, EventArgs e)
        {
            using OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "SMO files (*.smo)|*.smo|All files (*.*)|*.*"
            };

            if (dlg.ShowDialog() != DialogResult.OK)
                return;

            _currentFilePath = dlg.FileName;
            txtFilePath.Text = _currentFilePath;
            _currentFileData = File.ReadAllBytes(_currentFilePath);
            _textures = SmoParser.FindTextures(_currentFileData);
            _replacementFiles.Clear();

            RefreshTextureList();
        }

        private void BtnExtractAll_Click(object sender, EventArgs e)
        {
            if (_currentFileData == null)
            {
                MessageBox.Show("Сначала открой файл.", "SMO Texture Tool",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using FolderBrowserDialog dlg = new FolderBrowserDialog();

            if (dlg.ShowDialog() != DialogResult.OK)
                return;

            SmoParser.ExtractAllTextures(_currentFilePath, dlg.SelectedPath);

            MessageBox.Show("Все текстуры извлечены.", "SMO Texture Tool",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnSelectFolder_Click(object sender, EventArgs e)
        {
            if (_currentFileData == null)
            {
                MessageBox.Show("Сначала открой файл.", "SMO Texture Tool",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using FolderBrowserDialog dlg = new FolderBrowserDialog();

            if (dlg.ShowDialog() != DialogResult.OK)
                return;

            int found = 0;

            foreach (var tex in _textures)
            {
                string path = Path.Combine(dlg.SelectedPath, tex.FileName);
                if (File.Exists(path))
                {
                    _replacementFiles[tex.Index1Based] = path;
                    found++;
                }
            }

            RefreshTextureList();

            MessageBox.Show($"Найдено замен: {found} из {_textures.Count}", "SMO Texture Tool",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnSaveSmo_Click(object sender, EventArgs e)
        {
            if (_currentFileData == null)
            {
                MessageBox.Show("Сначала открой файл.", "SMO Texture Tool",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using SaveFileDialog dlg = new SaveFileDialog
            {
                Filter = "SMO files (*.smo)|*.smo",
                FileName = Path.GetFileNameWithoutExtension(_currentFilePath) + "_mod.smo"
            };

            if (dlg.ShowDialog() != DialogResult.OK)
                return;

            byte[] newData = _currentFileData;
            var textures = SmoParser.FindTextures(newData);

            for (int i = 0; i < textures.Count; i++)
            {
                var tex = textures[i];

                if (!_replacementFiles.TryGetValue(tex.Index1Based, out string replacementPath))
                    continue;

                using Bitmap bmp = new Bitmap(replacementPath);
                newData = SmoParser.ReplaceTexture(newData, tex, bmp);
                textures = SmoParser.FindTextures(newData);
            }

            File.WriteAllBytes(dlg.FileName, newData);

            MessageBox.Show("Новый SMO сохранён.", "SMO Texture Tool",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnClearAll_Click(object sender, EventArgs e)
        {
            _replacementFiles.Clear();
            RefreshTextureList();
        }

        private void RefreshTextureList()
        {
            panelTextures.SuspendLayout();
            panelTextures.Controls.Clear();

            if (_currentFileData == null)
            {
                lblStatus.Text = "Файл не открыт";
                panelTextures.ResumeLayout();
                return;
            }

            int replacementsCount = _replacementFiles.Count;
            string fileName = Path.GetFileName(_currentFilePath);

            lblStatus.Text =
                $"Файл: {fileName}   |   Текстур: {_textures.Count}   |   Замен выбрано: {replacementsCount}";

            foreach (var tex in _textures)
            {
                panelTextures.Controls.Add(CreateTextureRow(tex));
            }

            panelTextures.ResumeLayout();
            UpdateRowWidths();
        }

        private void UpdateRowWidths()
        {
            int rowWidth = Math.Max(1100, panelTextures.ClientSize.Width - 26);

            foreach (Control control in panelTextures.Controls)
            {
                if (control is Panel row)
                {
                    row.Width = rowWidth;

                    foreach (Control child in row.Controls)
                    {
                        if (child is TableLayoutPanel body)
                            body.Width = row.Width - 24;

                        if (child is Label header)
                            header.Width = row.Width - 32;
                    }
                }
            }
        }

        private Control CreateTextureRow(TextureInfo tex)
        {
            bool hasReplacement = _replacementFiles.TryGetValue(tex.Index1Based, out string replacementPath) &&
                                  File.Exists(replacementPath);

            Panel row = new Panel
            {
                Width = Math.Max(1100, panelTextures.ClientSize.Width - 26),
                Height = 320,
                Margin = new Padding(0, 0, 0, 20),
                BackColor = hasReplacement ? Color.FromArgb(248, 252, 248) : Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblHeader = new Label
            {
                Left = 16,
                Top = 14,
                Width = row.Width - 32,
                Height = 30,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = $"{tex.FileName}   |   {tex.Width}x{tex.Height}   |   {tex.Variant}   |   fmt 0x{tex.FormatCode:X4}",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(45, 50, 60)
            };
            row.Controls.Add(lblHeader);

            TableLayoutPanel body = new TableLayoutPanel
            {
                Left = 12,
                Top = 60,
                Width = row.Width - 24,
                Height = 240,
                ColumnCount = 4,
                RowCount = 1
            };

            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260F));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260F));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            using Bitmap originalBmp = SmoParser.ExtractTextureBitmap(_currentFileData, tex);
            Panel originalBlock = BuildImageBlock("Оригинал", new Bitmap(originalBmp));
            body.Controls.Add(originalBlock, 0, 0);

            Label arrow = new Label
            {
                Dock = DockStyle.Fill,
                Text = "➜",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Symbol", 48F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(120, 125, 135)
            };
            body.Controls.Add(arrow, 1, 0);

            Bitmap replacementBitmap = null;
            if (hasReplacement)
            {
                using Bitmap bmp = new Bitmap(replacementPath);
                replacementBitmap = new Bitmap(bmp);
            }

            Panel replacementBlock = BuildImageBlock("Замена", replacementBitmap);
            body.Controls.Add(replacementBlock, 2, 0);

            Panel actionsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8, 4, 8, 4)
            };

            Button btnSave = new Button
            {
                Text = "Сохранить оригинал",
                Width = 240,
                Height = 64,
                Left = 10,
                Top = 10
            };
            btnSave.Click += (s, e) => SaveSingleTexture(tex);
            actionsPanel.Controls.Add(btnSave);

            Button btnChoose = new Button
            {
                Text = "Выбрать замену",
                Width = 240,
                Height = 64,
                Left = 10,
                Top = 90
            };
            btnChoose.Click += (s, e) => ChooseSingleReplacement(tex);
            actionsPanel.Controls.Add(btnChoose);

            Button btnReset = new Button
            {
                Text = "Сбросить",
                Width = 240,
                Height = 64,
                Left = 10,
                Top = 170
            };
            btnReset.Click += (s, e) =>
            {
                _replacementFiles.Remove(tex.Index1Based);
                RefreshTextureList();
            };
            actionsPanel.Controls.Add(btnReset);

            body.Controls.Add(actionsPanel, 3, 0);

            row.Controls.Add(body);

            return row;
        }

        private Panel BuildImageBlock(string title, Bitmap image)
        {
            Panel block = new Panel
            {
                Dock = DockStyle.Fill
            };

            Label lbl = new Label
            {
                Dock = DockStyle.Top,
                Height = 36,
                Text = title,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(80, 85, 95)
            };
            block.Controls.Add(lbl);

            Panel imageHost = new Panel
            {
                Dock = DockStyle.Fill
            };
            block.Controls.Add(imageHost);

            PictureBox pic = new PictureBox
            {
                Width = 160,
                Height = 160,
                Top = 0,
                Anchor = AnchorStyles.None,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.None,
                BackColor = Color.Transparent,
                Image = image
            };

            imageHost.Resize += (s, e) =>
            {
                pic.Left = (imageHost.Width - pic.Width) / 2;
                pic.Top = (imageHost.Height - pic.Height) / 2;
            };

            imageHost.Controls.Add(pic);

            return block;
        }

        private void SaveSingleTexture(TextureInfo tex)
        {
            using SaveFileDialog dlg = new SaveFileDialog
            {
                FileName = tex.FileName,
                Filter = "PNG file (*.png)|*.png"
            };

            if (dlg.ShowDialog() != DialogResult.OK)
                return;

            using Bitmap bmp = SmoParser.ExtractTextureBitmap(_currentFileData, tex);
            bmp.Save(dlg.FileName);
        }

        private void ChooseSingleReplacement(TextureInfo tex)
        {
            using OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.bmp)|*.png;*.bmp|All files (*.*)|*.*"
            };

            if (dlg.ShowDialog() != DialogResult.OK)
                return;

            _replacementFiles[tex.Index1Based] = dlg.FileName;
            RefreshTextureList();
        }
    }
}
