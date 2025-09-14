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
        private bool _inHexMode = true;
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

            // Loop through each line to be drawn
            for (int line = startLine; line < endLine; line++)
            {
                int y = (line - startLine) * _lineHeight;
                int address = line * _bytesPerLine;

                // Draw the address column
                g.DrawString(address.ToString("X8"), _font, Brushes.Gray, 0, y);

                // Loop through each byte in the current line
                for (int i = 0; i < _bytesPerLine; i++)
                {
                    int index = address + i;
                    if (index >= _fileData.Length) break;
                    byte b = _fileData[index];
                    string hex = b.ToString("X2");

                    // Draw the hex bytes
                    g.DrawString(hex, _font, Brushes.Black, 60 + i * _charWidth * 3, y);

                    // Draw the ASCII representation
                    char c = (b >= 32 && b <= 126) ? (char)b : '.';
                    g.DrawString(c.ToString(), _font, Brushes.Black, 60 + _bytesPerLine * _charWidth * 3 + i * _charWidth, y);
                }
            }

            // Draw the cursor highlight
            if (this.Focused && _fileData != null && _caretPos.Y >= _vScrollPos)
            {
                int y = (_caretPos.Y - _vScrollPos) * _lineHeight;
                int x;

                // Calculate the starting position for the byte
                int byteX = 60 + _caretPos.X * _charWidth * 3;

                // Adjust the position based on the nibble
                x = byteX + _editNibble * _charWidth;

                // Draw a semi-transparent yellow rectangle to highlight the single nibble
                g.FillRectangle(new SolidBrush(Color.FromArgb(100, Color.Yellow)), x, y, _charWidth, _lineHeight);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (_fileData == null) return;

            // Handle Hex Input
            if (IsHexKey(e.KeyCode))
            {
                // Fix: Correctly get the single hex character from the key code
                string hexChar = e.KeyCode.ToString();
                if (hexChar.StartsWith("D"))
                {
                    hexChar = hexChar.Substring(1); // For Keys.D0 to D9, remove the "D"
                }

                int byteIndex = _caretPos.Y * _bytesPerLine + _caretPos.X;

                if (byteIndex < _fileData.Length)
                {
                    byte currentByte = _fileData[byteIndex];
                    byte newNibble = ConvertHexCharToByte(hexChar);

                    if (_editNibble == 0)
                    {
                        // Update the high nibble
                        currentByte = (byte)((currentByte & 0x0F) | (newNibble << 4));
                        _editNibble = 1;
                    }
                    else
                    {
                        // Update the low nibble
                        currentByte = (byte)((currentByte & 0xF0) | newNibble);
                        _editNibble = 0;

                        // Move caret to the next byte
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
            // Handle Navigation Keys
            else
            {
                switch (e.KeyCode)
                {
                    case Keys.Right:
                        SetCaretPosition((_caretPos.Y * _bytesPerLine) + _caretPos.X + 1);
                        _editNibble = 0; // Reset nibble state on navigation
                        break;
                    case Keys.Left:
                        SetCaretPosition((_caretPos.Y * _bytesPerLine) + _caretPos.X - 1);
                        _editNibble = 0;
                        break;
                    case Keys.Up:
                        SetCaretPosition((_caretPos.Y * _bytesPerLine) + _caretPos.X - _bytesPerLine);
                        _editNibble = 0;
                        break;
                    case Keys.Down:
                        SetCaretPosition((_caretPos.Y * _bytesPerLine) + _caretPos.X + _bytesPerLine);
                        _editNibble = 0;
                        break;
                    case Keys.Home:
                        SetCaretPosition((_caretPos.Y * _bytesPerLine));
                        _editNibble = 0;
                        break;
                    case Keys.End:
                        SetCaretPosition((_caretPos.Y * _bytesPerLine) + _bytesPerLine - 1);
                        _editNibble = 0;
                        break;
                }
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
            }
            else if (e.X >= asciiStart)
            {
                int charIndex = (e.X - asciiStart) / _charWidth;
                int byteIndex = charIndex;
                clickedIndex = (line * _bytesPerLine) + byteIndex;
            }

            if (clickedIndex != -1 && clickedIndex < _fileData.Length)
            {
                SetCaretPosition(clickedIndex);
                _editNibble = 0;
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
            return (byte)int.Parse(hexChar, System.Globalization.NumberStyles.HexNumber);
        }
    }
}