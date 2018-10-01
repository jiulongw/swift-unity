/*
MIT License

Copyright (c) 2017 Jiulong Wang

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

#if UNITY_IOS

using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;

using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

/// <summary>
/// Adding this post build script to Unity project enables Unity iOS build output to be embedded
/// into existing Xcode Swift project.
///
/// However, since this script touches Unity iOS build output, you will not be able to use Unity
/// iOS build directly in Xcode. As a result, it is recommended to put Unity iOS build output into
/// a temporary directory that you generally do not touch, such as '/tmp'.
///
/// In order for this to work, necessary changes to the target Xcode Swift project are needed.
/// Especially the 'AppDelegate.swift' should be modified to properly initialize Unity.
/// See https://github.com/jiulongw/swift-unity for details.
/// </summary>
public class XcodePostBuild : EditorWindow
{
    /// <summary>
    /// Set to false to disable this post-processor, so you can build proper iOS app projects
    /// from the same code base.
    /// </summary>
    private bool enabled = true;

    /// <summary>
    /// Path to the root directory of Xcode project.
    /// This should point to the directory of '${XcodeProjectName}.xcodeproj'.
    /// It is recommended to use relative path here.
    /// Current directory is the root directory of this Unity project, i.e. the directory that contains the 'Assets' folder.
    /// Sample value: "../xcode"
    /// </summary>
    private string XcodeProjectRoot;

    /// <summary>
    /// Name of the Xcode project.
    /// This script looks for '${XcodeProjectName} + ".xcodeproj"' under '${XcodeProjectRoot}'.
    /// Sample value: "DemoApp"
    /// </summary>
    private string XcodeProjectName;

    /// <summary>
    /// The identifier added to touched file to avoid double edits when building to existing directory without
    /// replace existing content.
    /// </summary>
    private const string PROJECT_URL = "https://github.com/jiulongw/swift-unity";
    private const string TOUCH_MARKER = PROJECT_URL + "#v1";

    private static GUIStyle LINK_STYLE;

#if UNITY_IOS
    [MenuItem("Tools/SwiftUnity")]
    public static void ShowConfiguration()
    {
        GetWindow(typeof(XcodePostBuild));
    }
#endif

    void OnEnable()
    {
        if (LINK_STYLE == null)
        {
            LINK_STYLE = new GUIStyle
            {
                normal = new GUIStyleState
                {
                    textColor = Color.Lerp(Color.blue, Color.white, 0.5f)
                }
            };
        }
    }

    static string[] XCODEPROJ_FILTER = { "Xcode project files", "xcodeproj" };
    void OnGUI()
    {
        GUILayout.Label("Swift-Unity", EditorStyles.boldLabel);

        enabled = EditorGUILayout.BeginToggleGroup("Enabled", enabled);
        GUILayout.Label(@"Enables Unity iOS build output to be embedded into existing Xcode Swift project.

However, since this script touches Unity iOS build output, you will not be able to use Unity iOS build directly in Xcode. As a result, it is recommended to put Unity iOS build output into a temporary directory that you generally do not touch, such as '/tmp'.

In order for this to work, necessary changes to the target Xcode Swift project are needed. Especially the 'AppDelegate.swift' should be modified to properly initialize Unity. For details, see:",
                       EditorStyles.wordWrappedLabel);

        if (GUILayout.Button(PROJECT_URL, LINK_STYLE))
        {
            Application.OpenURL(PROJECT_URL);
        }

        GUILayout.Label("Settings", EditorStyles.boldLabel);
        {
            var currentBuildLocation = EditorUserBuildSettings.GetBuildLocation(BuildTarget.iOS);
            GUILayout.Label(string.Format("Current Build Location: {0}", Abs2Rel(currentBuildLocation)));

            if (string.IsNullOrEmpty(XcodeProjectRoot))
            {
                XcodeProjectRoot = Path.GetDirectoryName(Environment.CurrentDirectory);
            }

            if (string.IsNullOrEmpty(XcodeProjectName))
            {
                XcodeProjectName = "Xcode-Project";
            }

            var xcodeProjectFile = Abs2Rel(Path.ChangeExtension(Combine(XcodeProjectRoot, XcodeProjectName), "xcodeproj"));
            xcodeProjectFile = EditorGUILayout.TextField("Xcode Project File", xcodeProjectFile);

            if (GUILayout.Button("browse..."))
            {
                xcodeProjectFile = EditorUtility.OpenFilePanelWithFilters("Select Xcode Project File", Path.GetDirectoryName(xcodeProjectFile), XCODEPROJ_FILTER);
            }

            XcodeProjectName = Path.GetFileNameWithoutExtension(xcodeProjectFile);
            XcodeProjectRoot = Path.GetDirectoryName(xcodeProjectFile);

            if (GUILayout.Button("Run now!"))
            {
                OnPostBuild(BuildTarget.iOS, currentBuildLocation);
            }
        }
        EditorGUILayout.EndToggleGroup();
    }

    static string Combine(params string[] parts)
    {
        if (parts == null || parts.Length == 0)
        {
            return null;
        }
        else
        {
            var path = parts[0];
            for (int i = 1; i < parts.Length; ++i)
            {
                path = Path.Combine(path, parts[i]);
            }
            return path;
        }
    }

    /// <summary>
    /// Creates a file path that is relative to the currently-edited demo path.
    /// </summary>
    /// <returns>The relative path.</returns>
    /// <param name="fullPath">Full path.</param>
    static string Abs2Rel(string fullPath)
    {
        if (Path.IsPathRooted(fullPath))
        {
            var partsA = Environment.CurrentDirectory.Split(Path.DirectorySeparatorChar).ToList();
            var partsB = fullPath.Split(Path.DirectorySeparatorChar).ToList();

            while (partsA.Count > 0
                   && partsB.Count > 0
                   && partsA[0] == partsB[0])
            {
                partsA.RemoveAt(0);
                partsB.RemoveAt(0);
            }

#pragma warning disable XS0001 // Find APIs marked as TODO in Mono
            var sb = new StringBuilder();
#pragma warning restore XS0001 // Find APIs marked as TODO in Mono
            foreach (var part in partsA)
            {
                sb.Append("..");
                sb.Append(Path.DirectorySeparatorChar);
            }

            var namePart = partsB.Last();
            partsB.RemoveAt(partsB.Count - 1);

            foreach (var part in partsB)
            {
                sb.Append(part);
                sb.Append(Path.DirectorySeparatorChar);
            }

            sb.Append(namePart);

            return sb.ToString();
        }
        else
        {
            return fullPath;
        }
    }

    /// <summary>
    /// Resolves an absolute path from a path that is relative to the currently-edited
    /// demo path.
    /// </summary>
    /// <returns>The absolute path.</returns>
    /// <param name="relativePath">Relative path.</param>
    static string Rel2Abs(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            return relativePath;
        }
        else
        {
            return Combine(Environment.CurrentDirectory, relativePath);
        }
    }

    [PostProcessBuild]
    public static void OnPostBuild(BuildTarget target, string pathToBuiltProject)
    {
        var build = GetWindow<XcodePostBuild>();
        if (build.enabled && target == BuildTarget.iOS)
        {
            PatchUnityNativeCode(pathToBuiltProject);
            build.UpdateUnityIOSExports(pathToBuiltProject);
            build.UpdateUnityProjectFiles(pathToBuiltProject);
        }
    }

    /// <summary>
    /// Writes current Unity version and output path to 'Exports.xcconfig' file.
    /// </summary>
    void UpdateUnityIOSExports(string pathToBuiltProject)
    {
        var config = new StringBuilder();
        config.AppendFormat("UNITY_RUNTIME_VERSION = {0};", Application.unityVersion);
        config.AppendLine();
        config.AppendFormat("UNITY_IOS_EXPORT_PATH = {0};", pathToBuiltProject);
        config.AppendLine();

        var ExportsConfigProjectPath = Combine(XcodeProjectRoot, XcodeProjectName, "Unity", "Exports.xcconfig");
        FillDirectories(ExportsConfigProjectPath);
        File.WriteAllText(ExportsConfigProjectPath, config.ToString());
    }

    static void CopyFile(string srcPath, string destPath)
    {
        FillDirectories(destPath);
        File.Copy(srcPath, destPath, true);
    }

    static void FillDirectories(string path)
    {
        var root = new DirectoryInfo(Path.GetDirectoryName(path));
        var dirs = new List<DirectoryInfo>();
        while (root != null)
        {
            dirs.Add(root);
            root = root.Parent;
        }

        dirs.Reverse();
        foreach (var dir in dirs)
        {
            if (!dir.Exists)
            {
                dir.Create();
            }
        }
    }

    /// <summary>
    /// Enumerates Unity output files and add necessary files into Xcode project file.
    /// It only add a reference entry into project.pbx file, without actually copy it.
    /// Xcode pre-build script will copy files into correct location.
    /// </summary>
    void UpdateUnityProjectFiles(string pathToBuiltProject)
    {
        var pbxPath = Combine(
            XcodeProjectRoot,
            Path.ChangeExtension(XcodeProjectName, "xcodeproj"),
            "project.pbxproj");

        var pbx = new PBXProject();
        pbx.ReadFromFile(pbxPath);

        string classesPath = Combine(XcodeProjectName, "Unity", "Classes");
        ProcessUnityDirectory(
            pbx,
            Combine(pathToBuiltProject, "Classes"),
            Combine(XcodeProjectRoot, classesPath),
            classesPath);

        string librariesPath = Combine(XcodeProjectName, "Unity", "Libraries");
        ProcessUnityDirectory(
            pbx,
            Combine(pathToBuiltProject, "Libraries"),
            Combine(XcodeProjectRoot, librariesPath),
            librariesPath);

        string dataPath = Combine(XcodeProjectName, "Data");
        ProcessUnityDirectory(
            pbx,
            Combine(pathToBuiltProject, "Data"),
            Combine(XcodeProjectRoot, dataPath),
            dataPath);

        string frameworksPath = Combine(XcodeProjectName, "Frameworks");
        ProcessUnityDirectory(
            pbx,
            Combine(pathToBuiltProject, "Frameworks"),
            Combine(XcodeProjectRoot, frameworksPath),
            frameworksPath);

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
    void ProcessUnityDirectory(PBXProject pbx, string src, string dest, string projectPathPrefix)
    {
        var targetGuid = pbx.TargetGuidByName(XcodeProjectName);
        if (string.IsNullOrEmpty(targetGuid))
        {
            throw new Exception(string.Format("TargetGuid could not be found for '{0}'", XcodeProjectName));
        }

        // newFiles: array of file names in build output that do not exist in project.pbx manifest.
        // extraFiles: array of file names in project.pbx manifest that do not exist in build output.
        // Build output files that already exist in project.pbx manifest will be skipped to minimize
        // changes to project.pbx file.
        string[] newFiles, extraFiles;
        CompareDirectories(src, dest, out newFiles, out extraFiles);

        foreach (var f in newFiles)
        {
            if (ShouldExcludeFile(f))
            {
                continue;
            }

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
            var projPath = Combine(projectPathPrefix, f);
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

    private static bool ShouldExcludeFile(string fileName)
    {
        if (fileName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Replaces the character component of Unity's version number with a period
    /// and the decimal ASCII value of that character, to create an ersatz revision
    /// number.
    /// </summary>
    /// <returns>A Version struct that can be used for range comparisons.</returns>
    /// <param name="versionString">Unity's version string, formatted as [YEAR].[MINOR].[PATCH][CHARACTER][REVISION].</param>
    private static Version ParseUnityVersionNumber(string versionString)
    {
        for (int i = versionString.Length - 1; i >= 0; --i)
        {
            var token = versionString[i];
            if (char.IsLetter(token))
            {
                versionString = versionString
                    .Remove(i)
                    .Insert(i, string.Format(".{0}", (int)token));
            }
        }

        return new Version(versionString);
    }

    static Version UNITY_VERSION_FOR_METAL_HELPER = ParseUnityVersionNumber("2017.1.1f1");
    static Version MIN_UNITY_VERSION_FOR_SPLASH_SCREEN = new Version(2017, 3, 0);

    /// <summary>
    /// Make necessary changes to Unity build output that enables it to be embedded into existing Xcode project.
    /// </summary>
    private static void PatchUnityNativeCode(string pathToBuiltProject)
    {
        var unityVersion = ParseUnityVersionNumber(Application.unityVersion);

        EditMainMM(Combine(pathToBuiltProject, "Classes/main.mm"));
        EditUnityAppControllerH(Combine(pathToBuiltProject, "Classes/UnityAppController.h"));
        EditUnityAppControllerMM(Combine(pathToBuiltProject, "Classes/UnityAppController.mm"));

        if (unityVersion == UNITY_VERSION_FOR_METAL_HELPER)
        {
            EditMetalHelperMM(Combine(pathToBuiltProject, "Classes/Unity/MetalHelper.mm"));
        }

        if (unityVersion >= MIN_UNITY_VERSION_FOR_SPLASH_SCREEN)
        {
            EditSplashScreenMM(Combine(pathToBuiltProject, "Classes/UI/SplashScreen.mm"));
        }
    }

    /// <summary>
    /// Edit 'main.mm': removes 'main' entry that would conflict with the Xcode project it embeds into.
    /// </summary>
    private static void EditMainMM(string path)
    {
        EditCodeFile(path, line =>
        {
            if (line.TrimStart().StartsWith("int main", StringComparison.Ordinal))
            {
                return line.Replace("int main", "int old_main");
            }

            return line;
        });
    }

    /// <summary>
    /// Edit 'UnityAppController.h': returns 'UnityAppController' from 'AppDelegate' class.
    /// </summary>
    private static void EditUnityAppControllerH(string path)
    {
        var inScope = false;
        var markerDetected = false;
        var markerAdded = false;

        EditCodeFile(path, line =>
        {
            markerDetected |= line.Contains(TOUCH_MARKER);
            inScope |= line.Contains("inline UnityAppController");

            if (inScope && !markerDetected)
            {
                if (line.Trim() == "}")
                {
                    inScope = false;

                    return new string[]
                    {
                        "// }",
                        "",
                        "NS_INLINE UnityAppController* GetAppController()",
                        "{",
                        "    NSObject<UIApplicationDelegate>* delegate = [UIApplication sharedApplication].delegate;",
                        @"    UnityAppController* currentUnityController = (UnityAppController*)[delegate valueForKey: @""currentUnityController""];",
                        "    return currentUnityController;",
                        "}",
                    };
                }

                if (!markerAdded)
                {
                    markerAdded = true;
                    return new string[]
                    {
                        "// Modified by " + TOUCH_MARKER,
                        "// " + line,
                    };
                }

                return new string[] { "// " + line };
            }

            return new string[] { line };
        });
    }

    /// <summary>
    /// Edit 'UnityAppController.mm': triggers 'UnityReady' notification after Unity is actually started.
    /// </summary>
    private static void EditUnityAppControllerMM(string path)
    {
        var inScope = false;
        var markerDetected = false;

        EditCodeFile(path, line =>
        {
            inScope |= line.Contains("- (void)startUnity:");
            markerDetected |= inScope && line.Contains(TOUCH_MARKER);

            if (inScope && line.Trim() == "}")
            {
                inScope = false;

                if (markerDetected)
                {
                    return new string[] { line };
                }
                else
                {
                    return new string[]
                    {
                        "    // Modified by " + TOUCH_MARKER,
                        "    // Post a notification so that Swift can load unity view once started.",
                        @"    [[NSNotificationCenter defaultCenter] postNotificationName: @""UnityReady"" object:self];",
                        "}",
                    };
                }
            }

            return new string[] { line };
        });
    }

    /// <summary>
    /// Edit 'MetalHelper.mm': fixes a bug (only in 2017.1.1f1) that causes crash.
    /// </summary>
    private static void EditMetalHelperMM(string path)
    {
        var markerDetected = false;

        EditCodeFile(path, line =>
        {
            markerDetected |= line.Contains(TOUCH_MARKER);

            if (!markerDetected && line.Trim() == "surface->stencilRB = [surface->device newTextureWithDescriptor: stencilTexDesc];")
            {
                return new string[]
                {
                    "",
                    "    // Modified by " + TOUCH_MARKER,
                    "    // Default stencilTexDesc.usage has flag 1. In runtime it will cause assertion failure:",
                    "    // validateRenderPassDescriptor:589: failed assertion `Texture at stencilAttachment has usage (0x01) which doesn't specify MTLTextureUsageRenderTarget (0x04)'",
                    "    // Adding MTLTextureUsageRenderTarget seems to fix this issue.",
                    "    stencilTexDesc.usage |= MTLTextureUsageRenderTarget;",
                    line,
                };
            }

            return new string[] { line };
        });
    }

    /// <summary>
    /// Edit 'SplashScreen.mm': Unity introduces its own 'LaunchScreen.storyboard' since 2017.3.0f3.
    /// Disable it here and use Swift project's launch screen instead.
    /// </summary>
    private static void EditSplashScreenMM(string path)
    {
        var markerDetected = false;
        var markerAdded = false;
        var inScope = false;
        var level = 0;

        EditCodeFile(path, line =>
        {
            inScope |= line.Trim() == "void ShowSplashScreen(UIWindow* window)";
            markerDetected |= line.Contains(TOUCH_MARKER);

            if (inScope && !markerDetected)
            {
                if (line.Trim() == "{")
                {
                    level++;
                }
                else if (line.Trim() == "}")
                {
                    level--;
                }

                if (line.Trim() == "}" && level == 0)
                {
                    inScope = false;
                }

                if (level > 0 && line.Trim().StartsWith("bool hasStoryboard", StringComparison.Ordinal))
                {
                    return new string[]
                    {
                        "    // " + line,
                        "    bool hasStoryboard = false;",
                    };
                }

                if (!markerAdded)
                {
                    markerAdded = true;
                    return new string[]
                    {
                        "// Modified by " + TOUCH_MARKER,
                        line,
                    };
                }
            }

            return new string[] { line };
        });
    }

    private static void EditCodeFile(string path, Func<string, string> lineHandler)
    {
        EditCodeFile(path, line =>
        {
            return new string[] { lineHandler(line) };
        });
    }

    private static void EditCodeFile(string path, Func<string, IEnumerable<string>> lineHandler)
    {
        var bakPath = path + ".bak";
        if (File.Exists(bakPath))
        {
            File.Delete(bakPath);
        }

        File.Move(path, bakPath);

        using (var reader = File.OpenText(bakPath))
        using (var stream = File.Create(path))
        using (var writer = new StreamWriter(stream))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var outputs = lineHandler(line);
                foreach (var o in outputs)
                {
                    writer.WriteLine(o);
                }
            }
        }
    }
}

#endif
