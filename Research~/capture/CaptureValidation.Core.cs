using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Core infrastructure for Capture V2 validation: timer benchmarking, visual marker freshness, and corpus generators.
    /// </summary>
    public static partial class MCPServerMethods
    {
        private static Texture2D _markerTex;

        /// <summary>
        /// Benchmarks Stopwatch timer frequency, resolution, and empty loop noise floor.
        /// </summary>
        public static JObject BenchmarkTimerResolution()
        {
            const int iterations = 100000;
            long freq = Stopwatch.Frequency;
            bool isHighRes = Stopwatch.IsHighResolution;

            long[] diffs = new long[iterations];
            for (int i = 0; i < iterations; i++)
            {
                long t0 = Stopwatch.GetTimestamp();
                long t1 = Stopwatch.GetTimestamp();
                diffs[i] = t1 - t0;
            }

            Array.Sort(diffs);
            double minMs = (double)diffs[0] * 1000.0 / freq;
            double medianMs = (double)diffs[iterations / 2] * 1000.0 / freq;
            double p95Ms = (double)diffs[(int)(iterations * 0.95)] * 1000.0 / freq;
            double p99Ms = (double)diffs[(int)(iterations * 0.99)] * 1000.0 / freq;
            double maxMs = (double)diffs[iterations - 1] * 1000.0 / freq;

            return new JObject
            {
                ["stopwatch_frequency_hz"] = freq,
                ["is_high_resolution"] = isHighRes,
                ["timer_tick_resolution_ns"] = 1000000000.0 / freq,
                ["empty_loop_iterations"] = iterations,
                ["noise_floor_min_ms"] = minMs,
                ["noise_floor_median_ms"] = medianMs,
                ["noise_floor_p95_ms"] = p95Ms,
                ["noise_floor_p99_ms"] = p99Ms,
                ["noise_floor_max_ms"] = maxMs,
                ["reliable_threshold_ms"] = Math.Max(0.005, p99Ms * 2.0)
            };
        }

        /// <summary>
        /// Computes statistical metrics (min, p50, mean, p95, p99, max, std dev) for a list of double samples.
        /// </summary>
        public static JObject ComputeStats(List<double> samples)
        {
            var res = new JObject();
            if (samples == null || samples.Count == 0)
            {
                res["count"] = 0;
                res["min"] = 0.0;
                res["p50"] = 0.0;
                res["mean"] = 0.0;
                res["p95"] = 0.0;
                res["p99"] = 0.0;
                res["max"] = 0.0;
                res["std_dev"] = 0.0;
                return res;
            }

            var sorted = new List<double>(samples);
            sorted.Sort();
            int n = sorted.Count;

            double sum = 0.0;
            for (int i = 0; i < n; i++) sum += sorted[i];
            double mean = sum / n;

            double sumSq = 0.0;
            for (int i = 0; i < n; i++)
            {
                double diff = sorted[i] - mean;
                sumSq += diff * diff;
            }
            double stdDev = Math.Sqrt(sumSq / n);

            res["count"] = n;
            res["min"] = sorted[0];
            res["p50"] = sorted[n / 2];
            res["mean"] = mean;
            res["p95"] = sorted[(int)(n * 0.95)];
            res["p99"] = sorted[(int)(n * 0.99)];
            res["max"] = sorted[n - 1];
            res["std_dev"] = stdDev;
            return res;
        }

        /// <summary>
        /// Renders a deterministic 32x32 color-coded marker encoding a frame sequence integer into an RT.
        /// </summary>
        internal static void RenderVisualMarker(RenderTexture rt, int frameId)
        {
            if (_markerTex == null)
            {
                _markerTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            }

            byte r = (byte)(frameId & 0xFF);
            byte g = (byte)((frameId >> 8) & 0xFF);
            byte b = (byte)((frameId >> 16) & 0xFF);
            Color markerColor = new Color32(r, g, b, 255);

            Color[] colors = new Color[32 * 32];
            for (int i = 0; i < colors.Length; i++) colors[i] = markerColor;
            _markerTex.SetPixels(colors);
            _markerTex.Apply();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, rt.width, 0, rt.height);
            Graphics.DrawTexture(new Rect(0, 0, 32, 32), _markerTex);
            GL.PopMatrix();
            RenderTexture.active = prev;
        }

        /// <summary>
        /// Decodes a visual marker sequence integer from raw readback pixel bytes.
        /// </summary>
        internal static int DecodeVisualMarker(NativeArray<byte> rawPixels, int width, int height)
        {
            if (rawPixels.Length < 4) return -1;
            // Sample center of 32x32 marker block (pixel at 16, 16)
            int sampleX = Math.Min(16, width - 1);
            int sampleY = Math.Min(16, height - 1);
            int offset = (sampleY * width + sampleX) * 4;

            if (offset + 3 >= rawPixels.Length) return -1;
            byte r = rawPixels[offset];
            byte g = rawPixels[offset + 1];
            byte b = rawPixels[offset + 2];

            return r | (g << 8) | (b << 16);
        }

        /// <summary>
        /// Generates a procedural 1080p benchmark corpus image for the specified category ID.
        /// </summary>
        internal static RenderTexture CreateCorpusRenderTexture(string corpusId, int width = 1920, int height = 1080)
        {
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, GraphicsFormat.R8G8B8A8_SRGB);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D proc = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];

            switch (corpusId.ToLowerInvariant())
            {
                case "corpus_flat_ui":
                    FillFlatUI(pixels, width, height);
                    break;
                case "corpus_text_heavy_ui":
                    FillTextHeavyUI(pixels, width, height);
                    break;
                case "corpus_high_frequency":
                    FillHighFrequency(pixels, width, height);
                    break;
                case "corpus_gradients":
                    FillGradients(pixels, width, height);
                    break;
                case "corpus_textured_gameplay":
                    FillTexturedGameplay(pixels, width, height);
                    break;
                case "corpus_particle_noise":
                    FillParticleNoise(pixels, width, height);
                    break;
                case "corpus_mixed_gameplay_ui":
                    FillMixedGameplayUI(pixels, width, height);
                    break;
                case "corpus_real_game_view":
                default:
                    FillRealGameView(rt);
                    RenderTexture.active = prev;
                    UnityEngine.Object.DestroyImmediate(proc);
                    return rt;
            }

            proc.SetPixels(pixels);
            proc.Apply();
            Graphics.Blit(proc, rt);
            UnityEngine.Object.DestroyImmediate(proc);
            RenderTexture.active = prev;
            return rt;
        }

        private static void FillFlatUI(Color[] p, int w, int h)
        {
            Color bg = new Color(0.15f, 0.15f, 0.18f, 1f);
            Color header = new Color(0.22f, 0.22f, 0.26f, 1f);
            Color card = new Color(0.28f, 0.28f, 0.33f, 1f);
            Color button = new Color(0.18f, 0.48f, 0.88f, 1f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    if (y < 80) p[idx] = header;
                    else if (x > 100 && x < 600 && y > 150 && y < 600) p[idx] = card;
                    else if (x > 700 && x < 1200 && y > 150 && y < 600) p[idx] = card;
                    else if (x > 200 && x < 500 && y > 700 && y < 760) p[idx] = button;
                    else p[idx] = bg;
                }
            }
        }

        private static void FillTextHeavyUI(Color[] p, int w, int h)
        {
            Color bg = new Color(0.12f, 0.12f, 0.14f, 1f);
            Color gridLine = new Color(0.3f, 0.3f, 0.35f, 1f);
            Color textGlyph = new Color(0.9f, 0.9f, 0.95f, 1f);
            Color labelSub = new Color(0.6f, 0.6f, 0.65f, 1f);

            for (int y = 0; y < h; y++)
            {
                bool isGridY = (y % 28 == 0);
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    bool isGridX = (x % 160 == 0);
                    if (isGridY || isGridX) p[idx] = gridLine;
                    else if ((y % 28 > 8 && y % 28 < 20) && ((x % 160 > 10 && x % 160 < 140) && ((x / 4) % 3 != 0)))
                        p[idx] = (x < 600) ? textGlyph : labelSub;
                    else p[idx] = bg;
                }
            }
        }

        private static void FillHighFrequency(Color[] p, int w, int h)
        {
            Color c1 = Color.black;
            Color c2 = Color.white;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    p[y * w + x] = ((x / 2 + y / 2) % 2 == 0) ? c1 : c2;
                }
            }
        }

        private static void FillGradients(Color[] p, int w, int h)
        {
            for (int y = 0; y < h; y++)
            {
                float v = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w;
                    p[y * w + x] = new Color(u, v, 1f - u, 1f);
                }
            }
        }

        private static void FillTexturedGameplay(Color[] p, int w, int h)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = Mathf.Sin(x * 0.05f) * Mathf.Cos(y * 0.05f);
                    float ny = Mathf.Sin(x * 0.01f + y * 0.02f);
                    float val = Mathf.Clamp01(0.5f + 0.3f * nx + 0.2f * ny);
                    p[y * w + x] = new Color(val * 0.6f, val * 0.8f, val * 0.4f, 1f);
                }
            }
        }

        private static void FillParticleNoise(Color[] p, int w, int h)
        {
            var rnd = new System.Random(42);
            for (int i = 0; i < p.Length; i++)
            {
                float r = (float)rnd.NextDouble();
                if (r > 0.85f)
                {
                    float bri = (float)rnd.NextDouble();
                    p[i] = new Color(bri, bri * 0.7f, bri * 0.2f, 1f);
                }
                else
                {
                    p[i] = new Color(0.02f, 0.02f, 0.04f, 1f);
                }
            }
        }

        private static void FillMixedGameplayUI(Color[] p, int w, int h)
        {
            FillTexturedGameplay(p, w, h);
            Color hudBg = new Color(0f, 0f, 0f, 0.75f);
            Color hpGreen = new Color(0.2f, 0.85f, 0.2f, 1f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    if (x > 50 && x < 350 && y > 50 && y < 100)
                    {
                        p[idx] = (x < 280) ? hpGreen : hudBg;
                    }
                    else if (x > w - 250 && x < w - 50 && y > 50 && y < 250)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(w - 150, 150));
                        if (dist < 90) p[idx] = (dist > 85) ? Color.white : hudBg;
                    }
                }
            }
        }

        private static void FillRealGameView(RenderTexture dst)
        {
            var gv = GetOrCreateGameView();
            var gvRT = GetGameViewRenderTexture(gv);
            if (gvRT != null)
            {
                Graphics.Blit(gvRT, dst);
            }
            else
            {
                Camera cam = Camera.main ?? UnityEngine.Object.FindFirstObjectByType<Camera>();
                if (cam != null && cam.isActiveAndEnabled)
                {
                    var prev = cam.targetTexture;
                    cam.targetTexture = dst;
                    cam.Render();
                    cam.targetTexture = prev;
                }
                else
                {
                    PopulateTestPattern(dst);
                }
            }
        }
    }
}
