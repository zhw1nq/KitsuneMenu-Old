using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;
using Menu.Enums;

namespace Menu
{
	public class MenuConfiguration
	{
		// Menu action constants
		public const ulong BUTTON_NONE = 0UL;
		public const ulong BUTTON_SELECT = (ulong)PlayerButtons.Jump;
		public const ulong BUTTON_BACK = (ulong)PlayerButtons.Speed;
		public const ulong BUTTON_UP = (ulong)PlayerButtons.Forward;
		public const ulong BUTTON_DOWN = (ulong)PlayerButtons.Back;
		public const ulong BUTTON_LEFT = (ulong)PlayerButtons.Moveleft;
		public const ulong BUTTON_RIGHT = (ulong)PlayerButtons.Moveright;
		public const ulong BUTTON_EXIT = 1UL << 33;  // Custom Scoreboard value
		public const ulong BUTTON_INPUT = 1UL << 63; // Special input mode

		// Sound constants
		private const string SOUND_SCROLL = "sounds/ui_sounds/uif/01_default_click_popup.vsnd_c";
		private const string SOUND_CLICK = "sounds/ui_sounds/uif/01_default_select.vsnd_c";
		private const string SOUND_DISABLED = "sounds/ui_sounds/uif/01_default_click_popup_close.vsnd_c";
		private const string SOUND_BACK = "sounds/ui_sounds/uif/01_default_click_popup_close.vsnd_c";
		private const string SOUND_OPEN = "sounds/ui_sounds/uif/10_chanui_vvip_gift_02.vsnd_c";
		private const string SOUND_EXIT = "sounds/ui_sounds/uif/10_chanui_vvip_gift_01.vsnd_c";

		private const string CONFIG_FILE = "menu_config.jsonc";
		private string _configPath = string.Empty;
		private static readonly JsonSerializerOptions _jsonOptions = new()
		{
			WriteIndented = true,
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true,
			PropertyNameCaseInsensitive = true,
		};

		// Button names in config
		public string Select { get; set; } = "Jump";
		public string Back { get; set; } = "Speed";
		public string Up { get; set; } = "Forward";
		public string Down { get; set; } = "Back";
		public string Left { get; set; } = "Moveleft";
		public string Right { get; set; } = "Moveright";
		public string Exit { get; set; } = "Scoreboard";

		// Sound paths in config
		public string OpenSound { get; set; } = SOUND_OPEN;
		public string ScrollSound { get; set; } = SOUND_SCROLL;
		public string ClickSound { get; set; } = SOUND_CLICK;
		public string DisabledSound { get; set; } = SOUND_DISABLED;
		public string BackSound { get; set; } = SOUND_BACK;
		public string ExitSound { get; set; } = SOUND_EXIT;

		// ===== SwiftlyS2 Style Configuration =====
		
		// Title Configuration
		public string TitleColor { get; set; } = "#FFFFFF";
		public string TitleGradientEndColor { get; set; } = "#96d5ff";
		public bool ShowItemCount { get; set; } = true;
		public bool HideTitle { get; set; } = false;
		
		// Guide Line
		public string GuideLine { get; set; } = "──────────────────────────";
		public string GuideLineColor { get; set; } = "#FFFFFF";
		
		// Navigation
		public string NavigationPrefix { get; set; } = "►";
		public string NavigationMarkerColor { get; set; } = "#FFFFFF";
		
		// Item Colors
		public string DefaultItemColor { get; set; } = "#00FF00";
		public string SelectedItemColor { get; set; } = "#FF69B4";
		
		// Footer
		public string FooterColor { get; set; } = "#FF0000";
		public bool HideFooter { get; set; } = false;
		
		// Comment/Branding
		public string DefaultComment { get; set; } = "";

		// Cached button values
		private ulong _selectButton;
		private ulong _backButton;
		private ulong _upButton;
		private ulong _downButton;
		private ulong _leftButton;
		private ulong _rightButton;
		private ulong _exitButton;

		public MenuConfiguration() { }

		public void Initialize()
		{
			_configPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, CONFIG_FILE);
			LoadConfig();
			ParseButtons();
		}

