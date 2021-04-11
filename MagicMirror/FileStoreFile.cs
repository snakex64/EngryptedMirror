using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MagicMirror
{
    public class FileStoreFile
    {
        public string RelativePath { get; set; }

        public DokanNet.FileInformation RealFileInformation { get; set; }

        public bool IsDownloaded { get; set; }

        public bool IsForcedDownloaded { get; set; }
    }
}
