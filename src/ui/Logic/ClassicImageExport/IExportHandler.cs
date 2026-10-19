using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
namespace Nikse.SubtitleEdit.Logic.ClassicImageExport {

public interface IExportHandler
{
    ExportImageType ExportImageType { get; }
    string Extension { get; }
    string Title { get; }
    bool UseFileName { get; }
    public void WriteHeader(string fileOrFolderName, ImageParameter imageParameter);
    void CreateParagraph(ImageParameter param);
    void WriteParagraph(ImageParameter param);
    public void WriteFooter();
}
}
