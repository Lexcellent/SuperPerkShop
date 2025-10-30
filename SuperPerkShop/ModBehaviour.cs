using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Duckov.Economy;
using HarmonyLib;
using ItemStatsSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SuperPerkShop
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private Harmony? _harmony = null;
        public const string SuperShopMerchantID = "Super_Merchant_Normal";
        private const string ShopGameObjectName = "SuperSaleMachine";

        // 已添加到商店中的物品ID
        private List<int> _vaildItemIds = new List<int>();

        protected override void OnAfterSetup()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchAll();
            }

            _harmony = new Harmony("Lexcellent.SuperPerkShop");
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            SceneManager.sceneLoaded -= OnAfterSceneInit;
            SceneManager.sceneLoaded += OnAfterSceneInit;

            StockShop.OnAfterItemSold -= ShopAutoSetItemCount;
            StockShop.OnAfterItemSold += ShopAutoSetItemCount;
        }

        protected override void OnBeforeDeactivate()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchAll();
            }

            SceneManager.sceneLoaded -= OnAfterSceneInit;
            StockShop.OnAfterItemSold -= ShopAutoSetItemCount;
        }
        // 自动补货
        void ShopAutoSetItemCount(StockShop shop)
        {
            // 超级售货机 才会自动补货
            if (shop.MerchantID != SuperShopMerchantID)
                return;
            foreach (var shopEntry in shop.entries)
            {
                shopEntry.CurrentStock = shopEntry.MaxStock;
            }
        }

        void OnAfterSceneInit(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"加载场景：{scene.name}，模式：{mode.ToString()}");

            if (scene.name == "Base_SceneV2")
            {
                // 启动协程延迟执行
                StartCoroutine(DelayedSetup());
            }
        }


        IEnumerator DelayedSetup()
        {
            // 延迟1秒
            yield return new WaitForSeconds(1f);

            var find = GameObject.Find("Buildings/SaleMachine");
            if (find != null)
            {
                // Debug.Log("找到了 SaleMachine 开始克隆");
                var superSaleMachine = Instantiate(find.gameObject);
                superSaleMachine.transform.SetParent(find.transform.parent, true);
                superSaleMachine.name = ShopGameObjectName;
                // 调试用 -7.4 0 -83
                // superSaleMachine.transform.position = new Vector3(-7.4f, 0f, -83f);
                // 正式用
                superSaleMachine.transform.position = new Vector3(-23f, 0f, -65.5f);
                var superPerkShop = superSaleMachine.transform.Find("PerkWeaponShop");
                var stockShop = InitShopItems(superPerkShop);

                superSaleMachine.SetActive(true);
                // Debug.Log("超级售货机已激活");
                // 修改模型，使用另一个版本，如果有的话
                UpdateModel(superSaleMachine);

                // 刷新商店物品
                if (stockShop != null)
                {
                    RefreshShop(stockShop);
                    // Debug.Log("超级售货机商品已刷新");
                }
            }
            else
            {
                Debug.LogWarning("未找到 Buildings/SaleMachine");
            }
        }

        // 初始化商店物品
        StockShop? InitShopItems(Transform? superPerkShop)
        {
            if (superPerkShop != null)
            {
                var stockShop = superPerkShop.GetComponent<StockShop>();
                if (stockShop != null)
                {
                    stockShop.entries.Clear();
                    // 修改售价因子
                    // stockShop.sellFactor = 1f;
                    // 修改id
                    var merchantIDField = typeof(StockShop).GetField("merchantID",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (merchantIDField != null)
                    {
                        merchantIDField.SetValue(stockShop, SuperShopMerchantID);
                    }
                    else
                    {
                        Debug.LogWarning("未找到 merchantID 字段");
                    }

                    // 检查是否已添加映射
                    var isAdded = false;
                    // 商店映射
                    var merchantProfiles = StockShopDatabase.Instance.merchantProfiles;
                    foreach (var profile in merchantProfiles)
                    {
                        if (profile.merchantID == SuperShopMerchantID)
                        {
                            isAdded = true;
                            break;
                        }
                    }

                    // 未添加则将映射添加到商店映射中
                    if (!isAdded)
                    {
                        _vaildItemIds.Clear();
                        // 全物品列表
                        var allItemEntries = ItemAssetsCollection.Instance.entries;
                        var merchantProfile = new StockShopDatabase.MerchantProfile();
                        merchantProfile.merchantID = SuperShopMerchantID;
                        foreach (var itemEntry in allItemEntries)
                        {
                            // if (!itemEntry.prefab.CanBeSold &&
                            //     !itemEntry.prefab.name.ToLower().EndsWith("template") &&
                            //     itemEntry.prefab.Icon != null && itemEntry.prefab.Icon.name != "cross")
                            // {
                            //     Debug.Log($"物品无法被出售:{itemEntry.prefab.TypeID}: {itemEntry.prefab.DisplayName}");
                            // }
                            // 过滤无效物品
                            if (itemEntry.prefab.CanBeSold &&
                                itemEntry.prefab.Icon != null &&
                                itemEntry.prefab.Icon.name != "cross")
                            {
                                var entry = new StockShopDatabase.ItemEntry();

                                entry.typeID = itemEntry.typeID;
                                entry.maxStock = itemEntry.prefab.MaxStackCount;
                                entry.forceUnlock = true;
                                entry.priceFactor = 1f;
                                entry.possibility = -1f;
                                entry.lockInDemo = false;
                                merchantProfile.entries.Add(entry);
                                _vaildItemIds.Add(entry.typeID);
                            }
                        }
                        // 添加mod物品
                        var dynamicDicField = typeof(ItemAssetsCollection).GetField("dynamicDic",
                            BindingFlags.NonPublic | BindingFlags.Static);
                        if (dynamicDicField != null)
                        {
                            var dynamicDic =
                                dynamicDicField.GetValue(ItemAssetsCollection.Instance) as
                                    Dictionary<int, ItemAssetsCollection.DynamicEntry>;
                            if (dynamicDic != null)
                            {
                                foreach (var kv in dynamicDic)
                                {
                                    var itemId = kv.Key;
                                    if (!_vaildItemIds.Contains(itemId))
                                    {
                                        var dynamicEntry = kv.Value;
                                        if (dynamicEntry.prefab.CanBeSold &&
                                            dynamicEntry.prefab.Icon != null &&
                                            dynamicEntry.prefab.Icon.name != "cross")
                                        {
                                            var entry = new StockShopDatabase.ItemEntry();
                                            entry.typeID = dynamicEntry.typeID;
                                            entry.maxStock = dynamicEntry.prefab.MaxStackCount;
                                            entry.forceUnlock = true;
                                            entry.priceFactor = 1f;
                                            entry.possibility = -1f;
                                            entry.lockInDemo = false;
                                            merchantProfile.entries.Add(entry);
                                            _vaildItemIds.Add(entry.typeID);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Debug.LogWarning("dynamicDic 为空");
                            }
                        }
                        else
                        {
                            Debug.LogWarning("dynamicDicField 为空");
                        }

                        merchantProfiles.Add(merchantProfile);
                    }

                    // 调用初始化方法
                    // 使用反射调用 InitializeEntries 方法
                    var initializeEntriesMethod = typeof(StockShop).GetMethod("InitializeEntries",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (initializeEntriesMethod != null)
                    {
                        try
                        {
                            initializeEntriesMethod.Invoke(stockShop, null);
                            // Debug.Log($"✅ 成功调用 InitializeEntries 方法，商店库存已刷新");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"❌ 调用 InitializeEntries 方法时发生异常: {ex.Message}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("⚠️ 未找到 InitializeEntries 方法");
                    }

                    return stockShop;
                }
            }
            else
            {
                Debug.LogWarning("未找到 PerkWeaponShop");
            }

            return null;
        }
        // 刷新商店
        void RefreshShop(StockShop stockShop)
        {
            // 使用反射调用 DoRefreshStock 方法
            var refreshMethod = typeof(StockShop).GetMethod("DoRefreshStock",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (refreshMethod != null)
            {
                try
                {
                    refreshMethod.Invoke(stockShop, null);
                    // Debug.Log($"✅ 成功调用 DoRefreshStock 方法，商店库存已刷新");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"❌ 调用 DoRefreshStock 方法时发生异常: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning("⚠️ 未找到 DoRefreshStock 方法");
            }

            // 使用反射设置 lastTimeRefreshedStock 字段
            var lastTimeField = typeof(StockShop).GetField("lastTimeRefreshedStock",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (lastTimeField != null)
            {
                try
                {
                    lastTimeField.SetValue(stockShop, DateTime.UtcNow.ToBinary());
                    // Debug.Log($"✅ 成功更新 lastTimeRefreshedStock 时间戳");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"❌ 设置 lastTimeRefreshedStock 字段时发生异常: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning("⚠️ 未找到 lastTimeRefreshedStock 字段");
            }
        }
        // 修改商店模型
        void UpdateModel(GameObject superSaleMachine)
        {
            try
            {
                // 查找所有名为 Visual 的子对象
                var visualChildren = new List<Transform>();
                foreach (Transform child in superSaleMachine.transform)
                {
                    if (child.name == "Visual")
                    {
                        visualChildren.Add(child);
                    }
                }

                // 如果有两个 Visual 子对象
                if (visualChildren.Count == 2)
                {
                    Transform? activeVisual = null;
                    Transform? inactiveVisual = null;

                    // 分别找出已激活和未激活的 Visual
                    foreach (var visual in visualChildren)
                    {
                        if (visual.gameObject.activeSelf)
                        {
                            activeVisual = visual;
                        }
                        else
                        {
                            inactiveVisual = visual;
                        }
                    }

                    // 如果找到了已激活和未激活的 Visual，则进行切换
                    if (activeVisual != null && inactiveVisual != null)
                    {
                        activeVisual.gameObject.SetActive(false);
                        inactiveVisual.gameObject.SetActive(true);
                        // Debug.Log("✅ 成功切换 Visual 模型");
                    }
                }
                // 如果只有一个或没有 Visual 子对象，则不处理
                else if (visualChildren.Count <= 1)
                {
                    Debug.Log("Visual 子对象数量不足，无需处理");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ 修改模型时发生异常: {ex.Message}");
            }
        }
    }
}