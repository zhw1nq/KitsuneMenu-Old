using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Config;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Menu.Enums;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Menu
{
    public sealed partial class KitsuneMenu
    {
        private static BasePlugin _plugin = null!;
        private static readonly ConcurrentDictionary<CCSPlayerController, Stack<MenuBase>> Menus = new();
        private static readonly SayEvent OnSay = new("say", OnSayEvent);
        private static readonly SayEvent OnSayTeam = new("say_team", OnSayEvent);
        private static readonly OnTick OnTick = new(OnTickListener);
        public static event EventHandler<MenuEvent>? OnDrawMenu;

        private static readonly ConcurrentDictionary<CCSPlayerController, (MenuButtons Button, DateTime LastPress, int RepeatCount)> ButtonHoldState = new();
        private static readonly ConcurrentDictionary<CCSPlayerController, MenuButtons> LastButtonState = new();
        private static readonly ConcurrentDictionary<CCSPlayerController, (int ObserverMode, bool BlockNext)> ObserverMode = new();
        private static readonly HashSet<CCSPlayerController> FrozenPlayers = [];
        private static readonly HashSet<CCSPlayerController> PendingFreeze = [];

        private const float InitialDelay = 0.5f;
        private const float RepeatDelay = 0.1f;

        private static MenuConfiguration _config = null!;
        private static MenuTranslator Translator = null!;
        private static bool MultiCast = true;

        private static readonly ConVar? mp_forcecamera = null;

        public KitsuneMenu(BasePlugin plugin, bool multiCast = false)
        {
            _plugin = plugin;

            if (!MultiCast && multiCast)
                MultiCast = multiCast;

            _config = new MenuConfiguration();
            _config.Initialize();

            Translator = new MenuTranslator();
            Translator.Initialize();

            _plugin.RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawnListener);

            if (mp_forcecamera?.GetPrimitiveValue<int>() > 0 && _config.GetSelectButton() == (MenuButtons)PlayerButtons.Jump)
            {
                _plugin.Logger.LogWarning("It is highly recommended to use a different button for the select button when mp_forcecamera is enabled.");
                _plugin.Logger.LogInformation("When a player is dead, they are switched to third person with forcecamera to fix a CS2 button hooking issue and it may be used as exploit for infos.");
            }
        }

        private static HookResult OnSayEvent(CCSPlayerController? controller, string message)
        {
            if (controller == null || !controller.IsValid || !Menus.TryGetValue(controller, out var value))
                return HookResult.Continue;

            var menu = value.Peek();

            if (!menu.AcceptInput)
                return HookResult.Continue;

            var selectedItem = menu.Items[menu.Option];
            if (selectedItem != null)
            {
                selectedItem.DataString = message;
                menu.AcceptInput = false;

                menu.Callback?.Invoke(_config.GetInputButton(), menu, selectedItem);
            }
            return HookResult.Handled;
        }

        private static HookResult OnPlayerSpawnListener(EventPlayerSpawn @event, GameEventInfo info)
        {
            var controller = @event.Userid;
            if (controller == null || !controller.IsValid)
                return HookResult.Continue;

            UpdatePlayerFreeze(controller);
            return HookResult.Continue;
        }

        private static void OnTickListener()
        {
            var currentTime = DateTime.Now;
            var playersToRemove = new List<CCSPlayerController>();

            foreach (var kvp in Menus)
            {
                var controller = kvp.Key;
                var menus = kvp.Value;

                if (!IsPlayerValid(controller) || menus.Count == 0)
                {
                    CleanupPlayer(controller);
                    playersToRemove.Add(controller);
                    continue;
                }

                UpdatePlayerFreeze(controller);

                var menu = menus.Peek();
                var currentButtons = GetCurrentButtons(controller);

                MenuItem? selectedItem = null;
                if (menu.Items.Count > 0 && menu.Option >= 0 && menu.Option < menu.Items.Count)
                {
                    selectedItem = menu.Items[menu.Option];
                }

                HandleButtonPress(controller, currentButtons, menu, selectedItem, currentTime);
                DrawMenu(controller, menu, selectedItem);
                RaiseDrawMenu(controller, menu, selectedItem);
            }

            foreach (var player in playersToRemove)
            {
                Menus.TryRemove(player, out _);
                UpdatePlayerFreeze(player);
            }
        }

        private static bool IsPlayerValid(CCSPlayerController controller)
        {
            return controller.IsValid && controller.PlayerPawn.IsValid && controller.Connected == PlayerConnectedState.PlayerConnected;
        }

        private static void CleanupPlayer(CCSPlayerController controller)
        {
            ObserverMode.TryRemove(controller, out _);
            LastButtonState.TryRemove(controller, out _);
        }

        private static void HandleButtonPress(CCSPlayerController controller, MenuButtons currentButtons, MenuBase menu, MenuItem? selectedItem, DateTime currentTime)
        {
            if (!LastButtonState.TryGetValue(controller, out var lastButtons))
            {
                lastButtons = 0;
            }

            bool buttonHandled = false;

            if (currentButtons != lastButtons)
            {
                if (currentButtons != 0)
                {
                    buttonHandled = HandleMenuButton(currentButtons, menu, selectedItem, controller);
                    ButtonHoldState[controller] = (currentButtons, currentTime, 0);
                }
                else
                {
                    ButtonHoldState.TryRemove(controller, out _);
                }
            }
            else if (currentButtons != 0)
            {
                if (menu.RepeatedButtons && ButtonHoldState.TryGetValue(controller, out var holdState))
                {
                    var elapsed = (currentTime - holdState.LastPress).TotalSeconds;
                    if (elapsed >= InitialDelay)
                    {
                        var repeatCount = (int)((elapsed - InitialDelay) / RepeatDelay);
                        if (repeatCount > holdState.RepeatCount)
                        {
                            buttonHandled = HandleMenuButton(currentButtons, menu, selectedItem, controller);
                            ButtonHoldState[controller] = (holdState.Button, holdState.LastPress, repeatCount);
                        }
                    }
                }
            }

            LastButtonState[controller] = currentButtons;
            menu.AcceptButtons = !buttonHandled;
        }

        private static void PlayMenuSound(CCSPlayerController player, string soundPath)
        {
            if (player?.IsValid == true)
            {
                player.ExecuteClientCommand($"play {soundPath}");
            }
        }

        private static bool CheckButton(MenuButtons buttons, MenuButtons targetButton)
        {
            if (MultiCast)
                return (buttons & targetButton) == targetButton;
            return buttons == targetButton;
        }

        private static bool HandleMenuButton(MenuButtons buttons, MenuBase menu, MenuItem? selectedItem, CCSPlayerController controller)
        {
            if (CheckButton(buttons, _config.GetSelectButton()))
            {
                PlayMenuSound(controller, _config.GetClickSound());

                if (selectedItem == null) return false;
                switch (selectedItem.Type)
                {
                    case MenuItemType.Bool:
                        if (selectedItem.Data.Length > 0)
                        {
                            selectedItem.Data[0] = selectedItem.Data[0] == 0 ? 1 : 0;
                        }
                        break;

                    case MenuItemType.ChoiceBool:
                        if (selectedItem.Data.Length > selectedItem.Option)
                        {
                            selectedItem.Data[selectedItem.Option] = selectedItem.Data[selectedItem.Option] == 0 ? 1 : 0;
                        }
                        break;

                    case MenuItemType.Input:
                        menu.AcceptInput = true;
                        break;
                }
                menu.Callback?.Invoke(_config.GetSelectButton(), menu, selectedItem);
                return true;
            }

            if (CheckButton(buttons, _config.GetUpButton()) || CheckButton(buttons, _config.GetDownButton()))
            {
                PlayMenuSound(controller, _config.GetScrollSound());

                if (!menu.AcceptInput)
                {
                    List<int> selectableValues = menu.Items.Any(i => i.Type is not (MenuItemType.Spacer or MenuItemType.Text)) ? menu.Items.Where(i => i.Type is not (MenuItemType.Spacer or MenuItemType.Text)).Select(i => menu.Items.IndexOf(i)).ToList() : menu.Items.Select((item, index) => index).ToList();

                    if (selectableValues.Count > 0)
                    {
                        int currentOption = selectableValues.IndexOf(menu.Option);

                        if (CheckButton(buttons, _config.GetUpButton()))
                        {
                            if (currentOption != 0)
                                menu.Option = selectableValues[currentOption - 1];

                            menu.Callback?.Invoke(_config.GetUpButton(), menu, selectedItem);
                        }
                        else
                        {
                            if (currentOption != selectableValues.Count - 1)
                                menu.Option = selectableValues[currentOption + 1];

                            menu.Callback?.Invoke(_config.GetDownButton(), menu, selectedItem);
                        }
                    }
                }
                return true;
            }

            if ((CheckButton(buttons, _config.GetLeftButton()) || CheckButton(buttons, _config.GetRightButton())) && selectedItem != null && !menu.AcceptInput)
            {
                return true;
            }

            if (CheckButton(buttons, _config.GetBackButton()))
            {
                PlayMenuSound(controller, _config.GetBackSound());

                if (menu.AcceptInput)
                {
                    menu.AcceptInput = false;
                }
                else if (Menus.TryGetValue(controller, out var menuStack) && menuStack.Count > 1)
                {
                    menu.Callback?.Invoke(_config.GetBackButton(), menu, null);
                    menuStack.Pop();
                }
                return false;
            }

            if (CheckButton(buttons, _config.GetExitButton()))
            {
                PlayMenuSound(controller, _config.GetExitSound());

                menu.Callback?.Invoke(_config.GetExitButton(), menu, null);

                ObserverMode.TryRemove(controller, out _);
                Menus.TryRemove(controller, out _);

                if (controller.IsValid)
                {
                    controller.PrintToCenterHtml(" ");
                }

                InitiatePlayerFreeze(controller, false);
                return false;
            }

            return false;
        }
        private static void RaiseDrawMenu(CCSPlayerController controller, MenuBase menu, MenuItem? selectedItem)
        {
            OnDrawMenu?.Invoke(null, new MenuEvent(controller, menu, selectedItem, 0));
        }

        public static void DrawMenu(CCSPlayerController controller, MenuBase menu, MenuItem? selectedItem)
        {
            if (!Menus.TryGetValue(controller, out var menus))
                return;

            // Use SwiftlyS2-style rendering if enabled
            if (menu.UseSwiftlyStyle)
            {
                DrawMenuSwiftlyStyle(controller, menu, selectedItem);
                return;
            }

            // Legacy style rendering
            var html = "";
            html += $"\u00A0{menu.Title}";

            bool hasSelectableItems = menu.Items.Any(item => item.Type is not (MenuItemType.Spacer or MenuItemType.Text));

            foreach (var menuItem in menu.Items)
            {
                html += $"<br>\u00A0{menu.Title.Suffix}";

                if (hasSelectableItems && selectedItem != null && menuItem == selectedItem)
                {
                    html += menu.Cursor[(int)MenuCursor.Left];
                }

                if (menuItem.Head != null)
                    html += menuItem.Head;

                switch (menuItem.Type)
                {
                    case MenuItemType.Choice or MenuItemType.ChoiceBool or MenuItemType.Button:
                        html += FormatValues(menu, menuItem, selectedItem!);
                        break;

                    case MenuItemType.Slider:
                        html += FormatSlider(menu, menuItem);
                        break;

                    case MenuItemType.Input:
                        html += FormatInput(menu, menuItem, selectedItem!);
                        break;

                    case MenuItemType.Bool:
                        html += FormatBool(menu, menuItem);
                        break;
                }

                if (menuItem.Tail != null)
                    html += menuItem.Tail;

                if (hasSelectableItems && selectedItem != null && menuItem == selectedItem)
                    html += menu.Cursor[(int)MenuCursor.Right];
            }

            controller.PrintToCenterHtml(html);
        }

        /// <summary>
        /// Renders menu using SwiftlyS2-style layout with gradient title, colored items, and footer.
        /// </summary>
        public static void DrawMenuSwiftlyStyle(CCSPlayerController controller, MenuBase menu, MenuItem? selectedItem)
        {
            if (!Menus.TryGetValue(controller, out var menus))
                return;

            var html = new System.Text.StringBuilder();

            // Configuration
            var guideLineColor = menu.GuideLineColor;
            var navigationColor = menu.NavigationMarkerColor;
            var footerColor = menu.FooterColor;
            var titleColor = menu.TitleColor;
            var guideLine = $"<font class='fontSize-s' color='{guideLineColor}'>{menu.GuideLine}</font>";

            // Calculate item counts
            var selectableItems = menu.Items.Where(i => i.Type is not (MenuItemType.Spacer or MenuItemType.Text)).ToList();
            int currentIndex = menu.CurrentGlobalIndex > 0 ? menu.CurrentGlobalIndex : (selectedItem != null ? selectableItems.IndexOf(selectedItem) + 1 : 0);
            int totalItems = menu.TotalSelectableItems > 0 ? menu.TotalSelectableItems : selectableItems.Count;
            bool hasSelectableItems = totalItems > 0;

            // ===== TITLE SECTION =====
            if (!menu.HideTitle)
            {
                var titleText = menu.Title.Value ?? "Menu";
                // Green → Aqua → White gradient
                html.Append($"<font class='fontSize-m'>{HtmlGradient.GenerateThreeColorGradient(titleText, "#00FF00", "#00FFFF", "#FFFFFF")}</font>");

                if (menu.ShowItemCount && totalItems > 0)
                {
                    html.Append($"<font class='fontSize-s' color='#888888'> [{currentIndex}/{totalItems}]</font>");
                }
                html.Append("<br>").Append(guideLine).Append("<br>");
            }

            // ===== MENU ITEMS =====
            foreach (var menuItem in menu.Items)
            {
                // Skip spacer and text items
                if (menuItem.Type is MenuItemType.Spacer or MenuItemType.Text)
                    continue;

                bool isSelected = hasSelectableItems && selectedItem != null && menuItem == selectedItem;

                // Navigation marker
                if (isSelected)
                {
                    html.Append($"<font color='{navigationColor}' class='fontSize-sm'>{menu.NavigationPrefix} </font>");
                }
                else
                {
                    html.Append("\u00A0\u00A0\u00A0 ");
                }

                // Item content with color - white for non-selected, gradient for selected
                if (isSelected)
                {
                    // Get item text and apply gradient with smaller font
                    var selectedItemText = GetMenuItemText(menu, menuItem, selectedItem);
                    html.Append("<font class='fontSize-sm'>");
                    html.Append(HtmlGradient.GenerateGradientText(selectedItemText, "#FFFFFF", "#FF69B4"));
                    html.Append("</font>");
                    if (menuItem.Tail != null)
                        html.Append(menuItem.Tail);
                    html.Append("<br>");
                    continue;
                }

                // Use scrolled text for non-selected items too
                var itemText = GetMenuItemText(menu, menuItem, selectedItem);
                html.Append($"<font color='#FFFFFF' class='fontSize-sm'>{itemText}</font>");

                if (menuItem.Tail != null)
                    html.Append(menuItem.Tail);

                html.Append("<br>");
            }

            // ===== BRANDING SECTION =====
            html.Append(guideLine).Append("<br>");
            html.Append($"<font class='fontSize-s'>Powered by {HtmlGradient.GenerateGradientText("zhw1nq", "#00FFFF", "#FFFFFF")}</font><br>");

            // Show custom comment if set
            if (!string.IsNullOrWhiteSpace(menu.DefaultComment))
            {
                html.Append($"<font class='fontSize-s' color='#FFFFFF'>{menu.DefaultComment}</font><br>");
            }

            // ===== FOOTER SECTION =====
            if (!menu.HideFooter)
            {
                html.Append($"<font class='fontSize-s' color='#FFFFFF'>");
                html.Append($"<font color='#FF69B4'>Move:</font> {GetKeyName(_config.Up)}/{GetKeyName(_config.Down)}");
                html.Append($" | <font color='#FF69B4'>Use:</font> {GetKeyName(_config.Select)}");
                html.Append($" | <font color='#FF69B4'>Back:</font> {GetKeyName(_config.Back)}");
                html.Append($" | <font color='#FF69B4'>Exit:</font> {GetKeyName(_config.Exit)}");
                html.Append("</font>");
            }

            controller.PrintToCenterHtml(html.ToString());
        }

        /// <summary>
        /// Converts button name to actual key name for display.
        /// </summary>
        private static string GetKeyName(string buttonName)
        {
            return buttonName switch
            {
                "Forward" => "W",
                "Back" => "S",
                "Moveleft" => "A",
                "Moveright" => "D",
                "Jump" => "SPACE",
                "Duck" => "CTRL",
                "Speed" => "SHIFT",
                "Use" => "E",
                "Reload" => "R",
                "Scoreboard" => "TAB",
                "Inspect" => "F",
                "Attack" => "MOUSE1",
                "Attack2" => "MOUSE2",
                _ => buttonName.ToUpper()
            };
        }

        /// <summary>
        /// Gets the plain text content of a menu item for gradient rendering.
        /// Applies scrolling animation if text is too long.
        /// </summary>
        private static string GetMenuItemText(MenuBase menu, MenuItem menuItem, MenuItem? selectedItem)
        {
            var text = "";

            if (menuItem.Head != null)
            {
                // Strip HTML tags from head for plain text
                text += System.Text.RegularExpressions.Regex.Replace(menuItem.Head.ToString() ?? "", "<.*?>", "");
            }

            text += menuItem.Type switch
            {
                MenuItemType.Choice or MenuItemType.ChoiceBool or MenuItemType.Button
                    => GetPlainTextFromValues(menuItem),
                MenuItemType.Slider => GetPlainTextFromSlider(menuItem),
                MenuItemType.Input => menuItem.DataString ?? "",
                MenuItemType.Bool => menuItem.Data[0] == 1 ? "✔" : "✘",
                _ => ""
            };

            // Apply text scrolling for long text (max 25 chars)
            return TextScroller.GetScrolledText(text, 25);
        }

        private static string GetPlainTextFromValues(MenuItem menuItem)
        {
            if (menuItem.Values == null || menuItem.Values.Count == 0)
                return "";

            var value = menuItem.Values[menuItem.Option];
            var rawText = value is MenuValue mv ? (mv.Value ?? "") : (value?.ToString() ?? "");
            // Strip HTML tags
            return System.Text.RegularExpressions.Regex.Replace(rawText, "<.*?>", "");
        }

        private static string GetPlainTextFromSlider(MenuItem menuItem)
        {
            return $"({new string('-', menuItem.Option)}|{new string('-', 10 - menuItem.Option)})";
        }

        private static string FormatValues(MenuBase menu, MenuItem menuItem, MenuItem selectedItem)
        {
            var html = "";

            if (menuItem.Pinwheel)
            {
                var prev = menuItem.Option - 1;
                var next = menuItem.Option + 1;

                if (prev < 0)
                    prev = menuItem.Values!.Count - 1;

                if (next > menuItem.Values!.Count - 1)
                    next = 0;

                html += $"{FormatString(menu, menuItem, prev)} ";
                html += $"{FormatSelector(menu, menuItem, selectedItem, MenuCursor.Left)}{FormatString(menu, menuItem, menuItem.Option)}{FormatSelector(menu, menuItem, selectedItem, MenuCursor.Right)}";
                html += $" {FormatString(menu, menuItem, next)}";

                return html;
            }

            if (menuItem.Option == 0)
            {
                html += $"{FormatSelector(menu, menuItem, selectedItem, MenuCursor.Left)}{FormatString(menu, menuItem, 0)}{FormatSelector(menu, menuItem, selectedItem, MenuCursor.Right)}";

                for (var i = 0; i < 2 && i < menuItem.Values!.Count - 1; i++)
                    html += $" {FormatString(menu, menuItem, i + 1)}";
            }
            else if (menuItem.Option == menuItem.Values!.Count - 1)
            {
                for (var i = 2; i > 0; i--)
                {
                    if (menuItem.Option - i >= 0)
                        html += $"{FormatString(menu, menuItem, menuItem.Option - i)} ";
                }

                html += $"{FormatSelector(menu, menuItem, selectedItem, MenuCursor.Left)}{FormatString(menu, menuItem, menuItem.Option)}{FormatSelector(menu, menuItem, selectedItem, MenuCursor.Right)}";
            }
            else
                html += $"{FormatString(menu, menuItem, menuItem.Option - 1)} {FormatSelector(menu, menuItem, selectedItem, MenuCursor.Left)}{FormatString(menu, menuItem, menuItem.Option)}{FormatSelector(menu, menuItem, selectedItem, MenuCursor.Right)} {FormatString(menu, menuItem, menuItem.Option + 1)}";

            return html;
        }

        private static string FormatString(MenuBase menu, MenuItem menuItem, int index)
        {
            if (menuItem.Values == null)
                return "";

            var menuValue = menuItem.Values[index];

            if (menuItem.Type != MenuItemType.ChoiceBool)
                return menuValue.ToString();

            menuValue.Prefix = menuItem.Data[index] == 0 ? menu.Bool[(int)MenuBool.False].Prefix : menu.Bool[(int)MenuBool.True].Prefix;
            menuValue.Suffix = menuItem.Data[index] == 0 ? menu.Bool[(int)MenuBool.False].Suffix : menu.Bool[(int)MenuBool.True].Suffix;

            return menuValue.ToString();
        }

        private static string FormatSelector(MenuBase menu, MenuItem menuItem, MenuItem selectedItem, MenuCursor selector)
        {
            if (menuItem.Type is MenuItemType.Button or MenuItemType.ChoiceBool && menuItem != selectedItem)
                return "";

            return menu.Selector[(int)selector].ToString();
        }

        private static string FormatSlider(MenuBase menu, MenuItem menuItem)
        {
            var html = "";

            html += menu.Slider[(int)MenuSlider.Left].ToString();

            for (var i = 0; i < 11; i++)
                html += $"{(i == menuItem.Data[0] ? menu.Slider[(int)MenuSlider.Selected] : menu.Slider[(int)MenuSlider.Spacer])}{(i != 10 ? " " : "")}";

            html += menu.Slider[(int)MenuSlider.Right].ToString();

            return html;
        }

        private static string FormatInput(MenuBase menu, MenuItem menuItem, MenuItem selectedItem)
        {
            var html = "";

            if (menu.AcceptInput && menuItem == selectedItem)
                html += menu.Selector[(int)MenuCursor.Left].ToString();

            if (menuItem.DataString.Length == 0)
                html += menu.Input.Value;
            else
                html += $"{menu.Input.Prefix}{menuItem.DataString}{menu.Input.Suffix}";

            if (menu.AcceptInput && menuItem == selectedItem)
                html += menu.Selector[(int)MenuCursor.Right].ToString();

            return html;
        }

        private static string FormatBool(MenuBase menu, MenuItem menuItem)
        {
            return menuItem.Data[0] == 0 ? menu.Bool[(int)MenuBool.False].ToString() : menu.Bool[(int)MenuBool.True].ToString();
        }

        private static void _ShowMenu(CCSPlayerController controller, MenuBase menu, Action<MenuButtons, MenuBase, MenuItem?> callback, bool isSubmenu = false, bool freezePlayer = false)
        {
            // Play open sound
            PlayMenuSound(controller, _config.GetOpenSound());

            var menus = Menus.GetOrAdd(controller, _ => new Stack<MenuBase>());

            menu.Callback = callback;
            menu.RequiresFreeze = freezePlayer;

            if (!isSubmenu)
            {
                menus.Clear();
            }

            List<MenuItem> filterList = menu.Items.Any(item => item.Type is not (MenuItemType.Spacer or MenuItemType.Text)) ? [.. menu.Items.Where(item => item.Type is not (MenuItemType.Spacer or MenuItemType.Text))] : menu.Items;

            menu.Option = filterList.Count > 0 ? menu.Items.IndexOf(filterList.First()) : 0;

            menus.Push(menu);

            InitiatePlayerFreeze(controller, freezePlayer);
        }

        public void ShowMenu(CCSPlayerController controller, MenuBase menu, Action<MenuButtons, MenuBase, MenuItem?> callback, bool isSubmenu = false, bool freezePlayer = false)
        {
            _ShowMenu(controller, menu, (buttons, menu, item) =>
            {
                callback(_config.GetButtonValue(buttons), menu, item);
            }, isSubmenu, freezePlayer);
        }

        public void ShowScrollableMenu(CCSPlayerController controller, string title, List<MenuItem> items, Action<MenuButtons, MenuBase, MenuItem?>? callback, bool isSubmenu = false, bool freezePlayer = false, int visibleItems = 5, Dictionary<int, object>? defaultValues = null, bool disableDeveloper = false)
        {
            // Force max 4 visible items for SwiftlyS2 style
            visibleItems = Math.Min(visibleItems, 4);

            MenuBase menu = null!;
            List<MenuItem> allItems = [.. items];
            List<MenuItem> filterList = items.Any(item => item.Type is not (MenuItemType.Spacer or MenuItemType.Text)) ? items.Where(item => item.Type is not (MenuItemType.Spacer or MenuItemType.Text)).ToList() : allItems;
            int currentIndex = filterList.Count > 0 ? allItems.IndexOf(filterList.First()) : 0;
            int startIndex = 0;

            void wrappedCallback(MenuButtons button, MenuBase m, MenuItem? selected)
            {
                if (allItems.Count == 0)
                    return;

                if (button == _config.GetDownButton())
                {
                    int currentRow = filterList.IndexOf(allItems[currentIndex]);

                    if (currentRow + 1 < filterList.Count)
                    {
                        int nextSelectableIndex = allItems.IndexOf(filterList[currentRow + 1]) - allItems.IndexOf(filterList[currentRow]);

                        if (currentIndex < allItems.Count - nextSelectableIndex)
                        {
                            currentIndex += nextSelectableIndex;

                            if (currentIndex >= startIndex + visibleItems)
                            {
                                startIndex = Math.Min(startIndex + nextSelectableIndex, allItems.Count - visibleItems);
                            }
                            UpdateMenuView();
                        }
                    }
                }
                else if (button == _config.GetUpButton())
                {
                    int currentRow = filterList.IndexOf(allItems[currentIndex]);

                    if (currentRow > 0)
                    {
                        int prevSelectableIndex = allItems.IndexOf(filterList[currentRow]) - allItems.IndexOf(filterList[currentRow - 1]);

                        currentIndex -= prevSelectableIndex;

                        if (currentIndex < startIndex)
                        {
                            startIndex = Math.Max(0, startIndex - prevSelectableIndex);
                        }
                        UpdateMenuView();
                    }
                }
                else
                {
                    if (filterList.Count > 0)
                    {
                        if (currentIndex >= 0 && currentIndex < allItems.Count)
                        {
                            int preservedIndex = menu.Option;

                            MenuItem? selectedItem = allItems[currentIndex];
                            menu.Option = currentIndex;

                            if (selected?.Values != null &&
                                selected.Values.Count > selected.Option &&
                                selected.Values[selected.Option] is MenuButtonCallback customButton)
                            {
                                if (callback is null)
                                {
                                    customButton.Callback?.Invoke(controller, customButton.Data);
                                }
                                else
                                {
                                    callback(_config.GetButtonValue(button), m, selectedItem);
                                }
                            }
                            else
                            {
                                callback?.Invoke(_config.GetButtonValue(button), m, selectedItem);
                            }

                            menu.Option = preservedIndex;
                        }
                    }
                    else
                    {
                        if (selected?.Values != null &&
                            selected.Values.Count > selected.Option &&
                            selected.Values[selected.Option] is MenuButtonCallback customButton)
                        {
                            if (callback is null)
                            {
                                customButton.Callback?.Invoke(controller, customButton.Data);
                            }
                            else
                            {
                                callback(_config.GetButtonValue(button), m, null);
                            }
                        }
                        else
                        {
                            callback?.Invoke(_config.GetButtonValue(button), m, null);
                        }
                    }
                }
            }

            void CreateMenu()
            {
                // Start with plain title - SwiftlyStyle handles formatting separately
                menu = new MenuBase(new MenuValue(title))
                {
                    Option = currentIndex,
                };

                UpdateMenuView();

                if (defaultValues != null)
                {
                    foreach (var kvp in defaultValues)
                    {
                        int index = kvp.Key;
                        object value = kvp.Value;

                        if (index >= 0 && index < items.Count)
                        {
                            if (value is bool boolValue)
                            {
                                items[index].Data[0] = boolValue ? 1 : 0;
                            }
                            else if (value is string stringValue)
                            {
                                items[index].DataString = stringValue;
                            }
                            else if (value is int intValue)
                            {
                                items[index].DataString = intValue.ToString();
                            }
                        }
                    }
                }

                _ShowMenu(controller, menu, wrappedCallback, isSubmenu, freezePlayer);
            }

            void UpdateMenuView()
            {
                menu.Items.Clear();

                // Use plain title for SwiftlyStyle (it handles item count separately with proper formatting)
                if (menu.UseSwiftlyStyle)
                {
                    menu.Title = new MenuValue(title);
                    menu.TotalSelectableItems = filterList.Count;
                    menu.CurrentGlobalIndex = filterList.Count > 0 ? filterList.IndexOf(allItems[currentIndex]) + 1 : 0;
                }
                else
                {
                    menu.Title = new MenuValue($"{title}{(allItems.Count > visibleItems ? $" <font class=\"fontSize-s\" color=\"#FFFFFF\">{Translator.GetTranslation("Items")} {filterList.IndexOf(allItems[currentIndex]) + 1}/{filterList.Count}</font>" : "")}")
                    {
                        Prefix = "<font class=\"fontSize-m\" color=\"#ff3333\">",
                        Suffix = "<font color=\"#FFFFFF\" class=\"fontSize-sm\">"
                    };
                }

                int visibleCount = 0;
                for (int i = startIndex; i < allItems.Count && visibleCount < visibleItems; i++)
                {
                    var item = allItems[i];
                    if (item.Values?.Count > 0 && item.Values[0] is MenuValue menuValue)
                    {
                        menuValue.Prefix = menuValue.OriginalPrefix ?? "";
                        menuValue.Suffix = menuValue.OriginalSuffix ?? "";

                        bool isDisabled = menuValue is MenuButtonCallback customButton && customButton.Disabled;
                        if (isDisabled)
                        {
                            menuValue.Prefix += "<font color=\"#8f3b3b\">";
                            menuValue.Suffix = "</font>" + menuValue.Suffix;
                        }
                    }

                    menu.AddItem(item);
                    visibleCount++;
                }

                if (filterList.Count == 0)
                {
                    menu.AddItem(new MenuItem(MenuItemType.Text, new MenuValue(Translator.GetTranslation("EmptyMenu")) { Prefix = "<font color=\"#8f3b3b\" class=\"fontSize-m\">", Suffix = "<font color=\"#FFFFFF\">" }));
                }

                menu.AddItem(new MenuItem(MenuItemType.Spacer));

                // Only add footer items if NOT using SwiftlyStyle (SwiftlyStyle has its own footer)
                if (!menu.UseSwiftlyStyle)
                {
                    // if (!isSubmenu && !disableDeveloper)
                    menu.AddItem(new MenuItem(MenuItemType.Text, new MenuValue($"Developed by <font color=\"#ffc0cb\">zhw1nq</font>") { Prefix = "<font color=\"#FFFFFF\" class=\"fontSize-s\">", Suffix = "</font>" }));

                    menu.AddItem(new MenuItem(MenuItemType.Text, new MenuValue(isSubmenu ? Translator.GetTranslation("FooterSubMenu") : Translator.GetTranslation("FooterMain")) { Prefix = "<font color=\"#ff3333\" class=\"fontSize-s\">", Suffix = "<font color=\"#FFFFFF\">" }));
                }

                MenuItem? selectedItem = null;
                if (filterList.Count > 0)
                {
                    selectedItem = allItems[currentIndex];
                    menu.Option = menu.Items.IndexOf(selectedItem);
                }
                else
                {
                    menu.Option = 0;
                }

                DrawMenu(controller, menu, selectedItem);
            }

            CreateMenu();
        }
        private static MenuButtons GetCurrentButtons(CCSPlayerController controller)
        {
            var currentButtons = (MenuButtons)controller.Buttons;

            MenuButtons observerButtons = 0;
            if ((ulong)_config.GetSelectButton() == (ulong)PlayerButtons.Jump)
                observerButtons = HandleObserverMode(controller);

            return observerButtons != 0 ? observerButtons : currentButtons;
        }

        public void ClearMenus(CCSPlayerController controller)
        {
            if (Menus.TryRemove(controller, out _))
            {
                UpdatePlayerFreeze(controller);
            }
        }

        public void PopMenu(CCSPlayerController controller, MenuBase? menu = null)
        {
            if (!Menus.TryGetValue(controller, out var value))
                return;

            if (menu != null && value.Peek() != menu)
                return;

            value.Pop();

            if (value.Count == 0)
            {
                Menus.TryRemove(controller, out _);
                InitiatePlayerFreeze(controller, false);
            }
            else
            {
                UpdatePlayerFreeze(controller);
            }
        }

        public bool IsCurrentMenu(CCSPlayerController controller, MenuBase menu)
        {
            if (!Menus.TryGetValue(controller, out var value))
                return false;

            return value.Peek() == menu;
        }

        public Stack<MenuBase>? GetMenus(CCSPlayerController controller)
        {
            return Menus.TryGetValue(controller, out var value) ? value : null;
        }

        public void SetMenus(CCSPlayerController controller, Stack<MenuBase> menus)
        {
            Menus[controller] = menus;
            UpdatePlayerFreeze(controller);
        }

        private static void InitiatePlayerFreeze(CCSPlayerController controller, bool shouldFreeze)
        {
            if (controller.IsValid && controller.PlayerPawn.IsValid)
            {
                if (shouldFreeze && !FrozenPlayers.Contains(controller))
                {
                    PendingFreeze.Add(controller);
                }
                else if (FrozenPlayers.Remove(controller) || PendingFreeze.Remove(controller))
                {
                    Freeze(controller, false);
                }
            }
            else
            {
                FrozenPlayers.Remove(controller);
                PendingFreeze.Remove(controller);
            }
        }

        private static void UpdatePlayerFreeze(CCSPlayerController controller)
        {
            if (!controller.IsValid || !controller.PlayerPawn.IsValid || controller.PlayerPawn.Value?.Health <= 0)
            {
                FrozenPlayers.Remove(controller);
                PendingFreeze.Remove(controller);
                return;
            }

            bool shouldFreeze = false;
            if (Menus.TryGetValue(controller, out var menuStack))
            {
                foreach (var menu in menuStack)
                {
                    if (menu.RequiresFreeze)
                    {
                        shouldFreeze = true;
                        break;
                    }
                }
            }

            if (shouldFreeze && !FrozenPlayers.Contains(controller))
            {
                var playerPawn = controller.PlayerPawn.Value;
                if (playerPawn?.MoveType == MoveType_t.MOVETYPE_WALK && playerPawn?.OnGroundLastTick == true)
                {
                    Freeze(controller, true);
                    FrozenPlayers.Add(controller);
                    PendingFreeze.Remove(controller);
                }
                else
                {
                    PendingFreeze.Add(controller);
                }
            }
            else if (!shouldFreeze && FrozenPlayers.Contains(controller))
            {
                Freeze(controller, false);
                FrozenPlayers.Remove(controller);
            }
        }

        public static MenuButtons HandleObserverMode(CCSPlayerController controller)
        {
            var playerPawn = controller.PlayerPawn.Value;
            if (playerPawn == null || !playerPawn.IsValid)
                return 0;

            if (controller.Team <= CsTeam.Spectator || (LifeState_t)playerPawn.LifeState != LifeState_t.LIFE_ALIVE)
            {
                var observerPawn = controller.ObserverPawn?.Value?.ObserverServices;
                if (observerPawn == null)
                    return 0;

                bool forceCamera = mp_forcecamera?.GetPrimitiveValue<int>() == 1;

                if (!ObserverMode.TryGetValue(controller, out var obsMode))
                {
                    obsMode = (observerPawn.ObserverMode, false);
                    ObserverMode[controller] = obsMode;

                    if (forceCamera)
                    {
                        ObserverMode[controller] = ((int)ObserverMode_t.OBS_MODE_CHASE, true);
                        return 0;
                    }
                }

                float deathTime = Server.CurrentTime - playerPawn.DeathTime;

                if (obsMode.ObserverMode != observerPawn.ObserverMode)
                {
                    observerPawn.ObserverMode = (byte)obsMode.ObserverMode;
                    if (!obsMode.BlockNext && !(forceCamera && deathTime > 4.0f && deathTime < 4.5f))
                        return _config.GetSelectButton();

                    ObserverMode[controller] = (obsMode.ObserverMode, false);
                }
            }
            return 0;
        }

        public static void SetMoveType(CCSPlayerController player, MoveType_t moveType)
        {
            var pawn = player.PlayerPawn.Value;
            if (pawn == null)
                return;

            pawn.MoveType = moveType;
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType");
            Schema.GetRef<MoveType_t>(pawn.Handle, "CBaseEntity", "m_nActualMoveType") = moveType;
        }

        public static void Freeze(CCSPlayerController player, bool freeze = true)
        {
            SetMoveType(player, freeze ? MoveType_t.MOVETYPE_OBSOLETE : MoveType_t.MOVETYPE_WALK);
        }

        public static string GetFooterTranslation(bool main)
        {
            return main ? Translator.GetTranslation("FooterMain") : Translator.GetTranslation("FooterSubMenu");
        }

        public void SetMenuLine(MenuBase menu, int line, List<MenuValue>? values)
        {
            if (menu.Items.Count > line)
            {
                menu.Items[line].Values = values;
            }
        }
    }
}