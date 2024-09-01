// PDFsharp - A .NET library for processing PDF
// See the LICENSE file in the solution root for more information.

using System;
using System.Collections.Generic;
using PdfSharp.Pdf.IO;

namespace PdfSharp.Drawing.Layout
{
    /// <summary>
    /// Represents a very simple text formatter.
    /// If this class does not satisfy your needs on formatting paragraphs, I recommend taking a look
    /// at MigraDoc Foundation. Alternatively, you should copy this class in your own source code and modify it.
    /// </summary>
    public class XTextFormatter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="XTextFormatter"/> class.
        /// </summary>
        public XTextFormatter(XGraphics gfx)
        {
            _gfx = gfx ?? throw new ArgumentNullException(nameof(gfx));
        }

        readonly XGraphics _gfx;

        /// <summary>
        /// Gets or sets the text.
        /// </summary>
        public string Text
        {
            get => _text;
            set => _text = value;
        }
        string _text = default!;

        /// <summary>
        /// Gets or sets the font.
        /// </summary>
        public XFont Font
        {
            get => _font;
            set
            {
                _font = value ?? throw new ArgumentNullException(nameof(Font));

                _lineSpace = _font.GetHeight(); // old: _font.GetHeight(_gfx);
                _cyAscent = _lineSpace * _font.CellAscent / _font.CellSpace;
                _cyDescent = _lineSpace * _font.CellDescent / _font.CellSpace;

                // The width of " " is 0, so we measure the space indirectly.
                _spaceWidth = _gfx.MeasureString("x x", value).Width;
                _spaceWidth -= _gfx.MeasureString("xx", value).Width;
            }
        }

        XFont _font = default!;
        double _lineSpace;
        double _cyAscent;
        double _cyDescent;
        double _spaceWidth;

        /// <summary>
        /// Gets or sets the bounding box of the layout.
        /// </summary>
        public XRect LayoutRectangle
        {
            get => _layoutRectangle;
            set => _layoutRectangle = value;
        }

        XRect _layoutRectangle;

        /// <summary>
        /// Gets or sets the letter spacing.
        /// </summary>
        public int Kerning {
            get => _kerning;
            set => _kerning = value;
        }
        int _kerning = 0;

        /// <summary>
        /// Gets or sets the line height.
        /// </summary>
        public double LineHeight {
            get => _lineSpace;
            set => _lineSpace = value;
        }
        
        /// <summary>
        /// Gets or sets the alignment of the text.
        /// </summary>
        public XParagraphAlignment Alignment
        {
            get => _alignment;
            set => _alignment = value;
        }

        XParagraphAlignment _alignment = XParagraphAlignment.Left;

        /// <summary>
        /// Draws the text.
        /// </summary>
        /// <param name="text">The text to be drawn.</param>
        /// <param name="font">The font.</param>
        /// <param name="brush">The text brush.</param>
        /// <param name="layoutRectangle">The layout rectangle.</param>
        public void DrawString(string text, XFont font, XBrush brush, XRect layoutRectangle)
        {
            DrawString(text, font, brush, layoutRectangle, XStringFormats.TopLeft);
        }

        /// <summary>
        /// Draws the text.
        /// </summary>
        /// <param name="text">The text to be drawn.</param>
        /// <param name="font">The font.</param>
        /// <param name="brush">The text brush.</param>
        /// <param name="layoutRectangle">The layout rectangle.</param>
        /// <param name="format">The format. Must be <c>XStringFormat.TopLeft</c></param>
        public void DrawString(string text, XFont font, XBrush brush, XRect layoutRectangle, XStringFormat format)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            if (font == null)
                throw new ArgumentNullException(nameof(font));
            if (brush == null)
                throw new ArgumentNullException(nameof(brush));
            if (format.Alignment != XStringAlignment.Near || format.LineAlignment != XLineAlignment.Near)
                throw new ArgumentException("Only TopLeft alignment is currently implemented.");

            Text = text;
            Font = font;
            _layoutRectangle = layoutRectangle;

            if (text.Length == 0)
                return;

            CreateLetters();

            CreateLayout();

            double dx = layoutRectangle.Location.X;
            double dy = layoutRectangle.Location.Y + _cyAscent;

