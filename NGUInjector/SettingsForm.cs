using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Windows.Forms;
using NGUInjector.Managers;
using static NGUInjector.Main;

namespace NGUInjector
{
    public partial class SettingsForm : Form
    {
        private enum Direction
        {
            Up = 1,
            Down = -1
        }

        private class ItemControlGroup
        {
            public ListBox ItemList { get; }

            public NumericUpDown ItemBox { get; }

            public ErrorProvider ErrorProvider { get; }

            public Label ItemLabel { get; }

            public Func<int[]> GetSettings { get; }

            public Action<int[]> SaveSettings { get; }

            public int MinVal { get; }

            public int MaxVal { get; }

            public bool CheckIsEquipment { get; }

            public Func<int, string> GetDisplayName { get; }

            public ItemControlGroup(ListBox itemList, NumericUpDown itemBox, ErrorProvider errorProvider, Label itemLabel,
                Func<int[]> getSettings, Action<int[]> saveSettings, bool checkIsEquipment = true)
            {
                ItemList = itemList;
                ItemBox = itemBox;
                ErrorProvider = errorProvider;
                ItemLabel = itemLabel;

                GetSettings = getSettings;
                SaveSettings = saveSettings;

                MinVal = 1;
                MaxVal = Consts.MAX_GEAR_ID;

                ItemBox.Minimum = MinVal;
                ItemBox.Maximum = MaxVal;

                CheckIsEquipment = checkIsEquipment;
                GetDisplayName = (id) => _character.itemInfo.itemName[id];
            }

            public ItemControlGroup(ListBox itemList, NumericUpDown itemBox, Label itemLabel,
                Func<int[]> getSettings, Action<int[]> saveSettings,
                bool checkIsEquipment, int minVal, int maxVal, Func<int, string> getDisplayName)
                : this(itemList, itemBox, null, itemLabel, getSettings, saveSettings, checkIsEquipment)
            {
                MinVal = minVal;
                MaxVal = maxVal;

                ItemBox.Minimum = MinVal;
                ItemBox.Maximum = MaxVal;

                GetDisplayName = getDisplayName;
            }

            public void ClearError() => SetError("");

            public void SetError(string message) => ErrorProvider?.SetError(ItemLabel, message);

            public void UpdateList(int[] newList) => UpdateItemList(ItemList, newList, GetDisplayName);
        }

        private sealed class StringOption
        {
            public StringOption(string key, string text)
            {
                Key = key;
                Text = text;
            }

            public string Key { get; }

            public string Text { get; }

            public override string ToString() => Text;
        }

        private static readonly Character _character = Main.Character;

        private static readonly Dictionary<string, string> BoostPriorityLabels = new Dictionary<string, string>
        {
            { "Power", "力量" },
            { "Toughness", "韧性" },
            { "Special", "特殊" }
        };

        private static readonly Dictionary<string, string> CardSortLabels = new Dictionary<string, string>
        {
            { "RARITY", "稀有度" },
            { "TIER", "阶层" },
            { "COST", "花费" },
            { "PROTECTED", "已保护" },
            { "CHANGE", "变化" },
            { "VALUE", "价值" },
            { "NORMALVALUE", "普通价值" }
        };

        private bool _initializing = true;

        private readonly Dictionary<int, string> zoneList;
        private readonly Dictionary<int, string> titanZoneList;
        private readonly Dictionary<int, string> spriteEnemyList;

        private readonly ItemControlGroup _yggControls;
        private readonly ItemControlGroup _priorityControls;
        private readonly ItemControlGroup _blacklistControls;
        private readonly ItemControlGroup _titanControls;
        private readonly ItemControlGroup _goldControls;
        private readonly ItemControlGroup _questControls;
        private readonly ItemControlGroup _wishControls;
        private readonly ItemControlGroup _wishBlacklistControls;
        private readonly ItemControlGroup _shockwaveControls;
        private readonly ItemControlGroup _cookingControls;

        private readonly CheckBox[] _killTitan = new CheckBox[14];
        private readonly ComboBox[] _titanVersion = new ComboBox[7];

        private readonly ComboBox[] _cardRarity = new ComboBox[14];
        private readonly ComboBox[] _cardCost = new ComboBox[14];

        private static string LocalizeBoostPriority(string priority)
        {
            return BoostPriorityLabels.TryGetValue(priority, out var label) ? label : priority;
        }

        private static string LocalizeCardBonus(string bonus)
        {
            var normalized = bonus.Replace("_", "").ToLowerInvariant();

            if (normalized.Contains("energy") && normalized.Contains("ngu"))
                return "能量 NGU 速度";
            if (normalized.Contains("magic") && normalized.Contains("ngu"))
                return "魔力 NGU 速度";
            if (normalized.Contains("wandoos"))
                return "Wandoos 速度";
            if (normalized.Contains("augment"))
                return "挂件速度";
            if (normalized.Contains("timemachine"))
                return "时间机器速度";
            if (normalized.Contains("hack"))
                return "黑客速度";
            if (normalized.Contains("wish"))
                return "许愿速度";
            if (normalized.Contains("attack") || normalized.Contains("defense"))
                return "攻击/防御";
            if (normalized.Contains("adventure"))
                return "冒险属性";
            if (normalized.Contains("dropchance"))
                return "掉落率";
            if (normalized.Contains("gold"))
                return "黄金掉落";
            if (normalized.Contains("daycare"))
                return "日托速度";
            if (normalized.Contains("qp") || normalized.Contains("quirk"))
                return "怪癖点获取";
            if (normalized.Contains("pp") || normalized.Contains("perk"))
                return "特权点获取";

            return bonus;
        }

        private static string LocalizeCardSortOption(string option)
        {
            var isAscending = option.Contains("-ASC");
            var key = option.Replace("-ASC", "");

            if (key.StartsWith("TYPE:"))
            {
                var label = $"类型：{LocalizeCardBonus(key.Substring("TYPE:".Length))}";
                return isAscending ? $"{label}（升序）" : label;
            }

            var baseLabel = CardSortLabels.TryGetValue(key, out var text) ? text : key;
            return isAscending ? $"{baseLabel}（升序）" : baseLabel;
        }

        private static StringOption CreateCardSortOption(string option)
        {
            return new StringOption(option, LocalizeCardSortOption(option));
        }

        private string[] GetCardSortKeysFromList()
        {
            return CardSortList.Items.Cast<StringOption>().Select(x => x.Key).ToArray();
        }

        private void SetCardSortList(string[] sortOrder)
        {
            CardSortList.DataSource = null;
            CardSortList.Items.Clear();
            foreach (var option in sortOrder)
                CardSortList.Items.Add(CreateCardSortOption(option));
        }

