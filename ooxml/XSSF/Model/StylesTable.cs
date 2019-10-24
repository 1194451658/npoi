/* ====================================================================
   Licensed to the Apache Software Foundation (ASF) under one or more
   contributor license agreements.  See the NOTICE file distributed with
   this work for Additional information regarding copyright ownership.
   The ASF licenses this file to You under the Apache License, Version 2.0
   (the "License"); you may not use this file except in compliance with
   the License.  You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
==================================================================== */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using NPOI.OpenXml4Net.OPC;
using NPOI.OpenXmlFormats.Spreadsheet;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.XSSF.UserModel.Extensions;
using System.Collections.ObjectModel;
using NPOI.SS;

namespace NPOI.XSSF.Model
{


    /**
     * Table of styles shared across all sheets in a workbook.
     *
     * @author ugo
     */
    public class StylesTable : POIXMLDocumentPart
    {
        private SortedDictionary<int, String> numberFormats = new SortedDictionary<int, String>();
        private bool[] usedNumberFormats = new bool[SpreadsheetVersion.EXCEL2007.MaxCellStyles];
        private List<XSSFFont> fonts = new List<XSSFFont>();
        private List<XSSFCellFill> fills = new List<XSSFCellFill>();
        private List<XSSFCellBorder> borders = new List<XSSFCellBorder>();
        private List<CT_Xf> styleXfs = new List<CT_Xf>();
        private List<CT_Xf> xfs = new List<CT_Xf>();

        private List<CT_Dxf> dxfs = new List<CT_Dxf>();

        /**
         * The first style id available for use as a custom style
         */
        // Is this right? Number formats (XSSFDataFormat) and cell styles (XSSFCellStyle) are different.
        // What's up with the plus 1?
        public static int FIRST_CUSTOM_STYLE_ID = BuiltinFormats.FIRST_USER_DEFINED_FORMAT_INDEX + 1;

        private static int FIRST_USER_DEFINED_NUMBER_FORMAT_ID = BuiltinFormats.FIRST_USER_DEFINED_FORMAT_INDEX;
        private static int MAXIMUM_NUMBER_OF_DATA_FORMATS = SpreadsheetVersion.EXCEL2007.MaxCellStyles; //FIXME: should be 250

        private static int MAXIMUM_STYLE_ID = SpreadsheetVersion.EXCEL2007.MaxCellStyles;

        private StyleSheetDocument doc;
        private XSSFWorkbook workbook;
        private ThemesTable theme;

        /**
         * Create a new, empty StylesTable
         */
        public StylesTable()
            : base()
        {

            doc = new StyleSheetDocument();
            doc.AddNewStyleSheet();
            // Initialization required in order to make the document Readable by MSExcel
            Initialize();
        }

        internal StylesTable(PackagePart part, PackageRelationship rel)
            : base(part, rel)
        {
            XmlDocument xmldoc = ConvertStreamToXml(part.GetInputStream());
            ReadFrom(xmldoc);
        }
        public void SetWorkbook(XSSFWorkbook wb)
        {
            this.workbook = wb;
        }
        public ThemesTable GetTheme()
        {
            return theme;
        }

        public void SetTheme(ThemesTable theme)
        {
            this.theme = theme;

            // Pass the themes table along to things which need to 
            //  know about it, but have already been Created by now
            foreach (XSSFFont font in fonts)
            {
                font.SetThemesTable(theme);
            }
            foreach (XSSFCellBorder border in borders)
            {
                border.SetThemesTable(theme);
            }
        }

        /**
         * If there isn't currently a {@link ThemesTable} for the
         *  current Workbook, then creates one and sets it up.
         * After this, calls to {@link #getTheme()} won't give null
         */
        public void EnsureThemesTable()
        {
            if (theme != null) return;

            theme = (ThemesTable)workbook.CreateRelationship(XSSFRelation.THEME, XSSFFactory.GetInstance());
        }
        /**
         * Read this shared styles table from an XML file.
         *
         * @param is The input stream Containing the XML document.
         * @throws IOException if an error occurs while Reading.
         */

