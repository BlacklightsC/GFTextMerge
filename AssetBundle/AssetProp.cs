namespace GFTextMerge.AssetBundles
{
    public class AssetProp
    {
        public long ofs;
        public long size;
        public int status;
        public string name;

        public AssetProp(long ofs, long size, int status, string name)
        {
            this.ofs = ofs;
            this.size = size;
            this.status = status;
            this.name = name;
        }
    }
}