            for (int idx = 0; idx < _lines.Count; idx++)
            {
                Letter[] line = _lines[idx];
                
                foreach (Letter letter in line)
                {
                    if (letter.Stop)
                        break;

                    _gfx.DrawString(letter.Value, font, brush, dx + letter.Location.X, dy + letter.Location.Y);
                }
            }
        }

        void CreateLetters()
        {
            List<Letter> letters = new List<Letter>();
            List<Letter> word = new List<Letter>();

            for (int idx = 0; idx < _text.Length; idx++)
            {
                char ch = _text[idx];

                // Treat CR and CRLF as LF
                if (ch == Chars.CR)
                {
                    if (idx < _text.Length - 1 && _text[idx + 1] == Chars.LF)
                        idx++;
                    ch = Chars.LF;
                }
                if (ch == Chars.LF)
                {
                    if (word.Count == 0)
                        word.Add(new Letter(_gfx, ch, _font, _kerning));

                    word[^1].LineBreak = true;

                    if (GetTextWidth(letters) + GetTextWidth(word) >= _layoutRectangle.Width)
                    {
                        _lines.Add(letters.ToArray());
                        letters.Clear();
                    }
                    letters.AddRange(word);
                    _lines.Add(letters.ToArray());
                    letters.Clear();
                    word.Clear();
                }
                else
                {
                    word.Add(new Letter(_gfx, ch, _font, _kerning));
                }
            }

            if (word.Count != 0)
            {
                if (GetTextWidth(letters) + GetTextWidth(word) >= _layoutRectangle.Width)
                {
                    _lines.Add(letters.ToArray());
                    letters.Clear();
                }
                letters.AddRange(word);
            }

            if (letters.Count != 0)
            {
                _lines.Add(letters.ToArray());
            }
        }

        void CreateLayout()
        {
            for (int idx = 0; idx < _lines.Count; idx++)
            {
                if (_lines[idx][^1].Value == " ")
                {
                    _lines[idx] = _lines[idx].ToList().Take(_lines[idx].Length - 1).ToArray();
                }

                Letter[] line = _lines[idx];
                double left = 0;
                double top = 0;
                double textWidth = GetTextWidth(line);

                switch (_alignment)
                {
                    case XParagraphAlignment.Center:
                        left += (_layoutRectangle.Width - textWidth) / 2;
                        break;

                    case XParagraphAlignment.Right:
                        left += _layoutRectangle.Width - textWidth;
                        break;

                    case XParagraphAlignment.Justify:
                        if (!line[^1].LineBreak && idx < _lines.Count - 1)
                        {
                            int spaces = line.Where(l => l.IsWhiteSpace).Count();
                            double offset = (_layoutRectangle.Width - textWidth) / spaces;
                            line.ToList().ForEach(l => l.Width += offset);
                        }
                        break;

                    default:
                        break;
                }

                foreach (Letter letter in line)
                {
                    letter.Location = new XPoint(left, top);
                    left += letter.Width;
                }

                top += _lineSpace;
                if (top >= _layoutRectangle.Height)
                {
                    line[^1].Stop = true;
                    break;
                }
            }
        }

        double GetTextWidth(IEnumerable<Letter> letters)
        {
            double width = 0;
            foreach (Letter letter in letters)
            {
                width += letter.Width;
            }
            return width;
        }

        readonly List<Letter[]> _lines = new List<Letter[]>();

        class Letter
        {
            public readonly bool IsWhiteSpace;
            public readonly string Value;
            public double Width;
            public bool LineBreak;
            public XPoint Location;
            public bool Stop;

            public Letter(XGraphics gfx, char letter, XFont font, int kerning)
            {
                IsWhiteSpace = Char.IsWhiteSpace(letter);
                Value = letter.ToString();
                Width = gfx.MeasureString(Value, font).Width;
                LineBreak = false;
                Location = new XPoint();
                Stop = false;

                if (kerning != 0)
                {
                    double geviert = gfx.MeasureString('\u2014'.ToString(), font).Width;
                    Width -= (double)kerning / 1000 * geviert;
                }
            }
        }

        // DONE:
        // - kerning
        // - line spacing

        // TODO: Possible Improvements for XTextFormatter:
        // - more XStringFormat variations
        // - calculate bounding box
        // - left and right indent
        // - first line indent
        // - margins and paddings
        // - background color
        // - text background color
        // - border style
        // - hyphens, soft hyphens, hyphenation
        // - change font, size, text color etc.
        // - underline and strike-out variation
        // - super- and sub-script
        // - ...
    }
}