        protected void ReadFrom(XmlDocument xmldoc)
        {
            try
            {
                doc = StyleSheetDocument.Parse(xmldoc, NamespaceManager);

                CT_Stylesheet styleSheet = doc.GetStyleSheet();

                // Grab all the different bits we care about
                CT_NumFmts ctfmts = styleSheet.numFmts;
                if (ctfmts != null)
                {
                    foreach (CT_NumFmt nfmt in ctfmts.numFmt)
                    {
                        int formatId = (int)nfmt.numFmtId;
                        numberFormats.Add(formatId, nfmt.formatCode);
                    }
                }

                CT_Fonts ctfonts = styleSheet.fonts;
                if (ctfonts != null)
                {
                    int idx = 0;
                    foreach (CT_Font font in ctfonts.font)
                    {
                        // Create the font and save it. Themes Table supplied later
                        XSSFFont f = new XSSFFont(font, idx);
                        fonts.Add(f);
                        idx++;
                    }
                }
                CT_Fills ctFills = styleSheet.fills;
                if (ctFills != null)
                {
                    foreach (CT_Fill fill in ctFills.fill)
                    {
                        fills.Add(new XSSFCellFill(fill));
                    }
                }

                CT_Borders ctborders = styleSheet.borders;
                if (ctborders != null)
                {
                    foreach (CT_Border border in ctborders.border)
                    {
                        borders.Add(new XSSFCellBorder(border));
                    }
                }

                CT_CellXfs cellXfs = styleSheet.cellXfs;
                if (cellXfs != null)
                    xfs.AddRange(cellXfs.xf);

                CT_CellStyleXfs cellStyleXfs = styleSheet.cellStyleXfs;
                if (cellStyleXfs != null)
                    styleXfs.AddRange(cellStyleXfs.xf);

                CT_Dxfs styleDxfs = styleSheet.dxfs;
                if (styleDxfs != null)
                    dxfs.AddRange(styleDxfs.dxf);

            }
            catch (XmlException e)
            {
                throw new IOException(e.Message);
            }
        }

        // ===========================================================
        //  Start of style related Getters and Setters
        // ===========================================================

        public String GetNumberFormatAt(int idx)
        {
            if (numberFormats.ContainsKey(idx))
                return numberFormats[idx];
            else
                return null;
        }
        /**
         * Puts <code>fmt</code> in the numberFormats map if the format is not
         * already in the the number format style table.
         * Does nothing if <code>fmt</code> is already in number format style table.
         *
         * @param fmt the number format to add to number format style table
         * @return the index of <code>fmt</code> in the number format style table
         */
        public int PutNumberFormat(String fmt)
        {
            // Check if number format already exists
            if (numberFormats.ContainsValue(fmt))
            {
                // Find the key, and return that
                foreach (KeyValuePair<int, string> numFmt in numberFormats)
                {
                    if (numFmt.Value.Equals(fmt))
                    {
                        return numFmt.Key;
                    }
                }
                throw new InvalidOperationException("Found the format, but couldn't figure out where - should never happen!");
            }

            if (numberFormats.Count >= MAXIMUM_NUMBER_OF_DATA_FORMATS)
            {
                throw new InvalidOperationException("The maximum number of Data Formats was exceeded. " +
                        "You can define up to " + MAXIMUM_NUMBER_OF_DATA_FORMATS + " formats in a .xlsx Workbook.");
            }

            // Find a spare key, and add that
            int formatIndex;
            if (numberFormats.Count == 0)
            {
                formatIndex = FIRST_USER_DEFINED_NUMBER_FORMAT_ID;
            }
            else
            {
                // get next-available numberFormat index.
                // Assumption: there are never gaps in numberFormats indices
                formatIndex = Math.Max(
                        numberFormats.Keys.Last() + 1,
                        FIRST_USER_DEFINED_NUMBER_FORMAT_ID);
            }

            if (numberFormats.ContainsKey(formatIndex))
                numberFormats[formatIndex] = fmt;
            else
                numberFormats.Add(formatIndex, fmt);

            return formatIndex;
        }

        /**
         * Add a number format with a specific ID into the numberFormats map.
         * If a format with the same ID already exists, overwrite the format code
         * with <code>fmt</code>
         * This may be used to override built-in number formats.
         *
         * @param index the number format ID
         * @param fmt the number format code
         */
        public void PutNumberFormat(short index, String fmt)
        {
            if (numberFormats.ContainsKey(index))
                numberFormats[index] = fmt;
            else
                numberFormats.Add(index, fmt);
        }

        public XSSFFont GetFontAt(int idx)
        {
            return fonts[idx];
        }

