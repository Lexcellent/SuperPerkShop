using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Duckov.Economy;
using Duckov.UI;
using HarmonyLib;
using ItemStatsSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SuperPerkShop
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private Harmony? _harmony = null;
        private const string MerchantID = "Super_Merchant_Normal";

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
        }

        protected override void OnBeforeDeactivate()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchAll();
            }
            SceneManager.sceneLoaded -= OnAfterSceneInit;
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
                        merchantIDField.SetValue(stockShop, MerchantID);
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
                        if (profile.merchantID == MerchantID)
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
                        merchantProfile.merchantID = MerchantID;
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
                                !itemEntry.prefab.name.ToLower().EndsWith("template") &&
                                itemEntry.prefab.Icon != null && itemEntry.prefab.Icon.name != "cross")
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

        private List<int> _vaildItemIds = new List<int>();

        bool ItemIdIsVaild(int itemId)
        {
            // return _vaildItemIds.Contains(itemId);
            return itemId >= 0;
        }

        // 处理无效配方
        void FixCrafting()
        {
            // 获取 CraftingManager 类型
            Type craftingManagerType = typeof(CraftingManager);

            // 获取 unlockedFormulaIDs 字段（注意是 GetField 而不是 GetProperty）
            FieldInfo? unlockedFormulaIDsField = craftingManagerType.GetField("unlockedFormulaIDs",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (unlockedFormulaIDsField != null)
            {
                // 获取字段值
                var unlockedFormulas = unlockedFormulaIDsField.GetValue(CraftingManager.Instance) as List<string>;

                // 如果用户加载慢 配方总合集可能是空的 不做处理
                if (unlockedFormulas != null && unlockedFormulas.Count > 0 &&
                    CraftingFormulaCollection.Instance.Entries.Count > 0)
                {
                    // 使用 unlockedFormulas 列表
                    Debug.Log($"已解锁配方数量: {unlockedFormulas.Count}");
                    var newUnlockedFormulas = new List<string>();
                    var invalid = 0;
                    foreach (var unlockedFormula in unlockedFormulas)
                    {
                        // 已解锁配方是否找到了对应的定义
                        var found = false;


                        foreach (var craftingFormula in CraftingFormulaCollection.Instance.Entries)
                        {
                            // Debug.Log($"配方ID:{craftingFormula.id},原材料种类数:{craftingFormula.cost.items.Length}");
                            if (craftingFormula.IDValid && craftingFormula.id == unlockedFormula)
                            {
                                // 配方的最终产物是否有效
                                if (!ItemIdIsVaild(craftingFormula.result.id))
                                {
                                    Debug.Log($"配方:{craftingFormula.id} 最终产物:{craftingFormula.result.id}无效");
                                    break;
                                }

                                var costIsVaild = true;
                                // 配方的原材料是否有效
                                foreach (var itemEntry in craftingFormula.cost.items)
                                {
                                    if (!ItemIdIsVaild(itemEntry.id))
                                    {
                                        Debug.Log($"配方:{craftingFormula.id} 原材料:{craftingFormula.result.id}无效");
                                        costIsVaild = false;
                                        break;
                                    }
                                }

                                if (costIsVaild)
                                {
                                    found = true;
                                }

                                break;
                            }
                        }

                        if (found)
                        {
                            // Debug.Log($"配方有效:{unlockedFormula}");
                            newUnlockedFormulas.Add(unlockedFormula);
                        }
                        else
                        {
                            Debug.Log($"无效配方:{unlockedFormula}");
                            invalid += 1;
                        }
                    }

                    if (invalid > 0)
                    {
                        // 设置新值
                        unlockedFormulaIDsField.SetValue(CraftingManager.Instance, newUnlockedFormulas);
                        // 调用 Save 方法
                        MethodInfo? saveMethod = craftingManagerType.GetMethod("Save",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        if (saveMethod != null)
                        {
                            try
                            {
                                // 调用 Save 方法
                                saveMethod.Invoke(CraftingManager.Instance, null);
                                // Debug.Log("✅ 成功调用配方管理器 Save 方法");
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"❌ 调用调用配方管理器 Save 方法时发生异常: {ex.Message}");
                            }
                        }
                        else
                        {
                            Debug.LogWarning("未找到调用配方管理器 Save 方法");
                        }

                        NotificationText.Push($"有{invalid}个蓝图/配方无效，已删除");
                    }
                    else
                    {
                        Debug.Log("没有无效配方");
                    }
                }
            }
            else
            {
                Debug.LogWarning("未找到 unlockedFormulaIDs 属性");
            }
        }

        void UpdateModel(GameObject superSaleMachine)
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
                superSaleMachine.name = "SuperSaleMachine";
                // 调试用 -7.4 0 -83
                // superSaleMachine.transform.position = new Vector3(-7.4f, 0f, -83f);
                // 正式用
                superSaleMachine.transform.position = new Vector3(-23f, 0f, -65.5f);
                var superPerkShop = superSaleMachine.transform.Find("PerkWeaponShop");
                var stockShop = InitShopItems(superPerkShop);

                superSaleMachine.SetActive(true);
                Debug.Log("超级售货机已激活");
                try
                {
                    // 修改模型，使用另一个版本，如果有的话
                    UpdateModel(superSaleMachine);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"❌ 修改模型时发生异常: {ex.Message}");
                }

                if (stockShop != null)
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

                    // Debug.Log("超级售货机商品已刷新");
                }

                try
                {
                    // 如果已解锁的配方 不在配方列表里 则删除处理 防止工作台无法使用
                    FixCrafting();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"❌ 修复配方时发生异常: {ex.Message}");
                }

                // NotificationText.Push("超级售货机已在训练场已生成");
            }
            else
            {
                Debug.LogWarning("未找到 Buildings/SaleMachine");
            }
        }
    }
}