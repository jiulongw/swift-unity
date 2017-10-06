using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public class PostBuild
{
#if UNITY_IOS
    private const string XcodeProjectRoot = "../xcode";

    private const string XcodeProjectName = "DemoApp";

    private const string ClassesProjectPath = XcodeProjectName + "/Unity/Classes";

    private const string LibrariesProjectPath = XcodeProjectName + "/Unity/Libraries";

    private const string ExportsConfigProjectPath = XcodeProjectName + "/Unity/Exports.xcconfig";

    private const string PbxFilePath = XcodeProjectName + ".xcodeproj/project.pbxproj";

    [PostProcessBuild]
    public static void OnPostBuild(BuildTarget target, string pathToBuiltProject)
    {
        UpdateUnityIOSExports(pathToBuiltProject);

        UpdateUnityProjectFiles(pathToBuiltProject);
    }

    private static void UpdateUnityIOSExports(string pathToBuiltProject)
    {
        var config = new StringBuilder();
        config.AppendFormat("UNITY_RUNTIME_VERSION = {0};", Application.unityVersion);
        config.AppendLine();
        config.AppendFormat("UNITY_IOS_EXPORT_PATH = {0};", pathToBuiltProject);
        config.AppendLine();

        var configPath = Path.Combine(XcodeProjectRoot, ExportsConfigProjectPath);
        var configDir = Path.GetDirectoryName(configPath);
        if (!Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }

        File.WriteAllText(configPath, config.ToString());
    }

    private static void UpdateUnityProjectFiles(string pathToBuiltProject)
    {
        var pbx = new PBXProject();
        var pbxPath = Path.Combine(XcodeProjectRoot, PbxFilePath);
        pbx.ReadFromFile(pbxPath);

        ProcessUnityDirectory(
            pbx,
            Path.Combine(pathToBuiltProject, "Classes"),
            Path.Combine(XcodeProjectRoot, ClassesProjectPath),
            ClassesProjectPath);

        ProcessUnityDirectory(
            pbx,
            Path.Combine(pathToBuiltProject, "Libraries"),
            Path.Combine(XcodeProjectRoot, LibrariesProjectPath),
            LibrariesProjectPath);

        pbx.WriteToFile(pbxPath);
    }

    /// <summary>
    /// Update pbx project file by adding src files and removing extra files that
    /// exists in dest but not in src any more.
    ///
    /// This method only updates the pbx project file. It does not copy or delete
    /// files in Swift Xcode project. The Swift Xcode project will do copy and delete
    /// during build, and it should copy files if contents are different, regardless
    /// of the file time.
    /// </summary>
    /// <param name="pbx">The pbx project.</param>
    /// <param name="src">The directory where Unity project is built.</param>
    /// <param name="dest">The directory of the Swift Xcode project where the
    /// Unity project is embedded into.</param>
    /// <param name="projectPathPrefix">The prefix of project path in Swift Xcode
    /// project for Unity code files. E.g. "DempApp/Unity/Classes" for all files
    /// under Classes folder from Unity iOS build output.</param>
    private static void ProcessUnityDirectory(PBXProject pbx, string src, string dest, string projectPathPrefix)
    {
        var targetGuid = pbx.TargetGuidByName(XcodeProjectName);

        string[] newFiles, extraFiles;
        CompareDirectories(src, dest, out newFiles, out extraFiles);

        foreach (var f in newFiles)
        {
            var projPath = Path.Combine(projectPathPrefix, f);
            if (!pbx.ContainsFileByProjectPath(projPath))
            {
                var guid = pbx.AddFile(projPath, projPath);
                pbx.AddFileToBuild(targetGuid, guid);

                Debug.LogFormat("Added file to pbx: '{0}'", projPath);
            }
        }

        foreach (var f in extraFiles)
        {
            var projPath = Path.Combine(projectPathPrefix, f);
            if (pbx.ContainsFileByProjectPath(projPath))
            {
                var guid = pbx.FindFileGuidByProjectPath(projPath);
                pbx.RemoveFile(guid);

                Debug.LogFormat("Removed file from pbx: '{0}'", projPath);
            }
        }
    }

    /// <summary>
    /// Compares the directories. Returns files that exists in src and
    /// extra files that exists in dest but not in src any more. 
    /// </summary>
    private static void CompareDirectories(string src, string dest, out string[] srcFiles, out string[] extraFiles)
    {
        srcFiles = GetFilesRelativePath(src);

        var destFiles = GetFilesRelativePath(dest);
        var extraFilesSet = new HashSet<string>(destFiles);

        extraFilesSet.ExceptWith(srcFiles);
        extraFiles = extraFilesSet.ToArray();
    }

    private static string[] GetFilesRelativePath(string directory)
    {
        var results = new List<string>();

        if (Directory.Exists(directory))
        {
            foreach (var path in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                var relative = path.Substring(directory.Length).TrimStart('/');
                results.Add(relative);
            }
        }

        return results.ToArray();
    }
#endif
}
