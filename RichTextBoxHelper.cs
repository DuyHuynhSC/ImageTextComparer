using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ImageTextComparer
{
    public static class RichTextBoxHelper
    {
        /// <summary>
        /// Renders diff results side-by-side into two RichTextBoxes.
        /// Adapts colors dynamically based on whether dark theme or light theme is active.
        /// </summary>
        public static void RenderDiff(RichTextBox leftBox, RichTextBox rightBox, List<DiffResult> diffs, bool isDarkTheme)
        {
            // Clear boxes
            leftBox.Document.Blocks.Clear();
            rightBox.Document.Blocks.Clear();

            var leftParagraph = new Paragraph();
            var rightParagraph = new Paragraph();

            // Set font family and size (increased to 16.0 for readability)
            leftParagraph.FontFamily = new FontFamily("Consolas");
            leftParagraph.FontSize = 16.0;
            
            rightParagraph.FontFamily = new FontFamily("Consolas");
            rightParagraph.FontSize = 16.0;

            // Setup Theme Brushes
            SolidColorBrush normalTextBrush = isDarkTheme 
                ? new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0))   // soft white
                : new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));  // dark slate

            SolidColorBrush deletedTextBrush = isDarkTheme
                ? new SolidColorBrush(Color.FromRgb(0xFA, 0x5C, 0x5C))   // soft red
                : new SolidColorBrush(Color.FromRgb(0xB9, 0x1C, 0x1C));  // dark red

            SolidColorBrush deletedBackgroundBrush = isDarkTheme
                ? new SolidColorBrush(Color.FromRgb(0x44, 0x1A, 0x1A))   // dark red bg
                : new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2));  // light red bg

            SolidColorBrush insertedTextBrush = isDarkTheme
                ? new SolidColorBrush(Color.FromRgb(0x4A, 0xD4, 0x98))   // soft green
                : new SolidColorBrush(Color.FromRgb(0x04, 0x78, 0x57));  // dark green

            SolidColorBrush insertedBackgroundBrush = isDarkTheme
                ? new SolidColorBrush(Color.FromRgb(0x1B, 0x3F, 0x2A))   // dark green bg
                : new SolidColorBrush(Color.FromRgb(0xD1, 0xFA, 0xE5));  // light green bg

            leftParagraph.Foreground = normalTextBrush;
            rightParagraph.Foreground = normalTextBrush;

            foreach (var diff in diffs)
            {
                switch (diff.Type)
                {
                    case DiffType.Unchanged:
                        leftParagraph.Inlines.Add(new Run(diff.Text));
                        rightParagraph.Inlines.Add(new Run(diff.Text));
                        break;

                    case DiffType.Deleted:
                        // Wrap in a Span because WPF RichTextBox ignores background colors directly set on a Run
                        var delSpan = new Span(new Run(diff.Text))
                        {
                            Foreground = deletedTextBrush,
                            Background = deletedBackgroundBrush,
                            TextDecorations = TextDecorations.Underline
                        };
                        leftParagraph.Inlines.Add(delSpan);
                        break;

                    case DiffType.Inserted:
                        // Wrap in a Span because WPF RichTextBox ignores background colors directly set on a Run
                        var insSpan = new Span(new Run(diff.Text))
                        {
                            Foreground = insertedTextBrush,
                            Background = insertedBackgroundBrush,
                            FontWeight = FontWeights.SemiBold
                        };
                        rightParagraph.Inlines.Add(insSpan);
                        break;
                }
            }

            leftBox.Document.Blocks.Add(leftParagraph);
            rightBox.Document.Blocks.Add(rightParagraph);
        }
    }
}
