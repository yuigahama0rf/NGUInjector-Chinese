using System;
using System.Collections.Generic;
using System.Linq;
using static NGUInjector.Main;

namespace NGUInjector.Managers
{
    public class FixedSizedQueue
    {
        private Queue<float> queue = new Queue<float>();

        public int Size { get; private set; }

        public FixedSizedQueue(int size)
        {
            Size = size;
        }

        public void Enqueue(float obj)
        {
            queue.Enqueue(obj);

            while (queue.Count > Size)
                queue.Dequeue();
        }

        public void Reset() => queue.Clear();

        public float Avg()
        {
            try
            {
                return queue.Average();
            }
            catch (Exception e)
            {
                Log(e.Message);
                return 0;
            }
        }
    }

    public class Cube
    {
        public float Power { get; set; }

        public float Toughness { get; set; }

        public bool Changed(float power, float toughness) => Power != power || Toughness != toughness;
    }

    public static class InventoryManager
    {
        private static readonly Character _character = Main.Character;
        private static readonly InventoryController _ic = Main.InventoryController;
        // Pendants, Lootys, Wanderer's Cane, Lonely Flubber, A Giant Seed
        private static readonly int[] _convertibles = { 53, 67, 76, 92, 94, 120, 128, 142, 154, 169, 170, 229, 230, 295, 296, 388, 389, 430, 431, 504, 505 };
        private static readonly int[] _filterExcludes = { 119, 129, 162, 171, 195, 196, 212, 293, 297, 344, 390 }; // Lemmi and Hearts
        private static BoostsNeeded _previousBoostsNeeded;
        private static readonly Cube _cube = new Cube { Power = Inventory.cubePower, Toughness = Inventory.cubeToughness };
        private static readonly FixedSizedQueue _invBoostAvg = new FixedSizedQueue(60);
        private static readonly FixedSizedQueue _cubeBoostAvg = new FixedSizedQueue(60);
        private static int[] _savedMacguffins = null;
        private static int _daycareSlot = -1;

        private static Inventory Inventory => _character.inventory;

        public static readonly Dictionary<int, string> macguffinList = new Dictionary<int, string>
        {
            { -1, "无" },
            { 198, "能量强度" },
            { 199, "能量上限" },
            { 200, "魔力强度" },
            { 201, "魔力上限" },
            { 202, "能量 NGU" },
            { 203, "魔力 NGU" },
            { 204, "能量条" },
            { 205, "魔力条" },
            { 206, "性感" },
            { 207, "聪明" },
            { 208, "掉落率" },
            { 209, "黄金" },
            { 210, "挂件" },
            { 211, "能量 Wandoos" },
            { 228, "属性" },
            { 250, "魔力 Wandoos" },
            { 289, "增数" },
            { 290, "血液" },
            { 291, "冒险" },
            { 298, "资源3强度" },
            { 299, "资源3上限" },
            { 300, "资源3条" }
        };

        public static void Reset()
        {
            _invBoostAvg.Reset();
            _cubeBoostAvg.Reset();
        }

        public static ih[] GetBoostSlots(ih[] ci)
        {
            var result = new List<ih>();
            // First, find items in our priority list
            foreach (var id in Settings.PriorityBoosts.Except(Settings.BoostBlacklist))
            {
                var f = LoadoutManager.FindItemSlot(id);
                if (f?.equipment.isEquipment() == true)
                    result.Add(f);
            }

            // Next, get equipped items that aren't in our priority list and aren't blacklisted
            var equipped = Inventory.GetConvertedEquips().Where(x => !IsPriority(x) && !IsBlacklisted(x));
            result.AddRange(equipped);

            // Finally, find locked items in inventory that aren't blacklisted
            var invItems = Array.FindAll(ci, x => x.locked && x.equipment.isEquipment() && !IsPriority(x) && !IsBlacklisted(x));
            result.AddRange(invItems);

            // Make sure we filter out non-equips again, just in case one snuck into priorityboosts
            return result.FindAll(x => x.equipment.GetNeededBoosts().Total() > 0).ToArray();
        }

        public static void BoostInventory(ih[] boostSlots)
        {
            foreach (var item in boostSlots)
            {
                if (!Inventory.HasBoosts())
                    break;
                _ic.applyAllBoosts(item.slot);
            }
        }

        private static int ChangePage(int slot)
        {
            var page = slot / 60;
            _ic.changePage(page);
            return slot - (page * 60);
        }

        public static void BoostInfinityCube()
        {
            if (!Inventory.HasBoosts())
                return;
            _ic.infinityCubeAll();
        }

