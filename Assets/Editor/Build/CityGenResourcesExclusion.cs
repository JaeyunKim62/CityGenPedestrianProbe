using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CityGenPedestrianProbe.Editor
{
    /// <summary>
    /// Keeps CityGen's Resources folder out of a player build for as long as
    /// the build lasts, then puts it back.
    /// </summary>
    /// <remarks>
    /// Unity packs everything under any folder named Resources into every
    /// build, referenced or not, because Resources.Load resolves by string at
    /// runtime and nothing can prove statically that no one will ask. CityGen
    /// ships twenty prefabs there; they pull in the facade and terrain
    /// materials, and those pull 678 MB of textures. The testbed draws nothing
    /// at all and still carried every byte of it.
    ///
    /// Renaming the folder permanently is not an option: CityGen does load
    /// from it, by name — Prefabs/MapIcon, MeshSlicer and ShapeNode in the
    /// runtime assembly, Prefabs/Highway, Shape and ShapeMesh in the editor
    /// one. Take it away and generating a city stops working.
    ///
    /// So it is genuinely needed at edit time and genuinely unused by our
    /// scenes at run time, and moving it only while a build runs says exactly
    /// that. If a build dies partway the folder is left renamed; putting the
    /// name back is the whole repair, and the next build reports it.
    /// </remarks>
    internal sealed class CityGenResourcesExclusion : IDisposable
    {
        private const string ResourcesPath = "Assets/CityGen3D/Resources";
        private const string ParkedPath = "Assets/CityGen3D/Resources~Build";

        private readonly bool moved;

        public CityGenResourcesExclusion()
        {
            if (Directory.Exists(ParkedPath))
            {
                Debug.LogWarning($"{ParkedPath} was already there, which means an " +
                                 "earlier build did not finish. Restoring it and " +
                                 "carrying on.");
                Restore();
            }

            if (!Directory.Exists(ResourcesPath))
                return;

            Directory.Move(ResourcesPath, ParkedPath);
            File.Move(ResourcesPath + ".meta", ParkedPath + ".meta");
            AssetDatabase.Refresh();
            moved = true;
            Debug.Log($"Parked {ResourcesPath} for the build. Nothing in the " +
                      "scenes reads it, and it returns when the build ends.");
        }

        public void Dispose()
        {
            if (!moved)
                return;
            Restore();
            AssetDatabase.Refresh();
            Debug.Log($"Restored {ResourcesPath}.");
        }

        private static void Restore()
        {
            if (!Directory.Exists(ParkedPath))
                return;
            if (Directory.Exists(ResourcesPath))
                return;

            Directory.Move(ParkedPath, ResourcesPath);
            if (File.Exists(ParkedPath + ".meta"))
                File.Move(ParkedPath + ".meta", ResourcesPath + ".meta");
        }

        /// <summary>
        /// Puts the folder back after a build that died before it could.
        /// </summary>
        [MenuItem("Tools/Pedestrian Probe/Restore CityGen Resources", priority = 60)]
        public static void RestoreFromMenu()
        {
            if (!Directory.Exists(ParkedPath))
            {
                Debug.Log($"Nothing parked at {ParkedPath}; nothing to restore.");
                return;
            }

            Restore();
            AssetDatabase.Refresh();
            Debug.Log($"Restored {ResourcesPath}.");
        }
    }
}
