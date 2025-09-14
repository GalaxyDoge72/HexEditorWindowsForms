using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.ComponentModel;
using System.Text;

namespace HexEditorWindowsForms
{
    public partial class HexEditorControl : UserControl
    {
        private byte[] _fileData;
        private int _bytesPerLine = 16;
        private int _lineHeight = 16;
        private Font _font;
        private int _charWidth;
        private Point _caretPos;
        private int _vScrollPos;

        private bool _inHexMode = true; 
        private int _editNibble = 0;

        private int _selectionStart = -1;
        private int _selectionEnd = -1;

        public HexEditorControl()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            _font = new Font("Consolas", 9);
            _charWidth = TextRenderer.MeasureText("W", _font).Width;
            this.BackColor = Color.White;
            this.DoubleBuffered = true;
            this.MouseWheel += new MouseEventHandler(HexEditorControl_MouseWheel);

            ContextMenuStrip contextMenu = new ContextMenuStrip();
            ToolStripMenuItem copyMenuItem = new ToolStripMenuItem("Copy");
            copyMenuItem.Click += new EventHandler(copyMenuItem_Click);
            contextMenu.Items.Add(copyMenuItem);
            this.ContextMenuStrip = contextMenu;
        }


        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public Font DisplayFont
        {
            get { return _font; }
            set
            {
                _font = value;
                _charWidth = TextRenderer.MeasureText("W", _font).Width;
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public int BytesPerRow
        {
            get { return _bytesPerLine; }
            set
            {
                if (value > 0)
                {
                    _bytesPerLine = value;
                    Invalidate();
                }
            }
        }

        public void LoadFile(byte[] data)
        {
            _fileData = data;
            _caretPos = new Point(0, 0);
            _vScrollPos = 0;
            Invalidate();
        }

        public byte[] GetFileData()
        {
            return _fileData;
        }

        private void copyMenuItem_Click(object sender, EventArgs e)
        {
            if (_fileData == null || _selectionEnd == -1 || _selectionStart == -1) return;

            int start = Math.Min(_selectionStart, _selectionEnd);
            int end = Math.Max(_selectionStart, _selectionEnd);

            if (start >= 0 && end < _fileData.Length)
            {
                StringBuilder hexString = new StringBuilder();
                for (int i = start; i <= end; i++)
                {
                    hexString.Append(_fileData[i].ToString("X2"));
                    hexString.Append(" ");
                }

                Clipboard.SetText(hexString.ToString().Trim());
            }
        }

        public void SetCaretPosition(int index)
        {
            if (_fileData == null) return;
            if (index >= 0 && index < _fileData.Length)
            {
                _caretPos.X = index % _bytesPerLine;
                _caretPos.Y = index / _bytesPerLine;

                if (_caretPos.Y < _vScrollPos)
                {
                    _vScrollPos = _caretPos.Y;
                }
                if (_caretPos.Y >= _vScrollPos + this.ClientSize.Height / _lineHeight)
                {
                    _vScrollPos = _caretPos.Y - (this.ClientSize.Height / _lineHeight) + 1;
                }
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_fileData == null) return;
            Graphics g = e.Graphics;
            g.Clear(this.BackColor);

            int startLine = _vScrollPos;
            int linesPerPage = this.ClientSize.Height / _lineHeight;
            int endLine = startLine + linesPerPage + 1;

            if (endLine > _fileData.Length / _bytesPerLine)
            {
                endLine = _fileData.Length / _bytesPerLine + 1;
            }

            int selStart = Math.Min(_selectionStart, _selectionEnd);
            int selEnd = Math.Max(_selectionStart, _selectionEnd);

            if (_selectionStart != -1 && selStart != selEnd)
            {
                for (int line = startLine; line < endLine; line++)
                {
                    int y = (line - startLine) * _lineHeight;
                    int address = line * _bytesPerLine;

                    for (int i = 0; i < _bytesPerLine; i++)
                    {
                        int index = address + i;
                        if (index >= _fileData.Length) break;
                        if (index >= selStart && index <= selEnd)
                        {
                            int hexX = 60 + i * _charWidth * 3;
                            int asciiX = 60 + _bytesPerLine * _charWidth * 3 + i * _charWidth;

                            g.FillRectangle(new SolidBrush(Color.FromArgb(50, Color.CornflowerBlue)), hexX, y, _charWidth * 2, _lineHeight);
                            g.FillRectangle(new SolidBrush(Color.FromArgb(50, Color.CornflowerBlue)), asciiX, y, _charWidth, _lineHeight);
                        }
                    }
                }
            }

            for (int line = startLine; line < endLine; line++)
            {
                int y = (line - startLine) * _lineHeight;
                int address = line * _bytesPerLine;

                g.DrawString(address.ToString("X8"), _font, Brushes.Gray, 0, y);

                for (int i = 0; i < _bytesPerLine; i++)
                {
                    int index = address + i;
                    if (index >= _fileData.Length) break;
                    byte b = _fileData[index];
                    string hex = b.ToString("X2");
                    g.DrawString(hex, _font, Brushes.Black, 60 + i * _charWidth * 3, y);
                    char c = (b >= 32 && b <= 126) ? (char)b : '.';
                    g.DrawString(c.ToString(), _font, Brushes.Black, 60 + _bytesPerLine * _charWidth * 3 + i * _charWidth, y);
                }
            }

            if (this.Focused && _fileData != null && _caretPos.Y >= _vScrollPos)
            {
                int y = (_caretPos.Y - _vScrollPos) * _lineHeight;
                int x;
                int highlightWidth;

                if (_inHexMode)
                {
                    int byteX = 60 + _caretPos.X * _charWidth * 3;
                    x = byteX + _editNibble * _charWidth;
                    highlightWidth = _charWidth;
                }
                else
                {
                    x = 60 + _bytesPerLine * _charWidth * 3 + _caretPos.X * _charWidth;
                    highlightWidth = _charWidth;
                }

                g.FillRectangle(new SolidBrush(Color.FromArgb(100, Color.Yellow)), x, y, highlightWidth, _lineHeight);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (_fileData == null) return;

            if ((e.KeyCode != Keys.ShiftKey) && (e.KeyCode != Keys.Left) && (e.KeyCode != Keys.Right) && (e.KeyCode != Keys.Up) && (e.KeyCode != Keys.Down) && (e.KeyCode != Keys.Home) && (e.KeyCode != Keys.End))
            {
                _selectionStart = -1;
                _selectionEnd = -1;
            }

            int byteIndex = _caretPos.Y * _bytesPerLine + _caretPos.X;

            if (e.KeyCode == Keys.Tab)
            {
                _inHexMode = !_inHexMode;
                _editNibble = 0;
                Invalidate();
                e.Handled = true;
                return;
            }

            if (_inHexMode)
            {
                if (IsHexKey(e.KeyCode))
                {
                    string hexChar;
                    if (e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9)
                    {
                        hexChar = ((char)('0' + (e.KeyCode - Keys.D0))).ToString();
                    }
                    else
                    {
                        hexChar = e.KeyCode.ToString();
                    }

                    if (byteIndex < _fileData.Length)
                    {
                        byte currentByte = _fileData[byteIndex];
                        byte newNibble = ConvertHexCharToByte(hexChar);

                        if (_editNibble == 0)
                        {
                            currentByte = (byte)((currentByte & 0x0F) | (newNibble << 4));
                            _editNibble = 1;
                        }
                        else
                        {
                            currentByte = (byte)((currentByte & 0xF0) | newNibble);
                            _editNibble = 0;
                            _caretPos.X++;
                            if (_caretPos.X >= _bytesPerLine)
                            {
                                _caretPos.X = 0;
                                _caretPos.Y++;
                            }
                        }
                        _fileData[byteIndex] = currentByte;
                        Invalidate();
                    }
                }
            }
            else // ASCII Mode
            {
                if (e.KeyCode >= Keys.Space && e.KeyCode <= Keys.Oemtilde)
                {
                    if (byteIndex < _fileData.Length)
                    {
                        _fileData[byteIndex] = (byte)e.KeyValue;
                        _caretPos.X++;
                        if (_caretPos.X >= _bytesPerLine)
                        {
                            _caretPos.X = 0;
                            _caretPos.Y++;
                        }
                        Invalidate();
                    }
                }
            }

            // Navigation Keys
            switch (e.KeyCode)
            {
                case Keys.Right:
                    int nextIndex = (_caretPos.Y * _bytesPerLine) + _caretPos.X + 1;
                    SetCaretPosition(nextIndex);
                    if (e.Shift) _selectionEnd = nextIndex; else _selectionStart = nextIndex;
                    break;
                case Keys.Left:
                    int prevIndex = (_caretPos.Y * _bytesPerLine) + _caretPos.X - 1;
                    SetCaretPosition(prevIndex);
                    if (e.Shift) _selectionEnd = prevIndex; else _selectionStart = prevIndex;
                    break;
                case Keys.Up:
                    int upIndex = (_caretPos.Y * _bytesPerLine) + _caretPos.X - _bytesPerLine;
                    SetCaretPosition(upIndex);
                    if (e.Shift) _selectionEnd = upIndex; else _selectionStart = upIndex;
                    break;
                case Keys.Down:
                    int downIndex = (_caretPos.Y * _bytesPerLine) + _caretPos.X + _bytesPerLine;
                    SetCaretPosition(downIndex);
                    if (e.Shift) _selectionEnd = downIndex; else _selectionStart = downIndex;
                    break;
                case Keys.Home:
                    int homeIndex = (_caretPos.Y * _bytesPerLine);
                    SetCaretPosition(homeIndex);
                    if (e.Shift) _selectionEnd = homeIndex; else _selectionStart = homeIndex;
                    break;
                case Keys.End:
                    int endIndex = (_caretPos.Y * _bytesPerLine) + _bytesPerLine - 1;
                    SetCaretPosition(endIndex);
                    if (e.Shift) _selectionEnd = endIndex; else _selectionStart = endIndex;
                    break;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (_fileData == null) return;
            this.Focus();

            int line = _vScrollPos + (e.Y / _lineHeight);
            int hexStart = 60;
            int asciiStart = hexStart + _bytesPerLine * _charWidth * 3;
            int clickedIndex = -1;

            if (e.X >= hexStart && e.X < asciiStart)
            {
                int charIndex = (e.X - hexStart) / _charWidth;
                int byteIndex = charIndex / 3;
                clickedIndex = (line * _bytesPerLine) + byteIndex;
                _inHexMode = true;
                _editNibble = (charIndex % 3 == 2) ? 1 : 0;
            }
            else if (e.X >= asciiStart)
            {
                int charIndex = (e.X - asciiStart) / _charWidth;
                int byteIndex = charIndex;
                clickedIndex = (line * _bytesPerLine) + byteIndex;
                _inHexMode = false;
            }

            if (e.Button == MouseButtons.Right)
            {
                int selStart = Math.Min(_selectionStart, _selectionEnd);
                int selend = Math.Max(_selectionStart, _selectionEnd);
                if (clickedIndex >= selStart && clickedIndex <= selend)
                {
                    return;
                }
                else
                {
                    _selectionStart = -1;
                    _selectionEnd = -1;
                    Invalidate();
                }
            }

            else if (e.Button == MouseButtons.Left)
            {
                if (clickedIndex != -1 && clickedIndex < _fileData.Length)
                {
                    _selectionStart = clickedIndex;
                    _selectionEnd = clickedIndex;
                    SetCaretPosition(clickedIndex);
                }
            }

            if (clickedIndex != -1 && clickedIndex < _fileData.Length)
            {
                // Start a new selection
                _selectionStart = clickedIndex;
                _selectionEnd = clickedIndex;
                SetCaretPosition(clickedIndex);
            }
            else
            {
                // Clear selection if click is outside data
                _selectionStart = -1;
                _selectionEnd = -1;
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_fileData == null) return;

            if (e.Button == MouseButtons.Left && _selectionStart != -1)
            {
                int line = _vScrollPos + (e.Y / _lineHeight);
                int hexStart = 60;
                int asciiStart = hexStart + _bytesPerLine * _charWidth * 3;
                int newIndex = -1;

                if (e.X >= hexStart && e.X < asciiStart)
                {
                    int charIndex = (e.X - hexStart) / _charWidth;
                    int byteIndex = charIndex / 3;
                    newIndex = (line * _bytesPerLine) + byteIndex;
                }
                else if (e.X >= asciiStart)
                {
                    int charIndex = (e.X - asciiStart) / _charWidth;
                    int byteIndex = charIndex;
                    newIndex = (line * _bytesPerLine) + byteIndex;
                }

                if (newIndex != -1 && newIndex < _fileData.Length)
                {
                    _selectionEnd = newIndex;
                    Invalidate();
                }
            }
        }
        private void HexEditorControl_MouseWheel(object sender, MouseEventArgs e)
        {
            if (e.Delta > 0)
            {
                _vScrollPos--;
            }
            else
            {
                _vScrollPos++;
            }
            if (_vScrollPos < 0) _vScrollPos = 0;

            int totalLines = _fileData.Length / _bytesPerLine;
            if (_vScrollPos > totalLines) _vScrollPos = totalLines;

            Invalidate();
        }

        private bool IsHexKey(Keys key)
        {
            return (key >= Keys.D0 && key <= Keys.D9) || (key >= Keys.A && key <= Keys.F);
        }

        private byte ConvertHexCharToByte(string hexChar)
        {
            try
            {
                return (byte)int.Parse(hexChar, System.Globalization.NumberStyles.HexNumber);
            }
            catch (FormatException)
            {
                return 0;
            }
        }
    }
}