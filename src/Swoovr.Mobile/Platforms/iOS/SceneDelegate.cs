using Foundation;

namespace Swoovr.Mobile;

/// <summary>iOS 27 SDK apps must adopt the UIScene life cycle or they fail to launch; MAUI provides the delegate.</summary>
[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
}
