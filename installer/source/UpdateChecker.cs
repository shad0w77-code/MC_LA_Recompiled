// GitHub Releases update checker and verified downloader/extractor for Midnight Club L.A. Recompiled.
// C# 5 syntax for compatibility with the .NET Framework 4.x compiler included with Windows.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace MclaBrFsrSetup {
    public sealed class ReleaseUpdateInfo {
        public string Version { get; set; }
        public string TagName { get; set; }
        public string Name { get; set; }
        public string HtmlUrl { get; set; }
        public string ZipDownloadUrl { get; set; }
        public string ChecksumsDownloadUrl { get; set; }
        public long ZipSizeBytes { get; set; }
        public string NotesSummary { get; set; }
    }

    public static class UpdateChecker {
        public const string RepoOwner = "shad0w77-code";
        public const string RepoName = "MC_LA_Recompiled";
        public const string RepoPageUrl = "https://github.com/shad0w77-code/MC_LA_Recompiled";
        public const string LatestReleaseApiUrl = "https://api.github.com/repos/shad0w77-code/MC_LA_Recompiled/releases/latest";
        public const string ReleasesPageUrl = "https://github.com/shad0w77-code/MC_LA_Recompiled/releases/latest";
        public const string ExpectedZipAssetName = "Midnight-Club-LA-Recompiled-Windows-x64.zip";
        public const string ExpectedExeName = "Midnight Club L.A. Recompiled.exe";
        public const string ExpectedChecksumsAssetName = "SHA256SUMS.txt";

        const long MaxZipBytes = 512L * 1024L * 1024L;
        const long MaxExeBytes = 512L * 1024L * 1024L;

        public static Version ParseVersion(string raw) {
            if (String.IsNullOrWhiteSpace(raw)) return null;
            string s = raw.Trim();
            if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s.Substring(1);
            int dash = s.IndexOfAny(new char[] { '-', '+' });
            if (dash >= 0) s = s.Substring(0, dash);
            string[] parts = s.Split('.');
            if (parts.Length < 2 || parts.Length > 4) return null;
            int[] nums = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++) {
                int v;
                if (!Int32.TryParse(parts[i], out v) || v < 0) return null;
                nums[i] = v;
            }
            if (nums.Length == 2) return new Version(nums[0], nums[1], 0);
            if (nums.Length == 3) return new Version(nums[0], nums[1], nums[2]);
            return new Version(nums[0], nums[1], nums[2], nums[3]);
        }

        public static bool IsNewer(string candidate, string current) {
            Version c = ParseVersion(candidate);
            Version cur = ParseVersion(current);
            if (c == null || cur == null) return false;
            return c.CompareTo(cur) > 0;
        }

        public static bool IsAllowedUrl(string url) {
            if (String.IsNullOrWhiteSpace(url)) return false;
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri)) return false;
            if (!String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return false;
            string host = uri.Host.ToLowerInvariant();
            return host == "github.com" ||
                   host == "api.github.com" ||
                   host == "objects.githubusercontent.com" ||
                   host == "release-assets.githubusercontent.com";
        }

        public static string ParseExpectedSha256(string checksumsText, string fileName) {
            if (String.IsNullOrEmpty(checksumsText) || String.IsNullOrEmpty(fileName)) return null;
            string[] lines = checksumsText.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++) {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                Match m = Regex.Match(line, @"^([0-9a-fA-F]{64})\s+\*?(.+?)\s*$");
                if (!m.Success) continue;
                string entryName = Path.GetFileName(m.Groups[2].Value.Trim());
                if (String.Equals(entryName, fileName, StringComparison.OrdinalIgnoreCase)) {
                    return m.Groups[1].Value.ToLowerInvariant();
                }
            }
            return null;
        }

        public static string ComputeSha256(string filePath) {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(filePath)) {
                byte[] hash = sha.ComputeHash(stream);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }

        static string SummarizeNotes(string body) {
            if (String.IsNullOrWhiteSpace(body)) return "";
            string cleaned = Regex.Replace(body, @"[#*_`>\[\]]+", " ");
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            if (cleaned.Length <= 220) return cleaned;
            return cleaned.Substring(0, 217).TrimEnd() + "...";
        }

        public static ReleaseUpdateInfo ParseLatestReleaseJson(string jsonText, string currentVersion) {
            if (String.IsNullOrWhiteSpace(jsonText)) return null;
            var serializer = new JavaScriptSerializer();
            var root = serializer.DeserializeObject(jsonText) as Dictionary<string, object>;
            if (root == null) return null;

            bool isDraft = root.ContainsKey("draft") && root["draft"] is bool && (bool)root["draft"];
            bool isPre = root.ContainsKey("prerelease") && root["prerelease"] is bool && (bool)root["prerelease"];
            if (isDraft || isPre) return null;

            string tagName = root.ContainsKey("tag_name") ? Convert.ToString(root["tag_name"]) : "";
            Version parsed = ParseVersion(tagName);
            if (parsed == null || !IsNewer(tagName, currentVersion)) return null;

            string htmlUrl = root.ContainsKey("html_url") ? Convert.ToString(root["html_url"]) : ReleasesPageUrl;
            if (!IsAllowedUrl(htmlUrl)) htmlUrl = ReleasesPageUrl;

            string releaseName = root.ContainsKey("name") ? Convert.ToString(root["name"]) : tagName;
            string body = root.ContainsKey("body") ? Convert.ToString(root["body"]) : "";

            string zipUrl = null;
            string fallbackZipUrl = null;
            long zipSize = 0;
            long fallbackZipSize = 0;
            string checksumsUrl = null;

            object assetsObj;
            if (root.TryGetValue("assets", out assetsObj) && assetsObj is object[]) {
                object[] assets = (object[])assetsObj;
                for (int i = 0; i < assets.Length; i++) {
                    var asset = assets[i] as Dictionary<string, object>;
                    if (asset == null) continue;
                    string name = asset.ContainsKey("name") ? Convert.ToString(asset["name"]) : "";
                    string dlUrl = asset.ContainsKey("browser_download_url") ? Convert.ToString(asset["browser_download_url"]) : "";
                    if (!IsAllowedUrl(dlUrl)) continue;
                    long size = 0;
                    if (asset.ContainsKey("size") && asset["size"] != null) {
                        Int64.TryParse(Convert.ToString(asset["size"]), out size);
                    }
                    if (String.Equals(name, ExpectedZipAssetName, StringComparison.OrdinalIgnoreCase)) {
                        zipUrl = dlUrl;
                        zipSize = size;
                    } else if (fallbackZipUrl == null &&
                               name.EndsWith("Windows-x64.zip", StringComparison.OrdinalIgnoreCase) &&
                               name.IndexOf("source", StringComparison.OrdinalIgnoreCase) < 0) {
                        fallbackZipUrl = dlUrl;
                        fallbackZipSize = size;
                    } else if (String.Equals(name, ExpectedChecksumsAssetName, StringComparison.OrdinalIgnoreCase)) {
                        checksumsUrl = dlUrl;
                    }
                }
            }

            if (zipUrl == null && fallbackZipUrl != null) {
                zipUrl = fallbackZipUrl;
                zipSize = fallbackZipSize;
            }

            return new ReleaseUpdateInfo {
                Version = parsed.ToString(3),
                TagName = tagName,
                Name = String.IsNullOrWhiteSpace(releaseName) ? ("v" + parsed.ToString(3)) : releaseName,
                HtmlUrl = htmlUrl,
                ZipDownloadUrl = zipUrl,
                ChecksumsDownloadUrl = checksumsUrl,
                ZipSizeBytes = zipSize,
                NotesSummary = SummarizeNotes(body)
            };
        }

        public static string ExtractUpdaterFromZip(string zipPath, string destinationDir, string expectedZipSha256) {
            if (!File.Exists(zipPath)) throw new IOException("Pacote de atualização não encontrado.");
            var info = new FileInfo(zipPath);
            if (info.Length <= 0 || info.Length > MaxZipBytes) {
                throw new IOException("Tamanho do pacote de atualização inválido.");
            }
            if (!String.IsNullOrWhiteSpace(expectedZipSha256)) {
                string actualZipHash = ComputeSha256(zipPath);
                if (!String.Equals(actualZipHash, expectedZipSha256.Trim(), StringComparison.OrdinalIgnoreCase)) {
                    throw new IOException("A verificação SHA-256 do pacote baixado falhou.");
                }
            }

            Directory.CreateDirectory(destinationDir);
            string exeDest = Path.Combine(destinationDir, ExpectedExeName);
            string internalChecksums = null;
            bool extracted = false;

            using (var archive = ZipFile.OpenRead(zipPath)) {
                long totalUncompressed = 0;
                ZipArchiveEntry exeEntry = null;
                ZipArchiveEntry sumsEntry = null;

                foreach (var entry in archive.Entries) {
                    string full = entry.FullName.Replace('\\', '/');
                    if (full.StartsWith("/", StringComparison.Ordinal) ||
                        full.IndexOf("..", StringComparison.Ordinal) >= 0 ||
                        full.IndexOf(":", StringComparison.Ordinal) >= 0) {
                        throw new IOException("Caminho inválido dentro do arquivo ZIP de atualização.");
                    }
                    totalUncompressed += entry.Length;
                    if (totalUncompressed > MaxZipBytes) {
                        throw new IOException("O arquivo ZIP excede o limite de tamanho permitido.");
                    }
                    if (String.IsNullOrEmpty(entry.Name)) continue;

                    string[] segments = full.Trim('/').Split('/');
                    if (segments.Length <= 2 && String.Equals(entry.Name, ExpectedExeName, StringComparison.OrdinalIgnoreCase)) {
                        if (entry.Length <= 0 || entry.Length > MaxExeBytes) {
                            throw new IOException("Executável de atualização inválido no pacote.");
                        }
                        exeEntry = entry;
                    } else if (segments.Length <= 2 && String.Equals(entry.Name, ExpectedChecksumsAssetName, StringComparison.OrdinalIgnoreCase)) {
                        if (entry.Length > 0 && entry.Length <= 1024 * 1024) {
                            sumsEntry = entry;
                        }
                    }
                }

                if (exeEntry == null) {
                    throw new IOException("O pacote baixado não contém '" + ExpectedExeName + "'.");
                }
                if (sumsEntry != null) {
                    using (var sr = new StreamReader(sumsEntry.Open(), Encoding.UTF8)) {
                        internalChecksums = sr.ReadToEnd();
                    }
                }
                exeEntry.ExtractToFile(exeDest, true);
                extracted = true;
            }

            if (extracted && !String.IsNullOrEmpty(internalChecksums)) {
                string expectedExeHash = ParseExpectedSha256(internalChecksums, ExpectedExeName);
                if (!String.IsNullOrEmpty(expectedExeHash)) {
                    string actualExeHash = ComputeSha256(exeDest);
                    if (!String.Equals(actualExeHash, expectedExeHash, StringComparison.OrdinalIgnoreCase)) {
                        try { File.Delete(exeDest); } catch { }
                        throw new IOException("A verificação SHA-256 do executável extraído falhou.");
                    }
                }
            }
            return exeDest;
        }

        static void EnableTls12() {
            try {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            } catch { }
        }

        static HttpWebRequest CreateRequest(string url, string userAgent, int timeoutMs) {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.UserAgent = userAgent;
            req.Accept = "application/vnd.github+json";
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            req.AllowAutoRedirect = true;
            req.MaximumAutomaticRedirections = 5;
            return req;
        }

        public static Task<ReleaseUpdateInfo> CheckForUpdatesAsync(string currentVersion, Action<string> log) {
            return Task.Run<ReleaseUpdateInfo>(delegate {
                try {
                    EnableTls12();
                    var req = CreateRequest(LatestReleaseApiUrl, "MCLA-LA-Recompiled/" + currentVersion, 8000);
                    using (var resp = (HttpWebResponse)req.GetResponse()) {
                        if (!IsAllowedUrl(resp.ResponseUri.AbsoluteUri)) return null;
                        using (var stream = resp.GetResponseStream())
                        using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                            string jsonText = reader.ReadToEnd();
                            return ParseLatestReleaseJson(jsonText, currentVersion);
                        }
                    }
                } catch (WebException ex) {
                    if (log != null) {
                        var http = ex.Response as HttpWebResponse;
                        if (http != null && http.StatusCode == HttpStatusCode.NotFound) {
                            log("Nenhuma release publicada encontrada ainda em " + RepoOwner + "/" + RepoName + ".");
                        } else {
                            log("Verificação de atualizações indisponível no momento (" + ex.Message + ").");
                        }
                    }
                    return null;
                } catch (Exception ex) {
                    if (log != null) log("Verificação de atualizações indisponível (" + ex.Message + ").");
                    return null;
                }
            });
        }

        public static Task<string> DownloadAndStageUpdateAsync(ReleaseUpdateInfo info, string currentVersion, Action<int> onProgress, Action<string> log) {
            return Task.Run<string>(delegate {
                if (info == null || !IsAllowedUrl(info.ZipDownloadUrl)) {
                    throw new IOException("URL de atualização inválida ou não confiável.");
                }
                EnableTls12();
                string ua = "MCLA-LA-Recompiled/" + currentVersion;

                string expectedZipSha256 = null;
                if (!String.IsNullOrEmpty(info.ChecksumsDownloadUrl) && IsAllowedUrl(info.ChecksumsDownloadUrl)) {
                    if (log != null) log("Baixando SHA256SUMS.txt para conferência...");
                    var sumReq = CreateRequest(info.ChecksumsDownloadUrl, ua, 15000);
                    using (var sumResp = (HttpWebResponse)sumReq.GetResponse()) {
                        if (!IsAllowedUrl(sumResp.ResponseUri.AbsoluteUri)) {
                            throw new IOException("Redirecionamento não confiável em SHA256SUMS.txt.");
                        }
                        using (var sr = new StreamReader(sumResp.GetResponseStream(), Encoding.UTF8)) {
                            expectedZipSha256 = ParseExpectedSha256(sr.ReadToEnd(), ExpectedZipAssetName);
                        }
                    }
                }

                string updatesRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MCLA_LA_Recompiled", "Updates",
                    "v" + info.Version + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(updatesRoot);

                string partZip = Path.Combine(updatesRoot, "update.zip.part");
                string finalZip = Path.Combine(updatesRoot, "update.zip");

                if (log != null) log("Baixando " + ExpectedZipAssetName + " (v" + info.Version + ")...");
                var req = CreateRequest(info.ZipDownloadUrl, ua, 60000);
                using (var resp = (HttpWebResponse)req.GetResponse()) {
                    if (!IsAllowedUrl(resp.ResponseUri.AbsoluteUri)) {
                        throw new IOException("Redirecionamento de download fora dos domínios permitidos.");
                    }
                    long total = resp.ContentLength > 0 ? resp.ContentLength : info.ZipSizeBytes;
                    if (total > MaxZipBytes) {
                        throw new IOException("O pacote de atualização excede o tamanho máximo suportado.");
                    }
                    using (var input = resp.GetResponseStream())
                    using (var output = File.Create(partZip)) {
                        byte[] buffer = new byte[65536];
                        long received = 0;
                        int lastPct = -1;
                        int read;
                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0) {
                            output.Write(buffer, 0, read);
                            received += read;
                            if (received > MaxZipBytes) {
                                throw new IOException("Download excedeu o tamanho máximo permitido.");
                            }
                            if (onProgress != null && total > 0) {
                                int pct = (int)Math.Min(100L, (received * 100L) / total);
                                if (pct != lastPct) {
                                    lastPct = pct;
                                    onProgress(pct);
                                }
                            }
                        }
                    }
                }

                if (File.Exists(finalZip)) File.Delete(finalZip);
                File.Move(partZip, finalZip);
                if (log != null) log("Verificando integridade SHA-256 e extraindo novo instalador...");
                string exePath = ExtractUpdaterFromZip(finalZip, updatesRoot, expectedZipSha256);
                try { File.Delete(finalZip); } catch { }
                return exePath;
            });
        }
    }
}
