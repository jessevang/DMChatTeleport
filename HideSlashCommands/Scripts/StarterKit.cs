using System.Collections.Generic;

namespace DMChatTeleport
{
    // -----------------------------
    // Items a kit can give
    // -----------------------------
    public class StarterKitItem
    {
        public string ItemName;
        public int Count;
        public int Quality = 1;
    }



    // -----------------------------
    // Starter Kit definition
    // -----------------------------
    public class StarterKit
    {
        public string Name;
        public string Description;
        public int Quality = 1;
        public List<StarterKitItem> Items = new List<StarterKitItem>();

    }

    // -----------------------------
    // Root config definition
    // -----------------------------
    public class StarterKitConfig
    {
        public List<StarterKit> StarterKits = new List<StarterKit>();
    }
}
