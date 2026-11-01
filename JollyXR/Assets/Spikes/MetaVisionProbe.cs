using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Jolly.Spikes
{
    /// <summary>
    /// Spike (c): passthrough JPEG → Spark vision (Chat Completions) and SAM 3.1 (Responses, one_bit mask).
    /// Request shapes per docs/research/meta-model-api.md §3 and §4. Live user frames go to the standard
    /// muse-spark-1.3 model, never *-contributor (ToS §6.2, critic.md C2).
    /// </summary>
    static class MetaVisionProbe
    {
        const string BaseUrl = "https://api.meta.ai/v1";
        const string VisionModel = "muse-spark-1.3";
        const string SamModel = "sam-3.1";

        public static async UniTask RunAsync(string apiKey, byte[] jpeg, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(apiKey) || jpeg == null)
            {
                SpikeLog.Result("vision", new JObject { ["error"] = apiKey == null ? "no META_API_KEY" : "no jpeg" });
                return;
            }

            var dataUrl = "data:image/jpeg;base64," + Convert.ToBase64String(jpeg);
            // Generic concept prompt in parallel with vision; the product flow uses Spark's noun phrase (second SAM call).
            var genericSam = SamAsync(apiKey, dataUrl, "object", "sam_generic", ct);
            var noun = await VisionAsync(apiKey, dataUrl, ct);
            await genericSam;
            if (!string.IsNullOrWhiteSpace(noun))
                await SamAsync(apiKey, dataUrl, noun, "sam_noun", ct);
        }

        static async UniTask<string> VisionAsync(string apiKey, string dataUrl, CancellationToken ct)
        {
            var body = new JObject
            {
                ["model"] = VisionModel,
                ["reasoning_effort"] = "minimal",
                ["max_completion_tokens"] = 1500,
                ["messages"] = new JArray
                {
                    new JObject
                    {
                        ["role"] = "developer",
                        ["content"] = "You are Nubi, a friendly mixed-reality companion. Line 1: a short noun phrase " +
                                      "(1-3 words, no article) naming the main object in the image. Line 2: one short " +
                                      "sentence on what it is and how to use it.",
                    },
                    new JObject
                    {
                        ["role"] = "user",
                        ["content"] = new JArray
                        {
                            new JObject { ["type"] = "text", ["text"] = "What is the main object?" },
                            new JObject { ["type"] = "image_url", ["image_url"] = new JObject { ["url"] = dataUrl } },
                        },
                    },
                },
            };

            var json = new JObject { ["model"] = VisionModel };
            var (code, text, ms) = await PostAsync(apiKey, "/chat/completions", body, ct);
            json["http"] = code;
            json["latencyMs"] = ms;
            string noun = null;
            try
            {
                var response = JObject.Parse(text);
                var content = (string)response["choices"]?[0]?["message"]?["content"];
                json["content"] = content;
                json["usage"] = response["usage"];
                noun = content?.Split('\n')[0].Trim().Trim('*', '.', '"').Trim();
                json["noun"] = noun;
            }
            catch (Exception e)
            {
                json["error"] = e.Message;
                json["body"] = Truncate(text, 600);
            }
            SpikeLog.Result("vision", json);
            return noun;
        }

        static async UniTask SamAsync(string apiKey, string dataUrl, string prompt, string label, CancellationToken ct)
        {
            var body = new JObject
            {
                ["model"] = SamModel,
                ["metadata"] = new JObject { ["include_confidence"] = "true", ["mask_encoding"] = "one_bit" },
                ["input"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "message",
                        ["role"] = "user",
                        ["content"] = new JArray
                        {
                            new JObject { ["type"] = "input_text", ["text"] = prompt },
                            new JObject { ["type"] = "input_image", ["image_url"] = dataUrl },
                        },
                    },
                },
            };

            var json = new JObject { ["prompt"] = prompt };
            var (code, text, ms) = await PostAsync(apiKey, "/responses", body, ct);
            json["http"] = code;
            json["latencyMs"] = ms;
            try
            {
                var response = JObject.Parse(text);
                var output = (string)response["output"]?[0]?["content"]?[0]?["text"] ?? string.Empty;
                json["outputChars"] = output.Length;
                json["usage"] = response["usage"];
                var boxes = new JArray();
                foreach (Match m in Regex.Matches(output, @"<\|box;([^|]*)\|>"))
                    boxes.Add(m.Groups[1].Value);
                json["boxes"] = boxes;
                var encodings = new JArray();
                foreach (Match m in Regex.Matches(output, @"data=\d+,\d+,(.)"))
                    encodings.Add(m.Groups[1].Value);
                json["maskEncodings"] = encodings;
                File.WriteAllText(Path.Combine(Application.persistentDataPath, label + ".txt"), output);
            }
            catch (Exception e)
            {
                json["error"] = e.Message;
                json["body"] = Truncate(text, 600);
            }
            SpikeLog.Result(label, json);
        }

        static async UniTask<(long code, string text, long ms)> PostAsync(string apiKey, string path, JObject body, CancellationToken ct)
        {
            using var request = new UnityWebRequest(BaseUrl + path, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Newtonsoft.Json.Formatting.None))),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 60,
            };
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            request.SetRequestHeader("Content-Type", "application/json");
            var t0 = SpikeLog.NowMs;
            try
            {
                await request.SendWebRequest().WithCancellation(ct);
            }
            catch (UnityWebRequestException)
            {
                // Non-2xx: fall through and report code + body (error bodies carry no secrets).
            }
            return (request.responseCode, request.downloadHandler.text, SpikeLog.NowMs - t0);
        }

        static string Truncate(string s, int max) => s == null || s.Length <= max ? s : s.Substring(0, max);
    }
}