        public static void MergeEquipped(ih[] ci)
        {
            if (!IsBlacklisted(Inventory.head.id) && Array.Exists(ci, x => x.id == Inventory.head.id))
                _ic.mergeAll(-1);

            if (!IsBlacklisted(Inventory.chest.id) && Array.Exists(ci, x => x.id == Inventory.chest.id))
                _ic.mergeAll(-2);

            if (!IsBlacklisted(Inventory.legs.id) && Array.Exists(ci, x => x.id == Inventory.legs.id))
                _ic.mergeAll(-3);

            if (!IsBlacklisted(Inventory.boots.id) && Array.Exists(ci, x => x.id == Inventory.boots.id))
                _ic.mergeAll(-4);

            if (!IsBlacklisted(Inventory.weapon.id) && Array.Exists(ci, x => x.id == Inventory.weapon.id))
                _ic.mergeAll(-5);

            if (_ic.weapon2Unlocked())
            {
                if (!IsBlacklisted(Inventory.weapon2.id) && Array.Exists(ci, x => x.id == Inventory.weapon2.id))
                    _ic.mergeAll(-6);
            }

            // Merge Accessories
            for (var i = 10000; _ic.accessoryID(i) < _ic.accessorySpaces(); i++)
            {
                int id = _ic.accessoryID(i);
                if (!IsBlacklisted(Inventory.accs[id].id) && Array.Exists(ci, x => x.id == Inventory.accs[id].id))
                    _ic.mergeAll(i);
            }
        }

        public static void MergeBoosts(ih[] ci)
        {
            var grouped = Array.FindAll(ci, x => IsBoost(x) && !IsBlacklisted(x) && IsLocked(x) && !IsMaxxed(x));
            foreach (var target in grouped)
            {
                if (ci.Count(x => x.id == target.id) <= 1)
                    continue;
                Log($"Merging {target.name} in slot {target.slot}");
                _ic.mergeAll(target.slot);
            }
        }

        private static string SanitizeName(string name)
        {
            if (name.Contains("\n"))
                name = name.Split('\n').Last();

            return name;
        }

        public static void ManageQuestItems(ih[] ci)
        {
            int curPage = _ic.inventory[0].id / 60;

            var questItems = Array.FindAll(ci, x => IsQuest(x) && !IsBlacklisted(x) && IsLocked(x) && !IsMaxxed(x));

            // Merge non-maxxed quest items first
            foreach (var item in questItems)
            {
                Log($"Merging {SanitizeName(item.name)} in slot {item.slot}");
                _ic.mergeAll(item.slot);
            }

            // Consume quest items that dont need to be merged
            var quest = Main.Character.beastQuest;
            if (quest.inQuest)
            {
                int num = quest.curDrops;
                _ic.dumpAllIntoQuest(quest.questID);
                if (quest.curDrops > num)
                    Log($"Turning in {quest.curDrops - num} quest items");
            }
        }

        public static void MergeInventory(ih[] ci)
        {
            var filtered = Array.FindAll(ci, x => !IsBoost(x) && x.level < 100 && !IsCooking(x) && !IsBlacklisted(x) && !IsGuff(x) && !IsQuest(x));
            var grouped = filtered.GroupBy(x => x.id).Where(x => x.Count() > 1);

            foreach (var item in grouped)
            {
                if (item.All(x => x.locked))
                    continue;

                ih target = item.MaxItem();

                Log($"Merging {SanitizeName(target.name)} in slot {target.slot}");
                _ic.mergeAll(target.slot);
            }
        }

        public static void MergeGuffs(ih[] ci)
        {
            for (var id = 0; id < Inventory.macguffins.Count; ++id)
            {
                int guffId = Inventory.macguffins[id].id;
                if (!IsBlacklisted(guffId) && Array.Exists(ci, x => x.id == guffId))
                    _ic.mergeAll(_ic.globalMacguffinID(id));
            }

            var invGuffs = Array.FindAll(ci, x => IsGuff(x) && !IsBlacklisted(x)).GroupBy(x => x.id).Where(x => x.Count() > 1);
            foreach (var guff in invGuffs)
            {
                ih target = guff.MaxItem();
                _ic.mergeAll(target.slot);
            }
        }

        public static void ManageConvertibles(ih[] ci)
        {
            int curPage = _ic.inventory[0].id / 60;
            var grouped = ci.Where(x => Array.BinarySearch(_convertibles, x.id) >= 0);
            foreach (var item in grouped)
            {
                if (item.level != 100)
                    continue;
                var temp = Inventory.inventory[item.slot];
                if (!temp.removable)
                    continue;
                var newSlot = ChangePage(item.slot);
                _ic.inventory[newSlot].CallMethod("consumeItem");
            }
            _ic.changePage(curPage);
        }

