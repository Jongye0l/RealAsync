using System;
using JALib.Core;
using JALib.Core.Patch;
using UnityEngine;
using UnityEngine.UI;
using UnityModManagerNet;
using Object = UnityEngine.Object;

namespace RealAsync;

public class Main : JAMod {
    public static Main Instance;
    public static JAPatcher Patcher;
    public static GameObject gameObject;

    public Main(UnityModManager.ModEntry modEntry) : base(modEntry, false) {
        Instance = this;
        Patcher = new JAPatcher(this);
        Patcher.AddPatch(typeof(RealAsyncManager));
    }

    protected override void OnEnable() {
        if(ADOBase.platform != Platform.Windows) {
            ModEntry.Info.DisplayName = Name + " <color=red>[Needs Windows]</color>";
            throw new NotSupportedException("RealAsync is only available on Windows.");
        }
        ModEntry.Info.DisplayName = Name;
        SetupWaterMark();
        RealAsyncManager.Initialize();
        Patcher.Patch();
    }

    public void Error() {
        ModEntry.Info.DisplayName = Name + " <color=red>[Error]</color>";
        Disable();
    }

    protected override void OnDisable() {
        if(gameObject) Object.Destroy(gameObject);
        Patcher.Unpatch();
        RealAsyncManager.Dispose();
    }

    public static void SetupWaterMark() {
        gameObject = new GameObject("RealAsyncCanvas");
        Object.DontDestroyOnLoad(gameObject);
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;
        CanvasScaler canvasScaler = gameObject.AddComponent<CanvasScaler>();
        canvasScaler.referenceResolution = new Vector2(1920, 1080);
        gameObject.AddComponent<GraphicRaycaster>();
        GameObject textObject = new("RealAsyncText");
        textObject.transform.SetParent(gameObject.transform);
        RectTransform textTransform = textObject.AddComponent<RectTransform>();
        textTransform.anchorMin = textTransform.anchorMax = new Vector2(0.5f, 1f);
        textTransform.anchoredPosition = new Vector2(0, -10);
        textTransform.sizeDelta = new Vector2(300, 100);
        Text text = textObject.AddComponent<Text>();
        text.font = RDString.GetFontDataForLanguage(RDString.language).font;
        text.fontSize = 25;
        text.text = "Real Async Used";
        text.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
    }
}