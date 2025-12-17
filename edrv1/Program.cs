// Program.cs
// version: d20251211 v1.2

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using Newtonsoft.Json;

namespace EDRPOC
{
    internal class Program
    {
        // =========================
        //  Build config & secrets
        // =========================

        private const string VERSION = "edrv1-20251216-1";

        // 官方 sample / 本地 Playground 用的 secret（0000...）
        private const string SECRET_DEBUG = "00000000000000000000000000000000";

        // 你比賽帳號的 secret（記得改成自己的）
        private const string SECRET_RELEASE = "8AfPntUI4d0KICm6ee6xWmqg8dBoVbDH";

#if DEBUG
        private const string BUILD_MODE = "DEBUG";
        private const string SECRET = SECRET_DEBUG;
#else
        private const string BUILD_MODE = "RELEASE";
        private const string SECRET = SECRET_RELEASE;
#endif

        // =========================
        //  Process tracking
        // =========================

        // PID -> EXE name（ImageFileName）
        private static readonly Dictionary<int, string> ProcessIdToExeName = new();

        // PID -> Parent PID（用來追溯誰生了 cmd.exe / notepad.exe）
        private static readonly Dictionary<int, int> ProcessIdToParentId = new();

        private static readonly object ProcessMapLock = new();

        // 終止條件：一旦決定並送出答案就設為 true
        private static bool _answerSent = false;

        // 保留 session 參考，方便關閉時一併釋放
        private static TraceEventSession? _session;

        // 要監控的檔案（malv1 / cmd.exe 最終都會去讀這個）
        private static readonly string TargetFilePath =
            @"C:\Users\bombe\AppData\Local\bhrome\Login Data".ToLowerInvariant();

        // =========================
        //  Main
        // =========================

        private static async Task Main(string[] args)
        {
            Console.WriteLine("========== [edrv1 DEBUG WRAPPER] ==========");
            Console.WriteLine($"[edrv1] Version    : {VERSION}");
            Console.WriteLine($"[edrv1] Build Mode : {BUILD_MODE}");
            Console.WriteLine($"[edrv1] SECRET     : {SECRET}");
            Console.WriteLine("===========================================");
            Console.WriteLine("[edrv1] Starting kernel ETW session...");

            // NOTE: EDR 必須 headless / unattended：
            // - 不吃任何 args
            // - 直接開始監控，直到被平台終止，或是送出答案

            using var kernelSession = new TraceEventSession(KernelTraceEventParser.KernelSessionName);
            _session = kernelSession;

            // 讓 Ctrl+C 可以在本機測試時正常關閉
            Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs e) =>
            {
                Console.WriteLine("[edrv1] Ctrl+C pressed, disposing session...");
                try
                {
                    kernelSession.Dispose();
                }
                catch
                {
                }
            };

            // 啟用我們需要的 ETW event 類型
            kernelSession.EnableKernelProvider(
                KernelTraceEventParser.Keywords.ImageLoad |
                KernelTraceEventParser.Keywords.Process |
                KernelTraceEventParser.Keywords.DiskFileIO |
                KernelTraceEventParser.Keywords.FileIOInit |
                KernelTraceEventParser.Keywords.FileIO |
                KernelTraceEventParser.Keywords.NetworkTCPIP
            );

            // 綁定事件處理器
            kernelSession.Source.Kernel.ProcessStart += ProcessStartedHandler;
            kernelSession.Source.Kernel.ProcessStop += ProcessStoppedHandler;
            kernelSession.Source.Kernel.FileIORead += FileReadHandler;
            kernelSession.Source.Kernel.TcpIpConnect += TcpIpConnectHandler;

            // 阻塞在這裡，直到 session 被 Dispose 或是 Process() 因為我們自己 Exit 結束
            kernelSession.Source.Process();
        }

        // =========================
        //  Event handlers
        // =========================