		public void OverrideSelectKey(string buttonName)
		{
			Select = buttonName;
			_selectButton = ParseButtonByName(buttonName);
		}

		private void LoadConfig()
		{
			if (!File.Exists(_configPath))
			{
				CreateDefaultConfig();
				return;
			}

			try
			{
				var jsonContent = File.ReadAllText(_configPath);
				var config = JsonSerializer.Deserialize<MenuConfiguration>(jsonContent, _jsonOptions);

				if (config != null)
				{
					Select = config.Select;
					Back = config.Back;
					Up = config.Up;
					Down = config.Down;
					Left = config.Left;
					Right = config.Right;
					Exit = config.Exit;

					// Load sound configurations
					OpenSound = config.OpenSound ?? SOUND_OPEN;
					ScrollSound = config.ScrollSound ?? SOUND_SCROLL;
					ClickSound = config.ClickSound ?? SOUND_CLICK;
					DisabledSound = config.DisabledSound ?? SOUND_DISABLED;
					BackSound = config.BackSound ?? SOUND_BACK;
					ExitSound = config.ExitSound ?? SOUND_EXIT;

					// Load SwiftlyS2 style configurations
					TitleColor = config.TitleColor ?? "#FFFFFF";
					TitleGradientEndColor = config.TitleGradientEndColor ?? "#96d5ff";
					ShowItemCount = config.ShowItemCount;
					HideTitle = config.HideTitle;
					GuideLine = config.GuideLine ?? "──────────────────────────";
					GuideLineColor = config.GuideLineColor ?? "#FFFFFF";
					NavigationPrefix = config.NavigationPrefix ?? "►";
					NavigationMarkerColor = config.NavigationMarkerColor ?? "#FFFFFF";
					DefaultItemColor = config.DefaultItemColor ?? "#00FF00";
					SelectedItemColor = config.SelectedItemColor ?? "#FF69B4";
					FooterColor = config.FooterColor ?? "#FF0000";
					HideFooter = config.HideFooter;
					DefaultComment = config.DefaultComment ?? "";
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error loading menu configuration: {ex.Message}");
				CreateDefaultConfig();
			}
		}

		private void ParseButtons()
		{
			_selectButton = ParseButtonByName(Select);
			_backButton = ParseButtonByName(Back);
			_upButton = ParseButtonByName(Up);
			_downButton = ParseButtonByName(Down);
			_leftButton = ParseButtonByName(Left);
			_rightButton = ParseButtonByName(Right);
			_exitButton = ParseButtonByName(Exit);
		}

		private static ulong ParseButtonByName(string buttonName)
		{
			switch (buttonName)
			{
				case "Scoreboard":
					return 1UL << 33;
				case "Inspect":
					return 1UL << 35;
			}

			if (Enum.TryParse<PlayerButtons>(buttonName, true, out var button))
			{
				return (ulong)button;
			}

			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine($"Warning: Invalid button name '{buttonName}', falling back to default");
			Console.ResetColor();
			return BUTTON_NONE;
		}

		private void CreateDefaultConfig()
		{
			var configContent = @"{
    /* Available buttons:
        Attack      - Primary attack button
        Jump        - Jump
        Duck        - Crouch
        Forward     - Move forward
        Back        - Move backward
        Use         - Use key
        Cancel      - Cancel action
        Left        - Turn left
        Right       - Turn right
        Moveleft    - Strafe left
        Moveright   - Strafe right
        Attack2     - Secondary attack
        Run         - Run
        Reload      - Reload weapon
        Alt1        - Alternative button 1
        Alt2        - Alternative button 2
        Speed       - Sprint/Fast movement
        Walk        - Walk
        Zoom        - Zoom view
        Weapon1     - Primary weapon
        Weapon2     - Secondary weapon
        Bullrush    - Bullrush
        Grenade1    - First grenade
        Grenade2    - Second grenade
        Attack3     - Third attack
        Scoreboard  - Show scoreboard (TAB)
		Inspect     - Inspect weapon (F)
    */

	// !!! To apply changes, restart the server as CSS only reload shared things when restarted !!!

    // ===== BUTTON CONFIGURATION =====
    ""Select"": ""Jump"",
    ""Back"": ""Speed"",
    ""Up"": ""Forward"",
    ""Down"": ""Back"",
    ""Left"": ""Moveleft"",
    ""Right"": ""Moveright"",
    ""Exit"": ""Scoreboard"",

    // ===== SOUND CONFIGURATION =====
    ""OpenSound"": ""sounds/ui_sounds/uif/01_default_click_popup.vsnd_c"",
    ""ScrollSound"": ""sounds/ui_sounds/uif/01_default_click_popup.vsnd_c"",
    ""ClickSound"": ""sounds/ui_sounds/uif/01_default_select.vsnd_c"",
    ""DisabledSound"": ""sounds/ui_sounds/uif/01_default_click_popup_close.vsnd_c"",
    ""BackSound"": ""sounds/ui_sounds/uif/01_default_click_popup_close.vsnd_c"",
    ""ExitSound"": ""sounds/ui_sounds/uif/10_chanui_vvip_gift_01.vsnd_c"",

    // ===== SWIFTLYS2 STYLE CONFIGURATION =====
    
    // Title settings
    ""TitleColor"": ""#FFFFFF"",
    ""TitleGradientEndColor"": ""#96d5ff"",
    ""ShowItemCount"": true,
    ""HideTitle"": false,
    
    // Guide line (visual separator)
    ""GuideLine"": ""──────────────────────────"",
    ""GuideLineColor"": ""#FFFFFF"",
    
    // Navigation marker
    ""NavigationPrefix"": ""►"",
    ""NavigationMarkerColor"": ""#FFFFFF"",
    
    // Item colors
    ""DefaultItemColor"": ""#00FF00"",
    ""SelectedItemColor"": ""#FF69B4"",
    
    // Footer settings
    ""FooterColor"": ""#FF0000"",
    ""HideFooter"": false,
    
    // Comment/Branding (leave empty for no comment)
    ""DefaultComment"": """"
}";

			try
			{
				File.WriteAllText(_configPath, configContent);
				LoadConfig();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error creating default configuration: {ex.Message}");
			}
		}

		public void SaveConfig()
		{
			try
			{
				var jsonContent = JsonSerializer.Serialize(this, _jsonOptions);
				File.WriteAllText(_configPath, jsonContent);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error saving menu configuration: {ex.Message}");
			}
		}

		// Getter methods for buttons
		public MenuButtons GetSelectButton() => (MenuButtons)_selectButton;
		public MenuButtons GetBackButton() => (MenuButtons)_backButton;
		public MenuButtons GetUpButton() => (MenuButtons)_upButton;
		public MenuButtons GetDownButton() => (MenuButtons)_downButton;
		public MenuButtons GetLeftButton() => (MenuButtons)_leftButton;
		public MenuButtons GetRightButton() => (MenuButtons)_rightButton;
		public MenuButtons GetExitButton() => (MenuButtons)_exitButton;
		public MenuButtons GetInputButton() => (MenuButtons)BUTTON_INPUT;

		// Getter methods for sounds
		public string GetOpenSound() => OpenSound;
		public string GetScrollSound() => ScrollSound;
		public string GetClickSound() => ClickSound;
		public string GetDisabledSound() => DisabledSound;
		public string GetBackSound() => BackSound;
		public string GetExitSound() => ExitSound;

		public MenuButtons GetButtonValue(MenuButtons button)
		{
			if ((ulong)button == _selectButton)
				return MenuButtons.Select;
			if ((ulong)button == _backButton)
				return MenuButtons.Back;
			if ((ulong)button == _upButton)
				return MenuButtons.Up;
			if ((ulong)button == _downButton)
				return MenuButtons.Down;
			if ((ulong)button == _leftButton)
				return MenuButtons.Left;
			if ((ulong)button == _rightButton)
				return MenuButtons.Right;
			if ((ulong)button == _exitButton)
				return MenuButtons.Exit;
			if ((ulong)button == BUTTON_INPUT)
				return MenuButtons.Input;
			return MenuButtons.None;
		}
	}
}