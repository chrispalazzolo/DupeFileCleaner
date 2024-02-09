using System.Collections.ObjectModel;

namespace DupeFileCleaner
{
    class TreeViewItemModel
    {
        public string Name { get; set; }
        public string Header { get; set; }
        public ObservableCollection<TreeViewItemModel> Children { get; set; }

        public TreeViewItemModel() { }
    }
}
