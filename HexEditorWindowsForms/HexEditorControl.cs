using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.ComponentModel;

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

        private bool _inHexMode = true; // Added variable for mode switching
        private int _editNibble = 0;

        public HexEditorControl()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            _font = new Font("Consolas", 9);
            _charWidth = TextRenderer.MeasureText("W", _font).Width;
            this.BackColor = Color.White;
            this.DoubleBuffered = true;
            this.MouseWheel += new MouseEventHandler(HexEditorControl_MouseWheel);
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

            // Draw the cursor highlight based on the current mode
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
                    SetCaretPosition((_caretPos.Y * _bytesPerLine) + _caretPos.X + 1);
                    break;
                case Keys.Left:
                    SetCaretPosition((_caretPos.Y * _bytesPerLine) + _caretPos.X - 1);
                    break;
                case Keys.Up:
                    SetCaretPosition((_caretPos.Y * _bytesPerLine) + _caretPos.X - _bytesPerLine);
                    break;
                case Keys.Down:
                    SetCaretPosition((_caretPos.Y * _bytesPerLine) + _caretPos.X + _bytesPerLine);
                    break;
                case Keys.Home:
                    SetCaretPosition((_caretPos.Y * _bytesPerLine));
                    break;
                case Keys.End:
                    SetCaretPosition((_caretPos.Y * _bytesPerLine) + _bytesPerLine - 1);
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

            if (clickedIndex != -1 && clickedIndex < _fileData.Length)
            {
                SetCaretPosition(clickedIndex);
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