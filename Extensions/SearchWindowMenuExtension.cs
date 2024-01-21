using Frosty.Core;

namespace SearchPlugin.Extensions
{
    public class SearchMenuExtension : MenuExtension
    {
        // The top level menu item to place this menu item into. In this case 'Tools'
        public override string TopLevelMenuName => "Tools";

        // The name of the menu item. In this case 'InitFS Explorer'
        public override string MenuItemName => "Search";

        // The action to perform when the menu item is clicked
        public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
        {

        });
    }
}
