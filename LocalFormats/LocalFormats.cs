using SRT2Markdown;
using System;
using System.Collections.Generic;

namespace DocumentTranslationService.LocalFormats
{
    /// <summary>
    /// Extensible list of locally converted formats. Add any new formats here
    /// Underlying assumption is that the format translated by the service is Markdown.
    /// You supply the functions to convert the local format to Markdown and from Markdown.
    /// </summary>
    public static class LocalFormats
    {
        public static readonly List<LocalDocumentTranslationFileFormat> Formats = new() { new LocalDocumentTranslationFileFormat()
        {
            Format = "SubRip",
            FileExtensions = new() { ".srt" },
            ConvertToMarkdown = SRTMarkdownConverter.ConvertToMarkdown,
            ConvertFromMarkdown = SRTMarkdownConverter.ConvertToSRT
        } };
    }
}