        /**
         * Records the given font in the font table.
         * Will re-use an existing font index if this
         *  font matches another, EXCEPT if forced
         *  registration is requested.
         * This allows people to create several fonts
         *  then customise them later.
         * Note - End Users probably want to call
         *  {@link XSSFFont#registerTo(StylesTable)}
         */
        public int PutFont(XSSFFont font, bool forceRegistration)
        {
            int idx = -1;
            if (!forceRegistration)
            {
                idx = fonts.IndexOf(font);
            }

            if (idx != -1)
            {
                return idx;
            }

            idx = fonts.Count;
            fonts.Add(font);
            return idx;
        }
        public int PutFont(XSSFFont font)
        {
            return PutFont(font, false);
        }

        public XSSFCellStyle GetStyleAt(int idx)
        {
            int styleXfId = 0;

            // 0 is the empty default
            if (xfs[idx].xfId > 0)
            {
                styleXfId = (int)xfs[idx].xfId;
            }

            return new XSSFCellStyle(idx, styleXfId, this, theme);
        }
        public int PutStyle(XSSFCellStyle style)
        {
            CT_Xf mainXF = style.GetCoreXf();

            if (!xfs.Contains(mainXF))
            {
                xfs.Add(mainXF);
            }
            return xfs.IndexOf(mainXF);
        }

        public XSSFCellBorder GetBorderAt(int idx)
        {
            return borders[idx];
        }
        /// <summary>
        /// Adds a border to the border style table if it isn't already in the style table
        /// Does nothing if border is already in borders style table
        /// </summary>
        /// <param name="border">border to add</param>
        /// <returns>return the index of the added border</returns>
        public int PutBorder(XSSFCellBorder border)
        {
            int idx = borders.IndexOf(border);
            if (idx != -1)
            {
                return idx;
            }
            borders.Add(border);
            border.SetThemesTable(theme);
            return borders.Count - 1;
        }

        public XSSFCellFill GetFillAt(int idx)
        {
            return fills[idx];
        }

        public ReadOnlyCollection<XSSFCellBorder> GetBorders()
        {
            return borders.AsReadOnly();
        }

        public ReadOnlyCollection<XSSFCellFill> GetFills()
        {
            return fills.AsReadOnly();
        }

        public ReadOnlyCollection<XSSFFont> GetFonts()
        {
            return fonts.AsReadOnly();
        }

        public IDictionary<int, String> GetNumberFormats()
        {
            return numberFormats;
        }
        /// <summary>
        /// Adds a fill to the fill style table if it isn't already in the style table
        /// Does nothing if fill is already in fill style table
        /// </summary>
        /// <param name="fill">fill to add</param>
        /// <returns>return the index of the added fill</returns>
        public int PutFill(XSSFCellFill fill)
        {
            int idx = fills.IndexOf(fill);
            if (idx != -1)
            {
                return idx;
            }
            fills.Add(fill);
            return fills.Count - 1;
        }

        internal CT_Xf GetCellXfAt(int idx)
        {
            return xfs[idx];
        }

        /// <summary>
        /// Adds a cell to the styles table. Does not check for duplicates
        /// </summary>
        /// <param name="cellXf">the cell to add to the styles table</param>
        /// <returns>return the added cell ID in the style table</returns>
        internal int PutCellXf(CT_Xf cellXf)
        {
            xfs.Add(cellXf);
            return xfs.Count;
        }
        internal void ReplaceCellXfAt(int idx, CT_Xf cellXf)
        {
            xfs[idx] = cellXf;
        }

        internal CT_Xf GetCellStyleXfAt(int idx)
        {
            if (idx < 0 || idx > styleXfs.Count)
                return null;
            return styleXfs[idx];
        }

        /// <summary>
        /// Adds a cell style to the styles table.Does not check for duplicates.
        /// </summary>
        /// <param name="cellStyleXf">the cell style to add to the styles table</param>
        /// <returns>return the cell style ID in the style table</returns>
        internal int PutCellStyleXf(CT_Xf cellStyleXf)
        {
            styleXfs.Add(cellStyleXf);
            // TODO: check for duplicate
            return styleXfs.Count;
        }
        internal void ReplaceCellStyleXfAt(int idx, CT_Xf cellStyleXf)
        {
            styleXfs[idx] = cellStyleXf;
        }

        /**
         * get the size of cell styles
         */
        public int NumCellStyles
        {
            get
            {
                // Each cell style has a unique xfs entry
                // Several might share the same styleXfs entry
                return xfs.Count;
            }
        }

        /**
         * For unit testing only
         */
        internal int NumberFormatSize
        {
            get
            {
                return numberFormats.Count;
            }
        }