        public static void ShowBoostProgress(ih[] boostSlots)
        {
            var needed = new BoostsNeeded();

            foreach (var item in boostSlots)
                needed += item.equipment.GetNeededBoosts();

            float current = needed.Total();

            if (current > 0)
            {
                if (_previousBoostsNeeded == null)
                {
                    Log($"Boosts Needed to Green: {needed.power} Power, {needed.toughness} Toughness, {needed.special} Special");
                    _previousBoostsNeeded = needed;
                }
                else
                {
                    float old = _previousBoostsNeeded.Total();

                    var diff = current - old;
                    if (diff == 0)
                        return;

                    // If diff is > 0, then we either added another item to boost or we levelled something. Don't add the diff to average
                    if (diff <= 0)
                        _invBoostAvg.Enqueue(-diff);

                    Log($"Boosts Needed to Green: {needed.power} Power, {needed.toughness} Toughness, {needed.special} Special");
                    float average = _invBoostAvg.Avg();
                    if (average > 0)
                    {
                        var eta = current / average;
                        Log($"Last Minute: {diff}. Average Per Minute: {average:0}. ETA: {eta:0} minutes.");
                    }
                    else
                    {
                        Log($"Last Minute: {diff}.");
                    }

                    _previousBoostsNeeded = needed;
                }
            }

            var power = Inventory.cubePower;
            var toughness = Inventory.cubeToughness;

            if (_cube.Changed(power, toughness))
            {
                var output = "Cube Progress:";
                float toughnessDiff = toughness - _cube.Toughness;
                float powerDiff = power - _cube.Power;

                output = toughnessDiff > 0 ? $"{output} {toughnessDiff} Toughness." : output;
                output = powerDiff > 0 ? $"{output} {powerDiff} Power." : output;

                _cubeBoostAvg.Enqueue(toughnessDiff + powerDiff);
                output = $"{output} Average Per Minute: {_cubeBoostAvg.Avg():0}";
                Log(output);
                Log($"Cube Power: {power} ({_ic.cubePowerSoftcap()} softcap). Cube Toughness: {toughness} ({_ic.cubeToughnessSoftcap()} softcap)");

                _cube.Power = power;
                _cube.Toughness = toughness;
            }
        }

        public static void ManageBoostConversion(ih[] boostSlots)
        {
            if (_character.challenges.levelChallenge10k.curCompletions < _character.allChallenges.level100Challenge.maxCompletions)
                return;

            if (!Settings.AutoConvertBoosts)
                return;

            var converted = Inventory.GetConvertedInventory();
            // If we have a boost locked, we want to stay on that until its maxxed
            var lockedBoosts = converted.Where(x => x.id < 40 && x.locked);
            if (lockedBoosts.Any())
            {
                // Unlock level 100 boosts
                lockedBoosts.Where(x => x.level == 100).ToList().ForEach(maxLockedBoost => Inventory.inventory[maxLockedBoost.slot].removable = true);

                int? minId = lockedBoosts.Where(x => x.level != 100).DefaultIfEmpty().Min(x => x.id);
                if (minId <= 13)
                    _ic.selectAutoPowerTransform();
                else if (minId <= 26)
                    _ic.selectAutoToughTransform();
                else if (minId <= 39)
                    _ic.selectAutoSpecialTransform();
                else
                    return;
            }

            var needed = new BoostsNeeded();

            foreach (var item in boostSlots)
                needed += item.equipment.GetNeededBoosts();

            string[] boostPriorities = Settings.BoostPriority.Length > 0 ? Settings.BoostPriority : new string[] { "Power", "Toughness", "Special" };

            foreach (var boostPriority in boostPriorities)
            {
                switch (boostPriority)
                {
                    case "Power":
                        if (needed.power > 0)
                        {
                            _ic.selectAutoPowerTransform();
                            return;
                        }
                        break;
                    case "Toughness":
                        if (needed.toughness > 0)
                        {
                            _ic.selectAutoToughTransform();
                            return;
                        }
                        break;
                    case "Special":
                        if (needed.special > 0)
                        {
                            _ic.selectAutoSpecialTransform();
                            return;
                        }
                        break;
                }
            }

            switch (Settings.CubePriority)
            {
                case 0:
                    _ic.selectAutoNoneTransform();
                    return;
                case 1:
                    if (Inventory.cubePower > Inventory.cubeToughness)
                        _ic.selectAutoToughTransform();
                    else
                        _ic.selectAutoPowerTransform();
                    return;
                case 2:
                    if (Inventory.cubePower / _ic.cubePowerSoftcap() > Inventory.cubeToughness / _ic.cubeToughnessSoftcap())
                        _ic.selectAutoToughTransform();
                    else
                        _ic.selectAutoPowerTransform();
                    return;
                case 3:
                    _ic.selectAutoPowerTransform();
                    return;
                case 4:
                    _ic.selectAutoToughTransform();
                    return;
            }
        }

