using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Voicevox;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Voicevox : BaseUnityPlugin
{
    public static Voicevox Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger { get; private set; } = null!;
    internal static Harmony? Harmony { get; set; }


    public static int[] speakers;
    

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        Patch();

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
        
        
        speakers = new int[4];
        
        ConfigUtil.Init(Config);
    }

    internal static void Patch()
    {
        Harmony ??= new Harmony(MyPluginInfo.PLUGIN_GUID);


        Harmony.PatchAll();
    }
    
}


public class ConfigUtil
{
    public static void Init(ConfigFile config)
    {
        ConfigEntry<int>[] speakers = new ConfigEntry<int>[4];

        for (int i = 0; i < 4; i++)
        {
            speakers[i] = config.Bind("General", $"Player{i+1}", i+1, "");
            Voicevox.speakers[i] = speakers[i].Value;
        }
    }
}