        /**
         * For unit testing only
         */
        internal int XfsSize
        {
            get
            {
                return xfs.Count;
            }
        }
        /**
         * For unit testing only
         */
        internal int StyleXfsSize
        {
            get
            {
                return styleXfs.Count;
            }
        }
        /**
         * For unit testing only!
         */
        internal CT_Stylesheet GetCTStylesheet()
        {
            return doc.GetStyleSheet();
        }
        internal int DXfsSize
        {
            get
            {
                return dxfs.Count;
            }
        }


        /**
         * Write this table out as XML.
         *
         * @param out The stream to write to.
         * @throws IOException if an error occurs while writing.
         */
        public void WriteTo(Stream out1)
        {

            // Work on the current one
            // Need to do this, as we don't handle
            //  all the possible entries yet
            CT_Stylesheet styleSheet = doc.GetStyleSheet();

            // Formats
            CT_NumFmts formats = new CT_NumFmts();
            formats.count = (uint)numberFormats.Count;

            foreach (KeyValuePair<int, String > entry in numberFormats)
            {
                CT_NumFmt ctFmt = formats.AddNewNumFmt();
                ctFmt.numFmtId = (uint)entry.Key;
                ctFmt.formatCode = entry.Value;
            }

            styleSheet.numFmts = formats;

            // Fonts
            CT_Fonts ctFonts = styleSheet.fonts;
            if (ctFonts == null)
                ctFonts = new CT_Fonts();
            ctFonts.count = (uint)fonts.Count;
            if (ctFonts.count > 0)
                ctFonts.countSpecified = true;
            List<CT_Font> ctfnt = new List<CT_Font>(fonts.Count);

            foreach (XSSFFont f in fonts)
                ctfnt.Add(f.GetCTFont());
            ctFonts.SetFontArray(ctfnt);
            styleSheet.fonts = (ctFonts);

            // Fills
            CT_Fills ctFills = styleSheet.fills;
            if (ctFills == null)
            {
                ctFills = new CT_Fills();
            }
            ctFills.count = (uint)fills.Count;
            List<CT_Fill> ctf = new List<CT_Fill>(fills.Count);
            
            foreach (XSSFCellFill f in fills)
                ctf.Add( f.GetCTFill());
            ctFills.SetFillArray(ctf);
            if (ctFills.count > 0)
                ctFills.countSpecified = true;
            styleSheet.fills = ctFills;

            // Borders
            CT_Borders ctBorders = styleSheet.borders;
            if (ctBorders == null)
            {
                ctBorders = new CT_Borders();
            }
            ctBorders.count = (uint)borders.Count;
            List<CT_Border> ctb = new List<CT_Border>(borders.Count);
            foreach (XSSFCellBorder b in borders) 
                ctb.Add(b.GetCTBorder());
            
            ctBorders.SetBorderArray(ctb);
            styleSheet.borders = ctBorders;

            // Xfs
            if (xfs.Count > 0)
            {
                CT_CellXfs ctXfs = styleSheet.cellXfs;
                if (ctXfs == null)
                {
                    ctXfs = new CT_CellXfs();
                }
                ctXfs.count = (uint)xfs.Count;
                if (ctXfs.count > 0)
                    ctXfs.countSpecified = true;
                ctXfs.xf = xfs;

                styleSheet.cellXfs = (ctXfs);
            }

            // Style xfs
            if (styleXfs.Count > 0)
            {
                CT_CellStyleXfs ctSXfs = styleSheet.cellStyleXfs;
                if (ctSXfs == null)
                {
                    ctSXfs = new CT_CellStyleXfs();
                }
                ctSXfs.count = (uint)(styleXfs.Count);
                if (ctSXfs.count > 0)
                    ctSXfs.countSpecified = true;
                ctSXfs.xf = styleXfs;

                styleSheet.cellStyleXfs = (ctSXfs);
            }

            // Style dxfs
            if (dxfs.Count > 0)
            {
                CT_Dxfs ctDxfs = styleSheet.dxfs;
                if (ctDxfs == null)
                {
                    ctDxfs = new CT_Dxfs();
                }
                ctDxfs.count = (uint)dxfs.Count;
                if (ctDxfs.count > 0)
                    ctDxfs.countSpecified = true;
                ctDxfs.dxf = dxfs;

                styleSheet.dxfs = (ctDxfs);
            }

            // Save
            doc.Save(out1);
        }


        protected internal override void Commit()
        {
            PackagePart part = GetPackagePart();
            Stream out1 = part.GetOutputStream();
            WriteTo(out1);
            out1.Close();
        }

