using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

[HarmonyPatch(typeof(HUDManager), "AddChatMessage")]
public static class ChatPatch
{
    private static VoicevoxClient voicevoxClient = new VoicevoxClient();
    private static GameObject audioObject = new GameObject("CustomAudioPlayer");
    
    static void Prefix(HUDManager __instance,string chatMessage, string nameOfUserWhoTyped,int playerWhoSent,bool dontRepeat)
    {
        if (dontRepeat && __instance.lastChatMessage == chatMessage)
        {
            return;
        }
        if ((int)GameNetworkManager.Instance.localPlayerController.playerClientId == playerWhoSent)
        {
            return;
        }
        
        if (!string.IsNullOrEmpty(nameOfUserWhoTyped))
        {
            
            UnityEngine.Debug.Log($"[ChatLogger] {nameOfUserWhoTyped}: {chatMessage}");
            int speaker = Voicevox.Voicevox.speakers[0];
            if (playerWhoSent != -1)speaker = Voicevox.Voicevox.speakers[playerWhoSent % 4];
            Task.Run(() => TextToVoice(chatMessage, speaker));
        }
    }

    private static async Task TextToVoice(string text,int speaker)
    {
        var voiceData = await voicevoxClient.GetVoicevoxAudio(text,speaker);
        if(voiceData == null)return;

        var clip = WavUtility.ToAudioClip(voiceData);
        if(clip == null)return;
        AudioSource audioSource = audioObject.AddComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.Play();
    }
}




public class VoicevoxClient
{

    public async Task<byte[]?> GetVoicevoxAudio(string text, int speaker = 1)
    {
        byte[] result;
        using (var httpClient = new HttpClient())
        {
            string query;
            // 音声クエリを生成
            using (var request = new HttpRequestMessage(new HttpMethod("POST"),
                       $"http://localhost:50021/audio_query?text={text}&speaker={speaker}"))
            {
                request.Headers.TryAddWithoutValidation("accept", "application/json");

                request.Content = new StringContent("");
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");

                var response = await httpClient.SendAsync(request);

                query = response.Content.ReadAsStringAsync().Result;
                query = EditVolume(query);
            }
            
            // 音声クエリから音声合成
            using (var request = new HttpRequestMessage(new HttpMethod("POST"), "http://localhost:50021/synthesis?speaker=1&enable_interrogative_upspeak=true"))
            {
                request.Headers.TryAddWithoutValidation("accept", "audio/wav");

                request.Content = new StringContent(query);
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

                var response = await httpClient.SendAsync(request);
                result = await response.Content.ReadAsByteArrayAsync();
                
            }
        }
        return result;
    }

    public static string EditVolume(string query)
    {

        // JSON文字列をDictionaryに変換
        Dictionary<string, object> dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(query);

        
        if (dict.ContainsKey("volumeScale"))
        {
            dict["volumeScale"] = Voicevox.Voicevox.voiceVolume/10;
        }

        // Dictionaryを再びJSON文字列に変換
        string updatedQuery = JsonConvert.SerializeObject(dict, Formatting.Indented);

        return updatedQuery;
    }
}


public static class WavUtility
{
    public static AudioClip ToAudioClip(byte[] wavData)
    {
        int channels = BitConverter.ToInt16(wavData, 22); // チャンネル数 (mono=1, stereo=2)
        int sampleRate = BitConverter.ToInt32(wavData, 24); // サンプルレート
        int bitsPerSample = BitConverter.ToInt16(wavData, 34); // ビット深度
        int dataStart = FindDataChunk(wavData); // データチャンクの開始位置
        int dataLength = BitConverter.ToInt32(wavData, dataStart - 4); // データサイズ

        float[] pcmData = new float[dataLength / (bitsPerSample / 8)];
        for (int i = 0; i < pcmData.Length; i++)
        {
            int offset = dataStart + i * (bitsPerSample / 8);
            if (bitsPerSample == 16)
            {
                short sample = BitConverter.ToInt16(wavData, offset);
                pcmData[i] = sample / 32768f;
            }
            else if (bitsPerSample == 8)
            {
                pcmData[i] = (wavData[offset] - 128) / 128f;
            }
        }

        // AudioClipを作成
        AudioClip clip = AudioClip.Create(
            name: "VoicevoxClip",
            lengthSamples: pcmData.Length / channels,
            channels: channels,
            frequency: sampleRate,
            stream: false
        );
        clip.SetData(pcmData, 0);
        return clip;
    }

    private static int FindDataChunk(byte[] wavData)
    {
        for (int i = 0; i < wavData.Length - 4; i++)
        {
            if (wavData[i] == 'd' && wavData[i + 1] == 'a' && wavData[i + 2] == 't' && wavData[i + 3] == 'a')
            {
                return i + 8; 
            }
        }
        throw new System.Exception("WAV data chunk not found");
    }
}