        public static int MoveFromDaycareToInventory(Inventory inv, int slot)
        {
            int emptySlot = inv.inventory.FindIndex(x => x.id == 0);
            if (emptySlot < 0 || emptySlot > inv.inventory.Count)
                return -1;

            inv.item1 = slot;
            inv.item2 = emptySlot;

            _ic.swapDaycare();

            return emptySlot;
        }

        public static int MoveFromMacguffinsToInventory(Inventory inv, int slot)
        {
            int emptySlot = inv.inventory.FindIndex(x => x.id == 0);
            if (emptySlot < 0 || emptySlot > inv.inventory.Count)
                return -1;

            inv.item1 = slot;
            inv.item2 = emptySlot;

            _ic.swapMacguffin();

            return emptySlot;
        }

        public static void ManageFavoredMacguffin(bool spell = false, bool fruit = false)
        {
            if (Settings.FavoredMacguffin < 0)
                return;

            var inventory = Main.Character.inventory;
            int slot;
            _savedMacguffins = inventory.macguffins.Select(x => x.id).ToArray();
            if (Array.Exists(_savedMacguffins, x => x == Settings.FavoredMacguffin))
            {
                _daycareSlot = -1;
                slot = Array.IndexOf(_savedMacguffins, Settings.FavoredMacguffin) + 1000000;
            }
            else if (inventory.inventory.Exists(x => x.id == Settings.FavoredMacguffin))
            {
                _daycareSlot = -1;
                // Equip highest level MacGuffin
                var item = inventory.inventory.Where(x => x.id == Settings.FavoredMacguffin).AllMaxBy(x => x.level).First();
                slot = inventory.inventory.IndexOf(item);
            }
            else if (inventory.daycare.Exists(x => x.id == Settings.FavoredMacguffin))
            {
                var item = inventory.daycare.First(x => x.id == Settings.FavoredMacguffin);
                _daycareSlot = inventory.daycare.IndexOf(item) + 100000;
                slot = MoveFromDaycareToInventory(inventory, _daycareSlot);
                if (slot < 0)
                {
                    try
                    {
                        Log("Failed to move an item from daycare: missing empty slots in the inventory.");
                    }
                    catch (Exception)
                    {
                        // pass
                    }
                    return;
                }
            }
            else
            {
                _daycareSlot = -1;
                return;
            }

            if (slot != 1000000)
            {
                inventory.item2 = slot;
                inventory.item1 = 1000000;
                _ic.swapMacguffin();
            }

            if ((!spell || Main.Character.wishes.wishes[24].level <= 0) && (!fruit || Main.Character.wishes.wishes[25].level <= 0))
            {
                for (var i = 1; i < inventory.macguffins.Count; i++)
                {
                    if (inventory.macguffins[i].id != Settings.FavoredMacguffin)
                    {
                        if (MoveFromMacguffinsToInventory(inventory, i + 1000000) < 0)
                        {
                            try
                            {
                                Log("Failed to unequip a macguffin: missing empty slots in the inventory.");
                            }
                            catch (Exception)
                            {
                                // pass
                            }
                            break;
                        }
                    }
                }
            }

            _ic.updateBonuses();
            _ic.updateInventory();
        }

