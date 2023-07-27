using SRT2Markdown;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentTranslationService.LocalFormats
{
    /// <summary>
    /// Extensible list of locally converted formats. Add any new formats here
    /// Underlying assumption is that the format translated by the service is Markdown.
    /// You supply the functions to convert the local format to Markdown and from Markdown.
    /// </summary>
    public class LocalFormats
    {
        public readonly List<FormatInfo> Formats = new() { new FormatInfo()
        {
            Name = "SRT",
            Extensions = { ".srt" },
            ConvertToMarkdown = SRTMarkdownConverter.ConvertToMarkdown,
            ConvertFromMarkdown = SRTMarkdownConverter.ConvertToSRT
        } };
    }

    public struct FormatInfo
    {
        public string Name { get; set; }
        public List<string> Extensions { get; set; }
        public Delegate ConvertToMarkdown { get; set; }
        public Delegate ConvertFromMarkdown { get; set; }
    }
}
