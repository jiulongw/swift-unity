# How to Embed Unity Project into Xcode Swift Project

This repo contains a demo Xcode 9 iOS project (Swift 4.0) that builds a single view app with an embeded Unity3d scene
(Unity 2017.1.1f1).  The idea here is to automate build processes for both projects as much as possible to avoid
manual copy & paste every time Unity project is updated.  It also demonstrates runtime communication between Swift
and Unity.

This would not be possible without [tutorial by BLITZ][1] and [video by the-nerd Frederik Jacques][2].  Minor updates
are applied to fit latest Xcode and Unity releases.  Here I will briefly go through the process and highlight
these updates.

## Unity Project

A post build process script is added to Unity project. After each build, Xcode project gets information about
where Unity dropped build outputs.  This way we don't have to use same build drop every time.

```cs
[PostProcessBuild]
public static void OnPostBuild(BuildTarget target, string pathToBuiltProject)
{
#if UNITY_IOS
    var config = new StringBuilder();
    config.AppendFormat("UNITY_RUNTIME_VERSION = {0};", Application.unityVersion);
    config.AppendLine();
    config.AppendFormat("UNITY_IOS_EXPORT_PATH = {0};", pathToBuiltProject);
    config.AppendLine();

    var configPath = Path.Combine(Environment.CurrentDirectory, "../xcode/DemoApp/Unity/Exports.xcconfig");
    File.WriteAllText(configPath, config.ToString());
#endif
}
```

This requires the Unity project to know the location of the Xcode project.  Typically it is a sibling folder in the
same workspace.

## Xcode Project

In a nutshell, to embed Unity project into Swift project, we need to:

1. Include Unity generated code in `Classes` and `Libraries` folders in Xcode build target.
2. Remove `main` entry point and pass events from `AppDelegate` to `UnityAppController`.
3. Get Unity GL view and add it as subview to the desired `UIViewController`.
4. Copy `Data` folder to `Product.app` after build.

### Add Code to Build Target

The `Classes` and `Libraries` folders need to be compiled into the Xcode project's build target. We're going to make
changes to some of the generated files.  As a result, as part of the build process, we copy these two folders and
exclude those unchanged files from source control.

Here is a list of files that needs to be modified.

* Classes/Unity/MetalHelper.mm

  Add `MTLTextureUsageRenderTarget` to the stencil texture descriptor to fix runtime assertion failure.  Not sure if
  this is a bug in Xcode or Unity but following line fixed the issue.

  ```objc
  stencilTexDesc.usage |= MTLTextureUsageRenderTarget;
  ```

* Classes/UnityAppController.h

  Because we modified how UnityAppController is created, we need to change the code that retrieves it.

  ```objc
  //inline UnityAppController* GetAppController()
  //{
  //    return (UnityAppController*)[UIApplication sharedApplication].delegate;
  //}

  NS_INLINE UnityAppController* GetAppController()
  {
      NSObject<UIApplicationDelegate>* delegate = [UIApplication sharedApplication].delegate;
      UnityAppController* currentUnityController = (UnityAppController *)[delegate valueForKey:@"currentUnityController"];
      return currentUnityController;
  }
  ```

* Classes/UnityAppController.mm

  Initializing Unity view is asynchronous.  Before it is done we cannot add it to our UIViewController.  To solve this,
  we issue a notification when Unity view is fully loaded and in our UIViewController, handle the notification and add
  view accordingly.

  ```objc
  [[NSNotificationCenter defaultCenter] postNotificationName:@"UnityReady" object:self];
  ```

* Classes/main.mm

  Here we simply rename `main` so that it won't conflict with Swift's `main` entry.

These files are added to source control and skipped by script that copies updates from Unity build.

### Setup UnityAppController

Since we renamed `main` entry in main.mm, UntiyAppController needs to be setup properly to handle app events.
Sample code can be found in `AppDelegate.swift`.

Note in order to call Objective-C method from Swift code, a [bridging header file][3] needs to be configured in Xcode.

### Embed Unity View

Once everything is in place, embedding the Unity view is pretty straightforward.

```swift
class ViewController: UIViewController {
    override func viewDidLoad() {
        super.viewDidLoad()
        if let appDelegate = UIApplication.shared.delegate as? AppDelegate {
            appDelegate.startUnity()
            NotificationCenter.default.addObserver(
                self,
                selector: #selector(handleUnityReady),
                name: NSNotification.Name("UnityReady"),
                object: nil)
        }
    }

    @objc func handleUnityReady() {
        if let unityView = UnityGetGLView() {
            // insert subview at index 0 ensures unity view is behind current UI view
            view?.insertSubview(unityView, at: 0)
        }
    }
}
```

### Build Scripts to Sync Unity Outputs

During prebuild, update code generated by Unity.

```sh
echo "Syncing code from $UNITY_IOS_EXPORT_PATH..."
rsync -rc --exclude-from=DemoApp/Unity/rsync_exclude --delete $UNITY_IOS_EXPORT_PATH/Classes/ DemoApp/Unity/Classes/
rsync -rc --exclude-from=DemoApp/Unity/rsync_exclude --delete $UNITY_IOS_EXPORT_PATH/Libraries/ DemoApp/Unity/Libraries/
```

During post build, copy `Data` folder that contains runtime resources.

```sh
echo "Syncing data from $UNITY_IOS_EXPORT_PATH..."
rm -rf "$TARGET_BUILD_DIR/$PRODUCT_NAME.app/Data"
cp -Rf "$UNITY_IOS_EXPORT_PATH/Data" "$TARGET_BUILD_DIR/$PRODUCT_NAME.app/Data"
```

## Known Issues

The Xcode project file can be out of sync if you add new plugins to the Unity project.  This is because Xcode explicitly
list source code files that are added to build target, instead of scanning whole directory for updates.  This can be
solved by manually add files to Xcode after build (prebuild script will copy all files), or write another post build
script on Unity side to programmatically add files to Xcode build target.


[1]: https://github.com/blitzagency/ios-unity5
[2]: http://www.the-nerd.be/2015/08/20/a-better-way-to-integrate-unity3d-within-a-native-ios-application/
[3]: https://developer.apple.com/library/content/documentation/Swift/Conceptual/BuildingCocoaApps/MixandMatch.html