        public static void RestoreMacguffins()
        {
            if (_savedMacguffins?.Length > 0 == false)
                return;

            var macguffins = Inventory.macguffins.Select(x => x.id);
            var allMacguffins = Inventory.macguffins.Select((x, i) => (equip: x, i: i + 1000000)).Where(x => x.equip.id != 0);
            allMacguffins = allMacguffins.Union(Inventory.inventory.Select((x, i) => (equip: x, i)).Where(x => x.equip.id != 0));
            for (var i = 0; i < _savedMacguffins.Length; i++)
            {
                if (_savedMacguffins[i] != macguffins.ElementAt(i))
                {
                    if (allMacguffins.Any(x => x.equip.id == _savedMacguffins[i]))
                    {
                        Inventory.item1 = i + 1000000;
                        // Equip highest level MacGuffins
                        Inventory.item2 = allMacguffins.Where(x => x.equip.id == _savedMacguffins[i]).AllMaxBy(x => x.equip.level).First().i;

                        _ic.swapMacguffin();
                    }
                    else
                    {
                        Log($"Failed to find a macguffin with id {_savedMacguffins[i]}.");
                    }
                }
            }

            if (_daycareSlot >= 0)
            {
                var favMacguffins = allMacguffins.Where(x => x.i < 1000000 && x.equip.id == Settings.FavoredMacguffin);
                if (favMacguffins.Any())
                {
                    Inventory.item1 = _daycareSlot;
                    // Put lowest level MacGuffin into daycare
                    Inventory.item2 = favMacguffins.AllMinBy(x => x.equip.level).First().i;

                    _ic.swapDaycare();
                }
                else
                {
                    Log($"Failed to find a macguffin with id {Settings.FavoredMacguffin}.");
                }
            }

            _savedMacguffins = null;
            _daycareSlot = -1;

            _ic.updateBonuses();
            _ic.updateInventory();
        }

        #region Filtering
        public static void EnsureFiltered(ih[] ci)
        {
            if (!_character.arbitrary.lootFilter)
                return;

            var targets = Array.FindAll(ci, x => x.level == 100);
            foreach (var target in targets)
                FilterItem(target.id);

            FilterEquip(Inventory.head);
            FilterEquip(Inventory.boots);
            FilterEquip(Inventory.chest);
            FilterEquip(Inventory.legs);
            FilterEquip(Inventory.weapon);
            if (_ic.weapon2Unlocked())
                FilterEquip(Inventory.weapon2);

            foreach (var acc in Inventory.accs)
                FilterEquip(acc);
        }

        private static void FilterItem(int id)
        {
            // Don't filter out wandoos 98 if it is not level 100
            if (id == 66 && _character.wandoos98.OSlevel < 100L)
                return;

            // Don't filter out wandoos XL if it is not level 100
            if (id == 163 && _character.wandoos98.XLLevels < 100L)
                return;

            // Don't filter out convertibles
            if (Array.BinarySearch(_convertibles, id) >= 0)
                return;

            // Don't filter out MacGuffins
            if (macguffinList.ContainsKey(id))
                return;

            // Don't filter out cooking
            if (id >= 367 && id <= 372)
                return;

            // Don't filter out Lemmi and hearts
            if (Array.BinarySearch(_filterExcludes, id) >= 0)
                return;

            // Dont filter out quest items
            if (id >= 278 && id <= 287)
                return;

            // Don't filter out boosts
            if (id < 40)
                return;

            Inventory.itemList.itemFiltered[id] = true;
        }

        private static void FilterEquip(Equipment e)
        {
            if (e.level == 100)
                FilterItem(e.id);
        }
        #endregion

        #region Lambda
        private static bool IsPriority(ih x) => Settings.PriorityBoosts.Contains(x.id);

        private static bool IsBlacklisted(ih x) => Settings.BoostBlacklist.Contains(x.id);

        private static bool IsBlacklisted(int id) => Settings.BoostBlacklist.Contains(id);

        private static bool IsLocked(ih x) => !Inventory.inventory[x.slot].removable;

        private static bool IsMaxxed(ih x) => Inventory.itemList.itemMaxxed[x.id];

        private static bool IsBoost(ih x) => x.id >= 1 && x.id <= 39;

        private static bool IsQuest(ih x) => x.id >= 278 && x.id <= 287;

        private static bool IsGuff(ih x) => macguffinList.ContainsKey(x.id);

        private static bool IsCooking(ih x) => x.id >= 367 && x.id <= 372;
        #endregion
    }

    public class ih
    {
        public int slot;
        public string name;
        public int level;
        public bool locked;
        public int id;
        public Equipment equipment;
    }

    public class BoostsNeeded
    {
        public float power;
        public float toughness;
        public float special;

        public BoostsNeeded(float power = 0f, float toughness = 0f, float special = 0f)
        {
            this.power = power;
            this.toughness = toughness;
            this.special = special;
        }

        public static BoostsNeeded operator +(BoostsNeeded boostsNeeded, BoostsNeeded other)
        {
            return new BoostsNeeded(
                boostsNeeded.power + other.power,
                boostsNeeded.toughness + other.toughness,
                boostsNeeded.special + other.special
            );
        }

        public float Total() => power + toughness + special;
    }
}
