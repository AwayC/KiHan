using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KiHan.Asset;
using KiHan.Config;
using UnityEngine;
using UnityEngine.Networking;

namespace KiHan.HotUpdate
{
    /// <summary>
    /// 启动热更：选出本地清单 (首包 / 持久目录) → 查服务器 version.json → 下载缺少的包与新清单。
    /// 服务器不可达或无更新时直接用本地清单 (离线可玩)；下载失败只能重试，不允许跳过。
    /// 只更新同一整包版本 (appVersion) 的资源，整包更新由登录时的版本检查把关。
    /// </summary>
    public sealed class HotUpdater
    {
        private const int MaxAttempts = 3;
        private const int VersionTimeout = 5;
        private const float BytesPerMb = 1024f * 1024f;

        /// <summary>热更完成后应使用的清单；首包缺失时为 null</summary>
        public BundleManifest Manifest { get; private set; }

        public IEnumerator Run(HotUpdateView view)
        {
            string appVersion = Application.version;
            string persistentRoot = BundlePaths.PersistentRoot;
            string persistentManifest = persistentRoot + "/" + BundlePaths.ManifestFile;

            // 1. 首包清单
            string builtinText = null;
            yield return ReadText(BundlePaths.BuiltinRoot + "/" + BundlePaths.ManifestFile, t => builtinText = t);
            BundleManifest builtin = Parse<BundleManifest>(builtinText);
            if (builtin == null)
            {
                view.ShowFatal("缺少首包资源");
                yield break;
            }

            // 2. 本地清单
            BundleManifest local = builtin;
            BundleManifest persistent = File.Exists(persistentManifest) ? Parse<BundleManifest>(File.ReadAllText(persistentManifest)) : null;
            if (HotUpdatePlan.UsePersistent(builtin, persistent, appVersion)) local = persistent;
            else if (Directory.Exists(persistentRoot)) Directory.Delete(persistentRoot, true);

            var builtinFiles = new HashSet<string>(builtin.bundles.Select(b => b.file));
            string baseUrl = $"{HttpConfig.HotUpdateUrl}/{BundlePaths.Platform}/";

            while (true)
            {
                // 3. 版本
                VersionInfo version = null;
                using (UnityWebRequest req = UnityWebRequest.Get(baseUrl + "version.json"))
                {
                    req.timeout = VersionTimeout;
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success) version = Parse<VersionInfo>(req.downloadHandler.text);
                }
                if (version == null || !HotUpdatePlan.ShouldUpdate(local, version, appVersion))
                {
                    if (version != null && version.appVersion != appVersion) Debug.LogWarning("[HotUpdate] 整包版本不一致，跳过资源更新");
                    Manifest = local;
                    yield break;
                }

                // 4. 远端清单
                byte[] manifestBytes = null;
                using (UnityWebRequest req = UnityWebRequest.Get(baseUrl + version.manifest))
                {
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success && BundleHash.Md5(req.downloadHandler.data) == version.manifestMd5) manifestBytes = req.downloadHandler.data;
                }
                BundleManifest remote = manifestBytes != null ? Parse<BundleManifest>(Encoding.UTF8.GetString(manifestBytes)) : null;

                // 5. 下载缺少的包
                bool ok = remote != null;
                List<BundleInfo> missing = null;
                if (ok)
                {
                    Directory.CreateDirectory(persistentRoot);
                    missing = HotUpdatePlan.Missing(remote, f => File.Exists(persistentRoot + "/" + f) || builtinFiles.Contains(f));
                    long total = System.Math.Max(1L, missing.Sum(b => b.size));
                    long done = 0;
                    foreach (BundleInfo info in missing)
                    {
                        bool got = false;
                        for (int attempt = 0; attempt < MaxAttempts && !got; attempt++)
                        {
                            string target = persistentRoot + "/" + info.file;
                            string tmp = target + ".tmp";
                            using (var req = new UnityWebRequest(baseUrl + info.file, UnityWebRequest.kHttpVerbGET, new DownloadHandlerFile(tmp) { removeFileOnAbort = true }, null))
                            {
                                UnityWebRequestAsyncOperation op = req.SendWebRequest();
                                while (!op.isDone)
                                {
                                    ReportProgress(view, done + (long)req.downloadedBytes, total);
                                    yield return null;
                                }
                                got = req.result == UnityWebRequest.Result.Success;
                            }
                            got = got && File.Exists(tmp) && new FileInfo(tmp).Length == info.size && BundleHash.Md5File(tmp) == info.md5;
                            if (got)
                            {
                                if (File.Exists(target)) File.Delete(target);
                                File.Move(tmp, target);
                            }
                            else if (File.Exists(tmp))
                            {
                                File.Delete(tmp);
                            }
                        }
                        if (!got)
                        {
                            Debug.LogWarning($"[HotUpdate] 下载失败: {info.file}");
                            ok = false;
                            break;
                        }
                        done += info.size;
                        ReportProgress(view, done, total);
                    }
                }

                // 7. 成功：落地清单，清理旧包
                if (ok)
                {
                    string tmpManifest = persistentManifest + ".tmp";
                    File.WriteAllBytes(tmpManifest, manifestBytes);
                    if (File.Exists(persistentManifest)) File.Delete(persistentManifest);
                    File.Move(tmpManifest, persistentManifest);

                    var keep = new HashSet<string>(remote.bundles.Select(b => b.file));
                    foreach (string file in Directory.GetFiles(persistentRoot))
                    {
                        string name = Path.GetFileName(file);
                        if ((name.EndsWith(".ab", StringComparison.Ordinal) || name.EndsWith(".ab.tmp", StringComparison.Ordinal)) && !keep.Contains(name)) File.Delete(file);
                    }

                    Debug.Log($"[HotUpdate] {local.resVersion} → {remote.resVersion}，下载 {missing.Count} 个包");
                    Manifest = remote;
                    yield break;
                }

                // 6. 失败：等待重试
                bool retry = false;
                void OnRetry() => retry = true;
                view.RetryClicked += OnRetry;
                view.ShowRetry("更新失败，点击重试");
                while (!retry) yield return null;
                view.RetryClicked -= OnRetry;
            }
        }

        private static void ReportProgress(HotUpdateView view, long done, long total)
        {
            view.SetProgress((float)done / total, $"正在更新资源 {done / BytesPerMb:F1}/{total / BytesPerMb:F1} MB");
        }

        private static IEnumerator ReadText(string path, Action<string> onDone)
        {
            if (path.Contains("://"))
            {
                using UnityWebRequest req = UnityWebRequest.Get(path);
                yield return req.SendWebRequest();
                onDone(req.result == UnityWebRequest.Result.Success ? req.downloadHandler.text : null);
            }
            else
            {
                onDone(File.Exists(path) ? File.ReadAllText(path) : null);
            }
        }

        private static T Parse<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (ArgumentException e)
            {
                Debug.LogWarning($"[HotUpdate] 解析 {typeof(T).Name} 失败: {e.Message}");
                return null;
            }
        }
    }
}