        private static void ProcessStartedHandler(ProcessTraceData data)
        {
            lock (ProcessMapLock)
            {
                ProcessIdToExeName[data.ProcessID] = data.ImageFileName ?? string.Empty;
                ProcessIdToParentId[data.ProcessID] = data.ParentID;

#if DEBUG
                if (!string.IsNullOrEmpty(data.ImageFileName) &&
                    data.ImageFileName.StartsWith("BOMBE", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(
                        $"[edrv1] ProcessStart: PID={data.ProcessID}, ParentPID={data.ParentID}, EXE={data.ImageFileName}");
                }
#endif
            }
        }

        private static void ProcessStoppedHandler(ProcessTraceData data)
        {
            lock (ProcessMapLock)
            {
                bool removed = ProcessIdToExeName.Remove(data.ProcessID);
                ProcessIdToParentId.Remove(data.ProcessID);

#if DEBUG
                if (removed)
                {
                    Console.WriteLine($"[edrv1] ProcessStop : PID={data.ProcessID}");
                }
#endif
            }
        }

        private static async void FileReadHandler(FileIOReadWriteTraceData data)
        {
            // 答案已經送出，就不要再做任何事情
            if (_answerSent) return;

            try
            {
                string fileName = data.FileName?.ToLowerInvariant() ?? string.Empty;
                if (!fileName.Equals(TargetFilePath, StringComparison.Ordinal))
                    return;

                int pid = data.ProcessID;
                string? directExeName;
                lock (ProcessMapLock)
                {
                    ProcessIdToExeName.TryGetValue(pid, out directExeName);
                }

#if DEBUG
                Console.WriteLine(
                    $"[edrv1] FileRead hit target: pid={pid}, process={data.ProcessName}, exe={directExeName}");
#endif

                // 透過 parent chain 追到真正的 BOMBE_* 來源
                string? rootBombeExe = FindBombeAncestor(pid);

                if (string.IsNullOrEmpty(rootBombeExe))
                {
#if DEBUG
                    Console.WriteLine(
                        $"[edrv1] No BOMBE ancestor found in chain for pid={pid}, ignore this read.");
#endif
                    return;
                }

                Console.WriteLine(
                    "[edrv1] Suspicious file read: {0}, pid={1}, leafExe={2}, rootBombeExe={3}",
                    data.FileName,
                    pid,
                    directExeName,
                    rootBombeExe
                );

#if DEBUG
                Console.WriteLine(
                    $"[edrv1] DEBUG build -> Would submit: answer=\"{rootBombeExe}\", secret=\"{SECRET}\"");
                _answerSent = true;
                RequestShutdown();
#else
                string payload = JsonConvert.SerializeObject(
                    new
                    {
                        answer = rootBombeExe,
                        secret = SECRET
                    }
                );

                Console.WriteLine("[edrv1] Submitting answer to server...");
                await SendAnswerToServer(payload);

                _answerSent = true;
                RequestShutdown();
#endif
            }
            catch (Exception ex)
            {
                // EDR 不能 crash，穩定最重要
                Console.WriteLine($"[edrv1] FileReadHandler exception: {ex.Message}");
            }
        }
        private static async void TcpIpConnectHandler(TcpIpConnectTraceData data)
        {
            if (_answerSent) return;
            int pid = data.ProcessID;
            // 檢查這個發起連線的人，是不是 BOMBE
            string? rootBombeExe = FindBombeAncestor(pid);

            if (!string.IsNullOrEmpty(rootBombeExe))
            {
                if (data.dport == 443)
                {
#if DEBUG
                    Console.WriteLine($"[NETWORK DETECT] Process {rootBombeExe} (PID={pid}) is connecting to remote IP {data.daddr}:{data.dport}");
#endif 
                    string payload = JsonConvert.SerializeObject(
                        new
                        {
                            answer = rootBombeExe,
                            secret = SECRET
                        }
                    );

                    Console.WriteLine("[edrv1] Submitting answer to server...");
                    await SendAnswerToServer(payload);

                    _answerSent = true;
                    RequestShutdown();
                }
            }
        }
        // =========================
        //  Ancestor resolution
        // =========================

        private static string? FindBombeAncestor(int pid)
        {
            const int maxDepth = 16; // 防止惡意 / 異常的深度導致無限迴圈
            int currentPid = pid;

            lock (ProcessMapLock)
            {
                for (int depth = 0; depth < maxDepth; depth++)
                {
                    if (!ProcessIdToExeName.TryGetValue(currentPid, out var exeName))
                    {
#if DEBUG
                        Console.WriteLine($"[edrv1]   Chain break: no record for PID={currentPid}");
#endif
                        return null;
                    }

#if DEBUG
                    Console.WriteLine($"[edrv1]   Chain[{depth}]: PID={currentPid}, EXE={exeName}");
#endif

                    if (!string.IsNullOrEmpty(exeName) &&
                        exeName.StartsWith("BOMBE_EDR_FLAG", StringComparison.OrdinalIgnoreCase))
                    {
                        return exeName;
                    }

                    if (!ProcessIdToParentId.TryGetValue(currentPid, out var parentPid))
                    {
#if DEBUG
                        Console.WriteLine($"[edrv1]   Chain end: no parent for PID={currentPid}");
#endif
                        return null;
                    }

                    if (parentPid == 0 || parentPid == currentPid)
                    {
#if DEBUG
                        Console.WriteLine(
                            $"[edrv1]   Chain end: parent invalid for PID={currentPid}, parent={parentPid}");
#endif
                        return null;
                    }

                    currentPid = parentPid;
                }

#if DEBUG
                Console.WriteLine($"[edrv1]   Chain exceeded maxDepth={maxDepth}, abort.");
#endif
                return null;
            }
        }

        // =========================
        //  Graceful shutdown
        // =========================

        private static void RequestShutdown()
        {
            // 保守一點再檢查一次，避免奇怪 race
            if (!_answerSent) return;

            Console.WriteLine("[edrv1] Answer decided, shutting down EDR.");

            try
            {
                _session?.Dispose();
            }
            catch
            {
            }

            // 對 CTF 來說，送出答案後就可以結束行程
            Environment.Exit(0);
        }

        // =========================
        //  HTTP submit
        // =========================

        private static async Task SendAnswerToServer(string jsonPayload)
        {
            using HttpClient client = new();
            using StringContent content = new(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                HttpResponseMessage response =
                    await client.PostAsync("https://submit.bombe.top/submitEdrAns", content);

                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[edrv1][NET] Response: {responseBody}");
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"[edrv1][NET] Request error: {e.Message}");
            }
        }
    }
}