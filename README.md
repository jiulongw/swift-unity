# How to Embed Unity Project into Xcode Swift Project

This repo contains a demo Xcode 9+ iOS project (Swift 4.0) that builds a single view app with an embeded Unity3d scene
(Unity 2017.1.1f1 to 2017.2.0f3).  The idea here is to *fully* automate build processes for both projects to avoid manual
copy / paste / drag / drop every time Unity project is updated.

It also demonstrates runtime communication between Swift and Unity.

This would not be possible without [tutorial by BLITZ][1] and [video by the-nerd Frederik Jacques][2].  Minor updates
are applied to fit latest Xcode and Unity releases.  Here I will briefly go through the process and highlight
these updates.

## Workflow
1. Develop in Unity as usual.
2. Build Unity project (iOS) to any directory (preferably `/tmp`, there is no need to access it manually).  
   This will generate the `Exports.xcconfig` file with exported path so that Swift Xcode project knows where to find
   the exported Unity project.
3. Open Swift Xcode project and build.
4. Update Unity / Swift code. Everytime Unity project is built, Xcode gets updates.

*Note*: Building the Unity demo project for the first time will change Xcode project file a lot.
It is safe to commit the change into source control as it only performs necessary updates going forward.

*Note*: `DemoApp/Unity/Classes` and `DemoApp/Unity/Libraries` are managed by the build process.
Do not add new files manually into these folder or they will be deleted upon build.

## Unity Project

A [post build process script][5] is added to Unity project. After each build, Xcode project gets information about
where Unity dropped build outputs and what's updated since last Unity built so the Xcode project file will be updated
accordingly.

This requires the Unity project to know the location of the Xcode project.  Typically it is a sibling folder in the
same workspace.

### Use your own Unity project
Simply add the [post build script][5] to your Unity project (it needs to be an editor script), and update the
Xcode project location if needed.

## Xcode Project

In a nutshell, to embed Unity project into Swift project, we need to:

1. Include Unity generated code in `Classes` and `Libraries` folders in Xcode build target. *Automated*
2. Remove `main` entry point and pass events from `AppDelegate` to `UnityAppController`.
3. Get Unity GL view and add it as subview to the desired `UIViewController`.
4. Copy `Data` folder to `Product.app` after build.

### Add Code to Build Target (Automated)

The `Classes` and `Libraries` folders need to be compiled into the Xcode project's build target. The build process will
setup everything for you.

We're going to make changes to some of the generated files. Here is a list of files that needs to be modified.

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
Sample code can be found in [AppDelegate.swift][4].

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

## FAQ

[Exports.xcconfig file missing][6]

## Updates
* 11/2/2017 - Support Unity version from 2017.1.1f1 to 2017.2.0f3.
* 10/25/2017 - Added FAQ about missing Exports.xcconfig file.
* 10/6/2017 - Fix previous known issue that new files generated by Unity are not added to Xcode project automatically.
* 9/26/2017 - Initial commit


[1]: https://github.com/blitzagency/ios-unity5
[2]: http://www.the-nerd.be/2015/08/20/a-better-way-to-integrate-unity3d-within-a-native-ios-application/
[3]: https://developer.apple.com/library/content/documentation/Swift/Conceptual/BuildingCocoaApps/MixandMatch.html
[4]: https://github.com/jiulongw/swift-unity/blob/master/demo/xcode/DemoApp/AppDelegate.swift
[5]: https://github.com/jiulongw/swift-unity/blob/master/demo/unity/Assets/Scripts/Editor/PostBuild.cs
[6]: https://github.com/jiulongw/swift-unity/issues/8
