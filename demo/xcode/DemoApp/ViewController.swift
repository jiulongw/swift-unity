//
//  ViewController.swift
//  DemoApp
//
//  Created by Jiulong Wang on 9/26/17.
//

import UIKit

class ViewController: UIViewController {
    @IBOutlet var rotateSwitch: UISwitch!

    override func viewDidLoad() {
        super.viewDidLoad()
        if let appDelegate = UIApplication.shared.delegate as? AppDelegate {
            appDelegate.startUnity()
            
            NotificationCenter.default.addObserver(self, selector: #selector(handleUnityReady), name: NSNotification.Name("UnityReady"), object: nil)
            NotificationCenter.default.addObserver(self, selector: #selector(handleUnityToggleRotation(_:)), name: NSNotification.Name("UnityToggleRotation"), object: nil)
        }
    }

    @objc func handleUnityReady() {
        showUnitySubView()
    }

    @objc func handleUnityToggleRotation(_ n: NSNotification) {
        if let isOn = n.userInfo?["isOn"] as? NSNumber {
            rotateSwitch.isOn = isOn.boolValue
        }
    }

    @IBAction func handleSwitchValueChanged(sender: UISwitch) {
        UnityPostMessage("NATIVE_BRIDGE", "RotateCube", sender.isOn ? "start" : "stop")
    }

    func showUnitySubView() {
        if let unityView = UnityGetGLView() {
            // insert subview at index 0 ensures unity view is behind current UI view
            view?.insertSubview(unityView, at: 0)
            
            unityView.translatesAutoresizingMaskIntoConstraints = false
            let views = ["view": unityView]
            let w = NSLayoutConstraint.constraints(withVisualFormat: "|-0-[view]-0-|", options: [], metrics: nil, views: views)
            let h = NSLayoutConstraint.constraints(withVisualFormat: "V:|-75-[view]-0-|", options: [], metrics: nil, views: views)
            view.addConstraints(w + h)
        }
    }
}
