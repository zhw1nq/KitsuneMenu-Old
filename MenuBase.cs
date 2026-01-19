using Menu.Enums;

namespace Menu
{
    public class MenuBase(MenuValue title)
    {
        public Action<MenuButtons, MenuBase, MenuItem?>? Callback;
        public MenuValue Title { get; set; } = title;
        public List<MenuItem> Items { get; set; } = [];
        public int Option { get; set; } = 0;

        public bool RequiresFreeze { get; set; } = false;
        public bool AcceptButtons { get; set; } = false;
        public bool AcceptInput { get; set; } = false;
        public bool RepeatedButtons { get; set; } = true;

        public MenuValue[] Cursor =
        [
            new MenuValue("►") { Prefix = "<font color=\"#ff3333\">", Suffix = "</font>" },
            new MenuValue("◄") { Prefix = "<font color=\"#ff3333\">", Suffix = "</font>" },
        ];

        public MenuValue[] Selector =
        [
            new MenuValue("[ ") { Prefix = "<font color=\"#ff3333\">", Suffix = "</font>" },
            new MenuValue(" ]") { Prefix = "<font color=\"#ff3333\">", Suffix = "</font>" },
        ];

        public MenuValue[] Bool =
        [
            new MenuValue("✘") { Prefix = "<font color=\"#FF0000\">", Suffix = "</font>" },
            new MenuValue("✔") { Prefix = "<font color=\"#008000\">", Suffix = "</font>" },
        ];

        public MenuValue[] Slider =
        [
            new MenuValue("(") { Prefix = "<font color=\"#FFFFFF\">", Suffix = "</font>" },
            new MenuValue(")") { Prefix = "<font color=\"#FFFFFF\">", Suffix = "</font>" },
            new MenuValue("-") { Prefix = "<font color=\"#FFFFFF\">", Suffix = "</font>" },
            new MenuValue("|") { Prefix = "<font color=\"#FFFFFF\">", Suffix = "</font>" },
        ];

        public MenuValue Input = new("________") { Prefix = "<font color=\"#FFFFFF\">", Suffix = "</font>" };
        public MenuValue Separator = new(" - ") { Prefix = "<font color=\"#FFFFFF\">", Suffix = "</font>" };

        // SwiftlyS2-style visual design properties
        public string GuideLine { get; set; } = "──────────────────────────";
        public string GuideLineColor { get; set; } = "#FFFFFF";
        public string NavigationMarkerColor { get; set; } = "#FFFFFF";
        public string NavigationPrefix { get; set; } = "►";
        public string FooterColor { get; set; } = "#FF0000";
        public string TitleColor { get; set; } = "#FFFFFF";
        public string? TitleGradientEndColor { get; set; } = null; // Set to enable gradient title
        
        // Item colors (SwiftlyS2 style - green items, highlighted selection)
        public string DefaultItemColor { get; set; } = "#00FF00"; // Green like SwiftlyS2
        public string SelectedItemColor { get; set; } = "#FF69B4"; // Pink/magenta for selected item
        
        // Display control
        public bool HideTitle { get; set; } = false;
        public bool HideFooter { get; set; } = false;
        public bool ShowItemCount { get; set; } = true;
        public bool UseSwiftlyStyle { get; set; } = true;
        
        // Comment/Description for current option
        public string DefaultComment { get; set; } = "";

        public void AddItem(MenuItem item)
        {
            Items.Add(item);
        }
    }
}