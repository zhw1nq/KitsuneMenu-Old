using Menu.Enums;

namespace Menu
{
    /// <summary>
    /// Base class for menu configuration and styling.
    /// Supports both legacy style and SwiftlyS2-style rendering.
    /// </summary>
    public class MenuBase(MenuValue title)
    {
        #region Core Properties
        
        public Action<MenuButtons, MenuBase, MenuItem?>? Callback;
        public MenuValue Title { get; set; } = title;
        public List<MenuItem> Items { get; set; } = [];
        public int Option { get; set; } = 0;

        #endregion

        #region Behavior Settings
        
        public bool RequiresFreeze { get; set; } = false;
        public bool AcceptButtons { get; set; } = false;
        public bool AcceptInput { get; set; } = false;
        public bool RepeatedButtons { get; set; } = true;

        #endregion

        #region Legacy Style Elements
        
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

        #endregion

        #region SwiftlyS2 Style Configuration
        
        /// <summary>Enable SwiftlyS2-style rendering (default: true)</summary>
        public bool UseSwiftlyStyle { get; set; } = true;
        
        /// <summary>Visual separator line character</summary>
        public string GuideLine { get; set; } = "──────────────────────────";
        
        /// <summary>Color for guide lines (hex)</summary>
        public string GuideLineColor { get; set; } = "#FFFFFF";
        
        /// <summary>Navigation arrow/marker symbol</summary>
        public string NavigationPrefix { get; set; } = "►";
        
        /// <summary>Color for navigation marker (hex)</summary>
        public string NavigationMarkerColor { get; set; } = "#FFFFFF";

        #endregion

        #region Title Configuration
        
        /// <summary>Color for title text (hex) - also gradient start color</summary>
        public string TitleColor { get; set; } = "#FFFFFF";
        
        /// <summary>Gradient end color for title (hex). Set to enable gradient effect.</summary>
        public string? TitleGradientEndColor { get; set; } = "#96d5ff";
        
        /// <summary>Hide the title section</summary>
        public bool HideTitle { get; set; } = false;
        
        /// <summary>Show item count [1/10] in title</summary>
        public bool ShowItemCount { get; set; } = true;

        #endregion

        #region Item Colors
        
        /// <summary>Default color for non-selected menu items (hex)</summary>
        public string DefaultItemColor { get; set; } = "#00FF00";
        
        /// <summary>Color for currently selected menu item (hex)</summary>
        public string SelectedItemColor { get; set; } = "#FF69B4";

        #endregion

        #region Footer Configuration
        
        /// <summary>Color for footer keybind labels (hex)</summary>
        public string FooterColor { get; set; } = "#FF0000";
        
        /// <summary>Hide the footer section</summary>
        public bool HideFooter { get; set; } = false;
        
        /// <summary>Optional comment/description shown above footer</summary>
        public string DefaultComment { get; set; } = "";

        #endregion

        #region Methods
        
        public void AddItem(MenuItem item)
        {
            Items.Add(item);
        }

        #endregion
    }
}