        public SettingsForm()
        {
            try
            {
                _initializing = true;
                InitializeComponent();

                for (int i = 0; i <= 13; i++)
                {
                    _killTitan[i] = GetElement<CheckBox>($"KillTitan{i + 1}");
                    _cardRarity[i] = GetElement<ComboBox>($"CardRarity{i + 1}");
                    _cardCost[i] = GetElement<ComboBox>($"CardCost{i + 1}");
                }

                for (int i = 0; i <= 6; i++)
                    _titanVersion[i] = GetElement<ComboBox>($"Titan{i + 6}Version");

                AdjustDimensions();

                // Populate our data sources
                var allZoneList = new Dictionary<int, string>(ZoneHelpers.ZoneList);

                spriteEnemyList = new Dictionary<int, string>();
                foreach (var x in _character.adventureController.enemyList)
                {
                    foreach (var enemy in x)
                    {
                        try
                        {
                            spriteEnemyList.Add(enemy.spriteID, enemy.name);
                        }
                        catch
                        {
                            // pass
                        }
                    }
                }

                UpdateTitanVersions();

                string[] cardBonusTypes = typeof(cardBonus).GetEnumNames().Where(x => x != "none").ToArray();
                var cardSortOptions = new List<string> { "RARITY", "TIER", "COST", "PROTECTED", "CHANGE", "VALUE", "NORMALVALUE" };
                foreach (string sortOption in cardSortOptions)
                {
                    CardSortOptions.Items.Add(CreateCardSortOption(sortOption));
                    CardSortOptions.Items.Add(CreateCardSortOption($"{sortOption}-ASC"));
                }
                foreach (string bonus in cardBonusTypes)
                {
                    CardSortOptions.Items.Add(CreateCardSortOption($"TYPE:{bonus}"));
                    CardSortOptions.Items.Add(CreateCardSortOption($"TYPE-ASC:{bonus}"));
                }

                for (int i = 0; i <= 13; i++)
                {
                    _cardRarity[i].DataSource = new BindingSource(CardManager.rarityList, null);
                    _cardRarity[i].ValueMember = "Key";
                    _cardRarity[i].DisplayMember = "Value";

                    _cardCost[i].DataSource = new BindingSource(CardManager.costList, null);
                }

                FavoredMacguffin.DataSource = new BindingSource(InventoryManager.macguffinList, null);
                FavoredMacguffin.ValueMember = "Key";
                FavoredMacguffin.DisplayMember = "Value";

                // Remove ITOPOD for non combat zones
                allZoneList.Remove(1000);
                allZoneList.Remove(-1);

                zoneList = allZoneList.Where(x => !ZoneHelpers.TitanZones.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);
                titanZoneList = allZoneList.Except(zoneList).ToDictionary(x => x.Key, x => x.Value);

                CombatTargetZone.DataSource = new BindingSource(zoneList, null);
                CombatTargetZone.ValueMember = "Key";
                CombatTargetZone.DisplayMember = "Value";

                EnemyBlacklistZone.DataSource = new BindingSource(zoneList, null);
                EnemyBlacklistZone.ValueMember = "Key";
                EnemyBlacklistZone.DisplayMember = "Value";
                EnemyBlacklistZone.SelectedIndex = 0;

                MoneyPitThreshold.DataSource = new BindingSource(MoneyPitManager.moneyPitThresholds, null);
                MoneyPitThreshold.ValueMember = "Key";
                MoneyPitThreshold.DisplayMember = "Value";

                numberErrProvider.SetIconAlignment(BloodNumberThreshold, ErrorIconAlignment.MiddleRight);

                YggLoadoutItem.TextChanged += YggLoadoutItem_TextChanged;
                PriorityBoostItemAdd.TextChanged += PriorityBoostItemAdd_TextChanged;
                BlacklistAddItem.TextChanged += BlacklistAddItem_TextChanged;
                TitanAddItem.TextChanged += TitanAddItem_TextChanged;
                GoldItemBox.TextChanged += GoldItemBox_TextChanged;
                QuestLoadoutItem.TextChanged += QuestLoadoutBox_TextChanged;
                WishAddInput.TextChanged += WishAddInput_TextChanged;
                WishBlacklistAddInput.TextChanged += WishBlacklistAddInput_TextChanged;
                ShockwaveInput.TextChanged += ShockwaveInput_TextChanged;
                CookingLoadoutItem.TextChanged += CookingLoadoutBox_TextChanged;

                _yggControls = new ItemControlGroup(
                    YggdrasilLoadoutBox, YggLoadoutItem, yggErrorProvider, YggItemLabel,
                    () => Settings.YggdrasilLoadout, (settings) => Settings.YggdrasilLoadout = settings);

                _priorityControls = new ItemControlGroup(
                    PriorityBoostBox, PriorityBoostItemAdd, invPrioErrorProvider, PriorityBoostLabel,
                    () => Settings.PriorityBoosts, (settings) => Settings.PriorityBoosts = settings);

                _blacklistControls = new ItemControlGroup(
                    BlacklistBox, BlacklistAddItem, null, BlacklistLabel,
                    () => Settings.BoostBlacklist, (settings) => Settings.BoostBlacklist = settings, false);

                _titanControls = new ItemControlGroup(
                    TitanLoadout, TitanAddItem, titanErrProvider, TitanLabel,
                    () => Settings.TitanLoadout, (settings) => Settings.TitanLoadout = settings);

                _goldControls = new ItemControlGroup(
                    GoldLoadout, GoldItemBox, goldErrorProvider, GoldItemLabel,
                    () => Settings.GoldDropLoadout, (settings) => Settings.GoldDropLoadout = settings);

                _questControls = new ItemControlGroup(
                    QuestLoadoutBox, QuestLoadoutItem, questErrorProvider, QuestItemLabel,
                    () => Settings.QuestLoadout, (settings) => Settings.QuestLoadout = settings);

                _wishControls = new ItemControlGroup(
                    WishPriority, WishAddInput, AddWishLabel,
                    () => Settings.WishPriorities, (settings) => Settings.WishPriorities = settings,
                    false, 0, Consts.MAX_WISH_ID, (id) => _character.wishesController.properties[id].wishName);

                _wishBlacklistControls = new ItemControlGroup(
                    WishBlacklist, WishBlacklistAddInput, AddWishBlacklistLabel,
                    () => Settings.WishBlacklist, (settings) => Settings.WishBlacklist = settings,
                    false, 0, Consts.MAX_WISH_ID, (id) => _character.wishesController.properties[id].wishName);

                _shockwaveControls = new ItemControlGroup(
                    ShockwaveBox, ShockwaveInput, shockwaveErrorProvider, ShockwaveLabel,
                    () => Settings.Shockwave, (settings) => Settings.Shockwave = settings, false);

                _cookingControls = new ItemControlGroup(
                    CookingLoadoutBox, CookingLoadoutItem, cookingErrorProvider, CookingItemLabel,
                    () => Settings.CookingLoadout, (settings) => Settings.CookingLoadout = settings);

                TryItemBoxTextChanged(_yggControls, out _);
                TryItemBoxTextChanged(_priorityControls, out _);
                TryItemBoxTextChanged(_blacklistControls, out _);
                TryItemBoxTextChanged(_titanControls, out _);
                TryItemBoxTextChanged(_goldControls, out _);
                TryItemBoxTextChanged(_questControls, out _);
                TryItemBoxTextChanged(_wishControls, out _);
                TryItemBoxTextChanged(_wishBlacklistControls, out _);
                TryItemBoxTextChanged(_shockwaveControls, out _);
                TryItemBoxTextChanged(_cookingControls, out _);

                VersionLabel.Text = $"版本：{Main.Version}";
                _initializing = false;
                Initialized = true;
            }
            catch (Exception ex)
            {
                _initializing = false;
                LogDebug(ex.ToString());
                ShowInitializationError(ex);
            }
        }

        public bool Initialized { get; private set; }

        private void ShowInitializationError(Exception ex)
        {
            try
            {
                Controls.Clear();

                Text = "注入器设置 - 初始化失败";
                Size = new Size(760, 420);
                StartPosition = FormStartPosition.CenterScreen;

                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(14),
                    ColumnCount = 1,
                    RowCount = 2
                };
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

                var message = new Label
                {
                    AutoSize = true,
                    Dock = DockStyle.Fill,
                    ForeColor = Color.DarkRed,
                    Text = "设置界面初始化失败。请查看日志：%UserProfile%\\AppData\\LocalLow\\NGUInjector\\logs\\debug.log"
                };

                var details = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical,
                    Text = ex.ToString()
                };

