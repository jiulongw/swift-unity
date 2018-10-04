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
    /// The version of Unity for which we need to edit Unity's Metal code.
    /// </summary>
    static Version UNITY_VERSION_FOR_METAL_HELPER = ParseUnityVersionNumber("2017.1.1f1");

    /// <summary>
    /// The minimum versino of Unity for which we need to edit Unity's splash-screen code.
    static Version MIN_UNITY_VERSION_FOR_SPLASH_SCREEN = new Version(2017, 3, 0);

    /// <summary>
    /// Set to false to disable this post-processor, so you can build proper iOS app projects
    /// from the same code base.
    /// </summary>
    private bool enabled = true;

    /// <summary>
    /// Path to the Xcode project.
    /// </summary>
    private string XcodeProjectPath;

    /// <summary>
    /// Path to the root directory of Xcode project.
    /// This should point to the directory of '${XcodeProjectName}.xcodeproj'.
    /// It is recommended to use relative path here.
    /// Current directory is the root directory of this Unity project, i.e. the directory that contains the 'Assets' folder.
    /// Sample value: "../xcode"
    /// </summary>
    private string XcodeProjectRoot { get { return Path.GetDirectoryName(XcodeProjectPath); } }

    /// <summary>
    /// Name of the Xcode project.
    /// This script looks for '${XcodeProjectName} + ".xcodeproj"' under '${XcodeProjectRoot}'.
    /// Sample value: "DemoApp"
    /// </summary>
    private string XcodeProjectName { get { return Path.GetFileNameWithoutExtension(XcodeProjectPath); } }


    /// <summary>
    /// The URL to the Git repository where this project originated.
    /// </summary>
    private const string PROJECT_URL = "https://github.com/jiulongw/swift-unity";

    /// <summary>
    /// The identifier added to touched file to avoid double edits when building to existing directory without
    /// replace existing content.
    /// </summary>
    private const string TOUCH_MARKER = PROJECT_URL + "#v1";

    /// <summary>
    /// A GUIStyle object for rendering links.
    /// </summary>
    private static GUIStyle LINK_STYLE;

    /// <summary>
    /// Creates a menu item in the Tools menu that opens an Editor Window
    /// for editing configuration values for the post build process.
    /// </summary>
    [MenuItem("Tools/SwiftUnity")]
    public static void ShowConfiguration()
    {
        GetWindow<XcodePostBuild>();
    }

    static string[] XCODEPROJ_FILTER = { "Xcode project files", "xcodeproj" };

    /// <summary>
    /// Builds the GUI for the custom editor window for this post process step. 
    /// Find it under `Tools > Swift-Unity`.
    void OnGUI()
    {
        GUILayout.Label("Swift-Unity", EditorStyles.boldLabel);

        enabled = EditorGUILayout.BeginToggleGroup("Enabled", enabled);
        {
            ShowProjectDescription();
            ShowSettings();
        }

        ShowRerunButton();
        EditorGUILayout.EndToggleGroup();
    }

    private void ShowSettings()
    {
        GUILayout.Label("Settings", EditorStyles.boldLabel);

        // If we don't have a directory root for the xcode project, just make a wild
        // guess at one to give the user a starting point for navigating to the real one.
        if (!Directory.Exists(XcodeProjectPath))
        {
            XcodeProjectPath = null;
        }

        if (string.IsNullOrEmpty(XcodeProjectPath))
        {
            XcodeProjectPath = PathExt.Combine(
                Path.GetDirectoryName(Environment.CurrentDirectory),
                "Xcode-Project.xcodeproj");
        }

        // Instead of defining the xcode project root and name separately, have the user
        // select the .xcodeproj file and then figure them out from there.
        var xcodeProjectFile = PathExt.Abs2Rel(XcodeProjectPath);
        xcodeProjectFile = EditorGUILayout.TextField("Xcode Project File", xcodeProjectFile);

        if (GUILayout.Button("Browse..."))
        {
            string userSelection = null;
            if (Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                userSelection = EditorUtility.OpenFilePanelWithFilters(
                    "Select Xcode project file",
                    Path.GetDirectoryName(XcodeProjectPath),
                    XCODEPROJ_FILTER);
            }
            else
            {
                userSelection = EditorUtility.OpenFolderPanel(
                    "Select Xcode project file",
                    Path.GetDirectoryName(XcodeProjectPath),
                    "Xcode-Project.xcodeproj");
            }

            if (!string.IsNullOrEmpty(userSelection))
            {
                xcodeProjectFile = PathExt.Abs2Rel(userSelection);
            }
        }

        XcodeProjectPath = PathExt.Rel2Abs(xcodeProjectFile);
    }

    /// <summary>
    /// TODO: remove this once all the bugs are worked out. This saves us a little
    /// time by avoiding having to run the full Unity build every time we want to 
    /// validate the post process.
    /// </summary>
    private static void ShowRerunButton()
    {
        // Displaying the current build location serves as a reminder to the user what is going on.
        var currentBuildLocation = EditorUserBuildSettings.GetBuildLocation(BuildTarget.iOS);
        bool hasBuildLocation = !string.IsNullOrEmpty(currentBuildLocation);

        if (hasBuildLocation)
        {
            currentBuildLocation = PathExt.Abs2Rel(currentBuildLocation);
        }
        else
        {
            currentBuildLocation = "<N/A>";
        }

        GUILayout.Label(
            string.Format(
                "Current Build Location: {0}",
                currentBuildLocation));

        if (hasBuildLocation && GUILayout.Button("Run post-build process"))
        {
            OnPostBuild(BuildTarget.iOS, currentBuildLocation);
        }
        else
        {
            GUILayout.Label("Please run build process for iOS.");
        }
    }

    private static void ShowProjectDescription()
    {
        GUILayout.Label(@"Enables Unity iOS build output to be embedded into existing Xcode Swift project.

However, since this script touches Unity iOS build output, you will not be able to use Unity iOS build directly in Xcode. As a result, it is recommended to put Unity iOS build output into a temporary directory that you generally do not touch, such as '/tmp'.

In order for this to work, necessary changes to the target Xcode Swift project are needed. Especially the 'AppDelegate.swift' should be modified to properly initialize Unity. For details, see:",
                                   EditorStyles.wordWrappedLabel);

        // Creating the GUIStyle can't be done in a normal constructor
        // context because of some weird Unity restriction.
        if (LINK_STYLE == null)
        {
            var blackTransparent = new Color(0, 0, 0, 0);
            var transparent = new Texture2D(4, 4, TextureFormat.ARGB32, false);
            for (int y = 0; y < transparent.height; ++y)
            {
                for (int x = 0; x < transparent.width; ++x)
                {
                    transparent.SetPixel(x, y, blackTransparent);
                }
            }
            transparent.Apply(false);

            LINK_STYLE = new GUIStyle(GUI.skin.label);

            LINK_STYLE.normal.background
                = LINK_STYLE.onNormal.background
                = transparent;
            LINK_STYLE.normal.textColor
                = LINK_STYLE.onNormal.textColor
                = Color.Lerp(Color.blue, Color.white, 0.5f);

            LINK_STYLE.focused.background
                = LINK_STYLE.onFocused.background
                = LINK_STYLE.hover.background
                = LINK_STYLE.onHover.background
                = transparent;
            LINK_STYLE.focused.textColor
                = LINK_STYLE.onFocused.textColor
                = LINK_STYLE.hover.textColor
                = LINK_STYLE.onHover.textColor
                = Color.Lerp(Color.cyan, Color.white, 0.5f);
        }

        if (GUILayout.Button(PROJECT_URL, LINK_STYLE))
        {
            Application.OpenURL(PROJECT_URL);
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

        var ExportsConfigProjectPath = PathExt.Combine(XcodeProjectRoot, XcodeProjectName, "Unity", "Exports.xcconfig");
        PathExt.FillDirectories(ExportsConfigProjectPath);
        File.WriteAllText(ExportsConfigProjectPath, config.ToString());
    }

    static void CopyFile(string srcPath, string destPath)
    {
        PathExt.FillDirectories(destPath);
        File.Copy(srcPath, destPath, true);
    }

    /// <summary>
    /// Enumerates Unity output files and add necessary files into Xcode project file.
    /// It only add a reference entry into project.pbx file, without actually copy it.
    /// Xcode pre-build script will copy files into correct location.
    /// </summary>
    void UpdateUnityProjectFiles(string pathToBuiltProject)
    {
        var pbxPath = PathExt.Combine(
            XcodeProjectRoot,
            Path.ChangeExtension(XcodeProjectName, "xcodeproj"),
            "project.pbxproj");
        var pbx = new PBXProject();
        pbx.ReadFromFile(pbxPath);

        string classesPath = PathExt.Combine(XcodeProjectName, "Unity", "Classes");
        ProcessUnityDirectory(
            pbx,
            PathExt.Combine(pathToBuiltProject, "Classes"),
            PathExt.Combine(XcodeProjectRoot, classesPath),
            classesPath);

        string librariesPath = PathExt.Combine(XcodeProjectName, "Unity", "Libraries");
        ProcessUnityDirectory(
            pbx,
            PathExt.Combine(pathToBuiltProject, "Libraries"),
            PathExt.Combine(XcodeProjectRoot, librariesPath),
            librariesPath);

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

        foreach (var projPath in
            from f in newFiles
            where !f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)
            let projPath = Path.Combine(projectPathPrefix, f)
            where !pbx.ContainsFileByProjectPath(projPath)
            select projPath)
        {
            var guid = pbx.AddFile(projPath, projPath);
            pbx.AddFileToBuild(targetGuid, guid);

            Debug.LogFormat("Added file to pbx: '{0}'", projPath);
        }

        foreach (var projPath in
            from f in extraFiles
            let projPath = PathExt.Combine(projectPathPrefix, f)
            where pbx.ContainsFileByProjectPath(projPath)
            select projPath)
        {
            var guid = pbx.FindFileGuidByProjectPath(projPath);
            pbx.RemoveFile(guid);

            Debug.LogFormat("Removed file from pbx: '{0}'", projPath);
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
                results.Add(PathExt.Abs2Rel(path, directory));
            }
        }

        return results.ToArray();
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

    /// <summary>
    /// Make necessary changes to Unity build output that enables it to be embedded into existing Xcode project.
    /// </summary>
    private static void PatchUnityNativeCode(string pathToBuiltProject)
    {
        var unityVersion = ParseUnityVersionNumber(Application.unityVersion);

        EditMainMM(PathExt.Combine(pathToBuiltProject, "Classes/main.mm"));
        EditUnityAppControllerH(PathExt.Combine(pathToBuiltProject, "Classes/UnityAppController.h"));
        EditUnityAppControllerMM(PathExt.Combine(pathToBuiltProject, "Classes/UnityAppController.mm"));

        if (unityVersion == UNITY_VERSION_FOR_METAL_HELPER)
        {
            EditMetalHelperMM(PathExt.Combine(pathToBuiltProject, "Classes/Unity/MetalHelper.mm"));
        }

        if (unityVersion >= MIN_UNITY_VERSION_FOR_SPLASH_SCREEN)
        {
            EditSplashScreenMM(PathExt.Combine(pathToBuiltProject, "Classes/UI/SplashScreen.mm"));
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
        File.Copy(path, bakPath, true);

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

/// <summary>
/// A static class containing a few functions that the normal System.IO.Path class does not contain.
/// </summary>
public static class PathExt
{
    /// <summary>
    /// Creates a path-like value for the current system, given a variable number of path parts.
    /// Path parts may be directories or files. The path parts are not validated against the file
    /// system.
    /// </summary>
    /// <returns>The parts array, encoded into a single string, with System.IO.Path.DirectorySeparatorChar
    /// separating individual parts</returns>
    /// <remarks>
    /// .NET 3.5 does not have a Path.Combine function that takes a variadric
    /// array of path parts as a parameter. On Unity projects using the .NET
    /// 3.5 runtime, we have to wrap it up and implement our own version.
    /// </remarks>
    /// <param name="parts">The path parts to combine into a path value.</param>
    public static string Combine(params string[] parts)
    {
#if NET_2_0 || NET_2_0_SUBSET
        if (parts == null || parts.Length == 0)
        {
            return null;
        }
        else
        {
            return string.Join(Path.DirectorySeparatorChar.ToString(), parts);
        }
#else
        return Path.Combine(parts);
#endif
    }

    /// <summary>
    /// Creates a file path that is relative to the currently-edited demo path.
    /// </summary>
    /// <returns>The relative path.</returns>
    /// <param name="fullPath">Full path.</param>
    /// <param name="directory">The directory from which to consider the relative path. If no value 
    /// is provided (i.e. `null` or empty string), then the current working directory is used.</param>
    public static string Abs2Rel(string fullPath, string directory = null)
    {
        if (!Path.IsPathRooted(fullPath))
        {
            return fullPath;
        }
        else
        {
            if (string.IsNullOrEmpty(directory))
            {
                directory = Environment.CurrentDirectory;
            }

            var partsA = directory.Split('/', '\\').ToList();
            var partsB = fullPath.Split('/', '\\').ToList();

            while (partsA.Count > 0
                   && partsB.Count > 0
                   && partsA[0] == partsB[0])
            {
                partsA.RemoveAt(0);
                partsB.RemoveAt(0);
            }

            if (partsB.Count == 0)
            {
                return null;
            }
            else
            {
                return Combine(partsA
                    .Select(_ => "..")
                    .Concat(partsB)
                    .ToArray());
            }
        }
    }

    /// <summary>
    /// Resolves an absolute path from a path that is relative to the currently-edited
    /// demo path.
    /// </summary>
    /// <returns>The absolute path.</returns>
    /// <param name="relativePath">Relative path.</param>
    /// <param name="directory">The directory from which to consider the relative path. If no value 
    /// is provided (i.e. `null` or empty string), then the current working directory is used.</param>
    public static string Rel2Abs(string relativePath, string directory = null)
    {
        if (Path.IsPathRooted(relativePath))
        {
            return relativePath;
        }
        else
        {
            if (string.IsNullOrEmpty(directory))
            {
                directory = Environment.CurrentDirectory;
            }

            var partsA = directory.Split('/', '\\').ToList();
            var partsB = relativePath.Split('/', '\\').ToList();

            while (partsA.Count > 0
                   && partsB.Count > 0
                   && partsB[0] == "..")
            {
                partsA.RemoveAt(partsA.Count - 1);
                partsB.RemoveAt(0);
            }

            if (partsB.Count == 0)
            {
                return null;
            }
            else
            {
                return Combine(partsA
                    .Concat(partsB)
                    .ToArray());
            }
        }
    }

    /// <summary>
    /// Fills in directory structures as necessary to make sure that file operations on
    /// `path` will succeed.
    /// </summary>
    /// <param name="path"></param>
    public static void FillDirectories(string path)
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
}

#endif
