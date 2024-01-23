using System;
using System.Windows;
using Frosty.Core;

namespace FindinFilesPlugin.Extensions
{
    public class FindinFilesMenuExtension : MenuExtension
    {
        // The top level menu item to place this menu item into.
        public override string TopLevelMenuName => "Tools";

        // The name of the menu item.
        public override string MenuItemName => "FindinFiles";

        // The action to perform when the menu item is clicked
        public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
        {
            FindinFilesPlugin.Windows.FindinFilesWindow win = new FindinFilesPlugin.Windows.FindinFilesWindow();
            win.Show();
        });
    }
}
