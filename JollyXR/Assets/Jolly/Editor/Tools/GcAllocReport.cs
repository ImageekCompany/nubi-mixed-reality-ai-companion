using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace Jolly.Editor.Tools
{
    /// <summary>
    /// Attributes the managed allocations of a self-test turn profile (<c>TurnProfileCapture</c>, pulled by
    /// <c>tools/device/selftest.sh --profile</c>) to call stacks and Profiler sample paths, all threads, and prints the
    /// heaviest. The headless counterpart of the Profiler window's GC Alloc column (docs/perf/README.md §7).
    /// Batch: <c>Unity -batchmode -nographics -projectPath JollyXR -executeMethod Jolly.Editor.Tools.GcAllocReport.RunBatch
    /// -nubiProfile ../artifacts/selftest/selftest_turns.raw [-nubiReport out.txt] -logFile -</c>
    /// </summary>
    public static class GcAllocReport
    {
        const string LogTag = "[GcAllocReport]";
        const int CallstackDepth = 12;
        const int TopRows = 40;

        sealed class Bucket
        {
            public long Bytes;
            public long Count;
        }

        public static void RunBatch()
        {
            var profile = Arg("-nubiProfile");
            var ok = profile != null && File.Exists(profile);
            if (!ok)
                Debug.LogError($"{LogTag} no profile at {profile}");
            else
            {
                var report = Build(profile);
                var output = Arg("-nubiReport");
                if (output != null)
                    File.WriteAllText(output, report);
                Debug.Log(report);
            }
            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
        }

        static string Build(string profile)
        {
            if (!ProfilerDriver.LoadProfile(profile, false))
                return $"{LogTag} could not load {profile}";

            var byStack = new Dictionary<string, Bucket>();
            var byPath = new Dictionary<string, Bucket>();
            var byThread = new Dictionary<string, Bucket>();
            var methods = new Dictionary<ulong, string>();
            var callstack = new List<ulong>(64);
            var parents = new Stack<(int Sample, int Remaining)>();
            var first = ProfilerDriver.firstFrameIndex;
            var last = ProfilerDriver.lastFrameIndex;
            long total = 0;

            for (var frame = first; frame <= last; frame++)
            {
                for (var thread = 0; ; thread++)
                {
                    using var view = ProfilerDriver.GetRawFrameDataView(frame, thread);
                    if (view == null || !view.valid)
                        break;
                    var gcAlloc = view.GetMarkerId("GC.Alloc");
                    if (gcAlloc == FrameDataView.invalidMarkerId)
                        continue;
                    var threadName = view.threadGroupName + "/" + view.threadName;
                    parents.Clear();
                    for (var i = 0; i < view.sampleCount; i++)
                    {
                        while (parents.Count > 0 && parents.Peek().Remaining == 0)
                            parents.Pop();
                        if (parents.Count > 0)
                        {
                            var (sample, remaining) = parents.Pop();
                            parents.Push((sample, remaining - 1));
                        }
                        var parentPath = parents.Count > 0 ? PathOf(view, parents) : "(root)";
                        parents.Push((i, view.GetSampleChildrenCount(i)));
                        if (view.GetSampleMarkerId(i) != gcAlloc)
                            continue;

                        var bytes = view.GetSampleMetadataCount(i) > 0 ? view.GetSampleMetadataAsLong(i, 0) : 0;
                        total += bytes;
                        Add(byThread, threadName, bytes);
                        Add(byPath, threadName + " :: " + parentPath, bytes);
                        view.GetSampleCallstack(i, callstack);
                        Add(byStack, threadName + " :: " + Stack(view, callstack, methods), bytes);
                    }
                }
            }

            var frames = last - first + 1;
            var text = new StringBuilder();
            text.AppendLine($"{LogTag} {profile}: frames={frames} gc_bytes={total} gc_bytes_per_frame={(frames > 0 ? total / frames : 0)}");
            Append(text, "threads", byThread, frames);
            Append(text, "sample paths", byPath, frames);
            Append(text, "call stacks (innermost first)", byStack, frames);
            return text.ToString();
        }

        static string PathOf(RawFrameDataView view, Stack<(int Sample, int Remaining)> parents)
        {
            // The innermost three markers are enough to tell a UniTask continuation from a MonoBehaviour update.
            var names = new List<string>(3);
            foreach (var (sample, _) in parents)
            {
                names.Add(view.GetSampleName(sample));
                if (names.Count == 3)
                    break;
            }
            names.Reverse();
            return string.Join(" > ", names);
        }

        static string Stack(RawFrameDataView view, List<ulong> callstack, Dictionary<ulong, string> methods)
        {
            if (callstack.Count == 0)
                return "(no call stack)";
            var frames = new List<string>(CallstackDepth);
            foreach (var address in callstack)
            {
                if (!methods.TryGetValue(address, out var name))
                {
                    var info = view.ResolveMethodInfo(address);
                    name = string.IsNullOrEmpty(info.methodName) ? null : info.methodName;
                    methods[address] = name;
                }
                if (name == null)
                    continue;
                frames.Add(name);
                if (frames.Count == CallstackDepth)
                    break;
            }
            return frames.Count == 0 ? "(unresolved)" : string.Join(" <- ", frames);
        }

        static void Add(Dictionary<string, Bucket> buckets, string key, long bytes)
        {
            if (!buckets.TryGetValue(key, out var bucket))
                buckets[key] = bucket = new Bucket();
            bucket.Bytes += bytes;
            bucket.Count++;
        }

        static void Append(StringBuilder text, string title, Dictionary<string, Bucket> buckets, int frames)
        {
            text.AppendLine($"-- {title} (bytes, bytes/frame, allocations)");
            foreach (var (key, bucket) in buckets.OrderByDescending(b => b.Value.Bytes).Take(TopRows).Select(b => (b.Key, b.Value)))
                text.AppendLine($"{bucket.Bytes,12} {bucket.Bytes / Math.Max(1, frames),8} {bucket.Count,8}  {key}");
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }
    }
}
