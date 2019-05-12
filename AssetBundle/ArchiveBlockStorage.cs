using System.IO;
using System.Collections.Generic;

namespace GFTextMerge.AssetBundles
{
    public class ArchiveBlockStorage
    {
        private List<ArchiveBlockInfo> blocks;
        private Stream stream;

        public ArchiveBlockStorage(List<ArchiveBlockInfo> blocks, Stream stream)
        {
            this.blocks = blocks;
            this.stream = stream;
        }
    }
}
