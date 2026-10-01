using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public class AndroidBuildScript
{
    private const string TriggerFile = "BuildTrigger.txt";
    private const string OutputLog = "BuildOutput.log";

    static AndroidBuildScript()
    {
        EditorApplication.update += CheckTrigger;
    }

    private static void CheckTrigger()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        if (File.Exists(TriggerFile))
        {
            try
            {
                File.Delete(TriggerFile);
            }
            catch (Exception ex)
            {
                Debug.LogError("Error deleting trigger file: " + ex.Message);
                return;
            }

            Debug.Log("[AndroidBuildScript] Trigger detected, starting build...");
            BuildAndroid();
        }
    }

    [MenuItem("Build/Build Android APK")]
    public static void BuildAndroid()
    {
        string logMsg = "";
        try
        {
            File.WriteAllText(OutputLog, "Starting Android Build...\n");

            // Ensure Android target
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            string buildDir = "Builds/Android";
            if (!Directory.Exists(buildDir))
            {
                Directory.CreateDirectory(buildDir);
            }

            string apkPath = Path.Combine(buildDir, "ARRacingGame.apk");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/MainGameAR_Foundation.unity" },
                locationPathName = apkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            Debug.Log($"[AndroidBuildScript] Building APK to: {apkPath}");
            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            logMsg = $"Build result: {summary.result}\n" +
                     $"Total errors: {summary.totalErrors}\n" +
                     $"Total warnings: {summary.totalWarnings}\n" +
                     $"Total time: {summary.totalTime}\n" +
                     $"Total size: {summary.totalSize} bytes\n" +
                     $"Output path: {summary.outputPath}\n";

            if (summary.result == BuildResult.Succeeded)
            {
                logMsg += "BUILD SUCCEEDED!\n";
                Debug.Log("[AndroidBuildScript] " + logMsg);
            }
            else
            {
                logMsg += "BUILD FAILED!\n";
                Debug.LogError("[AndroidBuildScript] " + logMsg);
            }

            File.AppendAllText(OutputLog, logMsg);
        }
        catch (Exception ex)
        {
            logMsg = $"BUILD EXCEPTION: {ex}\n";
            Debug.LogError("[AndroidBuildScript] " + logMsg);
            File.AppendAllText(OutputLog, logMsg);
        }
    }
}
