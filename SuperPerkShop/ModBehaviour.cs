using System;
using System.Reflection;
using Duckov.Economy;
using ItemStatsSystem;
using UnityEngine;

namespace SuperPerkShop
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private const string MerchantID = "Super_Merchant_Normal";

        protected override void OnAfterSetup()
        {
            SceneLoader.onAfterSceneInitialize -= OnAfterSceneInit;
            SceneLoader.onAfterSceneInitialize += OnAfterSceneInit;
        }

        protected override void OnBeforeDeactivate()
        {
            SceneLoader.onAfterSceneInitialize -= OnAfterSceneInit;
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
                        // 全物品列表
                        var allItemEntries = ItemAssetsCollection.Instance.entries;
                        var merchantProfile = new StockShopDatabase.MerchantProfile();
                        merchantProfile.merchantID = MerchantID;
                        foreach (var itemEntry in allItemEntries)
                        {
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
                            }
                        }

                        merchantProfiles.Add(merchantProfile);
                    }

                    // 调用初始化方法
                    // 使用反射调用 InitializeEntries 方法
                    var initializeEntriesMethod = typeof(StockShop).GetMethod("InitializeEntries",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (initializeEntriesMethod != null)
                    {
                        try
                        {
                            initializeEntriesMethod.Invoke(stockShop, null);
                            Debug.Log($"✅ 成功调用 InitializeEntries 方法，商店库存已刷新");
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

        void OnAfterSceneInit(SceneLoadingContext context)
        {
            Debug.Log($"场景加载完成: {context.sceneName}");

            if (context.sceneName == "Base")
            {
                var find = GameObject.Find("Buildings/SaleMachine");
                if (find != null)
                {
                    Debug.Log("找到了 SaleMachine 开始克隆");
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
                                Debug.Log($"✅ 成功调用 DoRefreshStock 方法，商店库存已刷新");
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
                                Debug.Log($"✅ 成功更新 lastTimeRefreshedStock 时间戳");
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

                        Debug.Log("超级售货机商品已刷新");
                    }
                }
                else
                {
                    Debug.LogWarning("未找到 Buildings/SaleMachine");
                }
            }
        }
    }
}