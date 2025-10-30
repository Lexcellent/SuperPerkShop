using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Duckov.Economy;
using Duckov.Economy.UI;
using Duckov.UI.Animations;
using Duckov.Utilities;
using ItemStatsSystem;
using SodaCraft.Localizations;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SuperPerkShop
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        public const string SuperShopMerchantID = "Super_Merchant_Normal";
        private const string ShopGameObjectName = "SuperSaleMachine";

        // 已添加到商店中的物品ID
        private List<int> _vaildItemIds = new List<int>();

        protected override void OnAfterSetup()
        {
            SceneManager.sceneLoaded -= OnAfterSceneInit;
            SceneManager.sceneLoaded += OnAfterSceneInit;

            StockShop.OnAfterItemSold -= ShopAutoSetItemCount;
            StockShop.OnAfterItemSold += ShopAutoSetItemCount;
        }

        protected override void OnBeforeDeactivate()
        {
            SceneManager.sceneLoaded -= OnAfterSceneInit;
            StockShop.OnAfterItemSold -= ShopAutoSetItemCount;
        }

        // 打开商店界面时 显示搜索框
        void OnFadeGroupShowComplete(FadeGroup fadeGroup)
        {
            try
            {
                var stockShopView = fadeGroup.gameObject.GetComponent<StockShopView>();
                // 查找目标父对象
                var merchantStuff = stockShopView.transform.Find("Content/MerchantStuff/Content");
                if (merchantStuff == null)
                {
                    Debug.LogWarning("Content/MerchantStuff/Content 未找到");
                    return;
                }

                // 检查是否已经存在搜索框，避免重复添加
                if (merchantStuff.Find("SearchBox") != null)
                {
                    // Debug.Log("搜索框已存在");
                    // 清空输入框
                    var searchBox1 = merchantStuff.Find("SearchBox").gameObject;
                    var tmpInputField = searchBox1.GetComponent<TMP_InputField>();
                    tmpInputField.text = string.Empty;
                    // 超级售货机才显示搜索框
                    searchBox1.SetActive(stockShopView.Target.MerchantID == SuperShopMerchantID);
                    return;
                }

                // 创建搜索框 GameObject
                GameObject searchBox = new GameObject("SearchBox");
                searchBox.transform.SetParent(merchantStuff, false);

                // 添加 CanvasRenderer
                searchBox.AddComponent<CanvasRenderer>();

                // 创建RectTransform
                RectTransform rectTransform = searchBox.AddComponent<RectTransform>();
                rectTransform.anchorMin = new Vector2(0, 1);
                rectTransform.anchorMax = new Vector2(1, 1);
                rectTransform.pivot = new Vector2(0.5f, 1);
                rectTransform.offsetMin = new Vector2(10, -60);
                rectTransform.offsetMax = new Vector2(-10, 0);
                rectTransform.anchoredPosition = Vector2.zero;

                // 防止被布局压缩
                var layoutElement = searchBox.AddComponent<LayoutElement>();
                layoutElement.minHeight = 60;
                layoutElement.preferredHeight = 60;
                layoutElement.flexibleHeight = 0;
                layoutElement.flexibleWidth = 1; // ✅ 允许宽度自动填满父级
                layoutElement.minWidth = 0;
                layoutElement.preferredWidth = -1;

                // 背景图
                var background = searchBox.AddComponent<Image>();
                background.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
                background.type = Image.Type.Sliced;

                // 创建 InputField
                var inputField = searchBox.AddComponent<TMP_InputField>();
                inputField.interactable = true;
                inputField.transition = Selectable.Transition.ColorTint;
                inputField.targetGraphic = background;

                // 文本区域容器
                GameObject textArea = new GameObject("Text Area");
                textArea.transform.SetParent(searchBox.transform, false);
                RectTransform textAreaRect = textArea.AddComponent<RectTransform>();
                textAreaRect.anchorMin = Vector2.zero;
                textAreaRect.anchorMax = Vector2.one;
                textAreaRect.offsetMin = new Vector2(10, 10);
                textAreaRect.offsetMax = new Vector2(-10, -10);
                textArea.AddComponent<RectMask2D>();

                // 文本组件
                GameObject textObject = new GameObject("Text");
                textObject.transform.SetParent(textArea.transform, false);
                var textComponent = textObject.AddComponent<TextMeshProUGUI>();
                textComponent.text = "";
                textComponent.alignment = TextAlignmentOptions.Left;
                textComponent.enableWordWrapping = false;

                // 占位符组件
                GameObject placeholderObject = new GameObject("Placeholder");
                placeholderObject.transform.SetParent(textArea.transform, false);
                var placeholderText = placeholderObject.AddComponent<TextMeshProUGUI>();
                if (LocalizationManager.CurrentLanguage == SystemLanguage.ChineseSimplified ||
                    LocalizationManager.CurrentLanguage == SystemLanguage.ChineseTraditional)
                {
                    placeholderText.text = "搜索商品...";
                }
                else if (LocalizationManager.CurrentLanguage == SystemLanguage.Korean)
                {
                    placeholderText.text = "상품 검색...";
                }
                else if (LocalizationManager.CurrentLanguage == SystemLanguage.Japanese)
                {
                    placeholderText.text = "商品検索...";
                }
                else if (LocalizationManager.CurrentLanguage == SystemLanguage.Spanish)
                {
                    placeholderText.text = "Buscar artículo...";
                }
                else if (LocalizationManager.CurrentLanguage == SystemLanguage.French)
                {
                    placeholderText.text = "Rechercher un article...";
                }
                else if (LocalizationManager.CurrentLanguage == SystemLanguage.German)
                {
                    placeholderText.text = "Artikel suchen...";
                }
                else if (LocalizationManager.CurrentLanguage == SystemLanguage.Russian)
                {
                    placeholderText.text = "Поиск товара...";
                }
                else
                {
                    placeholderText.text = "Search Item ...";
                }


                placeholderText.alignment = TextAlignmentOptions.Left;
                placeholderText.fontStyle = FontStyles.Italic;
                placeholderText.color = new Color(1, 1, 1, 0.5f);

                // 绑定 InputField 属性
                inputField.textViewport = textAreaRect;
                inputField.textComponent = textComponent;
                inputField.placeholder = placeholderText;

                // 将搜索框放在最上层
                searchBox.transform.SetAsFirstSibling();

                // 添加 EventTrigger 以确保可点击
                var eventTrigger = searchBox.AddComponent<EventTrigger>();
                var entry = new EventTrigger.Entry
                {
                    eventID = EventTriggerType.PointerClick
                };
                entry.callback.AddListener((data) =>
                {
                    inputField.Select();
                    inputField.ActivateInputField();
                });
                eventTrigger.triggers.Add(entry);

                // 搜索事件
                void RefreshItemShow(string keyword)
                {
                    // 通过反射获取 EntryPool 属性
                    var entryPoolProperty =
                        typeof(StockShopView).GetProperty("EntryPool", BindingFlags.NonPublic | BindingFlags.Instance);
                    // 获取 EntryPool 值
                    var entryPoolValue = entryPoolProperty?.GetValue(stockShopView);
                    // 现在 entryPoolValue 包含了 PrefabPool<StockShopItemEntry> 实例
                    var prefabPool = entryPoolValue as PrefabPool<StockShopItemEntry>;
                    if (prefabPool == null)
                        return;
                    foreach (var prefabPoolActiveEntry in prefabPool.ActiveEntries)
                    {
                        if (keyword == string.Empty || keyword == "")
                        {
                            prefabPoolActiveEntry.gameObject.SetActive(true);
                            continue;
                        }

                        var item = prefabPoolActiveEntry.GetItem();
                        if (item.DisplayName.Contains(keyword) || item.TypeID.ToString() == keyword)
                        {
                            prefabPoolActiveEntry.gameObject.SetActive(true);
                        }
                        else
                        {
                            prefabPoolActiveEntry.gameObject.SetActive(false);
                        }
                    }
                }

                // 添加搜索事件监听
                inputField.onDeselect.AddListener((inputText) =>
                {
                    inputText = inputText.Trim();
                    // Debug.Log($"[SuperPerkShop] 检测到搜索值：{inputText}");
                    RefreshItemShow(inputText);
                });
                inputField.text = string.Empty;
                // 超级售货机才显示搜索框
                searchBox.SetActive(stockShopView.Target.MerchantID == SuperShopMerchantID);
                // inputField.onSelect.AddListener((value) => { Debug.Log("[SuperPerkShop] 输入框获得焦点"); });
                // inputField.onDeselect.AddListener((value) => { Debug.Log("[SuperPerkShop] 输入框失去焦点"); });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SuperPerkShop模组：错误：{e.Message}");
            }
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

                var fadeGroup = StockShopView.Instance.GetComponent<FadeGroup>();
                fadeGroup.OnShowComplete -= OnFadeGroupShowComplete;
                fadeGroup.OnShowComplete += OnFadeGroupShowComplete;
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
                RefreshShop(stockShop);
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
        void RefreshShop(StockShop? stockShop)
        {
            if (stockShop == null)
            {
                Debug.LogWarning("stockShop 为空");
                return;
            }

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