                layout.Controls.Add(message, 0, 0);
                layout.Controls.Add(details, 0, 1);
                Controls.Add(layout);
            }
            catch
            {
                // If WinForms itself cannot build controls, keep the original log entry as the source of truth.
            }
        }

        private T GetElement<T>(string name) => this.GetFieldValue<SettingsForm, T>(name);

        private void AlignWidth(Control target, Control source)
        {
            target.Width = source.Width + source.Margin.Left + source.Margin.Right;
            target.Width -= target.Margin.Left + target.Margin.Right;
        }

        private void AlignHeight(Control target, Control source)
        {
            target.Width = source.Height + source.Margin.Top + source.Margin.Bottom;
            target.Width -= target.Margin.Top + target.Margin.Bottom;
        }

        private void AdjustDimensions()
        {
            // Adjust separator height in case it has changed due to rescaling
            Separator1.Height = 1;
            Separator2.Height = 1;
            Separator3.Height = 1;
            Separator4.Height = 1;

            Graphics g = CreateGraphics();
            var scale = g.DpiY / 96f;
            g.Dispose();

            tabControl1.ItemSize = new Size(tabControl1.ItemSize.Width, (int)(tabControl1.ItemSize.Height * scale));

            // General Tab
            UnloadButton.Height = OpenSettingsFolder.Height;

            // Allocation Tab
            ChangeProfileFile.Size = OpenProfileFolder.Size;
            AlignWidth(SpaghettiCap, AutoSpellSwap);
            AlignWidth(CounterfeitCap, AutoSpellSwap);
            AlignWidth(BloodNumberThreshold, AutoSpellSwap);
            BloodNumberThreshold.Width -= BloodNumberThreshold.Height;
            AlignWidth(GuffAThreshold, IronPillThreshold);
            AlignWidth(GuffBThreshold, IronPillThreshold);

            // Yggdrasil Tab
            YggAddButton.Size = YggRemoveButton.Size;

            // Inventory Tab
            AlignWidth(CubePriority, ManageBoostConvert);
            AlignWidth(FavoredMacguffin, ManageBoostConvert);
            PriorityBoostAdd.Size = PriorityBoostRemove.Size;
            BlacklistAdd.Size = BlacklistRemove.Size;

            // Titans Tab
            TitanAdd.Size = TitanRemove.Size;

            var height = Titan6Version.Height;
            label64.Height = height;
            label65.Height = height;
            Titan1Placeholder.Height = height;
            Titan2Placeholder.Height = height;
            Titan3Placeholder.Height = height;
            Titan4Placeholder.Height = height;
            Titan5Placeholder.Height = height;
            Titan13Placeholder.Height = height;
            Titan14Placeholder.Height = height;

            if (tableLayoutPanel15.Height < (height + 1) * 14)
                tableLayoutPanel22.ColumnStyles[2].Width = SystemInformation.VerticalScrollBarWidth - 1;
            else
                tableLayoutPanel22.ColumnCount = 2;

            // Adventure Tab
            BlacklistAddEnemyButton.Size = BlacklistRemoveEnemyButton.Size;

            // Gold Tab
            AlignHeight(label10, ManageGold);
            GoldLoadoutAdd.Size = GoldLoadoutRemove.Size;

            // Wishes Tab
            AddWishButton.Size = RemoveWishButton.Size;

            // Pit Tab
            AlignWidth(MoneyPitThreshold, AutoMoneyPit);
            ShockwaveAdd.Size = ShockwaveRemove.Size;

            // Cards Tab
            CardSortAdd.Size = CardSortRemove.Size;
            label1.Height = CardRarity1.Height;

            if (tableLayoutPanel13.Height < (CardRarity1.Height + 1) * 14)
                tableLayoutPanel14.ColumnStyles[3].Width = SystemInformation.VerticalScrollBarWidth - 1;
            else
                tableLayoutPanel14.ColumnCount = 3;

            // Cooking Tab
            CookingAddButton.Size = CookingRemoveButton.Size;
        }

        public static void UpdateItemList(ListBox itemList, int[] newList, Func<int, string> getDisplayName)
        {
            if (newList?.Length > 0)
            {
                var temp = newList.ToDictionary(x => x, x => getDisplayName(x));
                itemList.DataSource = null;
                itemList.DataSource = new BindingSource(temp, null);
                itemList.ValueMember = "Key";
                itemList.DisplayMember = "Value";
            }
            else
            {
                itemList.Items.Clear();
            }
        }

        public void SetTitanGoldBox(SavedSettings newSettings)
        {
            TitanGoldTargets.Items.Clear();
            for (var i = 0; i < ZoneHelpers.TitanZones.Length; i++)
            {
                var zone = ZoneHelpers.TitanZones[i];
                var text = $"{titanZoneList[zone]}";
                if (newSettings.TitanGoldTargets[i])
                    text = $"{text} ({(newSettings.TitanMoneyDone[i] ? "已完成" : "等待中")})";
                var item = new ListViewItem
                {
                    Tag = i,
                    Checked = newSettings.TitanGoldTargets[i],
                    Text = text,
                    BackColor = newSettings.TitanGoldTargets[i]
                        ? newSettings.TitanMoneyDone[i] ? Color.LightGreen : Color.Yellow
                        : Color.White
                };
                TitanGoldTargets.Items.Add(item);
            }

            TitanGoldTargets.Columns[0].Width = -1;
        }

        private void SetSnipeZone(ComboBox control, int setting)
        {
            if (zoneList.ContainsKey(setting))
                control.SelectedItem = new KeyValuePair<int, string>(setting, zoneList[setting]);
        }

        private void SetMoneyPitThreshold(ComboBox control, SavedSettings newSettings)
        {
            if (newSettings.MoneyPitThreshold == MoneyPitManager.moneyPitThresholds[MoneyPitThreshold.SelectedIndex])
                return;
            var i = MoneyPitManager.moneyPitThresholds.BinarySearch(newSettings.MoneyPitThreshold);
            if (i < 0)
                i = -i - 2;
            if (i < 0)
                i = 0;
            control.SelectedIndex = i;
        }

        private string FormatDoubleNumber(double number)
        {
            if (number == 0.0)
                return "";

            if (number >= 1e6)
                return number.ToString("#.###E+0");

            return number.ToString("");
        }

        public void UpdateFromSettings(SavedSettings newSettings)
        {
            _initializing = true;

            // General Tab
            MasterEnable.Checked = newSettings.GlobalEnabled;
            DisableOverlay.Checked = newSettings.DisableOverlay;
            MoneyPitRunMode.Checked = newSettings.MoneyPitRunMode;
            AutoFightBosses.Enabled = !newSettings.MoneyPitRunMode;
            AutoFightBosses.Checked = newSettings.AutoFight;
            AutoBuyAdv.Checked = newSettings.AutoBuyAdventure;
            AutoBuyEM.Checked = newSettings.AutoBuyEM;
            AutoBuyConsumables.Checked = newSettings.AutoBuyConsumables;
            ConsumeIfRunning.Checked = newSettings.ConsumeIfAlreadyRunning;
            Autosave.Checked = newSettings.Autosave;

            // Allocation Tab
            ManageEnergy.Checked = newSettings.ManageEnergy;
            ManageMagic.Checked = newSettings.ManageMagic;
            ManageR3.Checked = newSettings.ManageR3;
            ManageWandoos.Checked = newSettings.ManageWandoos;
            ManageNGUDiff.Checked = newSettings.ManageNGUDiff;
            ManageBeards.Checked = newSettings.ManageBeards;
            ManageDiggers.Checked = newSettings.ManageDiggers;
            UpgradeDiggers.Checked = newSettings.UpgradeDiggers;
            DiggerCap.Text = $"{newSettings.DiggerCap:F2}";
            ManageGear.Checked = newSettings.ManageGear;
            ManageConsumables.Checked = newSettings.ManageConsumables;
            AutoRebirth.Checked = newSettings.AutoRebirth;

            AutoSpellSwap.Checked = newSettings.AutoSpellSwap;
            SpaghettiCap.Value = newSettings.SpaghettiThreshold;
            CounterfeitCap.Value = newSettings.CounterfeitThreshold;
            BloodNumberThreshold.Text = FormatDoubleNumber(newSettings.BloodNumberThreshold);
            CastBloodSpells.Checked = newSettings.CastBloodSpells;
            IronPillThreshold.Value = Convert.ToDecimal(newSettings.IronPillThreshold);
            GuffAThreshold.Value = newSettings.BloodMacGuffinAThreshold;
            GuffBThreshold.Value = newSettings.BloodMacGuffinBThreshold;
            IronPillOnRebirth.Checked = newSettings.IronPillOnRebirth;
            GuffAOnRebirth.Checked = newSettings.BloodMacGuffinAOnRebirth;
            GuffBOnRebirth.Checked = newSettings.BloodMacGuffinBOnRebirth;

            // Yggdrasil Tab
            ManageYggdrasil.Checked = newSettings.ManageYggdrasil;
            ActivateFruits.Checked = newSettings.ActivateFruits;
            YggSwapThreshold.Value = newSettings.YggSwapThreshold;
            YggdrasilSwap.Checked = newSettings.SwapYggdrasilLoadouts;
            SwapYggdrasilDiggers.Checked = newSettings.SwapYggdrasilDiggers;
            SwapYggdrasilBeards.Checked = newSettings.SwapYggdrasilBeards;
            _yggControls.UpdateList(newSettings.YggdrasilLoadout);

            // Inventory Tab
            ManageInventory.Checked = newSettings.ManageInventory;
            ManageBoostConvert.Checked = newSettings.AutoConvertBoosts;
            CubePriority.SelectedIndex = newSettings.CubePriority;
            FavoredMacguffin.SelectedIndex = InventoryManager.macguffinList.Keys.ToList().IndexOf(newSettings.FavoredMacguffin);

            BoostPriorityList.Items.Clear();
            foreach (string priority in newSettings.BoostPriority)
                BoostPriorityList.Items.Add(LocalizeBoostPriority(priority));

            if (BoostPriorityList.Items.Count != 3)
                BoostPriorityList.Items.AddRange(new string[]
                {
                    LocalizeBoostPriority("Power"),
                    LocalizeBoostPriority("Toughness"),
                    LocalizeBoostPriority("Special")
                });

            _priorityControls.UpdateList(newSettings.PriorityBoosts);
            _blacklistControls.UpdateList(newSettings.BoostBlacklist);

            // Titans Tab
            ManageTitans.Checked = newSettings.ManageTitans;
            SwapTitanLoadout.Checked = newSettings.SwapTitanLoadouts;
            SwapTitanDiggers.Checked = newSettings.SwapTitanDiggers;
            SwapTitanBeards.Checked = newSettings.SwapTitanBeards;
            _titanControls.UpdateList(newSettings.TitanLoadout);

            for (int i = 0; i <= 13; i++)
                _killTitan[i].Checked = newSettings.TitanSwapTargets[i];

            TitanCombatMode.SelectedIndex = newSettings.TitanCombatMode;
            TitanBeastMode.Checked = newSettings.TitanBeastMode;

            // Adventure Tab
            CombatActive.Checked = newSettings.CombatEnabled;
            CombatMode.SelectedIndex = newSettings.CombatMode;
            SetSnipeZone(CombatTargetZone, newSettings.SnipeZone);
            BeastMode.Checked = newSettings.BeastMode;
            BossesOnly.Checked = newSettings.SnipeBossOnly;
            AllowFallthrough.Checked = newSettings.AllowZoneFallback;

            TargetITOPOD.Checked = newSettings.AdventureTargetITOPOD;
            ITOPODCombatMode.SelectedIndex = newSettings.ITOPODCombatMode;
            ITOPODOptimizeMode.SelectedIndex = newSettings.ITOPODOptimizeMode;
            ITOPODBeastMode.Checked = newSettings.ITOPODBeastMode;
            ITOPODAutoPush.Checked = newSettings.ITOPODAutoPush;

            UpdateItemList(BlacklistedBosses, newSettings.BlacklistedBosses, x => spriteEnemyList[x]);

            // Gold Tab
            ManageGold.Enabled = !newSettings.MoneyPitRunMode;
            ManageGold.Checked = newSettings.ManageGoldLoadouts;
            ResnipeInput.Value = newSettings.ResnipeTime;
            CBlockMode.Enabled = !newSettings.MoneyPitRunMode;
            CBlockMode.Checked = newSettings.GoldCBlockMode;
            _goldControls.UpdateList(newSettings.GoldDropLoadout);
            SetTitanGoldBox(newSettings);

            // Quests Tab
            ManageQuests.Checked = newSettings.AutoQuest;
            AllowMajor.Checked = newSettings.AllowMajorQuests;
            ButterMajors.Checked = newSettings.UseButterMajor;
            QuestsFullBank.Checked = newSettings.QuestsFullBank;
            ManualMinor.Checked = newSettings.ManualMinors;
            ButterMinors.Checked = newSettings.UseButterMinor;
            FiftyItemMinors.Checked = newSettings.FiftyItemMinors;
            AbandonMinors.Checked = newSettings.AbandonMinors;
            AbandonMinorThreshold.Value = newSettings.MinorAbandonThreshold;
            ManageQuestLoadout.Checked = newSettings.ManageQuestLoadouts;
            _questControls.UpdateList(newSettings.QuestLoadout);
            QuestCombatMode.SelectedIndex = newSettings.QuestCombatMode;
            QuestBeastMode.Checked = newSettings.QuestBeastMode;

            // Wishes Tab
            ManageWishes.Checked = newSettings.ManageWishes;
            WishLimit.Value = newSettings.WishLimit;
            WishEnergy.Value = Convert.ToDecimal(newSettings.WishEnergy);
            WishMagic.Value = Convert.ToDecimal(newSettings.WishMagic);
            WishR3.Value = Convert.ToDecimal(newSettings.WishR3);
            WishMode.SelectedIndex = newSettings.WishMode;
            WeakPriorities.Checked = newSettings.WeakPriorities;
            _wishControls.UpdateList(newSettings.WishPriorities);
            _wishBlacklistControls.UpdateList(newSettings.WishBlacklist);

            // Pit Tab
            AutoDailySpin.Checked = newSettings.AutoSpin;
            AutoMoneyPit.Enabled = !newSettings.MoneyPitRunMode;
            AutoMoneyPit.Checked = newSettings.AutoMoneyPit;
            SwapPitDiggers.Checked = newSettings.SwapPitDiggers;
            PredictMoneyPit.Enabled = !newSettings.MoneyPitRunMode;
            PredictMoneyPit.Checked = newSettings.PredictMoneyPit;
            MoneyPitDaycare.Checked = newSettings.MoneyPitDaycare;
            SetMoneyPitThreshold(MoneyPitThreshold, newSettings);
            DaycareThreshold.Value = newSettings.DaycareThreshold;
            _shockwaveControls.UpdateList(newSettings.Shockwave);

            // Cards Tab
            BalanceMayo.Checked = newSettings.ManageMayo;
            AutoCastCards.Checked = newSettings.AutoCastCards;
            CastProtectedCards.Checked = newSettings.CastProtectedCards;
            TrashCards.Checked = newSettings.TrashCards;
            TrashProtectedCards.Checked = newSettings.TrashProtectedCards;
            SortCards.Checked = newSettings.CardSortEnabled;

            if (newSettings.CardSortOrder.Length > 0)
            {
                SetCardSortList(newSettings.CardSortOrder);
            }
            else
            {
                CardSortList.DataSource = null;
                CardSortList.Items.Clear();
            }

            for (int i = 0; i <= 13; i++)
            {
                _cardRarity[i].SelectedIndex = CardManager.rarityList.Keys.ToList().IndexOf(newSettings.CardRarities[i]);
                _cardCost[i].SelectedIndex = Array.IndexOf(CardManager.costList, newSettings.CardCosts[i]);
            }

            // Cooking Tab
            ManageCooking.Checked = newSettings.ManageCooking;
            ManageCookingLoadout.Checked = newSettings.ManageCookingLoadouts;
            _cookingControls.UpdateList(newSettings.CookingLoadout);

            Refresh();
            _initializing = false;
        }

        private bool TryGetValueFromNumericUpDown(NumericUpDown upDown, out int val)
        {
            try
            {
                val = (int)upDown.Value;
                return true;
            }
            catch
            {
                val = 0;
                return false;
            }
        }

        private bool TryGetTextFromNumericUpDown(NumericUpDown upDown, out int val) => int.TryParse(upDown.Text, out val);

        private bool TryItemBoxTextChanged(ItemControlGroup controls, out int val)
        {
            controls.ClearError();

            if (!TryGetTextFromNumericUpDown(controls.ItemBox, out val) || val < controls.MinVal || val > controls.MaxVal)
            {
                controls.ItemLabel.Text = "";
                return false;
            }

            var itemName = controls.GetDisplayName(val).Replace("<b><color=blue>[QUEST ITEM]</color></b>", "[任务物品]");
            bool isValid = true;

            if (controls.CheckIsEquipment)
            {
                isValid = (int)_character.itemInfo.type[val] <= 5;
                if (!isValid)
                    itemName += "（不可装备）";
            }
            controls.ItemLabel.Text = itemName;

            return isValid;
        }

        private void ItemBoxKeyDown(KeyEventArgs e, ItemControlGroup controls)
        {
            if (e.KeyCode == Keys.Enter)
                ItemListAdd(controls);
        }

        private void ItemListAdd(ItemControlGroup controls)
        {
            controls.ClearError();

            if (!TryItemBoxTextChanged(controls, out int val))
            {
                controls.SetError("无效的物品 ID");
                return;
            }

            var settings = controls.GetSettings();
            if (settings.Contains(val))
                return;

            var index = settings.Length;

            Array.Resize(ref settings, index + 1);
            settings[index] = val;
            controls.SaveSettings(settings);

            controls.ItemList.SelectedIndex = index;
        }

        private void ItemListRemove(ItemControlGroup controls)
        {
            controls.ClearError();

            var item = controls.ItemList.SelectedItem;
            if (item == null)
                return;

            var index = controls.ItemList.SelectedIndex;

            var id = ((KeyValuePair<int, string>)item).Key;

            var settings = controls.GetSettings();
            settings = settings.Where(x => x != id).ToArray();
            controls.SaveSettings(settings);

            if (settings.Length > index)
                controls.ItemList.SelectedIndex = index;
            else if (settings.Length > 0)
                controls.ItemList.SelectedIndex = settings.Length - 1;
        }

        private void ItemListUp(ItemControlGroup controls)
        {
            controls.ClearError();

            ItemListMove(controls.ItemList, controls.GetSettings(), Direction.Up);
        }

        private void ItemListDown(ItemControlGroup controls)
        {
            controls.ClearError();

            ItemListMove(controls.ItemList, controls.GetSettings(), Direction.Down);
        }

        private void ItemListMove<T>(ListBox itemList, T[] settings, Direction direction)
        {
            var index = itemList.SelectedIndex;
            if (index == -1)
                return;

            var newIndex = index - (int)direction;
            if (newIndex < 0 || newIndex >= settings.Length)
                return;

            (settings[newIndex], settings[index]) = (settings[index], settings[newIndex]);
            Settings.SaveSettings();

            itemList.SelectedIndex = newIndex;
        }

        private void SimpleListMove<T>(ListBox itemList, T[] settings, Direction direction)
        {
            var index = itemList.SelectedIndex;
            if (index == -1)
                return;

            var newIndex = index - (int)direction;
            if (newIndex < 0 || newIndex >= settings.Length)
                return;

            (settings[newIndex], settings[index]) = (settings[index], settings[newIndex]);
            var item = itemList.Items[index];
            itemList.Items[index] = itemList.Items[newIndex];
            itemList.Items[newIndex] = item;
            Settings.SaveSettings();

            itemList.SelectedIndex = newIndex;
        }

        public void UpdateProfileList(string[] profileList, string selectedProfile)
        {
            AllocationProfileFile.DataSource = null;
            AllocationProfileFile.DataSource = new BindingSource(profileList, null);
            AllocationProfileFile.SelectedItem = selectedProfile;
        }

        public void UpdateProgressBar(int progress)
        {
            if (progress < 0)
                return;
            progressBar1.Value = progress;
        }

        public void UpdateTitanVersions()
        {
            for (int i = 6; i <= 12; i++)
                _titanVersion[i - 6].SelectedIndex = ZoneHelpers.TitanVersion(i - 1) - 1;
        }

        private void MasterEnable_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.GlobalEnabled = MasterEnable.Checked;
        }

        private void AutoDailySpin_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.AutoSpin = AutoDailySpin.Checked;
        }

        private void AutoMoneyPit_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.AutoMoneyPit = AutoMoneyPit.Checked;
        }

        private void SwapPitDiggers_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.SwapPitDiggers = SwapPitDiggers.Checked;
        }

        private void PredictMoneyPit_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.PredictMoneyPit = PredictMoneyPit.Checked;
        }

        private void MoneyPitDaycare_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.MoneyPitDaycare = MoneyPitDaycare.Checked;
        }

        private void AutoFightBosses_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.AutoFight = AutoFightBosses.Checked;
        }

        private void MoneyPitThreshold_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.MoneyPitThreshold = (double)MoneyPitThreshold.SelectedItem;
        }

        private void MoneyPitThreshold_Format(object sender, ListControlConvertEventArgs e)
        {
            e.Value = FormatDoubleNumber((double)e.ListItem);
        }

        private void DaycareThreshold_ValueChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            if (TryGetValueFromNumericUpDown(DaycareThreshold, out int val))
                Settings.DaycareThreshold = val;
        }

        private void ManageEnergy_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.ManageEnergy = ManageEnergy.Checked;
        }

        private void ManageMagic_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.ManageMagic = ManageMagic.Checked;
        }

        private void ManageGear_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.ManageGear = ManageGear.Checked;
        }

        private void ManageBeards_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.ManageBeards = ManageBeards.Checked;
        }

        private void ManageDiggers_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.ManageDiggers = ManageDiggers.Checked;
        }

        private void ManageWandoos_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.ManageWandoos = ManageWandoos.Checked;
        }

        private void AutoRebirth_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.AutoRebirth = AutoRebirth.Checked;
        }

        private void ManageYggdrasil_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.ManageYggdrasil = ManageYggdrasil.Checked;
        }

        private void YggdrasilSwap_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.SwapYggdrasilLoadouts = YggdrasilSwap.Checked;
        }

        private void YggdrasilSwapDiggers_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.SwapYggdrasilDiggers = SwapYggdrasilDiggers.Checked;
        }

        private void YggdrasilSwapBeards_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.SwapYggdrasilBeards = SwapYggdrasilBeards.Checked;
        }

        private void YggLoadoutItem_TextChanged(object sender, EventArgs e) => TryItemBoxTextChanged(_yggControls, out _);

        private void YggLoadoutItem_KeyDown(object sender, KeyEventArgs e) => ItemBoxKeyDown(e, _yggControls);

        private void YggAddButton_Click(object sender, EventArgs e) => ItemListAdd(_yggControls);

        private void YggRemoveButton_Click(object sender, EventArgs e) => ItemListRemove(_yggControls);

        private void ManageInventory_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.ManageInventory = ManageInventory.Checked;
        }

        private void ManageBoostConvert_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.AutoConvertBoosts = ManageBoostConvert.Checked;
        }

        private void BoostPrioUpButton_Click(object sender, EventArgs e) => SimpleListMove(BoostPriorityList, Settings.BoostPriority, Direction.Up);

        private void BoostPrioDownButton_Click(object sender, EventArgs e) => SimpleListMove(BoostPriorityList, Settings.BoostPriority, Direction.Down);

        private void PriorityBoostItemAdd_TextChanged(object sender, EventArgs e) => TryItemBoxTextChanged(_priorityControls, out _);

        private void PriorityBoostItemAdd_KeyDown(object sender, KeyEventArgs e) => ItemBoxKeyDown(e, _priorityControls);

        private void PriorityBoostAdd_Click(object sender, EventArgs e) => ItemListAdd(_priorityControls);

        private void PriorityBoostRemove_Click(object sender, EventArgs e) => ItemListRemove(_priorityControls);

        private void PrioUpButton_Click(object sender, EventArgs e) => ItemListUp(_priorityControls);

        private void PrioDownButton_Click(object sender, EventArgs e) => ItemListDown(_priorityControls);

        private void BlacklistAddItem_TextChanged(object sender, EventArgs e) => TryItemBoxTextChanged(_blacklistControls, out _);

        private void BlacklistAddItem_KeyDown(object sender, KeyEventArgs e) => ItemBoxKeyDown(e, _blacklistControls);

        private void BlacklistAdd_Click(object sender, EventArgs e) => ItemListAdd(_blacklistControls);

        private void BlacklistRemove_Click(object sender, EventArgs e) => ItemListRemove(_blacklistControls);

        private void ManageTitans_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.ManageTitans = ManageTitans.Checked;
        }

        private void SwapTitanLoadout_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.SwapTitanLoadouts = SwapTitanLoadout.Checked;
        }

        private void SwapTitanDiggers_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.SwapTitanDiggers = SwapTitanDiggers.Checked;
        }

        private void SwapTitanBeards_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.SwapTitanBeards = SwapTitanBeards.Checked;
        }

        private void ManageQuestLoadout_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.ManageQuestLoadouts = ManageQuestLoadout.Checked;
        }

        private void TitanAddItem_TextChanged(object sender, EventArgs e) => TryItemBoxTextChanged(_titanControls, out _);

        private void TitanAddItem_KeyDown(object sender, KeyEventArgs e) => ItemBoxKeyDown(e, _titanControls);

        private void TitanAdd_Click(object sender, EventArgs e) => ItemListAdd(_titanControls);

        private void TitanRemove_Click(object sender, EventArgs e) => ItemListRemove(_titanControls);

        private void CombatActive_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.CombatEnabled = CombatActive.Checked;
        }

        private void BossesOnly_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.SnipeBossOnly = BossesOnly.Checked;
        }

        private void CombatMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.CombatMode = CombatMode.SelectedIndex;
        }

        private void CombatTargetZone_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.SnipeZone = ((KeyValuePair<int, string>)CombatTargetZone.SelectedItem).Key;
        }

        private void AllowFallthrough_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.AllowZoneFallback = AllowFallthrough.Checked;
        }

        private void GoldItemBox_TextChanged(object sender, EventArgs e) => TryItemBoxTextChanged(_goldControls, out _);

        private void GoldItemBox_KeyDown(object sender, KeyEventArgs e) => ItemBoxKeyDown(e, _goldControls);

        private void GoldLoadoutAdd_Click(object sender, EventArgs e) => ItemListAdd(_goldControls);

        private void GoldLoadoutRemove_Click(object sender, EventArgs e) => ItemListRemove(_goldControls);

        private void SettingsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Hide();
            e.Cancel = true;
        }

        private void ManageQuests_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.AutoQuest = ManageQuests.Checked;
        }

        private void AllowMajor_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.AllowMajorQuests = AllowMajor.Checked;
        }

        private void QuestsFullBank_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.QuestsFullBank = QuestsFullBank.Checked;
        }

        private void AbandonMinors_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.AbandonMinors = AbandonMinors.Checked;
        }

        private void AbandonMinorThreshold_ValueChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            if (TryGetValueFromNumericUpDown(AbandonMinorThreshold, out int val))
                Settings.MinorAbandonThreshold = val;
        }

        private void QuestBeastMode_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.QuestBeastMode = QuestBeastMode.Checked;
        }

        private void QuestLoadoutBox_TextChanged(object sender, EventArgs e) => TryItemBoxTextChanged(_questControls, out _);

        private void QuestLoadoutItem_KeyDown(object sender, KeyEventArgs e) => ItemBoxKeyDown(e, _questControls);

        private void QuestAddButton_Click(object sender, EventArgs e) => ItemListAdd(_questControls);

        private void QuestRemoveButton_Click(object sender, EventArgs e) => ItemListRemove(_questControls);

        private void AutoSpellSwap_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.AutoSpellSwap = AutoSpellSwap.Checked;
        }

        private void SpaghettiCap_ValueChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            if (TryGetValueFromNumericUpDown(SpaghettiCap, out int val))
                Settings.SpaghettiThreshold = val;
        }

        private void CounterfeitCap_ValueChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            if (TryGetValueFromNumericUpDown(CounterfeitCap, out int val))
                Settings.CounterfeitThreshold = val;
        }

        private void BloodNumberThreshold_TextChanged(object sender, EventArgs e)
        {
            numberErrProvider.SetError(BloodNumberThreshold, "");
        }

        private void UpdateBloodNumberThreshold()
        {
            double saved;
            if (BloodNumberThreshold.Text == "")
            {
                saved = 0.0;
            }
            else if (!double.TryParse(BloodNumberThreshold.Text, out saved))
            {
                numberErrProvider.SetError(BloodNumberThreshold, "格式无效");
                return;
            }
            if (saved < 0.0)
                saved = 0.0;
            var divisor = saved >= 1E6 ? Math.Pow(10.0, (int)Math.Log10(saved) - 3) : 1.0;
            saved -= saved % divisor;
            if (Settings.BloodNumberThreshold == saved)
                BloodNumberThreshold.Text = FormatDoubleNumber(saved);
            Settings.BloodNumberThreshold = saved;
        }

        private void BloodNumberThreshold_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                UpdateBloodNumberThreshold();
        }

        private void BloodNumberThreshold_Leave(object sender, EventArgs e) => UpdateBloodNumberThreshold();

        private void IronPillThreshold_ValueChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            if (TryGetValueFromNumericUpDown(IronPillThreshold, out int val))
                Settings.IronPillThreshold = val;
        }

        private void GuffAThreshold_ValueChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            if (TryGetValueFromNumericUpDown(GuffAThreshold, out int val))
                Settings.BloodMacGuffinAThreshold = val;
        }

        private void GuffBThreshold_ValueChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            if (TryGetValueFromNumericUpDown(GuffBThreshold, out int val))
                Settings.BloodMacGuffinBThreshold = val;
        }

        private void AutoBuyEM_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.AutoBuyEM = AutoBuyEM.Checked;
        }

        private void IdleMinor_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.ManualMinors = ManualMinor.Checked;
        }

        private void FiftyItemMinors_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.FiftyItemMinors = FiftyItemMinors.Checked;
        }

        private void UseButter_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.UseButterMajor = ButterMajors.Checked;
        }

        private void ManageR3_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.ManageR3 = ManageR3.Checked;
        }

        private void ButterMinors_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.UseButterMinor = ButterMinors.Checked;
        }

        private void ActivateFruits_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.ActivateFruits = ActivateFruits.Checked;
        }

        private void WishAddInput_TextChanged(object sender, EventArgs e) => TryItemBoxTextChanged(_wishControls, out _);

        private void WishAddInput_KeyDown(object sender, KeyEventArgs e) => ItemBoxKeyDown(e, _wishControls);

        private void AddWishButton_Click(object sender, EventArgs e) => ItemListAdd(_wishControls);

        private void RemoveWishButton_Click(object sender, EventArgs e) => ItemListRemove(_wishControls);

        private void WishUpButton_Click(object sender, EventArgs e) => ItemListUp(_wishControls);

        private void WishDownButton_Click(object sender, EventArgs e) => ItemListDown(_wishControls);

        private void WishBlacklistAddInput_TextChanged(object sender, EventArgs e) => TryItemBoxTextChanged(_wishBlacklistControls, out _);

        private void WishBlacklistAddInput_KeyDown(object sender, KeyEventArgs e) => ItemBoxKeyDown(e, _wishBlacklistControls);

        private void AddWishBlacklistButton_Click(object sender, EventArgs e) => ItemListAdd(_wishBlacklistControls);

        private void RemoveWishBlacklistButton_Click(object sender, EventArgs e) => ItemListRemove(_wishBlacklistControls);

        private void BeastMode_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.BeastMode = BeastMode.Checked;
        }

        private void CubePriority_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.CubePriority = CubePriority.SelectedIndex;
        }

        private void FavoredMacguffin_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.FavoredMacguffin = ((KeyValuePair<int, string>)FavoredMacguffin.SelectedItem).Key;
        }

        private void ManageNGUDiff_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.ManageNGUDiff = ManageNGUDiff.Checked;
        }

        private void ChangeProfileFile_Click(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.AllocationFile = AllocationProfileFile.SelectedItem.ToString();
            LoadAllocation();
        }

        private void TitanGoldTargets_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (_initializing) return;
            Settings.TitanGoldTargets[(int)e.Item.Tag] = e.Item.Checked;
            Settings.SaveSettings();
        }

        private void ResetTitanStatus_Click(object sender, EventArgs e)
        {
            if (_initializing) return;
            var temp = new bool[ZoneHelpers.TitanZones.Length];
            Settings.TitanMoneyDone = temp;
        }

        private void ManageGoldLoadouts_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.ManageGoldLoadouts = ManageGold.Checked;
        }

        private void ResnipeInput_ValueChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            if (TryGetValueFromNumericUpDown(ResnipeInput, out int val))
                Settings.ResnipeTime = val;
        }

        private void GoldSnipeNow_Click(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.GoldSnipeComplete = false;
        }

        private void CBlockMode_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.GoldCBlockMode = CBlockMode.Checked;
        }

        private void HarvestSafety_CheckedChanged(object sender, EventArgs e) => HarvestAllButton.Enabled = HarvestSafety.Checked;

        private void HarvestAllButton_Click(object sender, EventArgs e)
        {
            if (YggdrasilManager.AnyHarvestable())
            {
                if (LockManager.TryYggdrasilSwap(true))
                    YggdrasilManager.HarvestAll(true);
                else
                    Log("现在无法收获");
            }
        }

        private void TargetITOPOD_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.AdventureTargetITOPOD = TargetITOPOD.Checked;
        }

        private void KillTitan_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            var checkBox = (CheckBox)sender;
            if (int.TryParse(checkBox.Name.Substring(9), out var index))
            {
                Settings.TitanSwapTargets[index - 1] = checkBox.Checked;
                Settings.SaveSettings();
            }
        }

        private void TitanVersion_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            var comboBox = (ComboBox)sender;
            if (int.TryParse(comboBox.Name.Substring(5, comboBox.Name.Length - 12), out var index))
                ZoneHelpers.SetTitanVersion(index - 1, comboBox.SelectedIndex + 1);
        }

        private void TitanSwapTargets_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (_initializing) return;
            Settings.TitanSwapTargets[(int)e.Item.Tag] = e.Item.Checked;
            Settings.SaveSettings();
        }

        private void UnloadSafety_CheckedChanged(object sender, EventArgs e) => UnloadButton.Enabled = UnloadSafety.Checked;

        private void UnloadButton_Click(object sender, EventArgs e) => Loader.Unload();

        private void ITOPODCombatMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.ITOPODCombatMode = ITOPODCombatMode.SelectedIndex;
        }

        private void ITOPODOptimizeMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.ITOPODOptimizeMode = ITOPODOptimizeMode.SelectedIndex;
        }

        private void ITOPODBeastMode_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.ITOPODBeastMode = ITOPODBeastMode.Checked;
        }

        private void ITOPODAutoPush_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.ITOPODAutoPush = ITOPODAutoPush.Checked;
        }

        private void DisableOverlay_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.DisableOverlay = DisableOverlay.Checked;
        }

        private void MoneyPitRunMode_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.MoneyPitRunMode = MoneyPitRunMode.Checked;
        }

        private void ShockwaveInput_TextChanged(object sender, EventArgs e) => TryItemBoxTextChanged(_shockwaveControls, out _);

        private void ShockwaveInput_KeyDown(object sender, KeyEventArgs e) => ItemBoxKeyDown(e, _shockwaveControls);

        private void ShockwaveAdd_Click(object sender, EventArgs e) => ItemListAdd(_shockwaveControls);

        private void ShockwaveRemove_Click(object sender, EventArgs e) => ItemListRemove(_shockwaveControls);

        private void ShockwavePrioUpButton_Click(object sender, EventArgs e) => ItemListUp(_shockwaveControls);

        private void ShockwavePrioDownButton_Click(object sender, EventArgs e) => ItemListDown(_shockwaveControls);

        private void UpgradeDiggers_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.UpgradeDiggers = UpgradeDiggers.Checked;
        }

        private void CastBloodSpells_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.CastBloodSpells = CastBloodSpells.Checked;
        }

        private void IronPillOnRebirth_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.IronPillOnRebirth = IronPillOnRebirth.Checked;
        }

        private void GuffAOnRebirth_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.BloodMacGuffinAOnRebirth = GuffAOnRebirth.Checked;
        }

        private void GuffBOnRebirth_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.BloodMacGuffinBOnRebirth = GuffBOnRebirth.Checked;
        }

        private void QuestCombatMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selected = QuestCombatMode.SelectedIndex;

            if (_initializing) return;
            Settings.QuestCombatMode = selected;
        }

        private void YggSwapThreshold_ValueChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            if (TryGetValueFromNumericUpDown(YggSwapThreshold, out int val))
                Settings.YggSwapThreshold = val;
        }

        private void EnemyBlacklistZone_SelectedIndexChanged(object sender, EventArgs e)
        {
            var item = (KeyValuePair<int, string>)EnemyBlacklistZone.SelectedItem;
            var values = _character.adventureController.enemyList[item.Key]
                .Select(x => new KeyValuePair<int, string>(x.spriteID, x.name)).Distinct().ToList();
            EnemyBlacklistNames.DataSource = null;
            EnemyBlacklistNames.ValueMember = "Key";
            EnemyBlacklistNames.DisplayMember = "Value";
            EnemyBlacklistNames.DataSource = values;
        }

        private void BlacklistRemoveEnemyButton_Click(object sender, EventArgs e)
        {
            var item = BlacklistedBosses.SelectedItem;
            if (item == null)
                return;

            var index = BlacklistedBosses.SelectedIndex;

            var id = (KeyValuePair<int, string>)item;

            var temp = Settings.BlacklistedBosses.ToList();
            temp.RemoveAll(x => x == id.Key);
            Settings.BlacklistedBosses = temp.ToArray();

            if (Settings.BlacklistedBosses.Length > index)
                BlacklistedBosses.SelectedIndex = index;
            else if (Settings.BlacklistedBosses.Length > 0)
                BlacklistedBosses.SelectedIndex = Settings.BlacklistedBosses.Length - 1;
        }

        private void BlacklistAddEnemyButton_Click(object sender, EventArgs e)
        {
            var item = EnemyBlacklistNames.SelectedItem;
            if (item == null)
                return;

            var id = (KeyValuePair<int, string>)item;

            // This enemy is excluded already
            if (Array.IndexOf(Settings.BlacklistedBosses, id.Key) >= 0)
                return;

            var temp = Settings.BlacklistedBosses.ToList();
            temp.Add(id.Key);
            Settings.BlacklistedBosses = temp.ToArray();

            BlacklistedBosses.SelectedIndex = Settings.BlacklistedBosses.Length - 1;
        }

        private void BoostAvgReset_Click(object sender, EventArgs e) => ResetBoostProgress();

        private void WeakPriorities_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.WeakPriorities = WeakPriorities.Checked;
        }

        private void ManageMayo_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.ManageMayo = BalanceMayo.Checked;
        }

        private void TrashCards_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.TrashCards = TrashCards.Checked;
        }

        private void AutoCastCards_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.AutoCastCards = AutoCastCards.Checked;
        }

        private void CastChonkers_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.CastProtectedCards = CastProtectedCards.Checked;
        }

        private void CardRarity_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            var comboBox = (ComboBox)sender;
            if (int.TryParse(comboBox.Name.Substring(10), out var index))
                Settings.SetCardRarity(index - 1, ((KeyValuePair<int, string>)comboBox.SelectedItem).Key);
        }

        private void CardCost_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            var comboBox = (ComboBox)sender;
            if (int.TryParse(comboBox.Name.Substring(8), out var index))
                Settings.SetCardCost(index - 1, (int)comboBox.SelectedItem);
        }

        private void OpenSettingsFolder_Click(object sender, EventArgs e) => Process.Start(GetSettingsDir());

        private void OpenProfileFolder_Click(object sender, EventArgs e) => Process.Start(GetProfilesDir());

        private void ProfileEditButton_Click(object sender, EventArgs e)
        {
            var filename = Settings.AllocationFile + ".json";
            var path = Path.Combine(GetProfilesDir(), filename);
            Process.Start(path);
        }

        private void TrashProtectedCards_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.TrashProtectedCards = TrashProtectedCards.Checked;
        }

        private void TitanCombatMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.TitanCombatMode = TitanCombatMode.SelectedIndex;
        }

        private void TitanBeastMode_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.TitanBeastMode = TitanBeastMode.Checked;
        }

        private void ManageConsumables_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.ManageConsumables = ManageConsumables.Checked;
        }

        private void AutoBuyAdv_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.AutoBuyAdventure = AutoBuyAdv.Checked;
        }

        private void AutoBuyConsumables_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.AutoBuyConsumables = AutoBuyConsumables.Checked;
        }

        private void ConsumeIfRunning_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.ConsumeIfAlreadyRunning = ConsumeIfRunning.Checked;
        }

        private void Autosave_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.Autosave = Autosave.Checked;
        }

        private void SortCards_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.CardSortEnabled = SortCards.Checked;
        }

        private void CardSortAdd_Click(object sender, EventArgs e)
        {
            var option = CardSortOptions.SelectedItem as StringOption;
            if (option != null && CardSortList.Items.Cast<StringOption>().All(x => x.Key != option.Key))
            {
                CardSortList.Items.Add(option);
                Settings.CardSortOrder = GetCardSortKeysFromList();
            }
        }

        private void CardSortRemove_Click(object sender, EventArgs e)
        {
            if (CardSortList.SelectedItem != null)
            {
                CardSortList.Items.RemoveAt(CardSortList.SelectedIndex);
                Settings.CardSortOrder = GetCardSortKeysFromList();
            }
        }

        private void CardSortUp_Click(object sender, EventArgs e) => SimpleListMove(CardSortList, Settings.CardSortOrder, Direction.Up);

        private void CardSortDown_Click(object sender, EventArgs e) => SimpleListMove(CardSortList, Settings.CardSortOrder, Direction.Down);

        private void LocateWalderp_Click(object sender, EventArgs e)
        {
            if (_character.waldoUnlocker.currentMenu >= 0)
                _character.menuSwapper.swapMenu(_character.waldoUnlocker.currentMenu);
        }

        private void ManageCooking_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.ManageCooking = ManageCooking.Checked;
        }

        private void ManageCookingLoadout_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.ManageCookingLoadouts = ManageCookingLoadout.Checked;
        }

        private void CookingLoadoutBox_TextChanged(object sender, EventArgs e) => TryItemBoxTextChanged(_cookingControls, out _);

        private void CookingLoadoutItem_KeyDown(object sender, KeyEventArgs e) => ItemBoxKeyDown(e, _cookingControls);

        private void CookingAddButton_Click(object sender, EventArgs e) => ItemListAdd(_cookingControls);

        private void CookingRemoveButton_Click(object sender, EventArgs e) => ItemListRemove(_cookingControls);

        private void DiggerCap_ValueChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.DiggerCap = (double)(DiggerCap.Value);
        }

        private void ManageWishes_CheckedChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.ManageWishes = ManageWishes.Checked;
        }

        private void WishLimit_ValueChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            if (TryGetValueFromNumericUpDown(WishLimit, out int val))
                Settings.WishLimit = val;
        }

        private void WishMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.WishMode = WishMode.SelectedIndex;
        }

        private void WishEnergy_ValueChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.WishEnergy = (double)WishEnergy.Value;
        }

        private void WishMagic_ValueChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.WishMagic = (double)WishMagic.Value;
        }

        private void WishR3_ValueChanged(object sender, EventArgs e)
        {
            if (_initializing) return;
            Settings.WishR3 = (double)WishR3.Value;
        }
    }
}
