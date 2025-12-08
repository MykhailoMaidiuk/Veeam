using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using NLog;
using NLog.Config;
using NLog.Targets;



class FolderSync
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();
    private static volatile bool stopTrigger = false;


    static int Main(string[] args)
    {
        if (args.Length != 4)
        {
            Console.WriteLine("Usage: FolderSync <sourcePath> <replicaPath> <intervalSeconds> <logFilePath>");
            return 1;
        }

        string source = args[0];
        string replica = args[1];
        int intervalSeconds = int.Parse(args[2]);
        try{
            if (intervalSeconds <= 0){
                Console.WriteLine("Interval must be positive.");
                return 1;
            }
        }
        catch{
            Console.WriteLine("Interval is not a number.");
            return 1;
        }
        string logPath = args[3];
        ConfigureNLog(logPath);

        if (!Directory.Exists(source)){
            logger.Error($"Source directory not exist: {source}");
            return 1;
        }
        try
        {
        // Ctrl+C for shutdown
            Console.CancelKeyPress += (s, e) => {
                e.Cancel = true;
                stopTrigger = true;
                logger.Info("Exiting");
            };
        logger.Info($"Starting sync. Source: {source}, Replica: {replica}, Interval: {intervalSeconds}, Log: {logPath}");
        Directory.CreateDirectory(replica);

            // main synchronization loop
            while (!stopTrigger){
                try{
                    SyncDirectories(source, replica);
                }
                catch (Exception ex){
                    logger.Error(ex, "ERROR during sync");
                }

                int elapsed = 0;
                while (elapsed < intervalSeconds && !stopTrigger){
                    Thread.Sleep(1000);
                    elapsed += 1;
                }
            }

            logger.Info("Sync stopped.");
            LogManager.Shutdown();
            return 0;
        }

        catch (Exception ex){
            logger.Fatal(ex, "Application error");
            LogManager.Shutdown();
            return 1;
        }
    }


    private static void SyncDirectories(string sourceDir, string replicaDir)
    {
        // create directories in replica which exist in source
        foreach (var dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories)){
            string relative = Path.GetRelativePath(sourceDir, dirPath);
            string targetDir = Path.Combine(replicaDir, relative);
            if (!Directory.Exists(targetDir)){
                Directory.CreateDirectory(targetDir);
                logger.Info($"Created directory: {targetDir}");
            }
        }

        // copy/upd to replica
        foreach (var srcFile in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories)){
            string relative = Path.GetRelativePath(sourceDir, srcFile);
            string targetFile = Path.Combine(replicaDir, relative);
            string targetDir = Path.GetDirectoryName(targetFile) ?? replicaDir;

            if (!Directory.Exists(targetDir)){
                Directory.CreateDirectory(targetDir);
                logger.Info($"Created directory: {targetDir}");
            }

            bool isNewFile = false;
            bool needCopy = false;

            if (!File.Exists(targetFile)){
                isNewFile = true;
                needCopy = true;
            }
            else{
                var srcInfo = new FileInfo(srcFile);
                var dstInfo = new FileInfo(targetFile);

                if ( dstInfo.Length != srcInfo.Length ||  dstInfo.LastWriteTimeUtc != srcInfo.LastWriteTimeUtc){
                    needCopy = true;
                }
            }

            // atomic сopy
            if (needCopy){
                try{
                    string tempFile = Path.GetTempFileName();

                    File.Copy(srcFile, tempFile, true);
                    File.SetLastWriteTimeUtc(tempFile, File.GetLastWriteTimeUtc(srcFile));
                    File.Move(tempFile, targetFile, overwrite: true);

                    logger.Info(isNewFile ? $"Created file: {targetFile}" : $"Updated file: {targetFile}");
                }
                catch (Exception ex){
                    logger.Error(ex, "ERROR copying file");
                }
            }
        }

        // delete files present in replica but not in source
        foreach (var targetFile in Directory.GetFiles(replicaDir, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(replicaDir, targetFile);
            string srcFile = Path.Combine(sourceDir, relative);
            if (!File.Exists(srcFile)){
                try{
                    File.Delete(targetFile);
                    logger.Info($"Deleted file: {targetFile}");
                }
                catch (Exception ex){
                    logger.Error(ex, $"ERROR deleting file '{targetFile}'");
                }
            }
        }

        // clean empty directories in replica
        var replicaDirs = Directory.GetDirectories(replicaDir, "*", SearchOption.AllDirectories);
        foreach (var targetDir in replicaDirs){
            string relative = Path.GetRelativePath(replicaDir, targetDir);
            string srcDir = Path.Combine(sourceDir, relative);
            if (!Directory.Exists(srcDir)){
                try{
                    if (Directory.Exists(targetDir) && Directory.GetFileSystemEntries(targetDir).Length == 0){
                        Directory.Delete(targetDir);
                        logger.Info($"Deleted directory: {targetDir}");
                    }
                    else{
                        Directory.Delete(targetDir, true);
                        logger.Info($"Deleted directory (recursive): {targetDir}");
                    }
                }
                catch (Exception ex){
                    logger.Error(ex, $"ERROR deleting directory '{targetDir}'");
                }
            }
        }
    }

    //NLog config
    public static void ConfigureNLog(string filePath)
    {
        var config = new LoggingConfiguration();

        // output to console
        var logConsole = new ConsoleTarget("logconsole"){
            Layout = "${longdate} | ${level:uppercase=true} | ${message} ${exception:format=tostring}"
        };

        // output to file
        var logFile = new FileTarget("logfile"){
            FileName = filePath,
            Layout = "${longdate} | ${level:uppercase=true} | ${message} ${exception:format=tostring}",
        };
        config.AddRule(LogLevel.Info, LogLevel.Fatal, logConsole);
        config.AddRule(LogLevel.Info, LogLevel.Fatal, logFile);
        LogManager.Configuration = config;
    }
}