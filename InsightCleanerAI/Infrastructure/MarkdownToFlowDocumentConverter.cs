using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using InsightCleanerAI.Resources;

namespace InsightCleanerAI.Infrastructure
{
    /// <summary>
    /// Very lightweight Markdown converter that understands headings, bold text, bullet lists, and paragraphs.
    /// </summary>
    public class MarkdownToFlowDocumentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var document = new FlowDocument
            {
                PagePadding = new Thickness(0),
                Background = null
            };

            if (value is not string markdown || string.IsNullOrWhiteSpace(markdown))
            {
                document.Blocks.Add(new Paragraph(new Run(Strings.DefaultInsightPlaceholder)));
                return document;
            }

            var lines = markdown.Replace("\r\n", "\n").Split('\n');
            var bulletBuffer = new List<string>();

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd();
                if (string.IsNullOrWhiteSpace(line))
                {
                    AppendBulletList(document, bulletBuffer);
                    continue;
                }

                if (line.StartsWith("#"))
                {
                    AppendBulletList(document, bulletBuffer);
                    var level = line.TakeWhile(ch => ch == '#').Count();
                    var text = line.TrimStart('#').TrimStart();
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    var heading = new Paragraph
                    {
                        FontSize = Math.Max(14, 20 - level * 2),
                        FontWeight = level <= 3 ? FontWeights.Bold : FontWeights.SemiBold,
                        Margin = new Thickness(0, level <= 2 ? 12 : 6, 0, 6)
                    };
                    AppendMarkdownInlines(heading, text);
                    document.Blocks.Add(heading);
                    continue;
                }

                if (line.StartsWith("- ") || line.StartsWith("* "))
                {
                    bulletBuffer.Add(line[2..]);
                    continue;
                }

                AppendBulletList(document, bulletBuffer);

                var paragraph = new Paragraph
                {
                    Margin = new Thickness(0, 0, 0, 6)
                };
                AppendMarkdownInlines(paragraph, line);
                document.Blocks.Add(paragraph);
            }

            AppendBulletList(document, bulletBuffer);
            return document;
        }

        private static void AppendMarkdownInlines(Paragraph paragraph, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var segments = text.Split(new[] { "**" }, StringSplitOptions.None);
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                var run = new Run(segment);
                if (i % 2 == 1)
                {
                    paragraph.Inlines.Add(new Bold(run));
                }
                else
                {
                    paragraph.Inlines.Add(run);
                }
            }
        }

        private static void AppendBulletList(FlowDocument document, List<string> items)
        {
            if (items.Count == 0)
            {
                return;
            }

            var list = new List();
            foreach (var item in items)
            {
                var paragraph = new Paragraph
                {
                    Margin = new Thickness(0, 0, 0, 4)
                };
                AppendMarkdownInlines(paragraph, item.Trim());
                list.ListItems.Add(new ListItem(paragraph));
            }

            document.Blocks.Add(list);
            items.Clear();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