        private void Initialize()
        {
            //CT_Font ctFont = CreateDefaultFont();
            XSSFFont xssfFont = CreateDefaultFont();
            fonts.Add(xssfFont);

            CT_Fill[] ctFill = CreateDefaultFills();
            fills.Add(new XSSFCellFill(ctFill[0]));
            fills.Add(new XSSFCellFill(ctFill[1]));

            CT_Border ctBorder = CreateDefaultBorder();
            borders.Add(new XSSFCellBorder(ctBorder));

            CT_Xf styleXf = CreateDefaultXf();
            styleXfs.Add(styleXf);
            CT_Xf xf = CreateDefaultXf();
            xf.xfId = 0;
            xfs.Add(xf);
        }

        private static CT_Xf CreateDefaultXf()
        {
            CT_Xf ctXf = new CT_Xf();
            ctXf.numFmtId = 0;
            ctXf.fontId = 0;
            ctXf.fillId = 0;
            ctXf.borderId = 0;
            return ctXf;
        }
        private static CT_Border CreateDefaultBorder()
        {
            CT_Border ctBorder = new CT_Border();
            ctBorder.AddNewLeft();
            ctBorder.AddNewRight();
            ctBorder.AddNewTop();
            ctBorder.AddNewBottom();
            ctBorder.AddNewDiagonal();
            return ctBorder;
        }


        private static CT_Fill[] CreateDefaultFills()
        {
            CT_Fill[] ctFill = new CT_Fill[] { new CT_Fill(), new CT_Fill() };
            ctFill[0].AddNewPatternFill().patternType = (ST_PatternType.none);
            ctFill[1].AddNewPatternFill().patternType = (ST_PatternType.darkGray);
            return ctFill;
        }

        private static XSSFFont CreateDefaultFont()
        {
            CT_Font ctFont = new CT_Font();
            XSSFFont xssfFont = new XSSFFont(ctFont, 0);
            xssfFont.FontHeightInPoints = (XSSFFont.DEFAULT_FONT_SIZE);
            xssfFont.Color = (XSSFFont.DEFAULT_FONT_COLOR);//SetTheme
            xssfFont.FontName = (XSSFFont.DEFAULT_FONT_NAME);
            xssfFont.SetFamily(FontFamily.SWISS);
            xssfFont.SetScheme(FontScheme.MINOR);
            return xssfFont;
        }

        public CT_Dxf GetDxfAt(int idx)
        {
            return dxfs[idx];
        }

        /// <summary>
        /// Adds a Dxf to the style table Does not check for duplicates.
        /// </summary>
        /// <param name="dxf">the Dxf to add</param>
        /// <returns>added dxf ID in the style table</returns>
        public int PutDxf(CT_Dxf dxf)
        {
            this.dxfs.Add(dxf);
            return this.dxfs.Count;
        }
        /**
         * Create a cell style in this style table.
         * Note - End users probably want to call {@link XSSFWorkbook#createCellStyle()}
         * rather than working with the styles table directly.
         */
        public XSSFCellStyle CreateCellStyle()
        {
            if (NumCellStyles > MAXIMUM_STYLE_ID)
                throw new InvalidOperationException("The maximum number of Cell Styles was exceeded. " +
                          "You can define up to " + MAXIMUM_STYLE_ID + " style in a .xlsx Workbook");

            int xfSize = styleXfs.Count;
            CT_Xf ctXf = new CT_Xf();
            ctXf.numFmtId = 0;
            ctXf.fontId = 0;
            ctXf.fillId = 0;
            ctXf.borderId = 0;
            ctXf.xfId = 0;
            
            int indexXf = PutCellXf(ctXf);
            return new XSSFCellStyle(indexXf - 1, xfSize - 1, this, theme);
        }

        /**
         * Finds a font that matches the one with the supplied attributes
         */
        public XSSFFont FindFont(short boldWeight, short color, short fontHeight, String name, bool italic, bool strikeout, FontSuperScript typeOffset,FontUnderlineType underline)
        {
            foreach (XSSFFont font in fonts)
            {
                if ((font.Boldweight == boldWeight)
                        && font.Color == color
                        && font.FontHeight == fontHeight
                        && font.FontName.Equals(name)
                        && font.IsItalic == italic
                        && font.IsStrikeout == strikeout
                        && font.TypeOffset == typeOffset
                        && font.Underline == underline)
                {
                    return font;
                }
            }
            return null;
        }
    }
}





