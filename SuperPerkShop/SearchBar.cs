using System;
using System.Reflection;
using Duckov.Economy.UI;
using Duckov.Utilities;
using HarmonyLib;
using SodaCraft.Localizations;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SuperPerkShop
{
    [HarmonyPatch(typeof(StockShopView))]
    public class SearchBar
    {
        [HarmonyPatch("OnOpen")]
        [HarmonyPostfix]
        public static void PostfixOnOpen(StockShopView __instance)
        {
            try
            {
                // 查找目标父对象
                var merchantStuff = __instance.transform.Find("Content/MerchantStuff/Content");
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
                    searchBox1.SetActive(__instance.Target.MerchantID == ModBehaviour.SuperShopMerchantID);
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
                    var entryPoolValue = entryPoolProperty?.GetValue(__instance);
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
                searchBox.SetActive(__instance.Target.MerchantID == ModBehaviour.SuperShopMerchantID);
                // inputField.onSelect.AddListener((value) => { Debug.Log("[SuperPerkShop] 输入框获得焦点"); });
                // inputField.onDeselect.AddListener((value) => { Debug.Log("[SuperPerkShop] 输入框失去焦点"); });
            }
            catch (Exception e)
            {
                Debug.Log($"SuperPerkShop模组：错误：{e.Message}");
                Debug.LogException(e);
            }
        }
    }
}