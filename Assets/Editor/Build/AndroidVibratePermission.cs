#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace CityGenPedestrianProbe.Editor
{
    /// <summary>
    /// Adds the vibration permission to the manifest Unity generates.
    /// </summary>
    /// <remarks>
    /// <para>Unity adds this permission by itself only when it recognises
    /// <c>Handheld.Vibrate</c> in the code, and that call is useless here: it
    /// buzzes for half a second where a footfall needs a tap. Reaching the
    /// platform's own one-shot effect through JNI works, but leaves Unity with
    /// no way to know a motor is being driven.</para>
    ///
    /// <para>Done here rather than by committing an AndroidManifest.xml,
    /// because a manifest in Assets/Plugins/Android replaces the generated one
    /// outright: the activity, the intent filter and everything else Unity
    /// writes would have to be copied into it and then kept in step with
    /// whatever the next editor version writes. One line added to the file
    /// Unity has already produced cannot fall behind it.</para>
    /// </remarks>
    public sealed class AndroidVibratePermission : IPostGenerateGradleAndroidProject
    {
        private const string Permission = "android.permission.VIBRATE";
        private const string AndroidNamespace =
            "http://schemas.android.com/apk/res/android";

        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning($"No generated manifest at {manifestPath}, so the " +
                                 "vibration permission could not be added and steps " +
                                 "will be heard but not felt.");
                return;
            }

            var manifest = new XmlDocument();
            manifest.Load(manifestPath);

            XmlElement root = manifest.DocumentElement;
            if (root == null)
                return;

            foreach (XmlNode existing in root.SelectNodes("uses-permission"))
            {
                if (existing.Attributes?["android:name"]?.Value == Permission)
                    return;
            }

            XmlElement element = manifest.CreateElement("uses-permission");
            element.SetAttribute("name", AndroidNamespace, Permission);
            root.AppendChild(element);
            manifest.Save(manifestPath);
            Debug.Log($"Added {Permission} to the generated Android manifest.");
        }
    }
}
#endif
